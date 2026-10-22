using Microsoft.Data.SqlClient;
using SankalpAPI.Helper;
using SankalpAPI.Interface;
using SankalpAPI.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace SankalpAPI.Repository
{
    public class PortalRepository : IPortalRepository
    {
        private readonly clsDataAccess _dataAccess;

        public PortalRepository(clsDataAccess dataAccess)
        {
            _dataAccess = dataAccess;
        }

        public Task<List<PortalDto>> GetActivePortalsAsync()
        {
            var list = new List<PortalDto>();

            try
            {
                const string query = @"
                    SELECT portal_id, portal_name, portal_url, portal_image_path, is_active
                    FROM dbo.mst_portal
                    ORDER BY portal_name, portal_id;";

                DataTable dt = _dataAccess.GetDataTable(query);

                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new PortalDto
                        {
                            PortalId = Convert.ToInt64(row["portal_id"]),
                            PortalName = row["portal_name"]?.ToString() ?? string.Empty,
                            PortalUrl = row.Table.Columns.Contains("portal_url") && row["portal_url"] != DBNull.Value ? row["portal_url"]?.ToString() : null,
                            PortalImagePath = row.Table.Columns.Contains("portal_image_path") && row["portal_image_path"] != DBNull.Value ? row["portal_image_path"]?.ToString() : null,
                            IsActive = row.Table.Columns.Contains("is_active") && row["is_active"] != DBNull.Value ? row["is_active"]?.ToString() : "Y"
                        });
                    }

                    return Task.FromResult(list);
                }
            }
            catch
            {
                // Fallback to stored proc if direct table query encounters any issue
            }

            try
            {
                DataTable dt = _dataAccess.GetDataTableWithProc("GetPortalList", null);

                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new PortalDto
                    {
                        PortalId = Convert.ToInt64(row["portal_id"]),
                        PortalName = row["portal_name"]?.ToString() ?? string.Empty,
                        PortalUrl = row.Table.Columns.Contains("portal_url") && row["portal_url"] != DBNull.Value ? row["portal_url"]?.ToString() : null,
                        PortalImagePath = row.Table.Columns.Contains("portal_image_path") && row["portal_image_path"] != DBNull.Value ? row["portal_image_path"]?.ToString() : null,
                        IsActive = row.Table.Columns.Contains("is_active") && row["is_active"] != DBNull.Value ? row["is_active"]?.ToString() : "Y"
                    });
                }
            }
            catch
            {
            }

            return Task.FromResult(list);
        }

        public Task<PortalDto?> GetPortalByIdAsync(long portalId)
        {
            try
            {
                SqlParameter[] sqlParams =
                {
                    new SqlParameter("@p_portal_id", portalId)
                };

                const string query = @"
                    SELECT TOP 1 portal_id, portal_name, portal_url, portal_image_path, is_active
                    FROM dbo.mst_portal
                    WHERE portal_id = @p_portal_id;";

                DataTable dt = _dataAccess.GetDataTable(query, sqlParams);

                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    var p = new PortalDto
                    {
                        PortalId = Convert.ToInt64(row["portal_id"]),
                        PortalName = row["portal_name"]?.ToString() ?? string.Empty,
                        PortalUrl = row.Table.Columns.Contains("portal_url") && row["portal_url"] != DBNull.Value ? row["portal_url"]?.ToString() : null,
                        PortalImagePath = row.Table.Columns.Contains("portal_image_path") && row["portal_image_path"] != DBNull.Value ? row["portal_image_path"]?.ToString() : null,
                        IsActive = row.Table.Columns.Contains("is_active") && row["is_active"] != DBNull.Value ? row["is_active"]?.ToString() : "Y"
                    };
                    return Task.FromResult<PortalDto?>(p);
                }
            }
            catch
            {
            }

            SqlParameter[] parameters =
            {
                new SqlParameter("@p_portal_id", portalId)
            };

            DataTable dtProc = _dataAccess.GetDataTableWithProc("sp_get_portal_by_id", parameters);

            if (dtProc.Rows.Count == 0)
            {
                return Task.FromResult<PortalDto?>(null);
            }

            DataRow procRow = dtProc.Rows[0];
            var portal = new PortalDto
            {
                PortalId = Convert.ToInt64(procRow["portal_id"]),
                PortalName = procRow["portal_name"]?.ToString() ?? string.Empty,
                PortalUrl = procRow.Table.Columns.Contains("portal_url") && procRow["portal_url"] != DBNull.Value ? procRow["portal_url"]?.ToString() : null,
                PortalImagePath = procRow.Table.Columns.Contains("portal_image_path") && procRow["portal_image_path"] != DBNull.Value ? procRow["portal_image_path"]?.ToString() : null,
                IsActive = procRow.Table.Columns.Contains("is_active") && procRow["is_active"] != DBNull.Value ? procRow["is_active"]?.ToString() : "Y"
            };

            return Task.FromResult<PortalDto?>(portal);
        }

        public Task<List<UserTypeDto>> GetUserTypeByIdAsync(long portalId)
        {
            var list = new List<UserTypeDto>();

            try
            {
                SqlParameter[] sqlParams =
                {
                    new SqlParameter("@p_portal_id", portalId)
                };

                const string query = @"
                    SELECT User_Type_ID, User_Type_Name, User_Type_Name_hn, Portal_ID
                    FROM dbo.UserType
                    WHERE Portal_ID = @p_portal_id
                    ORDER BY User_Type_Name;";

                DataTable dtQuery = _dataAccess.GetDataTable(query, sqlParams);
                if (dtQuery != null && dtQuery.Rows.Count > 0)
                {
                    foreach (DataRow row in dtQuery.Rows)
                    {
                        list.Add(new UserTypeDto
                        {
                            PortalId = portalId,
                            UserType_id = row["User_Type_ID"]?.ToString() ?? string.Empty,
                            UserType_name = row["User_Type_Name"]?.ToString() ?? string.Empty,
                            UserType_name_hn = row.Table.Columns.Contains("User_Type_Name_hn") && row["User_Type_Name_hn"] != DBNull.Value ? row["User_Type_Name_hn"]?.ToString() ?? string.Empty : string.Empty
                        });
                    }
                    return Task.FromResult(list);
                }
            }
            catch
            {
            }

            try
            {
                SqlParameter[] parameters =
                {
                    new SqlParameter("@p_portal_id", portalId)
                };

                DataTable dt = _dataAccess.GetDataTableWithProc("sp_get_userType_portal_by_id", parameters);            

                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new UserTypeDto
                    {
                        PortalId = Convert.ToInt64(row["portal_id"]),
                        UserType_id = row["User_Type_ID"]?.ToString() ?? string.Empty,
                        UserType_name = row["User_Type_Name"]?.ToString() ?? string.Empty,
                        UserType_name_hn = row.Table.Columns.Contains("User_Type_Name_hn") && row["User_Type_Name_hn"] != DBNull.Value ? row["User_Type_Name_hn"]?.ToString() ?? string.Empty : string.Empty
                    });
                }
            }
            catch
            {
            }

            return Task.FromResult(list);
        }

        public Task<PortalUserMappingDto?> GetPortalAndUserMappingAsync(long portalId, string? mobile)
        {
            SqlParameter[] parameters =
            {
                new SqlParameter("@p_portal_id", portalId),
                new SqlParameter("@p_mobile", string.IsNullOrWhiteSpace(mobile) ? DBNull.Value : mobile.Trim())
            };

            DataTable dt = _dataAccess.GetDataTableWithProc("sp_get_portal_and_user_mapping", parameters);

            if (dt.Rows.Count == 0)
            {
                return Task.FromResult<PortalUserMappingDto?>(null);
            }

            DataRow row = dt.Rows[0];
            var dto = new PortalUserMappingDto
            {
                PortalId = Convert.ToInt64(row["portal_id"]),
                PortalName = row["portal_name"]?.ToString() ?? string.Empty,
                PortalUrl = row["portal_url"] == DBNull.Value ? null : row["portal_url"]?.ToString(),
                IsActive = row["is_active"] == DBNull.Value ? null : row["is_active"]?.ToString(),
                UserPortalId = row["user_portal_id"] == DBNull.Value ? null : Convert.ToInt64(row["user_portal_id"]),
                Mobile = row["mobile"] == DBNull.Value ? null : row["mobile"]?.ToString(),
                UserId = row["userid"] == DBNull.Value ? null : row["userid"]?.ToString(),
                SecretKey = row["secretkey"] == DBNull.Value ? null : row["secretkey"]?.ToString(),
                Token = row["token"] == DBNull.Value ? null : row["token"]?.ToString()
            };

            return Task.FromResult<PortalUserMappingDto?>(dto);
        }

        public Task<List<MapUserPortalDto>> GetMapUserPortalAsync(long? portalId, long? userTypeId)
        {
            var list = new List<MapUserPortalDto>();

            SqlParameter[] parameters =
            {
                new SqlParameter("@p_portal_id", SqlDbType.BigInt)
                {
                    Value = portalId.HasValue ? (object)portalId.Value : DBNull.Value
                },
                new SqlParameter("@p_user_type_id", SqlDbType.BigInt)
                {
                    Value = userTypeId.HasValue ? (object)userTypeId.Value : DBNull.Value
                }
            };

            const string query = @"
                SELECT
                    m.user_portal_id,
                    m.portal_id,
                    p.portal_name,
                    m.User_Type_ID,
                    ut.User_Type_Name,
                    u.Mobile AS mobile,
                    u.Name AS name,
                    CONVERT(varchar(20), u.User_id) AS userid,
                    u.Deviceid AS deviceid,
                    d.DistName AS district_code,
                    b.BlockName AS block_code,
                    Panch.PanchayatName AS panchayatcode,
                    m.entry_by,
                    m.entry_date
                FROM dbo.tbl_user u
                LEFT OUTER JOIN dbo.tbl_map_user_portal m
                    ON m.userid = u.Mobile
                   AND (@p_portal_id IS NULL OR m.portal_id = @p_portal_id)
                   AND (@p_user_type_id IS NULL OR m.User_Type_ID = @p_user_type_id)
                LEFT JOIN dbo.mst_portal p ON m.portal_id = p.portal_id
                LEFT JOIN dbo.UserType ut ON m.User_Type_ID = ut.User_Type_ID AND m.portal_id = ut.Portal_ID
                LEFT OUTER JOIN Districts d ON d.DistCode=u.DistCode
                LEFt OUTER JOIN Blocks b ON b.BlockCode=u.BlockCode
                LEFT OUTER JOIN Panchayats Panch ON Panch.PanchayatCode=u.PanchayatCode
                ORDER BY m.entry_date DESC, m.user_portal_id DESC, u.User_id DESC;";

            DataTable dt = _dataAccess.GetDataTable(query, parameters);

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new MapUserPortalDto
                {
                    UserPortalId = row["user_portal_id"] == DBNull.Value ? null : Convert.ToInt64(row["user_portal_id"]),
                    PortalId = row["portal_id"] == DBNull.Value ? null : Convert.ToInt64(row["portal_id"]),
                    PortalName = row["portal_name"] == DBNull.Value ? null : row["portal_name"]?.ToString(),
                    UserTypeId = row["User_Type_ID"] == DBNull.Value ? null : Convert.ToInt64(row["User_Type_ID"]),
                    UserTypeName = row["User_Type_Name"] == DBNull.Value ? null : row["User_Type_Name"]?.ToString(),
                    Mobile = row["mobile"] == DBNull.Value ? null : row["mobile"]?.ToString(),
                    Name = row["name"] == DBNull.Value ? null : row["name"]?.ToString(),
                    UserId = row["userid"] == DBNull.Value ? null : row["userid"]?.ToString(),
                    DeviceId = row["deviceid"] == DBNull.Value ? null : row["deviceid"]?.ToString(),
                    DistrictCode = row["district_code"] == DBNull.Value ? null : row["district_code"].ToString(),
                    BlockCode = row["block_code"] == DBNull.Value ? null : row["block_code"].ToString(),
                    PanchayatCode = row["panchayatcode"] == DBNull.Value ? null : row["panchayatcode"].ToString(),
                    EntryBy = row["entry_by"] == DBNull.Value ? null : row["entry_by"]?.ToString(),
                    EntryDate = row["entry_date"] == DBNull.Value ? null : Convert.ToDateTime(row["entry_date"])
                });
            }

            return Task.FromResult(list);
        }
    }
}
