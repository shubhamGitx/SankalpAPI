using System.Data;
using Microsoft.Data.SqlClient;

namespace SankalpAPI.Helper
{
    public class clsDataAccess
    {
        private readonly IConfiguration _config;
        private readonly ExceptionLogging _exceptionLogging;

        public clsDataAccess(IConfiguration config, ExceptionLogging exceptionLogging)
        {
            _config = config;
            _exceptionLogging = exceptionLogging;
        }

        public SqlConnection GetConnection()
        {
            string? connectionString =
                _config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
            }

            return new SqlConnection(connectionString);
        }

        // ============================================================
        // GET DATATABLE
        // ============================================================
        public DataTable GetDataTable(string query)
        {
            DataTable dt = new DataTable();

            try
            {
                using (var con = GetConnection())
                using (var cmd = new SqlCommand(query, con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandTimeout = 120;

                    con.Open();
                    da.Fill(dt);
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }

            return dt;
        }

        private static void AttachParameters(SqlCommand cmd, SqlParameter[]? parameters)
        {
            if (parameters == null || parameters.Length == 0) return;
            foreach (var p in parameters)
            {
                if (p == null) continue;
                cmd.Parameters.Add((SqlParameter)((ICloneable)p).Clone());
            }
        }

        // ============================================================
        // GET DATATABLE WITH PARAMETERS
        // ============================================================
        public DataTable GetDataTable(string query, SqlParameter[] parameters)
        {
            DataTable dt = new DataTable();

            try
            {
                using (var con = GetConnection())
                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.CommandTimeout = 120;

                    AttachParameters(cmd, parameters);

                    using (var da = new SqlDataAdapter(cmd))
                    {
                        con.Open();
                        da.Fill(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }

            return dt;
        }

        // ============================================================
        // GET DATATABLE USING STORED PROCEDURE
        // ============================================================
        public DataTable GetDataTableWithProc(string procName, SqlParameter[] parameters)
        {
            DataTable dt = new DataTable();

            try
            {
                using (var con = GetConnection())
                using (var cmd = new SqlCommand(procName, con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;

                    AttachParameters(cmd, parameters);

                    using (var da = new SqlDataAdapter(cmd))
                    {
                        con.Open();
                        da.Fill(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }

            return dt;
        }

        // ============================================================
        // EXECUTE SQL - INSERT / UPDATE / DELETE
        // ============================================================
        public int ExecuteSql(string query, SqlParameter[] parameters)
        {
            try
            {
                using (var con = GetConnection())
                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.CommandTimeout = 120;

                    AttachParameters(cmd, parameters);

                    con.Open();

                    return cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }
        }

        // ============================================================
        // EXECUTE STORED PROCEDURE - INSERT / UPDATE / DELETE
        // ============================================================
        public int ExecuteProcedure(string procName, SqlParameter[] parameters)
        {
            try
            {
                using (var con = GetConnection())
                using (var cmd = new SqlCommand(procName, con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;

                    AttachParameters(cmd, parameters);

                    con.Open();

                    return cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }
        }

        // ============================================================
        // EXECUTE SCALAR
        // ============================================================
        public object? ExecuteScalar(string query, SqlParameter[] parameters)
        {
            try
            {
                using (var con = GetConnection())
                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.CommandTimeout = 120;

                    AttachParameters(cmd, parameters);

                    con.Open();

                    return cmd.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }
        }

        // ============================================================
        // EXECUTE STORED PROCEDURE - SCALAR
        // ============================================================
        public object? ExecuteScalarProcedure(string procName, SqlParameter[] parameters)
        {
            try
            {
                using (var con = GetConnection())
                using (var cmd = new SqlCommand(procName, con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;

                    AttachParameters(cmd, parameters);

                    con.Open();

                    return cmd.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);
                throw;
            }
        }

        // ============================================================
        // TRANSACTION SUPPORT
        // ============================================================
        public int ExecuteTransaction(List<SqlCommand> commands)
        {
            if (commands == null || commands.Count == 0)
            {
                throw new ArgumentException(
                    "At least one SQL command is required.",
                    nameof(commands));
            }

            using (var con = GetConnection())
            {
                con.Open();

                using (var trans = con.BeginTransaction())
                {
                    try
                    {
                        foreach (var cmd in commands)
                        {
                            cmd.Connection = con;
                            cmd.Transaction = trans;
                            cmd.CommandTimeout = 120;

                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();

                        return 1;
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            trans.Rollback();
                        }
                        catch
                        {
                            // Do not hide the original exception.
                        }

                        _exceptionLogging.SendErrorToText(ex);

                        throw;
                    }
                }
            }
        }
    }
}
