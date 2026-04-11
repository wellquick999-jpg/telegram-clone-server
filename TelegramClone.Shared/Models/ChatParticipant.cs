namespace TelegramClone.Shared.Models;

public class ChatParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChatId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    
    // Навигационные свойства
    public Chat? Chat { get; set; }
    public User? User { get; set; }
}