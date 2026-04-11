using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using TelegramClone.Client.Messages;
using TelegramClone.Shared.Models;

namespace TelegramClone.Client;

public partial class ChatsPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _serverUrl;
    private List<ChatDisplayModel> _chats = new();
    
    public ChatsPage(string token, string serverUrl)
    {
        InitializeComponent();
        _token = token;
        _serverUrl = serverUrl;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        
        WeakReferenceMessenger.Default.Register<RefreshChatsMessage>(this, async (recipient, message) =>
        {
            await RefreshChats();
        });
        
        LoadChats();
    }
    
    private async void LoadChats()
    {
        await RefreshChats();
    }
    
    private async Task RefreshChats()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/api/chats");
            var responseText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var chats = JsonSerializer.Deserialize<List<ChatFromServer>>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ChatFromServer>();
                
                _chats = chats.Select(c => new ChatDisplayModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Type = c.Type,
                    LastMessageText = c.LastMessage?.Text ?? "Нет сообщений",
                    LastMessageTime = c.LastMessage != null ? 
                        DateTime.Parse(c.LastMessage.Timestamp.ToString()).ToLocalTime().ToString("HH:mm") : "",
                    UnreadCount = c.UnreadCount,
                    IsOnline = c.IsOnline,
                    LastSeen = c.LastSeen
                }).ToList();
                
                ChatsCollection.ItemsSource = null;
                ChatsCollection.ItemsSource = _chats;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось загрузить чаты: {ex.Message}", "OK");
        }
    }
    
    private async void OnNewChatClicked(object sender, EventArgs e)
    {
        var searchPage = new SearchUsersPage(_token, _serverUrl);
        await Navigation.PushAsync(searchPage);
    }
    
    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        var settingsPage = new SettingsPage();
        await Navigation.PushAsync(settingsPage);
    }
    
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Выход", "Вы уверены, что хотите выйти?", "Да", "Нет");
        if (confirm)
        {
            await Navigation.PopToRootAsync();
        }
    }
    
    private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ChatDisplayModel selectedChat)
        {
            ChatsCollection.SelectedItem = null;
            
            var chat = new Chat
            {
                Id = selectedChat.Id,
                Name = selectedChat.Name,
                Type = selectedChat.Type
            };
            
            var chatPage = new ChatPage(chat, _token, _serverUrl);
            await Navigation.PushAsync(chatPage);
        }
    }
}

public class ChatFromServer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChatType Type { get; set; }
    public List<Guid> ParticipantIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Message? LastMessage { get; set; }
    public int UnreadCount { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
}

public class ChatDisplayModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChatType Type { get; set; }
    public string LastMessageText { get; set; } = "Нет сообщений";
    public string LastMessageTime { get; set; } = "";
    public int UnreadCount { get; set; }
    public bool HasUnreadMessages => UnreadCount > 0;
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    
    public string StatusText => IsOnline ? "● В сети" : (LastSeen.HasValue ? $"Был(а) {GetTimeAgo(LastSeen.Value)}" : "Не в сети");
    public Color StatusColor => IsOnline ? Colors.Green : Colors.Gray;
    public Color OnlineColor => IsOnline ? Colors.Green : Colors.Gray;
    
    private static string GetTimeAgo(DateTime time)
    {
        var localTime = time.ToLocalTime();
        var diff = DateTime.Now - localTime;
        
        if (diff.TotalMinutes < 1) return "только что";
        if (diff.TotalMinutes < 60) return $"{Math.Floor(diff.TotalMinutes)} мин назад";
        if (diff.TotalHours < 24) return $"{Math.Floor(diff.TotalHours)} ч назад";
        if (diff.TotalDays < 7) return $"{Math.Floor(diff.TotalDays)} дн назад";
        return localTime.ToString("dd.MM.yyyy");
    }
}