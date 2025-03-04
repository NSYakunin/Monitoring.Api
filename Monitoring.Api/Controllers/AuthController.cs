using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ILoginService _loginService;
        private readonly JwtSettings _jwtSettings;

        public AuthController(
            ILoginService loginService,
            IOptions<JwtSettings> jwtOptions
        )
        {
            _loginService = loginService;
            _jwtSettings = jwtOptions.Value;
        }

        [HttpGet("FilterUsers")]
        [AllowAnonymous]
        public async Task<ActionResult<List<string>>> FilterUsers([FromQuery] string query)
        {
            try
            {
                var result = await _loginService.FilterUsersAsync(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Ошибка сервера: " + ex.Message);
            }
        }

        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.SelectedUser))
                return BadRequest("Не задано имя пользователя");
            if (string.IsNullOrEmpty(request.Password))
                return BadRequest("Не задан пароль");

            var (divisionId, isValid) = await _loginService.CheckUserCredentialsAsync(request.SelectedUser, request.Password);
            if (!isValid || !divisionId.HasValue)
            {
                return Unauthorized("Неверное имя пользователя или пароль");
            }

            // Генерируем токен
            var token = GenerateJwtToken(request.SelectedUser, divisionId.Value);

            return Ok(new LoginResponse
            {
                Token = token,
                UserName = request.SelectedUser,
                DivisionId = divisionId
            });
        }

        [HttpGet("TestAuth")]
        [Authorize]
        public ActionResult<string> TestAuth()
        {
            // Посмотреть имя пользователя
            var userName = User.Identity?.Name;
            var divId = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;

            return Ok($"Вы авторизованы как {userName}, divisionId={divId}");
        }

        private string GenerateJwtToken(string userName, int divisionId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userName),
                new Claim("divisionId", divisionId.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}