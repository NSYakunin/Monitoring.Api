using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1) ������������ DbContext
builder.Services.AddDbContext<MyDbContext>(options =>
{
    // ������ ������ ����������� �� appsettings.json
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// 2) ������������ ��� ������
builder.Services.AddScoped<IWorkItemAppService, WorkItemAppService>();

// ��������� �����������
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();