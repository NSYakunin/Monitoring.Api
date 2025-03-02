using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1) Регистрируем DbContext
builder.Services.AddDbContext<MyDbContext>(options =>
{
    // Читаем строку подключения из appsettings.json
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// 2) Регистрируем наш сервис
builder.Services.AddScoped<IWorkItemAppService, WorkItemAppService>();

// Добавляем контроллеры
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();