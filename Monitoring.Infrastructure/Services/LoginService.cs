using Monitoring.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace Monitoring.Infrastructure.Services
{
    public class LoginService : ILoginService
    {
        private readonly IConfiguration _configuration;

        public LoginService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<string>> GetAllUsersAsync()
        {
            var result = new List<string>();
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [smallName] 
                    FROM [DocumentControl].[dbo].[Users]
                    WHERE [Isvalid] = 1
                    ORDER BY [smallName]";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(reader["smallName"].ToString());
                        }
                    }
                }
            }
            return result;
        }

        public async Task<List<string>> FilterUsersAsync(string query)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(query)) query = "";

            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [smallName] 
                    FROM [DocumentControl].[dbo].[Users]
                    WHERE [Isvalid] = 1
                      AND [smallName] LIKE '%' + @q + '%'
                    ORDER BY [smallName]";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@q", query);
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(reader["smallName"].ToString());
                        }
                    }
                }
            }
            return result;
        }

        public async Task<(int? divisionId, bool isValid)> CheckUserCredentialsAsync(
            string selectedUser,
            string password
        )
        {
            if (string.IsNullOrEmpty(selectedUser) || string.IsNullOrEmpty(password))
                return (null, false);

            int? divisionId = null;
            bool isPasswordValid = false;
            var connStr = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [Password], [idDivision]
                    FROM [DocumentControl].[dbo].[Users]
                    WHERE [smallName] = @user
                      AND [Isvalid] = 1";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@user", selectedUser);
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var passFromDb = reader["Password"]?.ToString();
                            divisionId = reader["idDivision"] as int?;
                            if (passFromDb == password)
                            {
                                isPasswordValid = true;
                            }
                        }
                    }
                }
            }
            return (divisionId, isPasswordValid);
        }
    }
}