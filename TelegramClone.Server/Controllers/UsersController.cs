using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelegramClone.Server.Data;
using TelegramClone.Shared.Models;

namespace TelegramClone.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public UsersController(AppDbContext context)
    {
        _context = context;
    }
    
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            throw new UnauthorizedAccessException("User not authenticated");
        return Guid.Parse(userIdClaim.Value);
    }
    
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string query)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            if (string.IsNullOrWhiteSpace(query))
                return Ok(new List<object>());
            
            var users = await _context.Users
                .Where(u => u.Id != currentUserId &&
                           (u.Username.Contains(query) || u.PhoneNumber.Contains(query)))
                .Take(20)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.PhoneNumber,
                    u.AvatarUrl,
                    u.Bio,
                    u.IsOnline,
                    u.LastSeen
                })
                .ToListAsync();
            
            return Ok(users);
        }
        catch (Exception ex)
        {
            return BadRequest($"Ошибка при поиске: {ex.Message}");
        }
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        
        var user = await _context.Users
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.PhoneNumber,
                u.AvatarUrl,
                u.Bio,
                u.IsOnline,
                u.LastSeen,
                u.CreatedAt
            })
            .FirstOrDefaultAsync();
        
        if (user == null)
            return NotFound("Пользователь не найден");
        
        return Ok(user);
    }
    
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = GetCurrentUserId();
            
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.PhoneNumber,
                    u.AvatarUrl,
                    u.Bio,
                    u.IsOnline,
                    u.LastSeen,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();
            
            if (user == null)
                return NotFound("Пользователь не найден");
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            return Unauthorized($"Ошибка авторизации: {ex.Message}");
        }
    }
    
    [HttpGet("status/{userId}")]
    public async Task<IActionResult> GetUserStatus(Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        
        var user = await _context.Users.FindAsync(userId);
        
        if (user == null)
            return NotFound("Пользователь не найден");
        
        return Ok(new
        {
            user.IsOnline,
            user.LastSeen,
            user.IsTyping,
            LastSeenFormatted = user.LastSeen.HasValue ? 
                GetTimeAgo(user.LastSeen.Value) : "никогда"
        });
    }
    
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var currentUserId = GetCurrentUserId();
        
        var user = await _context.Users.FindAsync(currentUserId);
        if (user == null)
            return NotFound("Пользователь не найден");
        
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username && u.Id != currentUserId);
            
            if (existingUser != null)
                return BadRequest("Это имя пользователя уже занято");
            
            user.Username = request.Username;
        }
        
        if (request.Bio != null)
        {
            user.Bio = request.Bio;
        }
        
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
            user.AvatarUrl = request.AvatarUrl;
        
        await _context.SaveChangesAsync();
        
        return Ok(new
        {
            user.Id,
            user.Username,
            user.Bio,
            user.AvatarUrl,
            user.IsOnline,
            user.LastSeen
        });
    }
    
    private string GetTimeAgo(DateTime time)
    {
        var diff = DateTime.UtcNow - time;
        
        if (diff.TotalMinutes < 1)
            return "только что";
        if (diff.TotalMinutes < 60)
            return $"{Math.Floor(diff.TotalMinutes)} мин назад";
        if (diff.TotalHours < 24)
            return $"{Math.Floor(diff.TotalHours)} ч назад";
        if (diff.TotalDays < 7)
            return $"{Math.Floor(diff.TotalDays)} дн назад";
        
        return time.ToLocalTime().ToString("dd.MM.yyyy");
    }
}

public class UpdateProfileRequest
{
    public string? Username { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}