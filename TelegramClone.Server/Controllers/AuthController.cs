using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TelegramClone.Server.Data;
using TelegramClone.Shared.Models;

namespace TelegramClone.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    
    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }
    
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdClaim?.Value ?? throw new UnauthorizedAccessException());
    }
    
    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    
    public class LoginRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public User User { get; set; } = null!;
    }
    
    public class UpdateStatusRequest
    {
        public bool IsOnline { get; set; }
    }
    
    // POST: api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        // Проверяем, не занят ли номер телефона
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
            
        if (existingUser != null)
            return BadRequest("Пользователь с таким номером телефона уже существует");
        
        // Проверяем, не занят ли никнейм
        existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);
            
        if (existingUser != null)
            return BadRequest("Пользователь с таким именем уже существует");
        
        // Создаем нового пользователя
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = request.Password, // В реальном приложении нужно хэшировать!
            CreatedAt = DateTime.UtcNow,
            IsOnline = false,
            LastSeen = DateTime.UtcNow,
            IsTyping = false
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        // Создаем JWT токен
        var token = GenerateJwtToken(user);
        
        return Ok(new AuthResponse
        {
            Token = token,
            User = user
        });
    }
    
    // POST: api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        // Ищем пользователя по номеру телефона
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
            
        if (user == null)
            return Unauthorized("Неверный номер телефона или пароль");
        
        // Проверяем пароль (временная проверка)
        if (user.PasswordHash != request.Password)
            return Unauthorized("Неверный номер телефона или пароль");
        
        // Обновляем статус онлайн
        user.IsOnline = true;
        user.LastSeen = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        // Создаем JWT токен
        var token = GenerateJwtToken(user);
        
        return Ok(new AuthResponse
        {
            Token = token,
            User = user
        });
    }
    
    // POST: api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        
        if (user != null)
        {
            user.IsOnline = false;
            user.LastSeen = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        
        return Ok(new { Message = "Logged out successfully" });
    }
    
    // POST: api/auth/updatestatus
    [HttpPost("updatestatus")]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        
        if (user == null)
            return NotFound("Пользователь не найден");
        
        user.IsOnline = request.IsOnline;
        if (!request.IsOnline)
        {
            user.LastSeen = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
        
        return Ok(new { user.IsOnline, user.LastSeen });
    }
    
    // GET: api/auth/status/{userId}
    [HttpGet("status/{userId}")]
    public async Task<IActionResult> GetUserStatus(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        
        if (user == null)
            return NotFound("Пользователь не найден");
        
        return Ok(new
        {
            user.IsOnline,
            user.LastSeen,
            user.IsTyping
        });
    }
    
    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not found")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.MobilePhone, user.PhoneNumber)
        };
        
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: credentials
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}