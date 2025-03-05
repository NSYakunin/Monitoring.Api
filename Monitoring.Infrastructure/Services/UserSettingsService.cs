using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Services
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly IConfiguration _configuration;

        public UserSettingsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> HasAccessToSettingsAsync(int userId)
        {
            var settings = await GetPrivacySettingsAsync(userId);
            return settings.CanAccessSettings;
        }

        public async Task<bool> HasAccessToSendCloseRequestAsync(int userId)
        {
            var s = await GetPrivacySettingsAsync(userId);
            return s.CanSendCloseRequest;
        }

        public async Task<bool> HasAccessToCloseWorkAsync(int userId)
        {
            var s = await GetPrivacySettingsAsync(userId);
            return s.CanCloseWork;
        }

        public async Task<PrivacySettingsDto> GetPrivacySettingsAsync(int userId)
        {
            var dto = new PrivacySettingsDto
            {
                CanCloseWork = false,
                CanSendCloseRequest = false,
                CanAccessSettings = false
            };

            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [CanCloseWork],[CanSendCloseRequest],[CanAccessSettings]
                    FROM [UserPrivacy]
                    WHERE [idUser] = @u
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    await conn.OpenAsync();
                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        if (await rdr.ReadAsync())
                        {
                            dto.CanCloseWork = rdr.GetBoolean(0);
                            dto.CanSendCloseRequest = rdr.GetBoolean(1);
                            dto.CanAccessSettings = rdr.GetBoolean(2);
                        }
                    }
                }
            }
            return dto;
        }

        public async Task<bool> IsUserValidAsync(int userId)
        {
            bool isActive = false;
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                string sql = @"SELECT Isvalid FROM [Users] WHERE [idUser] = @u";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    var obj = await cmd.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                    {
                        isActive = (Convert.ToInt32(obj) == 1);
                    }
                }
            }
            return isActive;
        }

        public async Task SavePrivacySettingsAsync(int userId, PrivacySettingsDto dto, bool isActive)
        {
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1) Обновляем Users (Isvalid)
                        string sqlUpdUser = @"
                            UPDATE [Users] SET [Isvalid] = @isv
                            WHERE [idUser] = @u
                        ";
                        using (var cmd1 = new SqlCommand(sqlUpdUser, conn, trans))
                        {
                            cmd1.Parameters.AddWithValue("@isv", isActive ? 1 : 0);
                            cmd1.Parameters.AddWithValue("@u", userId);
                            await cmd1.ExecuteNonQueryAsync();
                        }

                        // 2) Проверяем запись в UserPrivacy
                        string sqlCheck = @"SELECT COUNT(*) FROM [UserPrivacy] WHERE [idUser] = @u";
                        using (var cmd2 = new SqlCommand(sqlCheck, conn, trans))
                        {
                            cmd2.Parameters.AddWithValue("@u", userId);
                            int count = (int)(await cmd2.ExecuteScalarAsync());
                            if (count == 0)
                            {
                                // INSERT
                                string ins = @"
                                    INSERT INTO [UserPrivacy]
                                    ([idUser],[CanCloseWork],[CanSendCloseRequest],[CanAccessSettings])
                                    VALUES (@u,@c1,@c2,@c3)
                                ";
                                using (var cmd3 = new SqlCommand(ins, conn, trans))
                                {
                                    cmd3.Parameters.AddWithValue("@u", userId);
                                    cmd3.Parameters.AddWithValue("@c1", dto.CanCloseWork);
                                    cmd3.Parameters.AddWithValue("@c2", dto.CanSendCloseRequest);
                                    cmd3.Parameters.AddWithValue("@c3", dto.CanAccessSettings);
                                    await cmd3.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                // UPDATE
                                string upd = @"
                                    UPDATE [UserPrivacy]
                                    SET [CanCloseWork] = @c1,
                                        [CanSendCloseRequest] = @c2,
                                        [CanAccessSettings] = @c3
                                    WHERE [idUser] = @u
                                ";
                                using (var cmd3 = new SqlCommand(upd, conn, trans))
                                {
                                    cmd3.Parameters.AddWithValue("@u", userId);
                                    cmd3.Parameters.AddWithValue("@c1", dto.CanCloseWork);
                                    cmd3.Parameters.AddWithValue("@c2", dto.CanSendCloseRequest);
                                    cmd3.Parameters.AddWithValue("@c3", dto.CanAccessSettings);
                                    await cmd3.ExecuteNonQueryAsync();
                                }
                            }
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<List<DivisionDto>> GetAllDivisionsAsync()
        {
            var list = new List<DivisionDto>();
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [idDivision],
                           [idParentDivision],
                           [NameDivision],
                           [smallNameDivision],
                           [position],
                           [idUserHead]
                    FROM [Divisions]
                    ORDER BY [idDivision];
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var d = new DivisionDto
                            {
                                IdDivision = reader.GetInt32(0),
                                IdParentDivision = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                                NameDivision = reader.GetString(2),
                                SmallNameDivision = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Position = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                                IdUserHead = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5)
                            };
                            list.Add(d);
                        }
                    }
                }
            }
            return list;
        }

        public async Task<List<int>> GetUserAllowedDivisionsAsync(int userId)
        {
            var list = new List<int>();
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [idDivision]
                    FROM [UserAllowedDivisions]
                    WHERE [idUser] = @u
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    await conn.OpenAsync();
                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            list.Add(rdr.GetInt32(0));
                        }
                    }
                }
            }
            return list;
        }

        public async Task SaveUserAllowedDivisionsAsync(int userId, List<int> divisionIds)
        {
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1) Удаляем все старые
                        string delSql = @"DELETE FROM [UserAllowedDivisions] WHERE [idUser] = @u";
                        using (var cmdDel = new SqlCommand(delSql, conn, trans))
                        {
                            cmdDel.Parameters.AddWithValue("@u", userId);
                            await cmdDel.ExecuteNonQueryAsync();
                        }

                        // 2) Вставляем новые
                        string insSql = @"
                            INSERT INTO [UserAllowedDivisions]([idUser],[idDivision])
                            VALUES (@u, @d)
                        ";
                        foreach (var divId in divisionIds)
                        {
                            using (var cmdIns = new SqlCommand(insSql, conn, trans))
                            {
                                cmdIns.Parameters.AddWithValue("@u", userId);
                                cmdIns.Parameters.AddWithValue("@d", divId);
                                await cmdIns.ExecuteNonQueryAsync();
                            }
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task ChangeUserPasswordAsync(int userId, string newPassword)
        {
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                string sql = @"
                    UPDATE [Users]
                    SET [Password] = @pwd
                    WHERE [idUser] = @u
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@pwd", newPassword);
                    cmd.Parameters.AddWithValue("@u", userId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<string?> GetUserCurrentPasswordAsync(int userId)
        {
            string? pwd = null;
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                string sql = @"SELECT [Password] FROM [Users] WHERE [idUser] = @u";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        if (await rdr.ReadAsync())
                        {
                            pwd = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                        }
                    }
                }
            }
            return pwd;
        }

        public async Task<int> RegisterUserInDbAsync(
            string fullName,
            string smallName,
            string password,
            int? idDivision,
            bool canCloseWork,
            bool canSendCloseRequest,
            bool canAccessSettings
        )
        {
            int newId = 0;
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1) Вставляем в Users
                        string insUser = @"
                            INSERT INTO [Users]
                            ([Name],[smallName],[idDivision],[Password],[idTypeUser],[Isvalid])
                            OUTPUT INSERTED.[idUser]
                            VALUES (@nm,@sn,@div,@pwd,2,1)
                        ";
                        using (var cmd1 = new SqlCommand(insUser, conn, trans))
                        {
                            cmd1.Parameters.AddWithValue("@nm", fullName);
                            cmd1.Parameters.AddWithValue("@sn", (object?)smallName ?? DBNull.Value);
                            cmd1.Parameters.AddWithValue("@div", (object?)idDivision ?? DBNull.Value);
                            cmd1.Parameters.AddWithValue("@pwd", password);
                            var objNew = await cmd1.ExecuteScalarAsync();
                            newId = Convert.ToInt32(objNew);
                        }

                        // 2) Вставляем в UserPrivacy
                        string insPriv = @"
                            INSERT INTO [UserPrivacy]
                            ([idUser],[CanCloseWork],[CanSendCloseRequest],[CanAccessSettings])
                            VALUES (@u,@c1,@c2,@c3)
                        ";
                        using (var cmd2 = new SqlCommand(insPriv, conn, trans))
                        {
                            cmd2.Parameters.AddWithValue("@u", newId);
                            cmd2.Parameters.AddWithValue("@c1", canCloseWork);
                            cmd2.Parameters.AddWithValue("@c2", canSendCloseRequest);
                            cmd2.Parameters.AddWithValue("@c3", canAccessSettings);
                            await cmd2.ExecuteNonQueryAsync();
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
            return newId;
        }
    }
}