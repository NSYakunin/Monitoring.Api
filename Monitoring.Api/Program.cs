using Monitoring.Api;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1) ������������ ��������� JWT (������ �� appsettings.json)
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// 2) ��������� ����� � ������ ���� (��� ��������)
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSection.GetValue<string>("SecretKey");
var issuer = jwtSection.GetValue<string>("Issuer");
var audience = jwtSection.GetValue<string>("Audience");

// 3) ����������� �������������� � JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // ��� ��������� �������� Https-���������� � ������� ��������� ������
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
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

// 4) ����������� � ��
builder.Services.AddDbContext<MyDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// 5) ������������ ���� �������
builder.Services.AddScoped<IWorkItemAppService, WorkItemAppService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();

// 6) ��������� ����������� + Swagger (���� �����)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 7) CORS-��������, ���� ���������
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseCors("MyPolicy");

app.MapControllers();
app.Run();