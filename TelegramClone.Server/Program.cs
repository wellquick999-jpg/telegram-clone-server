using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TelegramClone.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// НАСТРОЙКА ПОРТА ДЛЯ RENDER.COM (ОЧЕНЬ ВАЖНО!)
var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddControllers();

// Добавляем базу данных SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Добавляем SignalR
builder.Services.AddSignalR();

// Добавляем CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});

// Добавляем JWT аутентификацию
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "my-super-secret-key-for-telegram-clone-app-2024-very-long"))
        };
    });

builder.Services.AddAuthorization();

// Добавляем Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Временно отключаем для теста
app.UseCors("AllowAll");
// app.UseAuthentication(); // Временно отключаем для теста
// app.UseAuthorization(); // Временно отключаем для теста
app.MapControllers();

// ТЕСТОВЫЕ ЭНДПОИНТЫ (для проверки работы сервера)
app.MapGet("/", () => "Server is alive!");
app.MapGet("/ping", () => "pong");
app.MapGet("/test", () => "Server is working!");

// Создаем базу данных при запуске
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
    Console.WriteLine("Database created successfully!");
}

Console.WriteLine($"Server running on http://0.0.0.0:{port}");
Console.WriteLine($"Test endpoint: http://0.0.0.0:{port}/test");
Console.WriteLine($"Ping endpoint: http://0.0.0.0:{port}/ping");
app.Run();