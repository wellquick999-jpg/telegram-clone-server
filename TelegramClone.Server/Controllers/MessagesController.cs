using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelegramClone.Server.Data;
using TelegramClone.Shared.Models;

namespace TelegramClone.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public MessagesController(AppDbContext context)
    {
        _context = context;
    }
    
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdClaim?.Value ?? throw new UnauthorizedAccessException());
    }
    
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var userId = GetCurrentUserId();
        
        var chat = await _context.Chats.FindAsync(request.ChatId);
        if (chat == null || !chat.ParticipantIds.Contains(userId))
            return NotFound("Чат не найден");
        
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatId = request.ChatId,
            SenderId = userId,
            Text = request.Text,
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsEdited = false
        };
        
        chat.UpdatedAt = DateTime.UtcNow;
        
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        
        return Ok(message);
    }
    
    [HttpPut("{messageId}")]
    public async Task<IActionResult> EditMessage(Guid messageId, [FromBody] EditMessageRequest request)
    {
        var userId = GetCurrentUserId();
        
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null)
            return NotFound("Сообщение не найдено");
        
        if (message.SenderId != userId)
            return Forbid("Вы не можете редактировать чужие сообщения");
        
        var timeSinceSent = DateTime.UtcNow - message.Timestamp;
        if (timeSinceSent.TotalMinutes > 60)
            return BadRequest("Нельзя редактировать сообщения старше 1 часа");
        
        message.Text = request.Text;
        message.IsEdited = true;
        
        await _context.SaveChangesAsync();
        
        return Ok(message);
    }
    
    [HttpDelete("{messageId}")]
    public async Task<IActionResult> DeleteMessage(Guid messageId)
    {
        var userId = GetCurrentUserId();
        
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null)
            return NotFound("Сообщение не найдено");
        
        if (message.SenderId != userId)
            return Forbid("Вы не можете удалять чужие сообщения");
        
        _context.Messages.Remove(message);
        await _context.SaveChangesAsync();
        
        return Ok(new { Message = "Сообщение удалено" });
    }
    
    [HttpPost("markasread")]
    public async Task<IActionResult> MarkMessagesAsRead([FromBody] MarkAsReadRequest request)
    {
        var userId = GetCurrentUserId();
        
        var messages = await _context.Messages
            .Where(m => m.ChatId == request.ChatId && 
                       m.SenderId != userId && 
                       !m.IsRead)
            .ToListAsync();
        
        foreach (var message in messages)
        {
            message.IsRead = true;
        }
        
        await _context.SaveChangesAsync();
        
        return Ok(new { Count = messages.Count });
    }
}

public class SendMessageRequest
{
    public Guid ChatId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class EditMessageRequest
{
    public string Text { get; set; } = string.Empty;
}

public class MarkAsReadRequest
{
    public Guid ChatId { get; set; }
}