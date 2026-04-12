using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TelegramClone.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// НЕ НАСТРАИВАЕМ ПОРТ ВРУЧНУЮ — Render сам задаст через PORT

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Тестовые эндпоинты
app.MapGet("/", () => "Server is alive!");
app.MapGet("/ping", () => "pong");

// Исправляем индекс Messages (удаляем UNIQUE constraint)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
    
    try
    {
        // Удаляем старый уникальный индекс
        dbContext.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Messages_ChatId");
        // Создаём обычный индекс
        dbContext.Database.ExecuteSqlRaw("CREATE INDEX IX_Messages_ChatId ON Messages(ChatId)");
        Console.WriteLine("Index IX_Messages_ChatId fixed (non-unique)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Note: {ex.Message}");
    }
}

app.Run();