using Monitoring.Api;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1) Подтягиваем настройки JWT
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// 2) Извлекаем ключи и другие поля (для удобства)
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSection.GetValue<string>("SecretKey");
var issuer = jwtSection.GetValue<string>("Issuer");
var audience = jwtSection.GetValue<string>("Audience");

// 2) JWT-аутентификация
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    opt.RequireHttpsMetadata = false;
    opt.SaveToken = true;
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 3) Подключаем EF Core к SQL
builder.Services.AddDbContext<MyDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(cs);
});

// 4) Добавляем MemoryCache (для кэширования WorkItems)
builder.Services.AddMemoryCache();

// 5) Регистрируем наши сервисы
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();
builder.Services.AddScoped<IWorkItemAppService, WorkItemAppService>();
builder.Services.AddScoped<IWorkRequestService, WorkRequestAppService>();

// 6) Контроллеры + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 7) CORS при необходимости
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("MyPolicy", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();


app.UseRouting();
app.UseCors("MyPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();