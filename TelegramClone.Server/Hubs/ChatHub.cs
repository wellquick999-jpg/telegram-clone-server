using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace TelegramClone.Server.Hubs;

public class ChatHub : Hub
{
    public async Task JoinChat(string chatId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
        Console.WriteLine($"User {Context.UserIdentifier} joined chat {chatId}");
    }
    
    public async Task LeaveChat(string chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
        Console.WriteLine($"User {Context.UserIdentifier} left chat {chatId}");
    }
    
    public async Task SendMessage(string chatId, string messageId)
    {
        await Clients.Group(chatId).SendAsync("ReceiveMessage", messageId);
        Console.WriteLine($"Message {messageId} sent to chat {chatId}");
    }
    
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Console.WriteLine($"User {userId} connected");
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Console.WriteLine($"User {userId} disconnected");
        await base.OnDisconnectedAsync(exception);
    }
}