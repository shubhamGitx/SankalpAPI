using Microsoft.Data.SqlClient;
using SankalpAPI.DTO;
using SankalpAPI.Helper;
using SankalpAPI.Interface;
using SankalpAPI.Security;
using SankalpAPI.Services;
using System.Data;

namespace SankalpAPI.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly clsDataAccess _dataAccess;
        private readonly IConfiguration _config;
        private readonly ILogger<UserRepository> _logger;
        private readonly ClientTokenService _clientTokenService;
        private readonly ExceptionLogging _exceptionLogging;

        public UserRepository(clsDataAccess dataAccess, IConfiguration config, ClientTokenService clientTokenService, ILogger<UserRepository> logger, ExceptionLogging exceptionLogging)
        {
            _dataAccess = dataAccess;
            _config = config;
            _clientTokenService = clientTokenService;
            _logger = logger;
            _exceptionLogging = exceptionLogging;
        }

        // ============================================================
        // CHECK IF MOBILE EXISTS
        // ============================================================
        public Task<bool> IsMobileExistsAsync(string mobileNo, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT COUNT(1) FROM dbo.tbl_user WHERE Mobile = @Mobile;";
            SqlParameter _MobileNo = new SqlParameter("@Mobile", SqlDbType.VarChar, 255) { Value = mobileNo?.Trim() ?? string.Empty };
            SqlParameter[] parameters = { _MobileNo };
            DataTable dt = _dataAccess.GetDataTable(sql, parameters);

            if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
            {
                return Task.FromResult(Convert.ToInt32(dt.Rows[0][0]) > 0);
            }

            return Task.FromResult(false);
        }

        // ============================================================
        // REGISTER USER WITH INTERESTS & FCM DEVICE (ATOMIC SQL SERVER TRANSACTION)
        // ============================================================
        public async Task<(bool isSuccess, bool isDuplicate, long userId, string? errorMessage)> RegisterUserWithInterestsAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                string clientToken = _clientTokenService.GenerateToken();
                string tokenHash = ClientTokenHasher.Hash(clientToken);

                string? deviceId = string.IsNullOrWhiteSpace(request.DeviceId) ? null : request.DeviceId.Trim();
                string? fcmToken = string.IsNullOrWhiteSpace(request.FcmToken) ? null : request.FcmToken.Trim();
                string platform = NormalizePlatform(request.Platform);

                await using var con = _dataAccess.GetConnection();
                await con.OpenAsync(cancellationToken);

                await using var trans = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

                try
                {
                    // ----------------------------------------------------
                    // STEP 1: Duplicate check with UPDLOCK, HOLDLOCK inside transaction
                    // ----------------------------------------------------
                    const string checkMobileSql = "SELECT COUNT(1) FROM dbo.tbl_user WITH (UPDLOCK, HOLDLOCK) WHERE Mobile = @Mobile;";
                    await using (var checkCmd = new SqlCommand(checkMobileSql, con, trans))
                    {
                        checkCmd.CommandTimeout = 120;
                        checkCmd.Parameters.Add(new SqlParameter("@Mobile", SqlDbType.VarChar, 255) { Value = request.MobileNo.Trim() });

                        object? countObj = await checkCmd.ExecuteScalarAsync(cancellationToken);
                        if (countObj != null && countObj != DBNull.Value && Convert.ToInt32(countObj) > 0)
                        {
                            await trans.RollbackAsync(cancellationToken);
                            return (isSuccess: false, isDuplicate: true, userId: 0, errorMessage: "Mobile number already registered.");
                        }
                    }

                    // ----------------------------------------------------
                    // STEP 2: Insert into tbl_user & retrieve generated User_id
                    // ----------------------------------------------------
                    const string insertUserSql = @"INSERT INTO dbo.tbl_user (Name, Mobile, Password, DistCode, BlockCode, PanchayatCode, AreaType, VILLCODE, WARDCODE, Gender_code, Age_GroupID, Category_Id,
                                                   Qualification_id, Occupation_id, Deviceid, Token, Entryby, Entrydate)
                                                   OUTPUT INSERTED.User_id
                                                   VALUES (@Name, @Mobile, @Password, @DistCode, @BlockCode, @PanchayatCode, @AreaType, @VILLCODE, @WARDCODE, @Gender_code, @Age_GroupID, @Category_Id,
                                                   @Qualification_id, @Occupation_id, @Deviceid, @Token, @Entryby, GETDATE());";

                    long generatedUserId;
                    await using (var insertUserCmd = new SqlCommand(insertUserSql, con, trans))
                    {
                        insertUserCmd.CommandTimeout = 120;
                        insertUserCmd.Parameters.AddRange(new[]
                        {
                            new SqlParameter("@Name", SqlDbType.VarChar, 255) { Value = request.Name.Trim() },
                            new SqlParameter("@Mobile", SqlDbType.VarChar, 255) { Value = request.MobileNo.Trim() },
                            new SqlParameter("@Password", SqlDbType.VarChar, 255) { Value = (object?)request.Password ?? string.Empty },
                            new SqlParameter("@DistCode", SqlDbType.Int) { Value = (object?)request.DistCode ?? DBNull.Value },
                            new SqlParameter("@BlockCode", SqlDbType.Int) { Value = (object?)request.BlockCode ?? DBNull.Value },
                            new SqlParameter("@PanchayatCode", SqlDbType.Int) { Value = (object?)request.PanchayatCode ?? DBNull.Value },
                            new SqlParameter("@AreaType", SqlDbType.Char, 1) { Value = request.AreaType.HasValue ? (object)request.AreaType.Value.ToString() : DBNull.Value },
                            new SqlParameter("@VILLCODE", SqlDbType.Float) { Value = (object?)request.VillCode ?? DBNull.Value },
                            new SqlParameter("@WARDCODE", SqlDbType.NVarChar, 50) { Value = (object?)request.WardCode ?? DBNull.Value },
                            new SqlParameter("@Gender_code", SqlDbType.Int) { Value = (object?)request.GenderCode ?? DBNull.Value },
                            new SqlParameter("@Age_GroupID", SqlDbType.Int) { Value = (object?)request.AgeGroupId ?? DBNull.Value },
                            new SqlParameter("@Category_Id", SqlDbType.Int) { Value = (object?)request.CategoryId ?? DBNull.Value },
                            new SqlParameter("@Qualification_id", SqlDbType.Int) { Value = (object?)request.QualificationId ?? DBNull.Value },
                            new SqlParameter("@Occupation_id", SqlDbType.Int) { Value = (object?)request.OccupationId ?? DBNull.Value },
                            new SqlParameter("@Deviceid", SqlDbType.VarChar, 255) { Value = (object?)deviceId ?? DBNull.Value },
                            new SqlParameter("@Token", SqlDbType.VarChar, 255) { Value = (object?)clientToken ?? DBNull.Value },
                            new SqlParameter("@Entryby", SqlDbType.VarChar, 255) { Value = (object?)request.EntryBy ?? "System" }
                        });

                        object? userIdObj = await insertUserCmd.ExecuteScalarAsync(cancellationToken);
                        if (userIdObj == null || userIdObj == DBNull.Value)
                        {
                            await trans.RollbackAsync(cancellationToken);
                            return (isSuccess: false, isDuplicate: false, userId: 0, errorMessage: "Failed to retrieve generated User_id from tbl_user.");
                        }

                        generatedUserId = Convert.ToInt64(userIdObj);
                    }

                    // ----------------------------------------------------
                    // STEP 3: Insert deduplicated interests into MapUser_Interest
                    // ----------------------------------------------------
                    var distinctInterests = request.Interests?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();

                    if (distinctInterests.Count > 0)
                    {
                        const string insertInterestSql = @"INSERT INTO dbo.MapUser_Interest (User_id, Interest_Id) VALUES (@User_id, @Interest_Id);";

                        foreach (var interestId in distinctInterests)
                        {
                            await using var interestCmd = new SqlCommand(insertInterestSql, con, trans);
                            interestCmd.CommandTimeout = 120;
                            interestCmd.Parameters.Add(new SqlParameter("@User_id", SqlDbType.BigInt) { Value = generatedUserId });
                            interestCmd.Parameters.Add(new SqlParameter("@Interest_Id", SqlDbType.BigInt) { Value = interestId });

                            await interestCmd.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }

                    // ----------------------------------------------------
                    // STEP 4: Upsert FCM Device token into dbo.UserFcmDevices
                    // Uses actual generatedUserId from tbl_user (never client-supplied UserId)
                    // ----------------------------------------------------
                    if (IsValidFcmDeviceParameters(deviceId, fcmToken, platform))
                    {
                        await UpsertUserFcmDeviceInternalAsync(
                            con,
                            trans,
                            generatedUserId.ToString(),
                            deviceId!,
                            fcmToken!,
                            platform,
                            cancellationToken);
                    }

                    // ----------------------------------------------------
                    // STEP 5: Commit atomic transaction
                    // ----------------------------------------------------
                    await trans.CommitAsync(cancellationToken);

                    // Best-effort registration of hashed ClientToken for X-Client-Token middleware
                    try
                    {
                        SqlParameter[] tokenParams =
                        {
                            new SqlParameter("@p_user_id", SqlDbType.VarChar, 100) { Value = generatedUserId.ToString() },
                            new SqlParameter("@p_token_hash", SqlDbType.Char, 64) { Value = tokenHash }
                        };
                        _dataAccess.GetDataTableWithProc("sp_save_client_token", tokenParams);
                    }
                    catch (Exception tokenEx)
                    {
                        _logger.LogDebug(tokenEx, "Non-fatal: could not persist client token hash via sp_save_client_token for User_id {UserId}.", generatedUserId);
                    }

                    return (isSuccess: true, isDuplicate: false, userId: generatedUserId, errorMessage: null);
                }
                catch
                {
                    try
                    {
                        await trans.RollbackAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // Do not mask original exception
                    }
                    throw;
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException during registration for Mobile: {Mobile}. Error Code: {Number}", request.MobileNo, ex.Number);
                _exceptionLogging.SendErrorToText(ex);

                // Handle unique index violation (error 2601 or 2627)
                if (ex.Number == 2601 || ex.Number == 2627)
                {
                    if (ex.Message.Contains("IX_tbl_user_Mobile", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
                    {
                        return (isSuccess: false, isDuplicate: true, userId: 0, errorMessage: "Mobile number already registered.");
                    }
                }

                return (isSuccess: false, isDuplicate: false, userId: 0, errorMessage: "Database error occurred.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration for Mobile: {Mobile}", request.MobileNo);
                _exceptionLogging.SendErrorToText(ex);

                return (isSuccess: false, isDuplicate: false, userId: 0, errorMessage: "Unable to register user.");
            }
        }

        // ============================================================
        // LEGACY INSERT USER (Preserved for backwards compatibility)
        // ============================================================
        public DataTable InsertUser(string Username, string Password, string mobile)
        {
            SqlParameter _UserID = new SqlParameter("@UserID", mobile);
            SqlParameter _Username = new SqlParameter("@Username", Username);
            SqlParameter _Password = new SqlParameter("@Password", Password);
            SqlParameter _UserRole = new SqlParameter("@UserRole", "MOB");
            SqlParameter _Mobile = new SqlParameter("@Mobile", mobile);

            SqlParameter[] parameters = { _UserID, _Username, _Password, _UserRole, _Mobile };

            return _dataAccess.GetDataTableWithProc("user_insert", parameters);
        }

        // ============================================================
        // GET USER BY MOBILE WITH MULTIPLE INTERESTS (FROM MapUser_Interest)
        // ============================================================
        public async Task<UserData?> GetUserByMobileAsync(string mobileNo, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            const string userSql = @"SELECT TOP(1) u.User_id, u.Name, u.Mobile, u.DistCode, u.BlockCode, u.PanchayatCode, u.AreaType, u.VILLCODE, u.WARDCODE, u.Gender_code, u.Age_GroupID, u.Category_Id, u.Qualification_id, u.Occupation_id, u.Deviceid,
                         u.Token, u.Entrydate, d.DistName, b.BlockName, p.PanchayatName, v.VILLNAME, w.WARDNAME, g.gender_name as genderName, ag.age_group_name as ageGroupName, c.category_name as categoryName, q.qualification_name as qualificationName,
						 o.occupation_name as occupationName
                         FROM tbl_user AS u LEFT OUTER JOIN
                         Districts AS d ON u.DistCode = d.DistCode LEFT OUTER JOIN
                         Blocks AS b ON u.BlockCode = b.BlockCode LEFT OUTER JOIN
						 Panchayats as p ON u.PanchayatCode=p.PanchayatCode LEFT OUTER JOIN
						 VillageMaster as v ON u.VILLCODE=v.VILLCODE LEFT OUTER JOIN
						 WARDS as w ON u.WARDCODE=w.WARDCODE LEFT OUTER JOIN
						 gender as g ON u.Gender_code=g.gender_code LEFT OUTER JOIN
						 mst_age_group as ag ON u.Age_GroupID=ag.id LEFT OUTER JOIN
						 mst_category as c ON u.Category_Id=c.category_id LEFT OUTER JOIN
						 mst_qualification as q ON u.Qualification_id=q.qualification_id LEFT OUTER JOIN
						 mst_occupation as o ON u.Occupation_id=o.occupation_id WHERE u.Mobile = @Mobile;";

            SqlParameter[] userParams =
            {
                new SqlParameter("@Mobile", SqlDbType.VarChar, 255) { Value = mobileNo?.Trim() ?? string.Empty }
            };

            DataTable dtUser = _dataAccess.GetDataTable(userSql, userParams);
            if (dtUser == null || dtUser.Rows.Count == 0)
            {
                return null;
            }

            DataRow row = dtUser.Rows[0];
            var user = new UserData
            {
                UserId = Convert.ToInt64(row["User_id"]),
                Name = row["Name"] != DBNull.Value ? row["Name"].ToString() ?? string.Empty : string.Empty,
                MobileNo = row["Mobile"] != DBNull.Value ? row["Mobile"].ToString() ?? string.Empty : string.Empty,
                DistCode = row["DistCode"] != DBNull.Value ? Convert.ToInt32(row["DistCode"]) : null,
                BlockCode = row["BlockCode"] != DBNull.Value ? Convert.ToInt32(row["BlockCode"]) : null,
                PanchayatCode = row["PanchayatCode"] != DBNull.Value ? Convert.ToInt32(row["PanchayatCode"]) : null,
                AreaType = row["AreaType"] != DBNull.Value ? row["AreaType"].ToString() : null,
                VILLCode = row["VILLCODE"] != DBNull.Value ? row["VILLCODE"].ToString() : null,
                WARDCODE = row["WARDCODE"] != DBNull.Value ? row["WARDCODE"].ToString() : null,
                GenderCode = row["Gender_code"] != DBNull.Value ? Convert.ToInt32(row["Gender_code"]) : null,
                AgeGroupId = row["Age_GroupID"] != DBNull.Value ? Convert.ToInt32(row["Age_GroupID"]) : null,
                CategoryId = row["Category_Id"] != DBNull.Value ? Convert.ToInt32(row["Category_Id"]) : null,
                QualificationId = row["Qualification_id"] != DBNull.Value ? Convert.ToInt32(row["Qualification_id"]) : null,
                OccupationId = row["Occupation_id"] != DBNull.Value ? Convert.ToInt32(row["Occupation_id"]) : null,
                DeviceId = row["Deviceid"] != DBNull.Value ? row["Deviceid"].ToString() : null,
                Token = row["Token"] != DBNull.Value ? row["Token"].ToString() : null,
                EntryDate = row["Entrydate"] != DBNull.Value ? Convert.ToDateTime(row["Entrydate"]) : null,
                DistName = row["DistName"] != DBNull.Value ? row["DistName"].ToString() ?? string.Empty : string.Empty,
                BlockName = row["BlockName"] != DBNull.Value ? row["BlockName"].ToString() ?? string.Empty : string.Empty,
                PanchayatName = row["PanchayatName"] != DBNull.Value ? row["PanchayatName"].ToString() ?? string.Empty : string.Empty,
                VILLNAME = row["VILLNAME"] != DBNull.Value ? row["VILLNAME"].ToString() ?? string.Empty : string.Empty,
                WARDNAME = row["WARDNAME"] != DBNull.Value ? row["WARDNAME"].ToString() ?? string.Empty : string.Empty,
                genderName = row["genderName"] != DBNull.Value ? row["genderName"].ToString() ?? string.Empty : string.Empty,
                ageGroupName = row["ageGroupName"] != DBNull.Value ? row["ageGroupName"].ToString() ?? string.Empty : string.Empty,
                categoryName = row["categoryName"] != DBNull.Value ? row["categoryName"].ToString() ?? string.Empty : string.Empty,
                qualificationName = row["qualificationName"] != DBNull.Value ? row["qualificationName"].ToString() ?? string.Empty : string.Empty,
                occupationName = row["occupationName"] != DBNull.Value ? row["occupationName"].ToString() ?? string.Empty : string.Empty,
                Interests = new List<long>(),
                UserInterests = new List<UserInterestData>()
            };

            // --------------------------------------------------------
            // Load multiple interests from MapUser_Interest
            // --------------------------------------------------------
            const string interestSql = @"SELECT mui.MapID, mui.Interest_Id, mi.interest_name 
                                         FROM dbo.MapUser_Interest mui 
                                         LEFT JOIN dbo.mst_interest mi ON mui.Interest_Id = mi.interest_id 
                                         WHERE mui.User_id = @UserId;";

            SqlParameter[] interestParams =
            {
                new SqlParameter("@UserId", SqlDbType.BigInt) { Value = user.UserId }
            };

            DataTable dtInterest = _dataAccess.GetDataTable(interestSql, interestParams);
            if (dtInterest != null && dtInterest.Rows.Count > 0)
            {
                foreach (DataRow interestRow in dtInterest.Rows)
                {
                    if (interestRow["Interest_Id"] != DBNull.Value)
                    {
                        long interestId = Convert.ToInt64(interestRow["Interest_Id"]);
                        user.Interests.Add(interestId);

                        user.UserInterests.Add(new UserInterestData
                        {
                            MapId = interestRow["MapID"] != DBNull.Value ? Convert.ToInt64(interestRow["MapID"]) : 0,
                            InterestId = interestId,
                            InterestName = interestRow["interest_name"] != DBNull.Value ? interestRow["interest_name"].ToString() : null
                        });
                    }
                }
            }

            return user;
        }

        // ============================================================
        // GET DEVICE ID BY MOBILE
        // ============================================================
        public async Task<string?> GetDeviceIdByMobileAsync(string mobileNo, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            try
            {
                const string sql = "SELECT TOP 1 Deviceid FROM dbo.tbl_user WHERE Mobile = @Mobile;";
                SqlParameter[] parameters =
                {
                    new SqlParameter("@Mobile", SqlDbType.VarChar, 255) { Value = mobileNo?.Trim() ?? string.Empty }
                };

                DataTable dt = _dataAccess.GetDataTable(sql, parameters);
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["Deviceid"] != DBNull.Value)
                {
                    return dt.Rows[0]["Deviceid"].ToString();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DeviceId for mobile {Mobile}", mobileNo);
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }
        }

        // ============================================================
        // UPDATE USER DEVICE ID IN tbl_user (WITHOUT CREATING DUPLICATE)
        // ============================================================
        public async Task<bool> UpdateUserDeviceIdAsync(string mobileNo, string deviceId, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            try
            {
                string clientToken = _clientTokenService.GenerateToken();
                const string updateSql = @"UPDATE dbo.tbl_user SET Deviceid = @DeviceId, Token = @Token WHERE Mobile = @Mobile;";

                SqlParameter[] parameters =
                {
                    new SqlParameter("@DeviceId", SqlDbType.VarChar, 255) { Value = (object?)deviceId?.Trim() ?? DBNull.Value },
                    new SqlParameter("@Token", SqlDbType.VarChar, 255) { Value = (object?)clientToken ?? DBNull.Value },
                    new SqlParameter("@Mobile", SqlDbType.VarChar, 255) { Value = mobileNo?.Trim() ?? string.Empty }
                };

                int rowsAffected = _dataAccess.ExecuteSql(updateSql, parameters);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating DeviceId for mobile {Mobile}", mobileNo);
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }
        }

        // ============================================================
        // UPSERT USER FCM DEVICE (STANDALONE / TOKEN REFRESH)
        // ============================================================
        public async Task<bool> UpsertUserFcmDeviceAsync(
            string userId,
            string deviceId,
            string fcmToken,
            string platform = "Android",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(deviceId) ||
                string.IsNullOrWhiteSpace(fcmToken))
            {
                return false;
            }

            string cleanPlatform = NormalizePlatform(platform);
            if (!IsValidFcmDeviceParameters(deviceId.Trim(), fcmToken.Trim(), cleanPlatform))
            {
                return false;
            }

            try
            {
                await using var con = _dataAccess.GetConnection();
                await con.OpenAsync(cancellationToken);

                await using var trans = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                try
                {
                    await UpsertUserFcmDeviceInternalAsync(
                        con,
                        trans,
                        userId.Trim(),
                        deviceId.Trim(),
                        fcmToken.Trim(),
                        cleanPlatform,
                        cancellationToken);

                    await trans.CommitAsync(cancellationToken);
                    return true;
                }
                catch
                {
                    await trans.RollbackAsync(CancellationToken.None);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting FCM device for UserId: {UserId}, DeviceId: {DeviceId}", userId, deviceId);
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }
        }

        // ============================================================
        // GET ACTIVE FCM DEVICES FOR A USER
        // ============================================================
        public async Task<List<UserFcmDeviceDto>> GetActiveUserFcmDevicesAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var list = new List<UserFcmDeviceDto>();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return list;
            }

            const string sql = @"SELECT Id, UserId, DeviceId, FcmToken, Platform, IsActive, CreatedAtUtc, UpdatedAtUtc, LastSeenAtUtc
                                 FROM dbo.UserFcmDevices
                                 WHERE UserId = @UserId
                                   AND IsActive = 1
                                 ORDER BY UpdatedAtUtc DESC;";

            SqlParameter[] parameters =
            {
                new SqlParameter("@UserId", SqlDbType.NVarChar, 100) { Value = userId.Trim() }
            };

            DataTable dt = _dataAccess.GetDataTable(sql, parameters);
            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new UserFcmDeviceDto
                    {
                        Id = Convert.ToInt64(row["Id"]),
                        UserId = row["UserId"]?.ToString() ?? string.Empty,
                        DeviceId = row["DeviceId"]?.ToString() ?? string.Empty,
                        FcmToken = row["FcmToken"]?.ToString() ?? string.Empty,
                        Platform = row["Platform"]?.ToString() ?? "Android",
                        IsActive = row["IsActive"] != DBNull.Value && Convert.ToBoolean(row["IsActive"]),
                        CreatedAtUtc = row["CreatedAtUtc"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAtUtc"]) : DateTime.UtcNow,
                        UpdatedAtUtc = row["UpdatedAtUtc"] != DBNull.Value ? Convert.ToDateTime(row["UpdatedAtUtc"]) : DateTime.UtcNow,
                        LastSeenAtUtc = row["LastSeenAtUtc"] != DBNull.Value ? Convert.ToDateTime(row["LastSeenAtUtc"]) : null
                    });
                }
            }

            return await Task.FromResult(list);
        }

        // ============================================================
        // DEACTIVATE INVALID OR EXPIRED FCM TOKENS
        // ============================================================
        public async Task<int> DeactivateInvalidFcmTokensAsync(
            IEnumerable<string> fcmTokens,
            CancellationToken cancellationToken = default)
        {
            if (fcmTokens == null)
            {
                return 0;
            }

            var tokenList = fcmTokens
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct()
                .ToList();

            if (tokenList.Count == 0)
            {
                return 0;
            }

            try
            {
                var parameters = new List<SqlParameter>();
                var paramPlaceholders = new List<string>();

                for (int i = 0; i < tokenList.Count; i++)
                {
                    string paramName = $"@tok{i}";
                    paramPlaceholders.Add(paramName);
                    parameters.Add(new SqlParameter(paramName, SqlDbType.NVarChar, 2048) { Value = tokenList[i] });
                }

                string updateSql = $@"UPDATE dbo.UserFcmDevices
                                     SET IsActive = 0,
                                         UpdatedAtUtc = SYSUTCDATETIME()
                                     WHERE FcmToken IN ({string.Join(",", paramPlaceholders)})
                                       AND IsActive = 1;";

                int affected = _dataAccess.ExecuteSql(updateSql, parameters.ToArray());
                return await Task.FromResult(affected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating invalid FCM tokens");
                _exceptionLogging.SendErrorToText(ex);
                return 0;
            }
        }

        // ============================================================
        // AUTHORIZE SENDER FOR SENDING NOTIFICATIONS
        // ============================================================
        public async Task<bool> IsAuthorizedNotificationSenderAsync(
            string authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(authenticatedUserId))
            {
                return false;
            }

            string cleanUserId = authenticatedUserId.Trim();

            // 1. Check if explicitly whitelisted in appsettings.json
            string? configuredUserIds = _config["Firebase:AuthorizedSenderUserIds"];
            if (!string.IsNullOrWhiteSpace(configuredUserIds))
            {
                var allowed = configuredUserIds.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (allowed.Contains(cleanUserId, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 2. Check user role in dbo.user_login
            try
            {
                const string sql = "SELECT TOP 1 userrole FROM dbo.user_login WHERE userid = @UserId;";
                SqlParameter[] parameters =
                {
                    new SqlParameter("@UserId", SqlDbType.VarChar, 100) { Value = cleanUserId }
                };

                DataTable dt = _dataAccess.GetDataTable(sql, parameters);
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["userrole"] != DBNull.Value)
                {
                    string role = dt.Rows[0]["userrole"].ToString()?.Trim() ?? string.Empty;
                    // Mobile users are not authorized to send arbitrary push notifications
                    if (!string.Equals(role, "MOB", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(role))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking user role for UserId: {UserId}", cleanUserId);
            }

            return await Task.FromResult(false);
        }

        // ============================================================
        // INTERNAL UPSERT COMMAND (RUNS INSIDE CALLER'S TRANSACTION)
        // ============================================================
        private static async Task UpsertUserFcmDeviceInternalAsync(
            SqlConnection con,
            SqlTransaction trans,
            string userId,
            string deviceId,
            string fcmToken,
            string platform,
            CancellationToken cancellationToken)
        {
            const string sql = @"UPDATE dbo.UserFcmDevices WITH (UPDLOCK, HOLDLOCK) SET FcmToken = @FcmToken, Platform = @Platform, IsActive = 1, UpdatedAtUtc = SYSUTCDATETIME(), LastSeenAtUtc = SYSUTCDATETIME()
                               WHERE UserId = @UserId AND DeviceId = @DeviceId;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.UserFcmDevices
    (
        UserId,
        DeviceId,
        FcmToken,
        Platform,
        IsActive,
        CreatedAtUtc,
        UpdatedAtUtc,
        LastSeenAtUtc
    )
    VALUES
    (
        @UserId,
        @DeviceId,
        @FcmToken,
        @Platform,
        1,
        SYSUTCDATETIME(),
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END;";

            await using var cmd = new SqlCommand(sql, con, trans);
            cmd.CommandTimeout = 120;
            cmd.Parameters.Add(new SqlParameter("@UserId", SqlDbType.NVarChar, 100) { Value = userId });
            cmd.Parameters.Add(new SqlParameter("@DeviceId", SqlDbType.NVarChar, 200) { Value = deviceId });
            cmd.Parameters.Add(new SqlParameter("@FcmToken", SqlDbType.NVarChar, 2048) { Value = fcmToken });
            cmd.Parameters.Add(new SqlParameter("@Platform", SqlDbType.NVarChar, 20) { Value = platform });

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static bool IsValidFcmDeviceParameters(string? deviceId, string? fcmToken, string platform)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(fcmToken))
            {
                return false;
            }

            if (deviceId.Length > 200 || fcmToken.Length > 2048 || platform.Length > 20)
            {
                return false;
            }

            if (string.Equals(fcmToken, "null", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fcmToken, "undefined", StringComparison.OrdinalIgnoreCase) ||
                fcmToken.Length < 10)
            {
                return false;
            }

            return true;
        }

        private static string NormalizePlatform(string? platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                return "Android";
            }

            string clean = platform.Trim();
            if (string.Equals(clean, "android", StringComparison.OrdinalIgnoreCase))
                return "Android";
            if (string.Equals(clean, "ios", StringComparison.OrdinalIgnoreCase))
                return "iOS";
            if (string.Equals(clean, "web", StringComparison.OrdinalIgnoreCase))
                return "Web";

            return clean.Length > 20 ? clean.Substring(0, 20) : clean;
        }
    }
}
