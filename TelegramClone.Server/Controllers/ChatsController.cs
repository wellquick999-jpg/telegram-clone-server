using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelegramClone.Server.Data;
using TelegramClone.Shared.Models;

namespace TelegramClone.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatsController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public ChatsController(AppDbContext context)
    {
        _context = context;
    }
    
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdClaim?.Value ?? throw new UnauthorizedAccessException());
    }
    
    [HttpGet]
    public async Task<IActionResult> GetUserChats()
    {
        var userId = GetCurrentUserId();
        
        var chats = await _context.Chats
            .Where(c => c.ParticipantIds.Contains(userId))
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();
        
        var result = new List<object>();
        
        foreach (var chat in chats)
        {
            var lastMessage = await _context.Messages
                .Where(m => m.ChatId == chat.Id)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();
            
            var unreadCount = await _context.Messages
                .Where(m => m.ChatId == chat.Id && 
                           m.SenderId != userId && 
                           !m.IsRead)
                .CountAsync();
            
            bool isOnline = false;
            DateTime? lastSeen = null;
            string chatName = chat.Name;
            
            if (chat.Type == ChatType.Private)
            {
                var otherUserId = chat.ParticipantIds.FirstOrDefault(id => id != userId);
                if (otherUserId != Guid.Empty)
                {
                    var otherUser = await _context.Users.FindAsync(otherUserId);
                    if (otherUser != null)
                    {
                        chatName = otherUser.Username ?? "Пользователь";
                        isOnline = otherUser.IsOnline;
                        lastSeen = otherUser.LastSeen;
                    }
                }
            }
            
            result.Add(new
            {
                chat.Id,
                Name = chatName,
                chat.Type,
                chat.ParticipantIds,
                chat.CreatedAt,
                chat.UpdatedAt,
                LastMessage = lastMessage,
                UnreadCount = unreadCount,
                IsOnline = isOnline,
                LastSeen = lastSeen
            });
        }
        
        return Ok(result);
    }
    
    [HttpPost("private")]
    public async Task<IActionResult> CreatePrivateChat([FromBody] Guid otherUserId)
    {
        var currentUserId = GetCurrentUserId();
        
        var existingChat = await _context.Chats
            .FirstOrDefaultAsync(c => c.Type == ChatType.Private &&
                                      c.ParticipantIds.Contains(currentUserId) &&
                                      c.ParticipantIds.Contains(otherUserId));
        
        var otherUser = await _context.Users.FindAsync(otherUserId);
        if (otherUser == null)
            return NotFound("Пользователь не найден");
        
        if (existingChat != null)
        {
            var lastMessage = await _context.Messages
                .Where(m => m.ChatId == existingChat.Id)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();
            
            var unreadCount = await _context.Messages
                .Where(m => m.ChatId == existingChat.Id && 
                           m.SenderId != currentUserId && 
                           !m.IsRead)
                .CountAsync();
            
            return Ok(new
            {
                existingChat.Id,
                Name = otherUser.Username,
                existingChat.Type,
                existingChat.ParticipantIds,
                existingChat.CreatedAt,
                existingChat.UpdatedAt,
                LastMessage = lastMessage,
                UnreadCount = unreadCount,
                IsOnline = otherUser.IsOnline,
                LastSeen = otherUser.LastSeen
            });
        }
        
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Name = otherUser.Username,
            Type = ChatType.Private,
            ParticipantIds = new List<Guid> { currentUserId, otherUserId },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.Chats.Add(chat);
        await _context.SaveChangesAsync();
        
        return Ok(new
        {
            chat.Id,
            Name = otherUser.Username,
            chat.Type,
            chat.ParticipantIds,
            chat.CreatedAt,
            chat.UpdatedAt,
            LastMessage = (Message?)null,
            UnreadCount = 0,
            IsOnline = otherUser.IsOnline,
            LastSeen = otherUser.LastSeen
        });
    }
    
    [HttpGet("{chatId}/messages")]
    public async Task<IActionResult> GetMessages(Guid chatId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var userId = GetCurrentUserId();
        
        var chat = await _context.Chats.FindAsync(chatId);
        if (chat == null || !chat.ParticipantIds.Contains(userId))
            return NotFound("Чат не найден");
        
        var messages = await _context.Messages
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.Timestamp)
            .Skip(skip)
            .Take(take)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        
        return Ok(messages);
    }
    
    [HttpGet("{chatId}/info")]
    public async Task<IActionResult> GetChatInfo(Guid chatId)
    {
        var userId = GetCurrentUserId();
        
        var chat = await _context.Chats.FindAsync(chatId);
        if (chat == null || !chat.ParticipantIds.Contains(userId))
            return NotFound("Чат не найден");
        
        string chatName = chat.Name;
        bool isOnline = false;
        DateTime? lastSeen = null;
        Guid otherUserId = Guid.Empty;
        
        if (chat.Type == ChatType.Private)
        {
            otherUserId = chat.ParticipantIds.FirstOrDefault(id => id != userId);
            if (otherUserId != Guid.Empty)
            {
                var otherUser = await _context.Users.FindAsync(otherUserId);
                if (otherUser != null)
                {
                    chatName = otherUser.Username ?? "Пользователь";
                    isOnline = otherUser.IsOnline;
                    lastSeen = otherUser.LastSeen;
                }
            }
        }
        
        return Ok(new
        {
            chat.Id,
            Name = chatName,
            chat.Type,
            chat.ParticipantIds,
            chat.CreatedAt,
            chat.UpdatedAt,
            IsOnline = isOnline,
            LastSeen = lastSeen,
            UserId = otherUserId
        });
    }
}