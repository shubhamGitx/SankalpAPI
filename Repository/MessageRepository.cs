using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SankalpAPI.Helper;
using SankalpAPI.Interface;
using SankalpAPI.Model;

namespace SankalpAPI.Repository
{
    public class MessageRepository : IMessageRepository
    {
        private readonly clsDataAccess _dataAccess;

        public MessageRepository(clsDataAccess dataAccess)
        {
            _dataAccess = dataAccess;
        }

        public Task<List<PersonalizedMessageDto>> GetUnsyncedMessagesAndMarkSyncedAsync(string DeviceID, long? portalId = null, long? msg_id = null)
        {
            var list = new List<PersonalizedMessageDto>();

            SqlParameter[] parameters =
            {
                new SqlParameter("@p_device_id", SqlDbType.NVarChar, 255) { Value = DeviceID ?? string.Empty },
                new SqlParameter("@p_portal_id", (portalId.HasValue && portalId.Value > 0) ? (object)portalId.Value : DBNull.Value),
                new SqlParameter("@p_msg_id", (msg_id.HasValue && msg_id.Value > 0) ? (object)msg_id.Value : DBNull.Value)
            };

            DataTable dt = _dataAccess.GetDataTableWithProc("sp_get_unsynced_messages", parameters);

            if (dt.Rows.Count == 0)
            {
                return Task.FromResult(list);
            }

            var logIds = new List<long>();

            foreach (DataRow row in dt.Rows)
            {
                long logId = SafeLong(row, "msg_log_id");
                if (logId > 0)
                {
                    logIds.Add(logId);
                }

                list.Add(new PersonalizedMessageDto
                {
                    msg_id = SafeLong(row, "msg_id"),
                    msg_log_id = logId > 0 ? logId : null,
                    DeviceID = SafeString(row, "device_id") ?? DeviceID ?? string.Empty,
                    PortalId = SafeNullableLong(row, "portal_id"),
                    PortalName = SafeString(row, "portal_name"),
                    MessageHead = SafeString(row, "message_head"),
                    MessageBody = SafeString(row, "message_body"),
                    ValidityInMinutes = SafeNullableShort(row, "validity_in_minutes", 60),
                    photo1path = SafeString(row, "photo1path"),
                    photo2path = SafeString(row, "photo2path"),
                    videopath = SafeString(row, "videopath"),
                    documentpath = SafeString(row, "documentpath"),
                    youtubeurl = SafeString(row, "youtubeurl"),
                    facebookurl = SafeString(row, "facebookurl"),
                    instagramurl = SafeString(row, "instagramurl"),
                    Xurl = SafeString(row, "Xurl"),
                    Interest_ID = SafeString(row, "interest_id"),
                    IsSynced = "Yes",
                    Synced = "Yes",
                    SyncDate = DateTime.UtcNow,
                    EntryDate = SafeNullableDateTime(row, "entrydate")
                });
            }

            // Immediately mark as synced in tbl_Message_log
            if (logIds.Count > 0)
            {
                SqlParameter[] syncParams =
                {
                    new SqlParameter("@p_log_ids", string.Join(",", logIds)),
                    new SqlParameter("@p_device_id", DeviceID ?? string.Empty)
                };
                _dataAccess.ExecuteProcedure("sp_mark_message_log_synced", syncParams);
            }

            return Task.FromResult(list);
        }

        public Task<long> CreateNotificationAsync(string deviceID, string? msg_id)
        {
            long? parsedMsgId = null;
            if (!string.IsNullOrWhiteSpace(msg_id) && long.TryParse(msg_id, out long idVal))
            {
                parsedMsgId = idVal;
            }

            SqlParameter[] parameters =
            {
                new SqlParameter("@p_device_id", SqlDbType.NVarChar, 255) { Value = deviceID ?? string.Empty },
                new SqlParameter("@p_msg_id", (object?)parsedMsgId ?? DBNull.Value)
            };

            DataTable dt = _dataAccess.GetDataTableWithProc("sp_create_message_log", parameters);
            long id = 0;
            if (dt.Rows.Count > 0 && dt.Rows[0]["msg_log_id"] != DBNull.Value)
            {
                id = Convert.ToInt64(dt.Rows[0]["msg_log_id"]);
            }
            return Task.FromResult(id);
        }

        public Task<int> MarkMessagesAsSyncedAsync(List<long> notificationIds, string? deviceId = null)
        {
            if (notificationIds == null || notificationIds.Count == 0)
                return Task.FromResult(0);

            var validIds = notificationIds.Where(id => id > 0).Distinct().ToList();
            if (validIds.Count == 0)
                return Task.FromResult(0);

            SqlParameter[] parameters =
            {
                new SqlParameter("@p_log_ids", string.Join(",", validIds)),
                new SqlParameter("@p_device_id", string.IsNullOrWhiteSpace(deviceId) ? (object)DBNull.Value : deviceId.Trim())
            };

            int rows = _dataAccess.ExecuteProcedure("sp_mark_message_log_synced", parameters);
            return Task.FromResult(rows);
        }

        public Task<List<PersonalizedMessageDto>> GetMessageHistoryAsync(string DeviceID, List<string>? interestIds = null, long? portalId = null)
        {
            var list = new List<PersonalizedMessageDto>();

            var validIds = new List<long>();
            if (interestIds != null && interestIds.Count > 0)
            {
                foreach (var id in interestIds)
                {
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    foreach (var part in id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (long.TryParse(part, out long num) && !validIds.Contains(num))
                        {
                            validIds.Add(num);
                        }
                    }
                }
            }

            SqlParameter[] parameters =
            {
                new SqlParameter("@p_device_id", SqlDbType.NVarChar, 255) { Value = DeviceID ?? string.Empty },
                new SqlParameter("@p_interest_ids", validIds.Count > 0 ? (object)string.Join(",", validIds) : DBNull.Value),
                new SqlParameter("@p_portal_id", (portalId.HasValue && portalId.Value > 0) ? (object)portalId.Value : DBNull.Value)
            };

            DataTable dt = _dataAccess.GetDataTableWithProc("sp_get_message_history", parameters);



            foreach (DataRow row in dt.Rows)
            {
                list.Add(new PersonalizedMessageDto
                {
                    msg_id = SafeLong(row, "msg_id"),
                    msg_log_id = SafeNullableLong(row, "msg_log_id"),
                    DeviceID = SafeString(row, "device_id") ?? DeviceID ?? string.Empty,
                    PortalId = SafeNullableLong(row, "portal_id"),
                    PortalName = SafeString(row, "portal_name"),
                    MessageHead = SafeString(row, "message_head"),
                    MessageBody = SafeString(row, "message_body"),
                    ValidityInMinutes = SafeNullableShort(row, "validity_in_minutes", 60),
                    photo1path = SafeString(row, "photo1path"),
                    photo2path = SafeString(row, "photo2path"),
                    videopath = SafeString(row, "videopath"),
                    documentpath = SafeString(row, "documentpath"),
                    youtubeurl = SafeString(row, "youtubeurl"),
                    facebookurl = SafeString(row, "facebookurl"),
                    instagramurl = SafeString(row, "instagramurl"),
                    Xurl = SafeString(row, "Xurl"),
                    Interest_ID = SafeString(row, "interest_id"),
                    IsDeleted = SafeString(row, "is_deleted") ?? "N",
                    IsSynced = SafeString(row, "is_synced") ?? "No",
                    Synced = SafeString(row, "is_synced") ?? "No",
                    SyncDate = SafeNullableDateTime(row, "sync_date"),
                    EntryDate = SafeNullableDateTime(row, "entrydate")
                });
            }

            return Task.FromResult(list);
        }

        public Task<PersonalizedMessageDto?> GetMessageByIdAsync(long msgId)
        {
            SqlParameter[] parameters = { new SqlParameter("@p_msg_id", SqlDbType.BigInt) { Value = msgId } };
            DataTable dt = _dataAccess.GetDataTableWithProc("sp_get_message_by_id", parameters);
            if (dt.Rows.Count == 0) return Task.FromResult<PersonalizedMessageDto?>(null);

            DataRow row = dt.Rows[0];
            var dto = new PersonalizedMessageDto
            {
                msg_id = SafeLong(row, "msg_id"),
                PortalId = SafeNullableLong(row, "portal_id"),
                PortalName = SafeString(row, "portal_name"),
                MessageHead = SafeString(row, "message_head"),
                MessageBody = SafeString(row, "message_body"),
                ValidityInMinutes = SafeNullableShort(row, "validity_in_minutes", 60),
                photo1path = SafeString(row, "photo1path"),
                photo2path = SafeString(row, "photo2path"),
                videopath = SafeString(row, "videopath"),
                documentpath = SafeString(row, "documentpath"),
                youtubeurl = SafeString(row, "youtubeurl"),
                facebookurl = SafeString(row, "facebookurl"),
                instagramurl = SafeString(row, "instagramurl"),
                Xurl = SafeString(row, "Xurl"),
                Interest_ID = SafeString(row, "interest_id"),
                EntryDate = SafeNullableDateTime(row, "entrydate")
            };

            return Task.FromResult<PersonalizedMessageDto?>(dto);
        }

        public Task<(bool hasExistingLog, int unsyncedCount)> CheckBroadcastSyncStatusAsync(string deviceId, List<string>? interestIds = null, long? portalId = null)
        {
            var validIds = new List<long>();
            if (interestIds != null && interestIds.Count > 0)
            {
                foreach (var id in interestIds)
                {
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    foreach (var part in id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (long.TryParse(part, out long num) && !validIds.Contains(num))
                        {
                            validIds.Add(num);
                        }
                    }
                }
            }

            string query;
            SqlParameter[] parameters;

            if (validIds.Count > 0)
            {
                string idsStr = string.Join(",", validIds);
                query = @"SELECT  
                        (SELECT COUNT(1) FROM dbo.tbl_Message_log ml INNER JOIN dbo.tbl_Message m ON ml.msg_id = m.msg_id INNER JOIN 
                         STRING_SPLIT(@p_interest_ids, ',') s ON LTRIM(RTRIM(s.value)) = CAST(m.interest_id AS VARCHAR(50))
                         WHERE ml.device_id = @p_device_id
                         AND (@p_portal_id IS NULL OR @p_portal_id = 0 OR m.portal_id = @p_portal_id)) AS ExistingLogCount,
                        (SELECT COUNT(1) FROM dbo.tbl_Message m INNER JOIN STRING_SPLIT(@p_interest_ids, ',') s ON LTRIM(RTRIM(s.value)) = CAST(m.interest_id AS VARCHAR(50))
                         WHERE (m.is_active IS NULL OR m.is_active = 'Y' OR m.is_active = '1')
                         AND (@p_portal_id IS NULL OR @p_portal_id = 0 OR m.portal_id = @p_portal_id)
                         AND NOT EXISTS (SELECT 1 FROM dbo.tbl_Message_log ml2 WHERE ml2.device_id = @p_device_id AND ml2.msg_id = m.msg_id)) AS UnsyncedCount;";

                parameters = new SqlParameter[]
                {
                    new SqlParameter("@p_device_id", SqlDbType.NVarChar, 255) { Value = deviceId ?? string.Empty },
                    new SqlParameter("@p_interest_ids", SqlDbType.NVarChar) { Value = idsStr },
                    new SqlParameter("@p_portal_id", (portalId.HasValue && portalId.Value > 0) ? (object)portalId.Value : DBNull.Value)
                };
            }
            else
            {
                query = @"SELECT  COUNT(1) AS ExistingLogCount, 0 AS UnsyncedCount FROM dbo.tbl_Message_log ml WHERE ml.device_id = @p_device_id;";

                parameters = new SqlParameter[]
                {
                    new SqlParameter("@p_device_id", SqlDbType.NVarChar, 255) { Value = deviceId ?? string.Empty }
                };
            }

            DataTable dt = _dataAccess.GetDataTable(query, parameters);
            int existingCount = 0;
            int unsyncedCount = 0;

            if (dt.Rows.Count > 0)
            {
                existingCount = dt.Rows[0]["ExistingLogCount"] != DBNull.Value ? Convert.ToInt32(dt.Rows[0]["ExistingLogCount"]) : 0;
                unsyncedCount = dt.Rows[0]["UnsyncedCount"] != DBNull.Value ? Convert.ToInt32(dt.Rows[0]["UnsyncedCount"]) : 0;
            }

            return Task.FromResult((existingCount > 0, unsyncedCount));
        }

        public Task<List<BroadcastMessageDto>> GetBroadcastMessagesAsync(string deviceId)
        {
            SqlParameter[] parameters;
            var list = new List<BroadcastMessageDto>();

            const string query = @"SELECT B.Portal_id, B.User_Type_ID, B.Bmsg_id, B.message_head, B.message_body, B.validity_in_minutes, B.is_active, B.photo1path, B.photo2path, B.videopath,
                                 B.documentpath, B.audiopath, B.entryby, B.entrydate, B.updatedby, B.updateddate, B.IsSynced, B.IsDeleted, B.IsAllDistrict, B.MesgTypeID,
                                 B.IsVerified, B.VerifiedBy, B.VerifiedDate, B.VerifiedIP, B.IsApproved, B.ApprovedBy, B.ApprovedDate, B.ApprovedIP
                                 FROM   tbl_BroadCastMessage AS B                 
                                 WHERE  (B.is_active IS NULL OR B.is_active = 'Y' OR B.is_active = '1')              
                                 AND  (@p_device_id = '' OR B.Portal_id IS NULL OR EXISTS (SELECT 1 FROM   dbo.tbl_map_user_portal M WHERE  M.portal_id    = B.Portal_id AND  M.User_Type_ID = B.User_Type_ID 
                                 AND  M.deviceid     = @p_device_id))                
                                 AND  (@p_device_id = ''  OR ISNULL(B.IsAllDistrict, 'Y') = 'Y' OR EXISTS (SELECT 1 FROM   dbo.tbl_BroadCastMapDistrict D INNER JOIN 
                                 dbo.tbl_map_user_portal   U ON U.deviceid = @p_device_id
                                 WHERE  D.Bmsg_id = B.Bmsg_id 
                                 AND  (D.district_code = U.district_code OR D.block_code    = U.block_code OR D.panchayatcode = U.panchayatcode)))
                                 ORDER BY B.entrydate DESC, B.Bmsg_id DESC;";

            parameters = new SqlParameter[]
               {
                    new SqlParameter("@p_device_id", SqlDbType.NVarChar, 255) { Value = deviceId ?? string.Empty }
               };

            DataTable dt = _dataAccess.GetDataTable(query, parameters);

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new BroadcastMessageDto
                {
                    Bmsg_id = SafeLong(row, "Bmsg_id"),
                    MesgTypeId = SafeNullableLong(row, "MesgTypeID"),
                    MessageHead = SafeString(row, "message_head"),
                    MessageBody = SafeString(row, "message_body"),
                    ValidityInMinutes = SafeNullableShort(row, "validity_in_minutes", 60),
                    IsActive = SafeString(row, "is_active") ?? "Y",
                    PortalId = SafeNullableLong(row, "Portal_id"),
                    UserTypeId = SafeString(row, "User_Type_ID"),
                    photo1path = SafeString(row, "photo1path"),
                    photo2path = SafeString(row, "photo2path"),
                    videopath = SafeString(row, "videopath"),
                    documentpath = SafeString(row, "documentpath"),
                    audiopath = SafeString(row, "audiopath"),
                    EntryBy = SafeString(row, "entryby"),
                    EntryDate = SafeNullableDateTime(row, "entrydate"),
                    UpdatedBy = SafeString(row, "updatedby"),
                    UpdatedDate = SafeNullableDateTime(row, "updateddate"),
                    IsSynced = SafeString(row, "IsSynced") ?? "N",
                    IsDeleted = SafeString(row, "IsDeleted") ?? "N",
                    MesgTypeID = SafeString(row, "MesgTypeID"),
                    IsVerified = SafeNullableBool(row, "IsVerified"),
                    VerifiedBy = SafeString(row, "VerifiedBy"),
                    VerifiedDate = SafeNullableDateTime(row, "VerifiedDate"),
                    VerifiedIP = SafeString(row, "VerifiedIP"),
                    IsApproved = SafeNullableBool(row, "IsApproved"),
                    ApprovedBy = SafeString(row, "ApprovedBy"),
                    ApprovedDate = SafeNullableDateTime(row, "ApprovedDate"),
                    ApprovedIP = SafeString(row, "ApprovedIP")
                });
            }

            return Task.FromResult(list);
        }

        public async Task<int> ForwardDraftMessagesAsync(
            IReadOnlyCollection<long> bmsgIds,
            string userRole,
            string actionBy,
            string? actionIp)
        {
            string normalizedRole = (userRole ?? string.Empty).Trim().ToUpperInvariant();
            string updateSql = normalizedRole switch
            {
                "MKR" or "MAKER" => @"UPDATE dbo.tbl_BroadCastMessage
                                        SET entryby = 'Checker',
                                            IsVerified = 'N',
                                            VerifiedBy = NULL,
                                            VerifiedDate = NULL,
                                            VerifiedIP = NULL,
                                            UpdatedBy = @action_by,
                                            UpdatedDate = GETDATE()
                                        WHERE Bmsg_id = @bmsg_id
                                          AND UPPER(ISNULL(NULLIF(LTRIM(RTRIM(IsVerified)), ''), 'N')) IN ('N', '0', 'FALSE')
                                          AND UPPER(ISNULL(NULLIF(LTRIM(RTRIM(IsApproved)), ''), 'N')) IN ('N', '0', 'FALSE')
                                          AND UPPER(ISNULL(entryby, '')) IN ('MAKER', 'MKR');",
                "CKR" or "CHECKER" => @"UPDATE dbo.tbl_BroadCastMessage
                                          SET entryby = 'Approver',
                                              IsVerified = 'Y',
                                              VerifiedBy = @action_by,
                                              VerifiedDate = GETDATE(),
                                              VerifiedIP = @action_ip,
                                              UpdatedBy = @action_by,
                                              UpdatedDate = GETDATE()
                                          WHERE Bmsg_id = @bmsg_id
                                            AND UPPER(ISNULL(NULLIF(LTRIM(RTRIM(IsVerified)), ''), 'N')) IN ('N', '0', 'FALSE')
                                            AND UPPER(ISNULL(NULLIF(LTRIM(RTRIM(IsApproved)), ''), 'N')) IN ('N', '0', 'FALSE')
                                            AND UPPER(ISNULL(entryby, '')) IN ('CHECKER', 'CKR');",
                "APR" or "APPROVER" => @"UPDATE dbo.tbl_BroadCastMessage
                                           SET entryby = 'Approved',
                                               IsApproved = 'Y',
                                               ApprovedBy = @action_by,
                                               ApprovedDate = GETDATE(),
                                               ApprovedIP = @action_ip,
                                               UpdatedBy = @action_by,
                                               UpdatedDate = GETDATE()
                                           WHERE Bmsg_id = @bmsg_id
                                             AND UPPER(ISNULL(NULLIF(LTRIM(RTRIM(IsVerified)), ''), 'N')) IN ('Y', '1', 'TRUE')
                                             AND UPPER(ISNULL(NULLIF(LTRIM(RTRIM(IsApproved)), ''), 'N')) IN ('N', '0', 'FALSE')
                                             AND UPPER(ISNULL(entryby, '')) IN ('APPROVER', 'APR');",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(updateSql)) return 0;

            await using var connection = _dataAccess.GetConnection();
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                int processedCount = 0;
                foreach (long bmsgId in bmsgIds.Where(id => id > 0).Distinct())
                {
                    await using var command = new SqlCommand(updateSql, connection, transaction);
                    command.Parameters.Add(new SqlParameter("@bmsg_id", SqlDbType.BigInt) { Value = bmsgId });
                    command.Parameters.Add(new SqlParameter("@action_by", SqlDbType.NVarChar, 255) { Value = actionBy.Trim() });
                    command.Parameters.Add(new SqlParameter("@action_ip", SqlDbType.NVarChar, 50)
                    {
                        Value = string.IsNullOrWhiteSpace(actionIp) ? DBNull.Value : actionIp.Trim()
                    });
                    processedCount += await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return processedCount;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public Task<List<MessageTypeDto>> GetMessageTypesAsync()
        {
            const string query = @"SELECT MesgTypeID, MesgTypeName, MsgAbbrCode
                                   FROM dbo.MessageType
                                   ORDER BY MesgTypeID;";
            DataTable dt = _dataAccess.GetDataTable(query, Array.Empty<SqlParameter>());
            var messageTypes = new List<MessageTypeDto>();

            foreach (DataRow row in dt.Rows)
            {
                messageTypes.Add(new MessageTypeDto
                {
                    MesgTypeId = SafeLong(row, "MesgTypeID"),
                    MesgTypeName = SafeString(row, "MesgTypeName") ?? string.Empty,
                    MsgAbbrCode = SafeString(row, "MsgAbbrCode") ?? string.Empty
                });
            }

            return Task.FromResult(messageTypes);
        }

        public Task<string?> GetMessageTypeAbbreviationAsync(long mesgTypeId)
        {
            const string query = @"SELECT TOP (1) MsgAbbrCode
                                   FROM dbo.MessageType
                                   WHERE MesgTypeID = @mesg_type_id;";
            SqlParameter[] parameters =
            {
                new SqlParameter("@mesg_type_id", SqlDbType.BigInt) { Value = mesgTypeId }
            };

            DataTable dt = _dataAccess.GetDataTable(query, parameters);
            string? abbreviation = dt.Rows.Count == 0
                ? null
                : SafeString(dt.Rows[0], "MsgAbbrCode")?.Trim();
            return Task.FromResult(abbreviation);
        }

        public async Task<long> InsertBroadcastMessageAsync(InsertBroadcastMessageRequest request)
        {
            const string insertSql = @"INSERT INTO dbo.tbl_BroadCastMessage (message_head, message_body, validity_in_minutes, is_active, Portal_id, User_Type_ID, photo1path, photo2path, videopath, documentpath, audiopath, MesgTypeID, entryby, entrydate, IsSynced, IsDeleted, IsAllDistrict)
                                       OUTPUT INSERTED.Bmsg_id
                                       VALUES (@message_head, @message_body, @validity_in_minutes, @is_active, @portal_id, @user_type_id, @photo1path, @photo2path, @videopath, @documentpath, @audiopath, @mesg_type_id, @entryby, GETDATE(), 'N','N', @is_all_district);";

            string activeVal = string.IsNullOrWhiteSpace(request.IsActive) ? "Y" : request.IsActive.Trim().Substring(0, 1).ToUpperInvariant();

            await using var connection = _dataAccess.GetConnection();
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                long bmsgId;
                await using (var insertCommand = new SqlCommand(insertSql, connection, transaction))
                {
                    insertCommand.CommandTimeout = 120;
                    insertCommand.Parameters.AddRange(new[]
                    {
                        new SqlParameter("@message_head", SqlDbType.NVarChar, 255) { Value = string.IsNullOrWhiteSpace(request.MessageHead) ? (object)DBNull.Value : request.MessageHead.Trim() },
                        new SqlParameter("@message_body", SqlDbType.NVarChar, -1) { Value = request.MessageBody?.Trim() ?? string.Empty },
                        new SqlParameter("@validity_in_minutes", SqlDbType.SmallInt) { Value = request.ValidityInMinutes.HasValue ? (object)request.ValidityInMinutes.Value : 60 },
                        new SqlParameter("@is_active", SqlDbType.Char, 1) { Value = activeVal },
                        new SqlParameter("@is_all_district", SqlDbType.Char, 1) { Value = request.IsAllDistrict ? "Y" : "N" },
                        new SqlParameter("@portal_id", SqlDbType.BigInt) { Value = request.PortalId.HasValue && request.PortalId.Value > 0 ? (object)request.PortalId.Value : DBNull.Value },
                        new SqlParameter("@user_type_id", SqlDbType.NVarChar, 100) { Value = string.IsNullOrWhiteSpace(request.UserTypeId) ? (object)DBNull.Value : request.UserTypeId.Trim() },
                        new SqlParameter("@photo1path", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(request.Photo1Path) ? (object)DBNull.Value : request.Photo1Path.Trim() },
                        new SqlParameter("@photo2path", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(request.Photo2Path) ? (object)DBNull.Value : request.Photo2Path.Trim() },
                        new SqlParameter("@videopath", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(request.VideoPath) ? (object)DBNull.Value : request.VideoPath.Trim() },
                        new SqlParameter("@documentpath", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(request.DocumentPath) ? (object)DBNull.Value : request.DocumentPath.Trim() },
                        new SqlParameter("@audiopath", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(request.AudioPath) ? (object)DBNull.Value : request.AudioPath.Trim() },
                        new SqlParameter("@mesg_type_id", SqlDbType.BigInt) { Value = request.MesgTypeId.HasValue && request.MesgTypeId.Value > 0 ? request.MesgTypeId.Value : 1 },
                        new SqlParameter("@entryby", SqlDbType.NVarChar, 255) { Value = string.IsNullOrWhiteSpace(request.EntryBy) ? "Admin" : request.EntryBy.Trim() }
                    });

                    object? insertedId = await insertCommand.ExecuteScalarAsync();
                    if (insertedId == null || insertedId == DBNull.Value)
                    {
                        throw new InvalidOperationException("Failed to retrieve the generated broadcast message ID.");
                    }

                    bmsgId = Convert.ToInt64(insertedId);
                }

                if (!request.IsAllDistrict)
                {
                    const string insertDistrictMapSql = @"INSERT INTO dbo.tbl_BroadCastMapDistrict (Bmsg_id, District_code, Block_code, Panchayatcode)
                                                           VALUES (@bmsg_id, @district_code, NULL, NULL);";

                    foreach (string districtCode in request.DistrictCodes!.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        await using var districtMapCommand = new SqlCommand(insertDistrictMapSql, connection, transaction);
                        districtMapCommand.CommandTimeout = 120;
                        districtMapCommand.Parameters.Add(new SqlParameter("@bmsg_id", SqlDbType.BigInt) { Value = bmsgId });
                        districtMapCommand.Parameters.Add(new SqlParameter("@district_code", SqlDbType.NVarChar, 50) { Value = districtCode });
                        await districtMapCommand.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
                return bmsgId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public Task<int> UpdateBroadcastAttachmentPathsAsync(
            long bmsgId,
            string? photo1Path,
            string? photo2Path,
            string? videoPath,
            string? documentPath,
            string? audioPath)
        {
            const string query = @"UPDATE dbo.tbl_BroadCastMessage
                                   SET photo1path = @photo1path,
                                       photo2path = @photo2path,
                                       videopath = @videopath,
                                       documentpath = @documentpath,
                                       audiopath = @audiopath,
                                       updateddate = GETDATE()
                                   WHERE Bmsg_id = @bmsg_id;";
            SqlParameter[] parameters =
            {
                new SqlParameter("@bmsg_id", SqlDbType.BigInt) { Value = bmsgId },
                new SqlParameter("@photo1path", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(photo1Path) ? DBNull.Value : photo1Path },
                new SqlParameter("@photo2path", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(photo2Path) ? DBNull.Value : photo2Path },
                new SqlParameter("@videopath", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(videoPath) ? DBNull.Value : videoPath },
                new SqlParameter("@documentpath", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(documentPath) ? DBNull.Value : documentPath },
                new SqlParameter("@audiopath", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(audioPath) ? DBNull.Value : audioPath }
            };

            return Task.FromResult(_dataAccess.ExecuteSql(query, parameters));
        }

        public Task<int> DeleteBroadcastMessageAsync(long bmsgId)
        {
            const string query = @"DELETE FROM dbo.tbl_BroadCastMessage
                                   WHERE Bmsg_id = @bmsg_id;";
            SqlParameter[] parameters =
            {
                new SqlParameter("@bmsg_id", SqlDbType.BigInt) { Value = bmsgId }
            };

            return Task.FromResult(_dataAccess.ExecuteSql(query, parameters));
        }

        public Task<BroadcastMessageDto?> GetBroadcastMessageByIdAsync(long bmsgId)
        {
            const string query = @"SELECT Bmsg_id, MesgTypeID, message_head, message_body, validity_in_minutes, is_active, Portal_id, User_Type_ID, photo1path, photo2path, videopath, documentpath, audiopath, entryby, entrydate, updatedby, updateddate, IsSynced, IsDeleted,
                                          IsVerified, IsApproved
                                   FROM dbo.tbl_BroadCastMessage
                                   WHERE Bmsg_id = @bmsg_id;";

            SqlParameter[] parameters =
            {
                new SqlParameter("@bmsg_id", SqlDbType.BigInt) { Value = bmsgId }
            };

            DataTable dt = _dataAccess.GetDataTable(query, parameters);
            if (dt == null || dt.Rows.Count == 0)
            {
                return Task.FromResult<BroadcastMessageDto?>(null);
            }

            DataRow row = dt.Rows[0];
            var dto = new BroadcastMessageDto
            {
                Bmsg_id = SafeLong(row, "Bmsg_id"),
                MesgTypeId = SafeNullableLong(row, "MesgTypeID"),
                MessageHead = SafeString(row, "message_head"),
                MessageBody = SafeString(row, "message_body"),
                ValidityInMinutes = SafeNullableShort(row, "validity_in_minutes", 60),
                IsActive = SafeString(row, "is_active") ?? "Y",
                PortalId = SafeNullableLong(row, "Portal_id"),
                UserTypeId = SafeString(row, "User_Type_ID"),
                photo1path = SafeString(row, "photo1path"),
                photo2path = SafeString(row, "photo2path"),
                videopath = SafeString(row, "videopath"),
                documentpath = SafeString(row, "documentpath"),
                audiopath = SafeString(row, "audiopath"),
                EntryBy = SafeString(row, "entryby"),
                EntryDate = SafeNullableDateTime(row, "entrydate"),
                UpdatedBy = SafeString(row, "updatedby"),
                UpdatedDate = SafeNullableDateTime(row, "updateddate"),
                IsSynced = SafeString(row, "IsSynced") ?? "N",
                IsDeleted = SafeString(row, "IsDeleted") ?? "N",
                IsVerified = SafeNullableBool(row, "IsVerified"),
                IsApproved = SafeNullableBool(row, "IsApproved")
            };

            return Task.FromResult<BroadcastMessageDto?>(dto);
        }

        public Task<bool> BroadcastMessageExistsAsync(long bmsgId)
        {
            const string query = @"SELECT COUNT(1) FROM dbo.tbl_BroadCastMessage WHERE Bmsg_id = @bmsg_id;";
            SqlParameter[] parameters =
            {
                new SqlParameter("@bmsg_id", SqlDbType.BigInt) { Value = bmsgId }
            };

            object? result = _dataAccess.ExecuteScalar(query, parameters);
            bool exists = result != null && result != DBNull.Value && Convert.ToInt64(result) > 0;
            return Task.FromResult(exists);
        }

        public Task<int> UpdateBroadcastVerifiedStatusAsync(
            long bmsgId,
            bool isVerified,
            string? verifiedBy,
            string? verifiedIp)
        {
            const string query = @"UPDATE dbo.tbl_BroadCastMessage
                                   SET IsVerified = @is_verified,
                                       entryby = CASE WHEN @is_verified = 'Y' THEN 'Approver' ELSE 'Maker' END,
                                       IsApproved = 'N',
                                       ApprovedBy = NULL,
                                       ApprovedDate = NULL,
                                       ApprovedIP = NULL,
                                       VerifiedBy = @verified_by,
                                       VerifiedDate = GETDATE(),
                                       VerifiedIP = @verified_ip,
                                       UpdatedBy = @verified_by,
                                       UpdatedDate = GETDATE()
                                   WHERE Bmsg_id = @bmsg_id
                                     AND UPPER(ISNULL(NULLIF(LTRIM(RTRIM(IsApproved)), ''), 'N')) IN ('N', '0', 'FALSE')
                                     AND UPPER(ISNULL(entryby, '')) IN ('CHECKER', 'CKR');";

            SqlParameter[] parameters =
            {
                new SqlParameter("@bmsg_id", SqlDbType.BigInt) { Value = bmsgId },
                new SqlParameter("@is_verified", SqlDbType.Char, 1) { Value = isVerified ? "Y" : "N" },
                new SqlParameter("@verified_by", SqlDbType.NVarChar, 255)
                {
                    Value = string.IsNullOrWhiteSpace(verifiedBy) ? "Checker" : verifiedBy.Trim()
                },
                new SqlParameter("@verified_ip", SqlDbType.NVarChar, 50)
                {
                    Value = string.IsNullOrWhiteSpace(verifiedIp) ? DBNull.Value : verifiedIp.Trim()
                }
            };

            int rowsAffected = _dataAccess.ExecuteSql(query, parameters);
            return Task.FromResult(rowsAffected);
        }

        public Task<int> UpdateBroadcastApprovedStatusAsync(
            long bmsgId,
            bool isApproved,
            string? approvedBy,
            string? approvedIp)
        {
            const string query = @"UPDATE dbo.tbl_BroadCastMessage
                                   SET IsApproved = @is_approved,
                                       entryby = CASE WHEN @is_approved = 'Y' THEN 'Approved' ELSE 'Checker' END,
                                       IsVerified = CASE WHEN @is_approved = 'Y' THEN 'Y' ELSE 'N' END,
                                       ApprovedBy = @approved_by,
                                       ApprovedDate = GETDATE(),
                                       ApprovedIP = @approved_ip,
                                       UpdatedBy = @approved_by,
                                       UpdatedDate = GETDATE()
                                   WHERE Bmsg_id = @bmsg_id
                                     AND (@is_approved = 'Y' OR UPPER(ISNULL(NULLIF(LTRIM(RTRIM(IsApproved)), ''), 'N')) IN ('N', '0', 'FALSE'))
                                     AND UPPER(ISNULL(entryby, '')) IN ('APPROVER', 'APR');";

            SqlParameter[] parameters =
            {
                new SqlParameter("@bmsg_id", SqlDbType.BigInt) { Value = bmsgId },
                new SqlParameter("@is_approved", SqlDbType.Char, 1) { Value = isApproved ? "Y" : "N" },
                new SqlParameter("@approved_by", SqlDbType.NVarChar, 255)
                {
                    Value = string.IsNullOrWhiteSpace(approvedBy) ? "Approver" : approvedBy.Trim()
                },
                new SqlParameter("@approved_ip", SqlDbType.NVarChar, 50)
                {
                    Value = string.IsNullOrWhiteSpace(approvedIp) ? DBNull.Value : approvedIp.Trim()
                }
            };

            int rowsAffected = _dataAccess.ExecuteSql(query, parameters);
            return Task.FromResult(rowsAffected);
        }

        public Task<long> InsertInterestWiseMessageAsync(InsertInterestWiseMessageRequest request)
        {
            const string insertSql = @"INSERT INTO dbo.tbl_Message (
                                            message_head,
                                            message_body,
                                            validity_in_minutes,
                                            is_active,
                                            interest_id,
                                            photo1path,
                                            photo2path,
                                            videopath,
                                            documentpath,
                                            youtubeurl,
                                            facebookurl,
                                            instagramurl,
                                            Xurl,
                                            entryby,
                                            entrydate,
                                            IsDeleted
                                       )
                                       OUTPUT INSERTED.msg_id
                                       VALUES (
                                            @message_head,
                                            @message_body,
                                            @validity_in_minutes,
                                            @is_active,
                                            @interest_id,
                                            @photo1path,
                                            @photo2path,
                                            @videopath,
                                            @documentpath,
                                            @youtubeurl,
                                            @facebookurl,
                                            @instagramurl,
                                            @Xurl,
                                            @entryby,
                                            GETDATE(),
                                            'N'
                                       );";

            string activeVal = string.IsNullOrWhiteSpace(request.IsActive) ? "Y" : request.IsActive.Trim().Substring(0, 1).ToUpperInvariant();

            SqlParameter[] parameters =
            {
                new SqlParameter("@message_head", SqlDbType.NVarChar, 255)
                {
                    Value = string.IsNullOrWhiteSpace(request.MessageHead) ? (object)DBNull.Value : request.MessageHead.Trim()
                },
                new SqlParameter("@message_body", SqlDbType.NVarChar, -1)
                {
                    Value = request.MessageBody?.Trim() ?? string.Empty
                },
                new SqlParameter("@validity_in_minutes", SqlDbType.SmallInt)
                {
                    Value = request.ValidityInMinutes.HasValue ? (object)request.ValidityInMinutes.Value : 60
                },
                new SqlParameter("@is_active", SqlDbType.Char, 1)
                {
                    Value = activeVal
                },
                new SqlParameter("@interest_id", SqlDbType.BigInt)
                {
                    Value = request.InterestId
                },
                new SqlParameter("@photo1path", SqlDbType.NVarChar, -1)
                {
                    Value = string.IsNullOrWhiteSpace(request.Photo1Path) ? (object)DBNull.Value : request.Photo1Path.Trim()
                },
                new SqlParameter("@photo2path", SqlDbType.NVarChar, -1)
                {
                    Value = string.IsNullOrWhiteSpace(request.Photo2Path) ? (object)DBNull.Value : request.Photo2Path.Trim()
                },
                new SqlParameter("@videopath", SqlDbType.NVarChar, -1)
                {
                    Value = string.IsNullOrWhiteSpace(request.VideoPath) ? (object)DBNull.Value : request.VideoPath.Trim()
                },
                new SqlParameter("@documentpath", SqlDbType.NVarChar, -1)
                {
                    Value = string.IsNullOrWhiteSpace(request.DocumentPath) ? (object)DBNull.Value : request.DocumentPath.Trim()
                },
                new SqlParameter("@youtubeurl", SqlDbType.NVarChar, -1)
                {
                    Value = string.IsNullOrWhiteSpace(request.YoutubeUrl) ? (object)DBNull.Value : request.YoutubeUrl.Trim()
                },
                new SqlParameter("@facebookurl", SqlDbType.NVarChar, -1)
                {
                    Value = string.IsNullOrWhiteSpace(request.FacebookUrl) ? (object)DBNull.Value : request.FacebookUrl.Trim()
                },
                new SqlParameter("@instagramurl", SqlDbType.NVarChar, -1)
                {
                    Value = string.IsNullOrWhiteSpace(request.InstagramUrl) ? (object)DBNull.Value : request.InstagramUrl.Trim()
                },
                new SqlParameter("@Xurl", SqlDbType.NVarChar, -1)
                {
                    Value = string.IsNullOrWhiteSpace(request.XUrl) ? (object)DBNull.Value : request.XUrl.Trim()
                },
                new SqlParameter("@entryby", SqlDbType.NVarChar, 255)
                {
                    Value = string.IsNullOrWhiteSpace(request.EntryBy) ? "Admin" : request.EntryBy.Trim()
                }
            };

            DataTable dt = _dataAccess.GetDataTable(insertSql, parameters);
            long msgId = 0;
            if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
            {
                msgId = Convert.ToInt64(dt.Rows[0][0]);
            }

            return Task.FromResult(msgId);
        }

        public Task<List<InterestWiseMessageDto>> GetInterestWiseMessagesAsync(long? interestId = null)
        {
            string query = @"SELECT m.msg_id,
                                    m.interest_id,
                                    i.interest_name,
                                    m.message_head,
                                    m.message_body,
                                    m.validity_in_minutes,
                                    m.is_active,
                                    m.photo1path,
                                    m.photo2path,
                                    m.videopath,
                                    m.documentpath,
                                    m.youtubeurl,
                                    m.facebookurl,
                                    m.instagramurl,
                                    m.Xurl,
                                    m.entryby,
                                    m.entrydate,
                                    m.IsDeleted
                             FROM dbo.tbl_Message m
                             LEFT JOIN dbo.mst_interest i ON m.interest_id = i.interest_id
                             WHERE m.interest_id IS NOT NULL
                               AND ISNULL(m.IsDeleted, 'N') <> 'Y' "
                               + (interestId.HasValue ? " AND m.interest_id = @interest_id " : "")
                               + " ORDER BY m.msg_id DESC;";

            SqlParameter[] parameters = interestId.HasValue
                ? new[] { new SqlParameter("@interest_id", SqlDbType.BigInt) { Value = interestId.Value } }
                : Array.Empty<SqlParameter>();

            DataTable dt = _dataAccess.GetDataTable(query, parameters);
            var list = new List<InterestWiseMessageDto>();

            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new InterestWiseMessageDto
                    {
                        MsgId = SafeLong(row, "msg_id"),
                        InterestId = SafeNullableLong(row, "interest_id"),
                        InterestName = SafeString(row, "interest_name"),
                        MessageHead = SafeString(row, "message_head"),
                        MessageBody = SafeString(row, "message_body"),
                        ValidityInMinutes = SafeNullableShort(row, "validity_in_minutes", 60),
                        IsActive = SafeString(row, "is_active") ?? "Y",
                        Photo1Path = SafeString(row, "photo1path"),
                        Photo2Path = SafeString(row, "photo2path"),
                        VideoPath = SafeString(row, "videopath"),
                        DocumentPath = SafeString(row, "documentpath"),
                        YoutubeUrl = SafeString(row, "youtubeurl"),
                        FacebookUrl = SafeString(row, "facebookurl"),
                        InstagramUrl = SafeString(row, "instagramurl"),
                        XUrl = SafeString(row, "Xurl"),
                        EntryBy = SafeString(row, "entryby"),
                        EntryDate = SafeNullableDateTime(row, "entrydate"),
                        IsDeleted = SafeString(row, "IsDeleted") ?? "N"
                    });
                }
            }

            return Task.FromResult(list);
        }

        private static long SafeLong(DataRow row, string colName, long defaultVal = 0)
        {
            if (!row.Table.Columns.Contains(colName)) return defaultVal;
            var val = row[colName];
            if (val == null || val == DBNull.Value) return defaultVal;
            return long.TryParse(val.ToString(), out long res) ? res : defaultVal;
        }

        private static long? SafeNullableLong(DataRow row, string colName)
        {
            if (!row.Table.Columns.Contains(colName)) return null;
            var val = row[colName];
            if (val == null || val == DBNull.Value) return null;
            return long.TryParse(val.ToString(), out long res) ? res : null;
        }

        private static short? SafeNullableShort(DataRow row, string colName, short? defaultVal = 60)
        {
            if (!row.Table.Columns.Contains(colName)) return defaultVal;
            var val = row[colName];
            if (val == null || val == DBNull.Value) return defaultVal;
            return short.TryParse(val.ToString(), out short res) ? res : defaultVal;
        }

        private static DateTime? SafeNullableDateTime(DataRow row, string colName)
        {
            if (!row.Table.Columns.Contains(colName)) return null;
            var val = row[colName];
            if (val == null || val == DBNull.Value) return null;
            return DateTime.TryParse(val.ToString(), out DateTime res) ? res : null;
        }

        private static string? SafeString(DataRow row, string colName)
        {
            if (!row.Table.Columns.Contains(colName)) return null;
            var val = row[colName];
            if (val == null || val == DBNull.Value) return null;
            return val.ToString();
        }

        private static bool? SafeNullableBool(DataRow row, string colName)
        {
            if (!row.Table.Columns.Contains(colName)) return null;
            var val = row[colName];
            if (val == null || val == DBNull.Value) return null;
            if (bool.TryParse(val.ToString(), out bool b)) return b;
            string str = val.ToString()?.Trim() ?? string.Empty;
            if (str == "1" || str.Equals("Y", StringComparison.OrdinalIgnoreCase) || str.Equals("True", StringComparison.OrdinalIgnoreCase)) return true;
            if (str == "0" || str.Equals("N", StringComparison.OrdinalIgnoreCase) || str.Equals("False", StringComparison.OrdinalIgnoreCase)) return false;
            return null;
        }
    }
}
