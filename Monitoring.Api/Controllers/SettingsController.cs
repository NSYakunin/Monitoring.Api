using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.Json;
using System.Security.Claims;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly IUserSettingsService _userSettingsService;
        private readonly ILoginService _loginService;

        public SettingsController(IUserSettingsService userSettingsService, ILoginService loginService)
        {
            _userSettingsService = userSettingsService;
            _loginService = loginService;
        }

        /// <summary>
        /// GET /api/Settings?showInactive=true/false&selectedUser=...
        /// Возвращает данные для страницы "Настройки":
        ///  - Список всех пользователей (активных или неактивных)
        ///  - Список подразделений
        ///  - Если передан selectedUser, подгружаем его настройки
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<SettingsLoadData>> GetSettings([FromQuery] bool showInactive, [FromQuery] string? selectedUser)
        {
            // 1) Проверяем, есть ли у текущего userId право на доступ к настройкам
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Forbid("Нет userId");
            int userId = int.Parse(userIdClaim);

            bool canAccess = await _userSettingsService.HasAccessToSettingsAsync(userId);
            if (!canAccess) return Forbid("Нет права на доступ к настройкам");

            // 2) Грузим список пользователей (активных или неактивных)
            List<string> all;
            if (!showInactive)
                all = await _loginService.GetAllUsersAsync();          // Isvalid=1
            else
                all = await _loginService.GetAllInactiveUsersAsync(); // Isvalid=0

            // 3) Список подразделений
            var subdivs = await _userSettingsService.GetAllDivisionsAsync();

            var result = new SettingsLoadData
            {
                AllUsers = all,
                Subdivisions = subdivs.Select(d => new SubdivisionInfo
                {
                    IdDivision = d.IdDivision,
                    SmallNameDivision = d.SmallNameDivision
                }).ToList(),
                SelectedUserName = null,
                CurrentPasswordForSelectedUser = null,
                CurrentPrivacySettings = null,
                UserSelectedDivisionIds = new List<int>(),
                IsUserValid = false
            };

            // Если передан конкретный пользователь, подгружаем его настройки
            if (!string.IsNullOrEmpty(selectedUser))
            {
                var selUserId = await _loginService.GetUserIdByNameAsync(selectedUser);
                if (selUserId > 0)
                {
                    result.SelectedUserName = selectedUser;

                    // Приватность
                    var priv = await _userSettingsService.GetPrivacySettingsAsync(selUserId);
                    result.CurrentPrivacySettings = new PrivacySettingsDto
                    {
                        CanCloseWork = priv.CanCloseWork,
                        CanSendCloseRequest = priv.CanSendCloseRequest,
                        CanAccessSettings = priv.CanAccessSettings
                    };

                    // Список доступных отделов
                    var userDivIds = await _userSettingsService.GetUserAllowedDivisionsAsync(selUserId);
                    result.UserSelectedDivisionIds = userDivIds;

                    // Текущий пароль
                    var pwd = await _userSettingsService.GetUserCurrentPasswordAsync(selUserId);
                    result.CurrentPasswordForSelectedUser = pwd;

                    // Активен ли
                    var isVal = await _userSettingsService.IsUserValidAsync(selUserId);
                    result.IsUserValid = isVal;
                }
            }

            return Ok(result);
        }

        /// <summary>
        /// POST /api/Settings/SavePrivacySettings
        /// Сохраняем (CanCloseWork, CanSendCloseRequest, CanAccessSettings, IsActive)
        /// </summary>
        [HttpPost("SavePrivacySettings")]
        public async Task<ActionResult> SavePrivacySettings([FromBody] SavePrivacyDto dto)
        {
            // Проверяем, что текущий пользователь имеет право на настройки
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Forbid("Нет userId");
            int meId = int.Parse(userIdClaim);

            bool canAccess = await _userSettingsService.HasAccessToSettingsAsync(meId);
            if (!canAccess) return Forbid("Нет доступа");

            // Ищем userId по smallName
            var userId = await _loginService.GetUserIdByNameAsync(dto.UserName);
            if (userId <= 0) return BadRequest(new { success = false, message = "Пользователь не найден" });

            var priv = new PrivacySettingsDto
            {
                CanCloseWork = dto.CanCloseWork,
                CanSendCloseRequest = dto.CanSendCloseRequest,
                CanAccessSettings = dto.CanAccessSettings
            };
            await _userSettingsService.SavePrivacySettingsAsync(userId, priv, dto.IsActive);

            return Ok(new { success = true });
        }

        /// <summary>
        /// POST /api/Settings/SaveSubdivisions
        /// Сохраняем список отделов, к которым пользователь имеет доступ
        /// </summary>
        [HttpPost("SaveSubdivisions")]
        public async Task<ActionResult> SaveSubdivisions([FromBody] SaveSubdivisionsDto dto)
        {
            var myIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (myIdClaim == null) return Forbid("Нет userId");
            int meId = int.Parse(myIdClaim);

            bool canAccess = await _userSettingsService.HasAccessToSettingsAsync(meId);
            if (!canAccess) return Forbid("Нет доступа");

            var userId = await _loginService.GetUserIdByNameAsync(dto.UserName);
            if (userId <= 0) return BadRequest(new { success = false, message = "Пользователь не найден" });

            await _userSettingsService.SaveUserAllowedDivisionsAsync(userId, dto.Subdivisions);
            return Ok(new { success = true });
        }

        /// <summary>
        /// POST /api/Settings/ChangeUserPassword
        /// </summary>
        [HttpPost("ChangeUserPassword")]
        public async Task<ActionResult> ChangeUserPassword([FromBody] ChangePasswordDto dto)
        {
            var meIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (meIdClaim == null) return Forbid("Нет userId");
            int meId = int.Parse(meIdClaim);

            bool canAccess = await _userSettingsService.HasAccessToSettingsAsync(meId);
            if (!canAccess) return Forbid("Нет доступа");

            if (string.IsNullOrEmpty(dto.UserName) || string.IsNullOrEmpty(dto.NewPassword))
                return BadRequest(new { success = false, message = "userName или newPassword пустые" });

            var userId = await _loginService.GetUserIdByNameAsync(dto.UserName);
            if (userId <= 0) return BadRequest(new { success = false, message = "Пользователь не найден" });

            await _userSettingsService.ChangeUserPasswordAsync(userId, dto.NewPassword.Trim());
            return Ok(new { success = true });
        }

        /// <summary>
        /// POST /api/Settings/RegisterUser
        /// </summary>
        [HttpPost("RegisterUser")]
        public async Task<ActionResult> RegisterUser([FromBody] RegisterUserDto dto)
        {
            var meIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (meIdClaim == null) return Forbid("Нет userId");
            int meId = int.Parse(meIdClaim);

            bool canAccess = await _userSettingsService.HasAccessToSettingsAsync(meId);
            if (!canAccess) return Forbid("Нет доступа");

            if (string.IsNullOrEmpty(dto.FullName) || string.IsNullOrEmpty(dto.SmallName) || string.IsNullOrEmpty(dto.Password))
            {
                return BadRequest(new { success = false, message = "Не заполнены ФИО, smallName, пароль" });
            }

            // Проверим, не существует ли уже пользователь
            var existingId = await _loginService.GetUserIdByNameAsync(dto.SmallName);
            if (existingId > 0) return BadRequest(new { success = false, message = "Такой пользователь уже есть" });

            int newUserId = await _userSettingsService.RegisterUserInDbAsync(
                dto.FullName,
                dto.SmallName,
                dto.Password,
                dto.IdDivision,
                dto.CanCloseWork,
                dto.CanSendCloseRequest,
                dto.CanAccessSettings
            );

            return Ok(new { success = true, newUserId = newUserId });
        }
    }

    // ========= DTO-классы, которые мы возвращаем / принимаем на этой странице ===========
    public class SettingsLoadData
    {
        public List<string> AllUsers { get; set; }
        public List<SubdivisionInfo> Subdivisions { get; set; }
        public string SelectedUserName { get; set; }
        public string CurrentPasswordForSelectedUser { get; set; }
        public PrivacySettingsDto CurrentPrivacySettings { get; set; }
        public List<int> UserSelectedDivisionIds { get; set; }
        public bool IsUserValid { get; set; }
    }

    public class SubdivisionInfo
    {
        public int IdDivision { get; set; }
        public string SmallNameDivision { get; set; }
    }

    public class SavePrivacyDto
    {
        public string UserName { get; set; }
        public bool CanCloseWork { get; set; }
        public bool CanSendCloseRequest { get; set; }
        public bool CanAccessSettings { get; set; }
        public bool IsActive { get; set; }
    }

    public class SaveSubdivisionsDto
    {
        public string UserName { get; set; }
        public List<int> Subdivisions { get; set; }
    }

    public class ChangePasswordDto
    {
        public string UserName { get; set; }
        public string NewPassword { get; set; }
    }

    public class RegisterUserDto
    {
        public string FullName { get; set; }
        public string SmallName { get; set; }
        public int? IdDivision { get; set; }
        public string Password { get; set; }
        public bool CanCloseWork { get; set; }
        public bool CanSendCloseRequest { get; set; }
        public bool CanAccessSettings { get; set; }
    }
}