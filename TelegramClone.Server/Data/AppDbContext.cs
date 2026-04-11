using Microsoft.EntityFrameworkCore;
using TelegramClone.Shared.Models;

namespace TelegramClone.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public DbSet<User> Users { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<ChatParticipant> ChatParticipants { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();
            
        modelBuilder.Entity<User>()
            .HasIndex(u => u.PhoneNumber)
            .IsUnique();
        
        // Message configuration - НЕТ UNIQUE!
        modelBuilder.Entity<Message>()
            .HasKey(m => m.Id);
        
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.ChatId);  // Только индекс
        
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.SenderId);
        
        modelBuilder.Entity<Chat>()
            .HasKey(c => c.Id);
        
        modelBuilder.Entity<Chat>()
            .HasIndex(c => c.UpdatedAt);
        
        modelBuilder.Entity<ChatParticipant>()
            .HasKey(cp => cp.Id);
        
        modelBuilder.Entity<ChatParticipant>()
            .HasIndex(cp => new { cp.ChatId, cp.UserId })
            .IsUnique();
        
        modelBuilder.Entity<ChatParticipant>()
            .HasOne(cp => cp.Chat)
            .WithMany(c => c.Participants)
            .HasForeignKey(cp => cp.ChatId);
        
        modelBuilder.Entity<ChatParticipant>()
            .HasOne(cp => cp.User)
            .WithMany()
            .HasForeignKey(cp => cp.UserId);
    }
}