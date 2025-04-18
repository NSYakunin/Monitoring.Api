﻿using Monitoring.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Реализация логики логина.
    /// </summary>
    public class LoginService : ILoginService
    {
        private readonly IConfiguration _configuration;

        public LoginService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Возвращает список активных (Isvalid=1) пользователей (smallName).
        /// </summary>
        public async Task<List<string>> GetAllUsersAsync()
        {
            var result = new List<string>();
            var connStr = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [smallName]
                    FROM [Users]
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

        /// <summary>
        /// Возвращает список НЕактивных пользователей (Isvalid=0).
        /// </summary>
        public async Task<List<string>> GetAllInactiveUsersAsync()
        {
            var result = new List<string>();
            var connStr = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [smallName]
                    FROM [Users]
                    WHERE [Isvalid] = 0
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

        /// <summary>
        /// Фильтрует пользователей (Isvalid=1) по подстроке.
        /// </summary>
        public async Task<List<string>> FilterUsersAsync(string query)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(query)) query = "";

            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [smallName]
                    FROM [Users]
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

        /// <summary>
        /// Проверка логина/пароля. Возвращает (userId, divisionId, isValid).
        /// </summary>
        public async Task<(int? userId, int? divisionId, bool isValid)> CheckUserCredentialsAsync(
            string selectedUser, string password
        )
        {
            if (string.IsNullOrEmpty(selectedUser) || string.IsNullOrEmpty(password))
                return (null, null, false);

            int? userId = null;
            int? divisionId = null;
            bool isPasswordValid = false;

            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [idUser], [Password], [idDivision]
                    FROM [Users]
                    WHERE [smallName] = @user
                      AND [Isvalid] = 1
                ";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@user", selectedUser);
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            userId = (int)reader["idUser"];
                            divisionId = (int)reader["idDivision"];
                            var passFromDb = reader["Password"]?.ToString();
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

        /// <summary>
        /// Возвращает idUser по полю smallName.
        /// Если такой пользователь не найден — вернёт null.
        /// </summary>
        public async Task<int> GetUserIdByNameAsync(string userName)
        {
            int userId = 0;
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new SqlConnection(connStr))
            {
                string query = @"
                    SELECT [idUser]
                    FROM [DocumentControl].[dbo].[Users]
                    WHERE [smallName] = @UserName
                ";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserName", userName);
                    await conn.OpenAsync();

                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        userId = Convert.ToInt32(result);
                    }
                }
            }

            return userId;
        }
    }
}