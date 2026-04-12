using System.Text.Json.Serialization;

namespace TelegramClone.Shared.Models;

public class Chat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ChatType Type { get; set; }
    public string? AvatarUrl { get; set; }
    public List<Guid> ParticipantIds { get; set; } = new(); // Сохраняем для совместимости
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Навигационное свойство для участников (не используется в JSON)
    [JsonIgnore]
    public List<ChatParticipant> Participants { get; set; } = new();
    
    // Последнее сообщение (не сохраняется в БД)
    [JsonIgnore]
    public Message? LastMessage { get; set; }
}

public enum ChatType
{
    Private,
    Group
}