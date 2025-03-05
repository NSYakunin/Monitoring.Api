using Monitoring.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Сервис для логина/аутентификации (работа с таблицей Users)
    /// </summary>
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
                    ORDER BY [smallName];
                ";
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
                    ORDER BY [smallName];
                ";
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

        public async Task<(int? userId, int? divisionId, bool isValid)> CheckUserCredentialsAsync(
            string selectedUser,
            string password
        )
        {
            if (string.IsNullOrEmpty(selectedUser) || string.IsNullOrEmpty(password))
                return (null, null, false);

            int? divisionId = null;
            int? userId = null;
            bool isPasswordValid = false;

            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [idUser], [Password], [idDivision]
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
                            userId = int.Parse(reader["idUser"].ToString());
                            divisionId = int.Parse(reader["idDivision"].ToString());
                            if (passFromDb == password)
                            {
                                isPasswordValid = true;
                            }
                        }
                    }
                }
            }
            return (userId, divisionId, isPasswordValid);
        }
    }
}