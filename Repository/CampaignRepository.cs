using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SankalpAPI.Helper;
using SankalpAPI.Interface;
using SankalpAPI.Model;

namespace SankalpAPI.Repository
{
    public class CampaignRepository : ICampaignRepository
    {
        private readonly clsDataAccess _dataAccess;
        private readonly ExceptionLogging _exceptionLogging;

        private static bool _schemaInitialized = false;
        private static readonly object _schemaLock = new object();

        public CampaignRepository(clsDataAccess dataAccess, ExceptionLogging exceptionLogging)
        {
            _dataAccess = dataAccess;
            _exceptionLogging = exceptionLogging;
        }

        private void EnsureSchemaInitialized()
        {
            if (_schemaInitialized) return;

            lock (_schemaLock)
            {
                if (_schemaInitialized) return;

                try
                {
                    const string ddl = @"
                        IF OBJECT_ID(N'dbo.tbl_CampaignAuditHistory', N'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.tbl_CampaignAuditHistory
                            (
                                AuditId          BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                CampaignId       BIGINT               NOT NULL,
                                Action           NVARCHAR(50)         NOT NULL,
                                FromStatus       NVARCHAR(50)         NULL,
                                ToStatus         NVARCHAR(50)         NOT NULL,
                                ActionBy         NVARCHAR(150)        NOT NULL,
                                ActionRole       NVARCHAR(50)         NULL,
                                ActionDate       DATETIME             NOT NULL DEFAULT (GETUTCDATE()),
                                Remarks          NVARCHAR(1000)       NULL,
                                ActionIP         NVARCHAR(50)         NULL
                            );

                            CREATE NONCLUSTERED INDEX IX_tbl_CampaignAuditHistory_CampaignId
                                ON dbo.tbl_CampaignAuditHistory (CampaignId ASC, ActionDate DESC);
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_MessageCampaign', N'U') AND name = N'RejectionReason')
                        BEGIN
                            ALTER TABLE dbo.tbl_MessageCampaign ADD RejectionReason NVARCHAR(1000) NULL;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_MessageCampaign', N'U') AND name = N'VerifiedRemarks')
                        BEGIN
                            ALTER TABLE dbo.tbl_MessageCampaign ADD VerifiedRemarks NVARCHAR(1000) NULL;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_MessageCampaign', N'U') AND name = N'ApprovedRemarks')
                        BEGIN
                            ALTER TABLE dbo.tbl_MessageCampaign ADD ApprovedRemarks NVARCHAR(1000) NULL;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_MessageCampaign', N'U') AND name = N'LastActionRole')
                        BEGIN
                            ALTER TABLE dbo.tbl_MessageCampaign ADD LastActionRole NVARCHAR(50) NULL;
                        END;";

                    using var con = _dataAccess.GetConnection();
                    using var cmd = new SqlCommand(ddl, con);
                    cmd.CommandTimeout = 30;
                    con.Open();
                    cmd.ExecuteNonQuery();

                    _schemaInitialized = true;
                }
                catch (Exception ex)
                {
                    _exceptionLogging.SendErrorToText(ex);
                }
            }
        }

        public Task<long> CreateCampaignAsync(CreateCampaignRequest request, long bmsgId)
        {
            EnsureSchemaInitialized();

            const string insertCampaignSql = @"
                INSERT INTO dbo.tbl_MessageCampaign (
                    CampaignName,
                    CampaignDescription,
                    Bmsg_id,
                    CampaignStatus,
                    StartDate,
                    EndDate,
                    ScheduledSendTime,
                    TargetScope,
                    Portal_id,
                    User_Type_ID,
                    IsAllDistrict,
                    IsAllBlock,
                    IsAllPanchayat,
                    CreatedBy,
                    CreatedDate
                )
                OUTPUT INSERTED.CampaignId
                VALUES (
                    @CampaignName,
                    @CampaignDescription,
                    @Bmsg_id,
                    @CampaignStatus,
                    @StartDate,
                    @EndDate,
                    @ScheduledSendTime,
                    @TargetScope,
                    @Portal_id,
                    @User_Type_ID,
                    @IsAllDistrict,
                    @IsAllBlock,
                    @IsAllPanchayat,
                    @CreatedBy,
                    GETUTCDATE()
                );";

            string initialStatus = request.SubmitForVerification 
                ? CampaignStatusConsts.SubmittedForVerification 
                : CampaignStatusConsts.Draft;

            SqlParameter[] parameters =
            {
                new SqlParameter("@CampaignName", SqlDbType.NVarChar, 250) { Value = request.CampaignName.Trim() },
                new SqlParameter("@CampaignDescription", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(request.CampaignDescription) ? (object)DBNull.Value : request.CampaignDescription.Trim() },
                new SqlParameter("@Bmsg_id", SqlDbType.BigInt) { Value = bmsgId },
                new SqlParameter("@CampaignStatus", SqlDbType.NVarChar, 50) { Value = initialStatus },
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = request.StartDate.HasValue ? (object)request.StartDate.Value : DBNull.Value },
                new SqlParameter("@EndDate", SqlDbType.DateTime) { Value = request.EndDate.HasValue ? (object)request.EndDate.Value : DBNull.Value },
                new SqlParameter("@ScheduledSendTime", SqlDbType.DateTime) { Value = request.ScheduledSendTime.HasValue ? (object)request.ScheduledSendTime.Value : DBNull.Value },
                new SqlParameter("@TargetScope", SqlDbType.NVarChar, 50) { Value = string.IsNullOrWhiteSpace(request.TargetScope) ? "ALL" : request.TargetScope.Trim().ToUpperInvariant() },
                new SqlParameter("@Portal_id", SqlDbType.BigInt) { Value = request.PortalId.HasValue ? (object)request.PortalId.Value : DBNull.Value },
                new SqlParameter("@User_Type_ID", SqlDbType.BigInt) { Value = request.UserTypeId.HasValue ? (object)request.UserTypeId.Value : DBNull.Value },
                new SqlParameter("@IsAllDistrict", SqlDbType.Char, 1) { Value = string.IsNullOrWhiteSpace(request.IsAllDistrict) ? "Y" : request.IsAllDistrict.Trim().ToUpperInvariant() },
                new SqlParameter("@IsAllBlock", SqlDbType.Char, 1) { Value = string.IsNullOrWhiteSpace(request.IsAllBlock) ? "Y" : request.IsAllBlock.Trim().ToUpperInvariant() },
                new SqlParameter("@IsAllPanchayat", SqlDbType.Char, 1) { Value = string.IsNullOrWhiteSpace(request.IsAllPanchayat) ? "Y" : request.IsAllPanchayat.Trim().ToUpperInvariant() },
                new SqlParameter("@CreatedBy", SqlDbType.NVarChar, 150) { Value = string.IsNullOrWhiteSpace(request.CreatedBy) ? "Admin" : request.CreatedBy.Trim() }
            };

            DataTable dt = _dataAccess.GetDataTable(insertCampaignSql, parameters);
            long campaignId = 0;
            if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
            {
                campaignId = Convert.ToInt64(dt.Rows[0][0]);
            }

            if (campaignId > 0)
            {
                if (request.TargetLocations != null && request.TargetLocations.Count > 0 && request.IsAllDistrict != "Y")
                {
                    SaveTargetLocations(campaignId, request.TargetLocations);
                }

                string action = request.SubmitForVerification ? "SUBMITTED_FOR_VERIFICATION" : "CREATED";
                string remarks = request.SubmitForVerification
                    ? "Campaign created and submitted for verification by Maker."
                    : "Campaign created as draft by Maker.";

                RecordAuditLogSync(campaignId, action, null, initialStatus, request.CreatedBy, CampaignRoles.Maker, remarks);
            }

            return Task.FromResult(campaignId);
        }

        public Task<bool> UpdateCampaignAsync(long campaignId, UpdateCampaignRequest request)
        {
            EnsureSchemaInitialized();

            // Look up current status to preserve decision trail
            string fromStatus = CampaignStatusConsts.Draft;
            try
            {
                const string queryStatus = "SELECT TOP 1 CampaignStatus FROM dbo.tbl_MessageCampaign WHERE CampaignId = @CampaignId;";
                SqlParameter[] statusParams = { new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId } };
                DataTable dtStatus = _dataAccess.GetDataTable(queryStatus, statusParams);
                if (dtStatus != null && dtStatus.Rows.Count > 0 && dtStatus.Rows[0][0] != DBNull.Value)
                {
                    fromStatus = CampaignStatusConsts.Normalize(dtStatus.Rows[0][0].ToString());
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
            }

            string targetStatus = request.SubmitForVerification 
                ? CampaignStatusConsts.SubmittedForVerification 
                : CampaignStatusConsts.Draft;

            const string updateSql = @"
                UPDATE dbo.tbl_MessageCampaign SET
                    CampaignName = @CampaignName,
                    CampaignDescription = @CampaignDescription,
                    StartDate = @StartDate,
                    EndDate = @EndDate,
                    ScheduledSendTime = @ScheduledSendTime,
                    TargetScope = @TargetScope,
                    Portal_id = @Portal_id,
                    User_Type_ID = @User_Type_ID,
                    IsAllDistrict = @IsAllDistrict,
                    IsAllBlock = @IsAllBlock,
                    IsAllPanchayat = @IsAllPanchayat,
                    CampaignStatus = @NewStatus,
                    UpdatedBy = @UpdatedBy,
                    UpdatedDate = GETUTCDATE()
                WHERE CampaignId = @CampaignId
                  AND CampaignStatus IN ('DRAFT', 'SUBMITTED_FOR_VERIFICATION', 'REJECTED_BY_CKR', 'REJECTED_BY_APR', 'Draft', 'Pending Verification');";

            SqlParameter[] parameters =
            {
                new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId },
                new SqlParameter("@CampaignName", SqlDbType.NVarChar, 250) { Value = request.CampaignName.Trim() },
                new SqlParameter("@CampaignDescription", SqlDbType.NVarChar, -1) { Value = string.IsNullOrWhiteSpace(request.CampaignDescription) ? (object)DBNull.Value : request.CampaignDescription.Trim() },
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = request.StartDate.HasValue ? (object)request.StartDate.Value : DBNull.Value },
                new SqlParameter("@EndDate", SqlDbType.DateTime) { Value = request.EndDate.HasValue ? (object)request.EndDate.Value : DBNull.Value },
                new SqlParameter("@ScheduledSendTime", SqlDbType.DateTime) { Value = request.ScheduledSendTime.HasValue ? (object)request.ScheduledSendTime.Value : DBNull.Value },
                new SqlParameter("@TargetScope", SqlDbType.NVarChar, 50) { Value = string.IsNullOrWhiteSpace(request.TargetScope) ? "ALL" : request.TargetScope.Trim().ToUpperInvariant() },
                new SqlParameter("@Portal_id", SqlDbType.BigInt) { Value = request.PortalId.HasValue ? (object)request.PortalId.Value : DBNull.Value },
                new SqlParameter("@User_Type_ID", SqlDbType.BigInt) { Value = request.UserTypeId.HasValue ? (object)request.UserTypeId.Value : DBNull.Value },
                new SqlParameter("@IsAllDistrict", SqlDbType.Char, 1) { Value = string.IsNullOrWhiteSpace(request.IsAllDistrict) ? "Y" : request.IsAllDistrict.Trim().ToUpperInvariant() },
                new SqlParameter("@IsAllBlock", SqlDbType.Char, 1) { Value = string.IsNullOrWhiteSpace(request.IsAllBlock) ? "Y" : request.IsAllBlock.Trim().ToUpperInvariant() },
                new SqlParameter("@IsAllPanchayat", SqlDbType.Char, 1) { Value = string.IsNullOrWhiteSpace(request.IsAllPanchayat) ? "Y" : request.IsAllPanchayat.Trim().ToUpperInvariant() },
                new SqlParameter("@NewStatus", SqlDbType.NVarChar, 50) { Value = targetStatus },
                new SqlParameter("@UpdatedBy", SqlDbType.NVarChar, 150) { Value = string.IsNullOrWhiteSpace(request.UpdatedBy) ? "Admin" : request.UpdatedBy.Trim() }
            };

            int rows = 0;
            using (var con = _dataAccess.GetConnection())
            using (var cmd = new SqlCommand(updateSql, con))
            {
                cmd.Parameters.AddRange(parameters);
                con.Open();
                rows = cmd.ExecuteNonQuery();
            }

            if (rows > 0)
            {
                // Refresh locations
                const string deleteLocSql = "DELETE FROM dbo.tbl_CampaignTargetLocation WHERE CampaignId = @CampaignId;";
                using (var con = _dataAccess.GetConnection())
                using (var cmd = new SqlCommand(deleteLocSql, con))
                {
                    cmd.Parameters.Add(new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId });
                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                if (request.TargetLocations != null && request.TargetLocations.Count > 0 && request.IsAllDistrict != "Y")
                {
                    SaveTargetLocations(campaignId, request.TargetLocations);
                }

                string action = request.SubmitForVerification
                    ? (fromStatus == CampaignStatusConsts.RejectedByCkr || fromStatus == CampaignStatusConsts.RejectedByApr
                        ? "RESUBMITTED_FOR_VERIFICATION"
                        : "SUBMITTED_FOR_VERIFICATION")
                    : "UPDATED_DRAFT";

                string remarks = request.SubmitForVerification
                    ? "Campaign resubmitted for verification by Maker after modifications."
                    : "Campaign draft updated by Maker.";

                RecordAuditLogSync(campaignId, action, fromStatus, targetStatus, request.UpdatedBy, CampaignRoles.Maker, remarks);
            }

            return Task.FromResult(rows > 0);
        }

        private void SaveTargetLocations(long campaignId, List<CampaignLocationDto> locations)
        {
            const string insertLocSql = @"
                INSERT INTO dbo.tbl_CampaignTargetLocation (CampaignId, District_code, Block_code, Panchayatcode)
                VALUES (@CampaignId, @District_code, @Block_code, @Panchayatcode);";

            using (var con = _dataAccess.GetConnection())
            {
                con.Open();
                foreach (var loc in locations)
                {
                    using (var cmd = new SqlCommand(insertLocSql, con))
                    {
                        cmd.Parameters.Add(new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId });
                        cmd.Parameters.Add(new SqlParameter("@District_code", SqlDbType.BigInt) { Value = loc.DistrictCode.HasValue ? (object)loc.DistrictCode.Value : DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@Block_code", SqlDbType.BigInt) { Value = loc.BlockCode.HasValue ? (object)loc.BlockCode.Value : DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@Panchayatcode", SqlDbType.BigInt) { Value = loc.PanchayatCode.HasValue ? (object)loc.PanchayatCode.Value : DBNull.Value });
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public async Task<CampaignDto?> GetCampaignByIdAsync(long campaignId)
        {
            EnsureSchemaInitialized();

            const string query = @"
                SELECT c.*, 
                       p.portal_name,
                       ut.User_Type_Name
                FROM dbo.tbl_MessageCampaign c
                LEFT JOIN dbo.mst_portal p ON c.Portal_id = p.portal_id
                LEFT JOIN dbo.UserType ut ON c.User_Type_ID = ut.User_Type_ID AND (c.Portal_id IS NULL OR ut.Portal_ID = c.Portal_id)
                WHERE c.CampaignId = @CampaignId;";

            SqlParameter[] parameters = { new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId } };
            DataTable dt = _dataAccess.GetDataTable(query, parameters);

            if (dt == null || dt.Rows.Count == 0)
                return null;

            var campaign = MapCampaignRow(dt.Rows[0]);

            // Load associated broadcast message
            campaign.BroadcastMessage = GetBroadcastMessageById(campaign.BmsgId);

            // Load target locations
            campaign.TargetLocations = GetCampaignLocations(campaignId);

            // Load complete audit history
            campaign.AuditHistory = await GetAuditHistoryAsync(campaignId);

            return campaign;
        }

        public Task<(List<CampaignDto> items, int totalCount)> GetAllCampaignsAsync(CampaignListFilter filter)
        {
            EnsureSchemaInitialized();
            SyncUnlinkedBroadcastMessages();

            string whereClause = " WHERE 1=1 ";
            var paramList = new List<SqlParameter>();

            // Maker (MKR) Role Restriction: Maker only sees campaigns created by their user account
            if (CampaignRoles.IsMaker(filter.CurrentUserRole) && !string.IsNullOrWhiteSpace(filter.CurrentUserId))
            {
                whereClause += " AND (c.CreatedBy = @MakerUserId OR c.CreatedBy = @MakerUserName) ";
                paramList.Add(new SqlParameter("@MakerUserId", SqlDbType.NVarChar, 150) { Value = filter.CurrentUserId.Trim() });
                paramList.Add(new SqlParameter("@MakerUserName", SqlDbType.NVarChar, 150) { Value = filter.CurrentUserId.Trim() });
            }
            else if (!string.IsNullOrWhiteSpace(filter.CreatedBy))
            {
                whereClause += " AND c.CreatedBy = @CreatedBy ";
                paramList.Add(new SqlParameter("@CreatedBy", SqlDbType.NVarChar, 150) { Value = filter.CreatedBy.Trim() });
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                string norm = CampaignStatusConsts.Normalize(filter.Status);
                whereClause += " AND (c.CampaignStatus = @Status OR c.CampaignStatus = @StatusRaw) ";
                paramList.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = norm });
                paramList.Add(new SqlParameter("@StatusRaw", SqlDbType.NVarChar, 50) { Value = filter.Status.Trim() });
            }

            if (filter.PortalId.HasValue && filter.PortalId.Value > 0)
            {
                whereClause += " AND c.Portal_id = @PortalId ";
                paramList.Add(new SqlParameter("@PortalId", SqlDbType.BigInt) { Value = filter.PortalId.Value });
            }

            if (filter.UserTypeId.HasValue && filter.UserTypeId.Value > 0)
            {
                whereClause += " AND c.User_Type_ID = @UserTypeId ";
                paramList.Add(new SqlParameter("@UserTypeId", SqlDbType.BigInt) { Value = filter.UserTypeId.Value });
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                whereClause += " AND (c.CampaignName LIKE '%' + @Search + '%' OR c.CampaignDescription LIKE '%' + @Search + '%' OR EXISTS (SELECT 1 FROM dbo.tbl_BroadCastMessage sbm WHERE sbm.Bmsg_id = c.Bmsg_id AND (sbm.message_head LIKE '%' + @Search + '%' OR sbm.message_body LIKE '%' + @Search + '%'))) ";
                paramList.Add(new SqlParameter("@Search", SqlDbType.NVarChar, 250) { Value = filter.SearchTerm.Trim() });
            }

            if (filter.FromDate.HasValue)
            {
                whereClause += " AND c.CreatedDate >= @FromDate ";
                paramList.Add(new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = filter.FromDate.Value });
            }

            if (filter.ToDate.HasValue)
            {
                whereClause += " AND c.CreatedDate <= @ToDate ";
                paramList.Add(new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = filter.ToDate.Value });
            }

            string countSql = $"SELECT COUNT(1) FROM dbo.tbl_MessageCampaign c {whereClause};";
            DataTable dtCount = _dataAccess.GetDataTable(countSql, paramList.ToArray());
            int totalCount = dtCount != null && dtCount.Rows.Count > 0 ? Convert.ToInt32(dtCount.Rows[0][0]) : 0;

            int pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            int pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;
            int offset = (pageNumber - 1) * pageSize;

            string querySql = $@"
                SELECT c.*, 
                       p.portal_name,
                       ut.User_Type_Name
                FROM dbo.tbl_MessageCampaign c
                LEFT JOIN dbo.mst_portal p ON c.Portal_id = p.portal_id
                LEFT JOIN dbo.UserType ut ON c.User_Type_ID = ut.User_Type_ID AND (c.Portal_id IS NULL OR ut.Portal_ID = c.Portal_id)
                {whereClause}
                ORDER BY c.CampaignId DESC
                OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY;";

            DataTable dtItems = _dataAccess.GetDataTable(querySql, paramList.ToArray());
            var items = new List<CampaignDto>();

            if (dtItems != null && dtItems.Rows.Count > 0)
            {
                foreach (DataRow row in dtItems.Rows)
                {
                    var c = MapCampaignRow(row);
                    c.BroadcastMessage = GetBroadcastMessageById(c.BmsgId);
                    items.Add(c);
                }
            }

            return Task.FromResult((items, totalCount));
        }

        public Task<bool> VerifyCampaignAsync(VerifyCampaignRequest request)
        {
            EnsureSchemaInitialized();

            string newStatus = request.IsVerified.Equals("Y", StringComparison.OrdinalIgnoreCase)
                ? CampaignStatusConsts.PendingApproval
                : CampaignStatusConsts.RejectedByCkr;

            string rejectionReason = !string.IsNullOrWhiteSpace(request.RejectionReason)
                ? request.RejectionReason.Trim()
                : (request.Remarks ?? string.Empty).Trim();

            const string sql = @"
                UPDATE dbo.tbl_MessageCampaign SET
                    IsVerified = @IsVerified,
                    VerifiedBy = @VerifiedBy,
                    VerifiedDate = GETUTCDATE(),
                    VerifiedIP = @VerifiedIP,
                    VerifiedRemarks = @Remarks,
                    RejectionReason = CASE WHEN @IsVerified = 'N' THEN @RejectionReason ELSE NULL END,
                    LastActionRole = 'CKR',
                    CampaignStatus = @NewStatus,
                    UpdatedBy = @VerifiedBy,
                    UpdatedDate = GETUTCDATE()
                WHERE CampaignId = @CampaignId
                  AND CampaignStatus IN ('SUBMITTED_FOR_VERIFICATION', 'Pending Verification', 'Draft');

                IF @@ROWCOUNT > 0
                BEGIN
                    UPDATE b SET
                        b.IsVerified = @IsVerified,
                        b.VerifiedDate = GETUTCDATE(),
                        b.VerifiedBy = @VerifiedBy,
                        b.VerifiedIP = @VerifiedIP
                    FROM dbo.tbl_BroadCastMessage b
                    INNER JOIN dbo.tbl_MessageCampaign c ON b.Bmsg_id = c.Bmsg_id
                    WHERE c.CampaignId = @CampaignId;
                END";

            SqlParameter[] parameters =
            {
                new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = request.CampaignId },
                new SqlParameter("@IsVerified", SqlDbType.Char, 1) { Value = string.IsNullOrWhiteSpace(request.IsVerified) ? "Y" : request.IsVerified.Trim().ToUpperInvariant() },
                new SqlParameter("@VerifiedBy", SqlDbType.NVarChar, 150) { Value = request.VerifiedBy.Trim() },
                new SqlParameter("@VerifiedIP", SqlDbType.NVarChar, 50) { Value = string.IsNullOrWhiteSpace(request.VerifiedIP) ? (object)DBNull.Value : request.VerifiedIP.Trim() },
                new SqlParameter("@Remarks", SqlDbType.NVarChar, 1000) { Value = string.IsNullOrWhiteSpace(request.Remarks) ? (object)DBNull.Value : request.Remarks.Trim() },
                new SqlParameter("@RejectionReason", SqlDbType.NVarChar, 1000) { Value = string.IsNullOrWhiteSpace(rejectionReason) ? (object)DBNull.Value : rejectionReason },
                new SqlParameter("@NewStatus", SqlDbType.NVarChar, 50) { Value = newStatus }
            };

            int rows = 0;
            using (var con = _dataAccess.GetConnection())
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters);
                con.Open();
                rows = cmd.ExecuteNonQuery();
            }

            if (rows > 0)
            {
                string action = request.IsVerified.Equals("Y", StringComparison.OrdinalIgnoreCase)
                    ? "VERIFIED"
                    : "REJECTED_BY_CKR";
                string loggedRemarks = request.IsVerified.Equals("Y", StringComparison.OrdinalIgnoreCase)
                    ? request.Remarks ?? "Campaign verified by Checker and forwarded to Approver."
                    : rejectionReason;

                RecordAuditLogSync(
                    request.CampaignId,
                    action,
                    CampaignStatusConsts.SubmittedForVerification,
                    newStatus,
                    request.VerifiedBy,
                    CampaignRoles.Checker,
                    loggedRemarks,
                    request.VerifiedIP);
            }

            return Task.FromResult(rows > 0);
        }

        public Task<bool> ApproveCampaignAsync(ApproveCampaignRequest request)
        {
            EnsureSchemaInitialized();

            string newStatus = request.IsApproved.Equals("Y", StringComparison.OrdinalIgnoreCase)
                ? CampaignStatusConsts.Approved
                : CampaignStatusConsts.RejectedByApr;

            string rejectionReason = !string.IsNullOrWhiteSpace(request.RejectionReason)
                ? request.RejectionReason.Trim()
                : (request.Remarks ?? string.Empty).Trim();

            const string sql = @"
                UPDATE dbo.tbl_MessageCampaign SET
                    IsApproved = @IsApproved,
                    ApprovedBy = @ApprovedBy,
                    ApprovedDate = GETUTCDATE(),
                    ApprovedIP = @ApprovedIP,
                    ApprovedRemarks = @Remarks,
                    RejectionReason = CASE WHEN @IsApproved = 'N' THEN @RejectionReason ELSE NULL END,
                    LastActionRole = 'APR',
                    CampaignStatus = @NewStatus,
                    UpdatedBy = @ApprovedBy,
                    UpdatedDate = GETUTCDATE()
                WHERE CampaignId = @CampaignId
                  AND CampaignStatus IN ('PENDING_APPROVAL', 'Pending Approval');

                IF @@ROWCOUNT > 0
                BEGIN
                    UPDATE b SET
                        b.IsApproved = @IsApproved,
                        b.ApprovedDate = GETUTCDATE(),
                        b.ApprovedBy = @ApprovedBy,
                        b.ApprovedIP = @ApprovedIP
                    FROM dbo.tbl_BroadCastMessage b
                    INNER JOIN dbo.tbl_MessageCampaign c ON b.Bmsg_id = c.Bmsg_id
                    WHERE c.CampaignId = @CampaignId;
                END";

            SqlParameter[] parameters =
            {
                new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = request.CampaignId },
                new SqlParameter("@IsApproved", SqlDbType.Char, 1) { Value = string.IsNullOrWhiteSpace(request.IsApproved) ? "Y" : request.IsApproved.Trim().ToUpperInvariant() },
                new SqlParameter("@ApprovedBy", SqlDbType.NVarChar, 150) { Value = request.ApprovedBy.Trim() },
                new SqlParameter("@ApprovedIP", SqlDbType.NVarChar, 50) { Value = string.IsNullOrWhiteSpace(request.ApprovedIP) ? (object)DBNull.Value : request.ApprovedIP.Trim() },
                new SqlParameter("@Remarks", SqlDbType.NVarChar, 1000) { Value = string.IsNullOrWhiteSpace(request.Remarks) ? (object)DBNull.Value : request.Remarks.Trim() },
                new SqlParameter("@RejectionReason", SqlDbType.NVarChar, 1000) { Value = string.IsNullOrWhiteSpace(rejectionReason) ? (object)DBNull.Value : rejectionReason },
                new SqlParameter("@NewStatus", SqlDbType.NVarChar, 50) { Value = newStatus }
            };

            int rows = 0;
            using (var con = _dataAccess.GetConnection())
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters);
                con.Open();
                rows = cmd.ExecuteNonQuery();
            }

            if (rows > 0)
            {
                string action = request.IsApproved.Equals("Y", StringComparison.OrdinalIgnoreCase)
                    ? "APPROVED"
                    : "REJECTED_BY_APR";
                string loggedRemarks = request.IsApproved.Equals("Y", StringComparison.OrdinalIgnoreCase)
                    ? request.Remarks ?? "Campaign given final approval by Approver for message dispatch."
                    : rejectionReason;

                RecordAuditLogSync(
                    request.CampaignId,
                    action,
                    CampaignStatusConsts.PendingApproval,
                    newStatus,
                    request.ApprovedBy,
                    CampaignRoles.Approver,
                    loggedRemarks,
                    request.ApprovedIP);
            }

            return Task.FromResult(rows > 0);
        }

        public Task<bool> CancelCampaignAsync(CancelCampaignRequest request)
        {
            EnsureSchemaInitialized();

            const string sql = @"
                UPDATE dbo.tbl_MessageCampaign SET
                    CampaignStatus = 'Cancelled',
                    CancellationReason = @Reason,
                    CancelledBy = @CancelledBy,
                    CancelledDate = GETUTCDATE(),
                    UpdatedBy = @CancelledBy,
                    UpdatedDate = GETUTCDATE()
                WHERE CampaignId = @CampaignId
                  AND CampaignStatus NOT IN ('SENT', 'Completed', 'Cancelled', 'SENDING', 'Sending');";

            SqlParameter[] parameters =
            {
                new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = request.CampaignId },
                new SqlParameter("@Reason", SqlDbType.NVarChar, 500) { Value = string.IsNullOrWhiteSpace(request.CancellationReason) ? "Cancelled by user" : request.CancellationReason.Trim() },
                new SqlParameter("@CancelledBy", SqlDbType.NVarChar, 150) { Value = request.CancelledBy.Trim() }
            };

            int rows = 0;
            using (var con = _dataAccess.GetConnection())
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters);
                con.Open();
                rows = cmd.ExecuteNonQuery();
            }

            if (rows > 0)
            {
                RecordAuditLogSync(
                    request.CampaignId,
                    "CANCELLED",
                    null,
                    "Cancelled",
                    request.CancelledBy,
                    "USER",
                    request.CancellationReason);
            }

            return Task.FromResult(rows > 0);
        }

        public Task<bool> SetCampaignSendingStateAsync(long campaignId)
        {
            EnsureSchemaInitialized();

            const string sql = @"
                UPDATE dbo.tbl_MessageCampaign SET
                    CampaignStatus = 'SENDING',
                    ActualSendTime = GETUTCDATE(),
                    UpdatedDate = GETUTCDATE()
                WHERE CampaignId = @CampaignId
                  AND CampaignStatus IN ('APPROVED', 'Approved', 'Scheduled');";

            SqlParameter[] parameters = { new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId } };
            int rows = 0;
            using (var con = _dataAccess.GetConnection())
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters);
                con.Open();
                rows = cmd.ExecuteNonQuery();
            }

            if (rows > 0)
            {
                RecordAuditLogSync(
                    campaignId,
                    "SENDING",
                    CampaignStatusConsts.Approved,
                    CampaignStatusConsts.Sending,
                    "Maker/Worker",
                    CampaignRoles.Maker,
                    "Message sending started.");
            }

            return Task.FromResult(rows > 0);
        }

        public Task<bool> FinalizeCampaignExecutionAsync(long campaignId, string status, int successCount, int failedCount, int skippedCount)
        {
            EnsureSchemaInitialized();

            string normalizedFinalStatus = string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) || 
                                           string.Equals(status, "SENT", StringComparison.OrdinalIgnoreCase) ||
                                           (successCount > 0 && failedCount == 0)
                ? CampaignStatusConsts.Sent
                : CampaignStatusConsts.SendFailed;

            const string sql = @"
                UPDATE dbo.tbl_MessageCampaign SET
                    CampaignStatus = @Status,
                    CompletedTime = GETUTCDATE(),
                    TotalSentSuccess = @SuccessCount,
                    TotalSentFailed = @FailedCount,
                    TotalSkipped = @SkippedCount,
                    UpdatedDate = GETUTCDATE()
                WHERE CampaignId = @CampaignId;";

            SqlParameter[] parameters =
            {
                new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId },
                new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = normalizedFinalStatus },
                new SqlParameter("@SuccessCount", SqlDbType.Int) { Value = successCount },
                new SqlParameter("@FailedCount", SqlDbType.Int) { Value = failedCount },
                new SqlParameter("@SkippedCount", SqlDbType.Int) { Value = skippedCount }
            };

            int rows = 0;
            using (var con = _dataAccess.GetConnection())
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters);
                con.Open();
                rows = cmd.ExecuteNonQuery();
            }

            if (rows > 0)
            {
                string summary = $"Dispatches completed: {successCount} sent, {failedCount} failed, {skippedCount} skipped.";
                RecordAuditLogSync(
                    campaignId,
                    normalizedFinalStatus,
                    CampaignStatusConsts.Sending,
                    normalizedFinalStatus,
                    "System/Worker",
                    "SYSTEM",
                    summary);
            }

            return Task.FromResult(rows > 0);
        }

        public Task<List<CampaignRecipientCandidate>> ResolveEligibleRecipientsAsync(long campaignId)
        {
            const string query = @"
                DECLARE @PortalId BIGINT;
                DECLARE @UserTypeId BIGINT;
                DECLARE @IsAllDistrict CHAR(1);

                SELECT @PortalId = Portal_id,
                       @UserTypeId = User_Type_ID,
                       @IsAllDistrict = IsAllDistrict
                FROM dbo.tbl_MessageCampaign
                WHERE CampaignId = @CampaignId;

                -- Candidate devices with deduplication
                SELECT DISTINCT 
                    f.UserId,
                    f.DeviceId,
                    f.FcmToken,
                    ISNULL(f.Platform, 'Android') AS Platform
                FROM dbo.UserFcmDevices f
                WHERE f.IsActive = 1
                  AND f.FcmToken IS NOT NULL
                  AND LEN(LTRIM(RTRIM(f.FcmToken))) > 10
                  -- Portal / User_Type filtering
                  AND (
                      @PortalId IS NULL
                      OR EXISTS (
                          SELECT 1 FROM dbo.tbl_map_user_portal mup
                          WHERE (mup.deviceid = f.DeviceId OR mup.userid = f.UserId)
                            AND mup.portal_id = @PortalId
                            AND (@UserTypeId IS NULL OR mup.User_Type_ID = @UserTypeId)
                      )
                  )
                  -- Location targeting filtering
                  AND (
                      @IsAllDistrict = 'Y'
                      OR EXISTS (
                          SELECT 1 FROM dbo.tbl_CampaignTargetLocation ctl
                          WHERE ctl.CampaignId = @CampaignId
                            AND (
                                EXISTS (
                                    SELECT 1 FROM dbo.tbl_user u
                                    WHERE (CAST(u.User_id AS NVARCHAR(100)) = f.UserId OR u.Deviceid = f.DeviceId)
                                      AND u.DistCode = ctl.District_code
                                      AND (ctl.Block_code IS NULL OR u.BlockCode = ctl.Block_code)
                                      AND (ctl.Panchayatcode IS NULL OR u.PanchayatCode = ctl.Panchayatcode)
                                )
                                OR EXISTS (
                                    SELECT 1 FROM dbo.tbl_map_user_portal mup2
                                    WHERE (mup2.deviceid = f.DeviceId OR mup2.userid = f.UserId)
                                      AND mup2.district_code = ctl.District_code
                                      AND (ctl.Block_code IS NULL OR mup2.block_code = ctl.Block_code)
                                      AND (ctl.Panchayatcode IS NULL OR mup2.panchayatcode = ctl.Panchayatcode)
                                )
                            )
                      )
                  );";

            SqlParameter[] parameters = { new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId } };
            DataTable dt = _dataAccess.GetDataTable(query, parameters);
            var recipients = new List<CampaignRecipientCandidate>();

            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    recipients.Add(new CampaignRecipientCandidate
                    {
                        UserId = row["UserId"] != DBNull.Value ? row["UserId"].ToString() : null,
                        DeviceId = row["DeviceId"] != DBNull.Value ? row["DeviceId"].ToString()! : string.Empty,
                        FcmToken = row["FcmToken"] != DBNull.Value ? row["FcmToken"].ToString()! : string.Empty,
                        Platform = row["Platform"] != DBNull.Value ? row["Platform"].ToString()! : "Android"
                    });
                }
            }

            // Update TotalRecipientsTargeted on campaign
            UpdateTargetedRecipientCount(campaignId, recipients.Count);

            return Task.FromResult(recipients);
        }

        private void UpdateTargetedRecipientCount(long campaignId, int count)
        {
            const string sql = "UPDATE dbo.tbl_MessageCampaign SET TotalRecipientsTargeted = @Count WHERE CampaignId = @CampaignId;";
            using (var con = _dataAccess.GetConnection())
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = count });
                cmd.Parameters.Add(new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId });
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public Task<int> RecordDeliveryLogsAsync(long campaignId, long bmsgId, List<CampaignDeliveryLogDto> logs)
        {
            if (logs == null || logs.Count == 0) return Task.FromResult(0);

            const string insertSql = @"
                IF NOT EXISTS (
                    SELECT 1 FROM dbo.tbl_CampaignDeliveryLog 
                    WHERE CampaignId = @CampaignId AND DeviceId = @DeviceId AND FcmToken = @FcmToken
                )
                BEGIN
                    INSERT INTO dbo.tbl_CampaignDeliveryLog (
                        CampaignId,
                        Bmsg_id,
                        UserId,
                        DeviceId,
                        FcmToken,
                        Platform,
                        Status,
                        FcmMessageId,
                        ErrorCode,
                        ErrorMessage,
                        AttemptCount,
                        SentAtUtc
                    )
                    VALUES (
                        @CampaignId,
                        @Bmsg_id,
                        @UserId,
                        @DeviceId,
                        @FcmToken,
                        @Platform,
                        @Status,
                        @FcmMessageId,
                        @ErrorCode,
                        @ErrorMessage,
                        @AttemptCount,
                        SYSUTCDATETIME()
                    );
                END";

            int insertedCount = 0;
            using (var con = _dataAccess.GetConnection())
            {
                con.Open();
                foreach (var log in logs)
                {
                    using (var cmd = new SqlCommand(insertSql, con))
                    {
                        cmd.Parameters.Add(new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId });
                        cmd.Parameters.Add(new SqlParameter("@Bmsg_id", SqlDbType.BigInt) { Value = bmsgId });
                        cmd.Parameters.Add(new SqlParameter("@UserId", SqlDbType.NVarChar, 100) { Value = string.IsNullOrWhiteSpace(log.UserId) ? (object)DBNull.Value : log.UserId });
                        cmd.Parameters.Add(new SqlParameter("@DeviceId", SqlDbType.NVarChar, 200) { Value = string.IsNullOrWhiteSpace(log.DeviceId) ? (object)DBNull.Value : log.DeviceId });
                        cmd.Parameters.Add(new SqlParameter("@FcmToken", SqlDbType.NVarChar, 2048) { Value = string.IsNullOrWhiteSpace(log.FcmToken) ? (object)DBNull.Value : log.FcmToken });
                        cmd.Parameters.Add(new SqlParameter("@Platform", SqlDbType.NVarChar, 50) { Value = string.IsNullOrWhiteSpace(log.Platform) ? "Android" : log.Platform });
                        cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = log.Status });
                        cmd.Parameters.Add(new SqlParameter("@FcmMessageId", SqlDbType.NVarChar, 255) { Value = string.IsNullOrWhiteSpace(log.FcmMessageId) ? (object)DBNull.Value : log.FcmMessageId });
                        cmd.Parameters.Add(new SqlParameter("@ErrorCode", SqlDbType.NVarChar, 100) { Value = string.IsNullOrWhiteSpace(log.ErrorCode) ? (object)DBNull.Value : log.ErrorCode });
                        cmd.Parameters.Add(new SqlParameter("@ErrorMessage", SqlDbType.NVarChar, 1000) { Value = string.IsNullOrWhiteSpace(log.ErrorMessage) ? (object)DBNull.Value : log.ErrorMessage });
                        cmd.Parameters.Add(new SqlParameter("@AttemptCount", SqlDbType.Int) { Value = log.AttemptCount > 0 ? log.AttemptCount : 1 });

                        insertedCount += cmd.ExecuteNonQuery() > 0 ? 1 : 0;
                    }
                }
            }

            return Task.FromResult(insertedCount);
        }

        public Task<CampaignDeliveryReportDto?> GetDeliveryReportAsync(long campaignId)
        {
            const string campaignSql = @"
                SELECT c.CampaignId,
                       c.CampaignName,
                       c.Bmsg_id,
                       b.message_head,
                       b.message_body,
                       c.CampaignStatus,
                       c.ScheduledSendTime,
                       c.ActualSendTime,
                       c.CompletedTime,
                       c.TotalRecipientsTargeted,
                       c.TotalSentSuccess,
                       c.TotalSentFailed,
                       c.TotalSkipped
                FROM dbo.tbl_MessageCampaign c
                LEFT JOIN dbo.tbl_BroadCastMessage b ON c.Bmsg_id = b.Bmsg_id
                WHERE c.CampaignId = @CampaignId;";

            SqlParameter[] parameters = { new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId } };
            DataTable dt = _dataAccess.GetDataTable(campaignSql, parameters);

            if (dt == null || dt.Rows.Count == 0) return Task.FromResult<CampaignDeliveryReportDto?>(null);

            DataRow row = dt.Rows[0];
            var report = new CampaignDeliveryReportDto
            {
                CampaignId = SafeLong(row, "CampaignId"),
                CampaignName = SafeString(row, "CampaignName") ?? string.Empty,
                BmsgId = SafeLong(row, "Bmsg_id"),
                MessageHead = SafeString(row, "message_head"),
                MessageBody = SafeString(row, "message_body"),
                CampaignStatus = SafeString(row, "CampaignStatus") ?? string.Empty,
                ScheduledSendTime = SafeNullableDateTime(row, "ScheduledSendTime"),
                ActualSendTime = SafeNullableDateTime(row, "ActualSendTime"),
                CompletedTime = SafeNullableDateTime(row, "CompletedTime"),
                TotalRecipientsTargeted = SafeInt(row, "TotalRecipientsTargeted"),
                TotalSentSuccess = SafeInt(row, "TotalSentSuccess"),
                TotalSentFailed = SafeInt(row, "TotalSentFailed"),
                TotalSkipped = SafeInt(row, "TotalSkipped")
            };

            const string logsSql = @"
                SELECT TOP 500
                    DeliveryLogId,
                    CampaignId,
                    UserId,
                    DeviceId,
                    Platform,
                    Status,
                    FcmMessageId,
                    ErrorCode,
                    ErrorMessage,
                    AttemptCount,
                    SentAtUtc
                FROM dbo.tbl_CampaignDeliveryLog
                WHERE CampaignId = @CampaignId
                ORDER BY DeliveryLogId DESC;";

            DataTable dtLogs = _dataAccess.GetDataTable(logsSql, parameters);
            if (dtLogs != null && dtLogs.Rows.Count > 0)
            {
                foreach (DataRow lRow in dtLogs.Rows)
                {
                    report.DeliveryLogs.Add(new CampaignDeliveryLogDto
                    {
                        DeliveryLogId = SafeLong(lRow, "DeliveryLogId"),
                        CampaignId = SafeLong(lRow, "CampaignId"),
                        UserId = SafeString(lRow, "UserId"),
                        DeviceId = SafeString(lRow, "DeviceId"),
                        Platform = SafeString(lRow, "Platform"),
                        Status = SafeString(lRow, "Status") ?? string.Empty,
                        FcmMessageId = SafeString(lRow, "FcmMessageId"),
                        ErrorCode = SafeString(lRow, "ErrorCode"),
                        ErrorMessage = SafeString(lRow, "ErrorMessage"),
                        AttemptCount = SafeInt(lRow, "AttemptCount", 1),
                        SentAtUtc = lRow["SentAtUtc"] != DBNull.Value ? Convert.ToDateTime(lRow["SentAtUtc"]) : DateTime.UtcNow
                    });
                }
            }

            return Task.FromResult<CampaignDeliveryReportDto?>(report);
        }

        public Task<List<CampaignDto>> GetDueScheduledCampaignsAsync()
        {
            EnsureSchemaInitialized();

            const string sql = @"
                SELECT c.*, 
                       p.portal_name,
                       ut.User_Type_Name
                FROM dbo.tbl_MessageCampaign c
                LEFT JOIN dbo.mst_portal p ON c.Portal_id = p.portal_id
                LEFT JOIN dbo.UserType ut ON c.User_Type_ID = ut.User_Type_ID AND (c.Portal_id IS NULL OR ut.Portal_ID = c.Portal_id)
                WHERE c.CampaignStatus IN ('APPROVED', 'Approved', 'Scheduled')
                  AND c.ScheduledSendTime IS NOT NULL
                  AND c.ScheduledSendTime <= GETUTCDATE()
                ORDER BY c.ScheduledSendTime ASC;";

            DataTable dt = _dataAccess.GetDataTable(sql);
            var list = new List<CampaignDto>();

            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    var c = MapCampaignRow(row);
                    c.BroadcastMessage = GetBroadcastMessageById(c.BmsgId);
                    list.Add(c);
                }
            }

            return Task.FromResult(list);
        }

        public Task<EstimateRecipientsResponse> EstimateRecipientsAsync(string targetScope, long? portalId, long? userTypeId, string isAllDistrict, string isAllBlock, string isAllPanchayat, List<CampaignLocationDto>? locations)
        {
            var response = new EstimateRecipientsResponse();

            string locationWhere = "";
            var paramList = new List<SqlParameter>();

            if (portalId.HasValue && portalId.Value > 0)
            {
                paramList.Add(new SqlParameter("@PortalId", SqlDbType.BigInt) { Value = portalId.Value });
            }

            if (userTypeId.HasValue && userTypeId.Value > 0)
            {
                paramList.Add(new SqlParameter("@UserTypeId", SqlDbType.BigInt) { Value = userTypeId.Value });
            }

            if (isAllDistrict != "Y" && locations != null && locations.Count > 0)
            {
                var distCodes = new List<string>();
                foreach (var loc in locations)
                {
                    if (loc.DistrictCode.HasValue) distCodes.Add(loc.DistrictCode.Value.ToString());
                }

                if (distCodes.Count > 0)
                {
                    string joinedCodes = string.Join(",", distCodes);
                    locationWhere = $@" AND (
                        EXISTS (
                            SELECT 1 FROM dbo.tbl_user u 
                            WHERE (CAST(u.User_id AS NVARCHAR(100)) = f.UserId OR u.Deviceid = f.DeviceId)
                              AND u.DistCode IN ({joinedCodes})
                        )
                        OR EXISTS (
                            SELECT 1 FROM dbo.tbl_map_user_portal mup 
                            WHERE (mup.deviceid = f.DeviceId OR mup.userid = f.UserId)
                              AND mup.district_code IN ({joinedCodes})
                        )
                    )";
                }
            }

            string portalWhere = "";
            if (portalId.HasValue && portalId.Value > 0)
            {
                portalWhere += @" AND EXISTS (
                    SELECT 1 FROM dbo.tbl_map_user_portal mup 
                    WHERE (mup.deviceid = f.DeviceId OR mup.userid = f.UserId)
                      AND mup.portal_id = @PortalId ";

                if (userTypeId.HasValue && userTypeId.Value > 0)
                {
                    portalWhere += " AND mup.User_Type_ID = @UserTypeId ";
                }

                portalWhere += ") ";
            }

            string query = $@"
                SELECT 
                    COUNT(DISTINCT f.DeviceId) AS DeviceCount,
                    COUNT(DISTINCT f.UserId) AS UserCount,
                    ISNULL(f.Platform, 'Android') AS Platform,
                    COUNT(1) AS SubCount
                FROM dbo.UserFcmDevices f
                WHERE f.IsActive = 1
                  AND f.FcmToken IS NOT NULL
                  AND LEN(LTRIM(RTRIM(f.FcmToken))) > 10
                  {portalWhere}
                  {locationWhere}
                GROUP BY ISNULL(f.Platform, 'Android');";

            DataTable dt = _dataAccess.GetDataTable(query, paramList.ToArray());
            if (dt != null && dt.Rows.Count > 0)
            {
                int totalDevices = 0;
                int totalUsers = 0;
                foreach (DataRow row in dt.Rows)
                {
                    string plat = row["Platform"] != DBNull.Value ? row["Platform"].ToString()! : "Android";
                    int subCount = row["SubCount"] != DBNull.Value ? Convert.ToInt32(row["SubCount"]) : 0;
                    response.PlatformBreakdown[plat] = subCount;

                    totalDevices += row["DeviceCount"] != DBNull.Value ? Convert.ToInt32(row["DeviceCount"]) : 0;
                    totalUsers += row["UserCount"] != DBNull.Value ? Convert.ToInt32(row["UserCount"]) : 0;
                }
                response.EstimatedDevicesCount = totalDevices;
                response.EstimatedRecipientCount = totalUsers > 0 ? totalUsers : totalDevices;
            }

            return Task.FromResult(response);
        }

        public Task<bool> DeactivateInvalidTokenAsync(string fcmToken)
        {
            if (string.IsNullOrWhiteSpace(fcmToken)) return Task.FromResult(false);

            const string sql = @"
                UPDATE dbo.UserFcmDevices SET 
                    IsActive = 0, 
                    UpdatedAtUtc = SYSUTCDATETIME() 
                WHERE FcmToken = @FcmToken;";

            SqlParameter[] parameters = { new SqlParameter("@FcmToken", SqlDbType.NVarChar, 2048) { Value = fcmToken.Trim() } };
            int rows = 0;
            using (var con = _dataAccess.GetConnection())
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters);
                con.Open();
                rows = cmd.ExecuteNonQuery();
            }

            return Task.FromResult(rows > 0);
        }

        private BroadcastMessageDto? GetBroadcastMessageById(long bmsgId)
        {
            const string query = @"
                SELECT Bmsg_id, message_head, message_body, validity_in_minutes, is_active, 
                       photo1path, photo2path, videopath, documentpath, entryby, entrydate, 
                       updatedby, updateddate, IsSynced, IsDeleted, IsVerified, IsApproved
                FROM dbo.tbl_BroadCastMessage
                WHERE Bmsg_id = @Bmsg_id;";

            SqlParameter[] parameters = { new SqlParameter("@Bmsg_id", SqlDbType.BigInt) { Value = bmsgId } };
            DataTable dt = _dataAccess.GetDataTable(query, parameters);
            if (dt == null || dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            return new BroadcastMessageDto
            {
                Bmsg_id = SafeLong(row, "Bmsg_id"),
                MessageHead = SafeString(row, "message_head"),
                MessageBody = SafeString(row, "message_body"),
                ValidityInMinutes = SafeNullableShort(row, "validity_in_minutes", 60),
                IsActive = SafeString(row, "is_active") ?? "Y",
                photo1path = SafeString(row, "photo1path"),
                photo2path = SafeString(row, "photo2path"),
                videopath = SafeString(row, "videopath"),
                documentpath = SafeString(row, "documentpath"),
                EntryBy = SafeString(row, "entryby"),
                EntryDate = SafeNullableDateTime(row, "entrydate"),
                UpdatedBy = SafeString(row, "updatedby"),
                UpdatedDate = SafeNullableDateTime(row, "updateddate"),
                IsSynced = SafeString(row, "IsSynced") ?? "N",
                IsDeleted = SafeString(row, "IsDeleted") ?? "N"
            };
        }

        private List<CampaignLocationDto> GetCampaignLocations(long campaignId)
        {
            const string query = @"
                SELECT ctl.*, 
                       d.DistName AS district_name,
                       b.BlockName AS block_name
                FROM dbo.tbl_CampaignTargetLocation ctl
                LEFT JOIN dbo.Districts d ON ctl.District_code = d.DistCode
                LEFT JOIN dbo.Blocks b ON ctl.Block_code = b.BlockCode
                WHERE ctl.CampaignId = @CampaignId;";

            SqlParameter[] parameters = { new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId } };
            DataTable dt = _dataAccess.GetDataTable(query, parameters);
            var list = new List<CampaignLocationDto>();

            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new CampaignLocationDto
                    {
                        DistrictCode = SafeNullableLong(row, "District_code"),
                        DistrictName = SafeString(row, "district_name"),
                        BlockCode = SafeNullableLong(row, "Block_code"),
                        BlockName = SafeString(row, "block_name"),
                        PanchayatCode = SafeNullableLong(row, "Panchayatcode")
                    });
                }
            }

            return list;
        }

        private static CampaignDto MapCampaignRow(DataRow row)
        {
            return new CampaignDto
            {
                CampaignId = SafeLong(row, "CampaignId"),
                CampaignName = SafeString(row, "CampaignName") ?? string.Empty,
                CampaignDescription = SafeString(row, "CampaignDescription"),
                BmsgId = SafeLong(row, "Bmsg_id"),
                CampaignStatus = CampaignStatusConsts.Normalize(SafeString(row, "CampaignStatus")),
                StartDate = SafeNullableDateTime(row, "StartDate"),
                EndDate = SafeNullableDateTime(row, "EndDate"),
                ScheduledSendTime = SafeNullableDateTime(row, "ScheduledSendTime"),
                ActualSendTime = SafeNullableDateTime(row, "ActualSendTime"),
                CompletedTime = SafeNullableDateTime(row, "CompletedTime"),
                TargetScope = SafeString(row, "TargetScope") ?? "ALL",
                PortalId = SafeNullableLong(row, "Portal_id"),
                PortalName = SafeString(row, "portal_name"),
                UserTypeId = SafeNullableLong(row, "User_Type_ID"),
                UserTypeName = SafeString(row, "User_Type_Name"),
                IsAllDistrict = SafeString(row, "IsAllDistrict") ?? "Y",
                IsAllBlock = SafeString(row, "IsAllBlock") ?? "Y",
                IsAllPanchayat = SafeString(row, "IsAllPanchayat") ?? "Y",
                TotalRecipientsTargeted = SafeInt(row, "TotalRecipientsTargeted"),
                TotalSentSuccess = SafeInt(row, "TotalSentSuccess"),
                TotalSentFailed = SafeInt(row, "TotalSentFailed"),
                TotalSkipped = SafeInt(row, "TotalSkipped"),
                IsVerified = SafeString(row, "IsVerified") ?? "N",
                VerifiedBy = SafeString(row, "VerifiedBy"),
                VerifiedDate = SafeNullableDateTime(row, "VerifiedDate"),
                IsApproved = SafeString(row, "IsApproved") ?? "N",
                ApprovedBy = SafeString(row, "ApprovedBy"),
                ApprovedDate = SafeNullableDateTime(row, "ApprovedDate"),
                RejectionReason = SafeString(row, "RejectionReason"),
                VerifiedRemarks = SafeString(row, "VerifiedRemarks"),
                ApprovedRemarks = SafeString(row, "ApprovedRemarks"),
                LastActionRole = SafeString(row, "LastActionRole"),
                CancellationReason = SafeString(row, "CancellationReason"),
                CancelledBy = SafeString(row, "CancelledBy"),
                CancelledDate = SafeNullableDateTime(row, "CancelledDate"),
                CreatedBy = SafeString(row, "CreatedBy") ?? string.Empty,
                CreatedDate = row["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(row["CreatedDate"]) : DateTime.UtcNow,
                UpdatedBy = SafeString(row, "UpdatedBy"),
                UpdatedDate = SafeNullableDateTime(row, "UpdatedDate")
            };
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

        private static int SafeInt(DataRow row, string colName, int defaultVal = 0)
        {
            if (!row.Table.Columns.Contains(colName)) return defaultVal;
            var val = row[colName];
            if (val == null || val == DBNull.Value) return defaultVal;
            return int.TryParse(val.ToString(), out int res) ? res : defaultVal;
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

        private void SyncUnlinkedBroadcastMessages()
        {
            try
            {
                const string syncSql = @"
                    INSERT INTO dbo.tbl_MessageCampaign (
                        CampaignName, CampaignDescription, Bmsg_id, CampaignStatus, TargetScope,
                        Portal_id, User_Type_ID, IsAllDistrict, IsAllBlock, IsAllPanchayat,
                        TotalRecipientsTargeted, TotalSentSuccess, TotalSentFailed, TotalSkipped,
                        IsVerified, IsApproved, CreatedBy, CreatedDate
                    )
                    SELECT 
                        CASE 
                            WHEN NULLIF(LTRIM(RTRIM(bm.message_head)), '') IS NOT NULL THEN LTRIM(RTRIM(bm.message_head))
                            WHEN NULLIF(LTRIM(RTRIM(bm.message_body)), '') IS NOT NULL THEN SUBSTRING(LTRIM(RTRIM(bm.message_body)), 1, 60)
                            ELSE 'Broadcast Notice #' + CAST(bm.Bmsg_id AS NVARCHAR(20))
                        END,
                        bm.message_body,
                        bm.Bmsg_id,
                        CASE 
                            WHEN bm.IsApproved = 'Y' OR bm.IsApproved = '1' THEN 'Approved'
                            WHEN bm.IsVerified = 'Y' OR bm.IsVerified = '1' THEN 'Pending Approval'
                            ELSE 'Draft'
                        END,
                        CASE 
                            WHEN bm.Portal_id IS NOT NULL AND bm.Portal_id > 0 THEN 'PORTAL'
                            WHEN bm.IsAllDistrict = 'N' THEN 'LOCATION'
                            ELSE 'ALL'
                        END,
                        bm.Portal_id,
                        TRY_CAST(bm.User_Type_ID AS BIGINT),
                        ISNULL(NULLIF(bm.IsAllDistrict, ''), 'Y'),
                        'Y',
                        'Y',
                        0, 0, 0, 0,
                        CASE WHEN bm.IsVerified = 'Y' OR bm.IsVerified = '1' THEN 'Y' ELSE 'N' END,
                        CASE WHEN bm.IsApproved = 'Y' OR bm.IsApproved = '1' THEN 'Y' ELSE 'N' END,
                        ISNULL(NULLIF(bm.entryby, ''), 'Admin'),
                        ISNULL(bm.entrydate, GETUTCDATE())
                    FROM dbo.tbl_BroadCastMessage bm
                    WHERE NOT EXISTS (
                        SELECT 1 FROM dbo.tbl_MessageCampaign c WHERE c.Bmsg_id = bm.Bmsg_id
                    );";
                _dataAccess.ExecuteSql(syncSql, Array.Empty<SqlParameter>());
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
            }
        }

        private void RecordAuditLogSync(long campaignId, string action, string? fromStatus, string toStatus, string actionBy, string? actionRole, string? remarks, string? actionIp = null)
        {
            try
            {
                EnsureSchemaInitialized();
                const string sql = @"
                    INSERT INTO dbo.tbl_CampaignAuditHistory (
                        CampaignId, Action, FromStatus, ToStatus, ActionBy, ActionRole, Remarks, ActionDate, ActionIp
                    ) VALUES (
                        @CampaignId, @Action, @FromStatus, @ToStatus, @ActionBy, @ActionRole, @Remarks, SYSUTCDATETIME(), @ActionIp
                    );";

                using (var con = _dataAccess.GetConnection())
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.Add(new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId });
                    cmd.Parameters.Add(new SqlParameter("@Action", SqlDbType.NVarChar, 50) { Value = action });
                    cmd.Parameters.Add(new SqlParameter("@FromStatus", SqlDbType.NVarChar, 50) { Value = (object?)fromStatus ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@ToStatus", SqlDbType.NVarChar, 50) { Value = toStatus });
                    cmd.Parameters.Add(new SqlParameter("@ActionBy", SqlDbType.NVarChar, 100) { Value = actionBy ?? "System" });
                    cmd.Parameters.Add(new SqlParameter("@ActionRole", SqlDbType.NVarChar, 50) { Value = (object?)actionRole ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@Remarks", SqlDbType.NVarChar, 1000) { Value = (object?)remarks ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@ActionIp", SqlDbType.NVarChar, 50) { Value = (object?)actionIp ?? DBNull.Value });
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
            }
        }

        public Task<bool> RecordAuditLogAsync(long campaignId, string action, string? fromStatus, string toStatus, string actionBy, string? actionRole, string? remarks, string? actionIp = null)
        {
            RecordAuditLogSync(campaignId, action, fromStatus, toStatus, actionBy, actionRole, remarks, actionIp);
            return Task.FromResult(true);
        }

        public Task<List<CampaignAuditLogDto>> GetAuditHistoryAsync(long campaignId)
        {
            EnsureSchemaInitialized();
            var list = new List<CampaignAuditLogDto>();
            try
            {
                const string sql = @"
                    SELECT AuditId, CampaignId, Action, FromStatus, ToStatus, ActionBy, ActionRole, Remarks, ActionDate, ActionIp
                    FROM dbo.tbl_CampaignAuditHistory
                    WHERE CampaignId = @CampaignId
                    ORDER BY AuditId ASC;";

                SqlParameter[] parameters = { new SqlParameter("@CampaignId", SqlDbType.BigInt) { Value = campaignId } };
                DataTable dt = _dataAccess.GetDataTable(sql, parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new CampaignAuditLogDto
                        {
                            AuditId = SafeLong(row, "AuditId"),
                            CampaignId = SafeLong(row, "CampaignId"),
                            Action = SafeString(row, "Action") ?? string.Empty,
                            FromStatus = SafeString(row, "FromStatus"),
                            ToStatus = SafeString(row, "ToStatus") ?? string.Empty,
                            ActionBy = SafeString(row, "ActionBy") ?? string.Empty,
                            ActionRole = SafeString(row, "ActionRole"),
                            Remarks = SafeString(row, "Remarks"),
                            ActionDate = row["ActionDate"] != DBNull.Value ? Convert.ToDateTime(row["ActionDate"]) : DateTime.UtcNow,
                            ActionIp = SafeString(row, "ActionIp")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
            }
            return Task.FromResult(list);
        }

        public Task<string?> GetUserRoleAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult<string?>(null);
            try
            {
                const string sql = "SELECT TOP 1 userrole FROM dbo.user_login WHERE userid = @UserId;";
                SqlParameter[] parameters = { new SqlParameter("@UserId", SqlDbType.VarChar, 100) { Value = userId.Trim() } };
                DataTable dt = _dataAccess.GetDataTable(sql, parameters);
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["userrole"] != DBNull.Value)
                {
                    return Task.FromResult<string?>(dt.Rows[0]["userrole"].ToString()?.Trim());
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
            }
            return Task.FromResult<string?>(null);
        }
    }
}
