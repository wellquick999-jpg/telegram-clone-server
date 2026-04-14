using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using CommunityToolkit.Mvvm.Messaging;
using TelegramClone.Client.Messages;
using TelegramClone.Shared.Models;

namespace TelegramClone.Client;

public partial class ChatPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _serverUrl;
    private readonly Chat _chat;
    private List<MessageViewModel> _messages = new();
    private Guid _currentUserId;
    private Timer? _typingTimer;
    private bool _isTypingSent;
    private HubConnection? _hubConnection;
    
    public string StatusText { get; private set; } = "";
    public Color StatusColor { get; private set; } = Colors.Gray;
    
    public ChatPage(Chat chat, string token, string serverUrl)
    {
        InitializeComponent();
        _chat = chat;
        _token = token;
        _serverUrl = serverUrl;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        
        Title = "";
        BindingContext = this;
        
        GetCurrentUserId();
        _ = LoadMessagesAsync();
        _ = LoadUserStatusAsync();
        _ = ConnectToSignalRAsync();
    }
    
    public string ChatName => _chat.Name;
    
    public List<MessageViewModel> Messages
    {
        get => _messages;
        set
        {
            _messages = value;
            OnPropertyChanged(nameof(Messages));
        }
    }
    
    private void GetCurrentUserId()
    {
        try
        {
            var tokenParts = _token.Split('.');
            if (tokenParts.Length == 3)
            {
                var payload = tokenParts[1];
                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }
                var jsonBytes = Convert.FromBase64String(payload);
                var json = Encoding.UTF8.GetString(jsonBytes);
                
                var claims = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (claims != null && claims.ContainsKey("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"))
                {
                    _currentUserId = Guid.Parse(claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"].ToString());
                }
            }
        }
        catch
        {
            _currentUserId = Guid.Empty;
        }
    }
    
    private async Task ConnectToSignalRAsync()
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/chathub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_token);
                })
                .WithAutomaticReconnect()
                .Build();
            
            _hubConnection.On<string>("ReceiveMessage", async (messageId) =>
            {
                await LoadNewMessageAsync(messageId);
            });
            
            _hubConnection.On<string>("MessageEdited", async (messageId) =>
            {
                await UpdateMessageAsync(messageId);
            });
            
            _hubConnection.On<string>("MessageDeleted", async (messageId) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var message = Messages.FirstOrDefault(m => m.Id.ToString() == messageId);
                    if (message != null)
                    {
                        Messages.Remove(message);
                        OnPropertyChanged(nameof(Messages));
                    }
                });
            });
            
            await _hubConnection.StartAsync();
            Console.WriteLine("Connected to SignalR hub");
            
            await _hubConnection.InvokeAsync("JoinChat", _chat.Id.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR connection error: {ex.Message}");
        }
    }
    
    private async Task LoadNewMessageAsync(string messageId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/api/messages/{messageId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var newMessage = JsonSerializer.Deserialize<Message>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (newMessage != null && !Messages.Any(m => m.Id == newMessage.Id))
                {
                    newMessage.Timestamp = newMessage.Timestamp.ToLocalTime();
                    
                    var newViewModel = new MessageViewModel
                    {
                        Id = newMessage.Id,
                        Text = newMessage.Text,
                        Timestamp = newMessage.Timestamp,
                        IsMine = newMessage.SenderId == _currentUserId,
                        IsEdited = newMessage.IsEdited
                    };
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var updatedMessages = new List<MessageViewModel>(Messages);
                        updatedMessages.Add(newViewModel);
                        Messages = updatedMessages;
                        
                        MessagesCollection.ScrollTo(newViewModel, position: ScrollToPosition.End, animate: true);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Load new message error: {ex.Message}");
        }
    }
    
    private async Task UpdateMessageAsync(string messageId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/api/messages/{messageId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var updatedMessage = JsonSerializer.Deserialize<Message>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (updatedMessage != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var index = Messages.FindIndex(m => m.Id.ToString() == messageId);
                        if (index != -1)
                        {
                            Messages[index].Text = updatedMessage.Text;
                            Messages[index].IsEdited = updatedMessage.IsEdited;
                            OnPropertyChanged(nameof(Messages));
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update message error: {ex.Message}");
        }
    }
    
    private async Task LoadMessagesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/api/chats/{_chat.Id}/messages");
            var responseText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var messages = JsonSerializer.Deserialize<List<Message>>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (messages != null)
                {
                    var viewModels = new List<MessageViewModel>();
                    foreach (var msg in messages)
                    {
                        msg.Timestamp = msg.Timestamp.ToLocalTime();
                        viewModels.Add(new MessageViewModel
                        {
                            Id = msg.Id,
                            Text = msg.Text,
                            Timestamp = msg.Timestamp,
                            IsMine = msg.SenderId == _currentUserId,
                            IsEdited = msg.IsEdited
                        });
                    }
                    Messages = viewModels;
                }
                else
                {
                    Messages = new List<MessageViewModel>();
                }
                
                if (Messages.Any())
                {
                    await Task.Delay(100);
                    MessagesCollection.ScrollTo(Messages.Last(), position: ScrollToPosition.End, animate: false);
                }
                
                await MarkMessagesAsReadAsync();
            }
            else
            {
                await DisplayAlert("Ошибка", $"Не удалось загрузить сообщения: {responseText}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось загрузить сообщения: {ex.Message}", "OK");
        }
    }
    
    private async Task LoadUserStatusAsync()
    {
        try
        {
            if (_chat.Type == ChatType.Private)
            {
                var response = await _httpClient.GetAsync($"{_serverUrl}/api/chats/{_chat.Id}/info");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var chatInfo = JsonSerializer.Deserialize<ChatInfoResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (chatInfo != null)
                    {
                        if (chatInfo.IsOnline)
                        {
                            StatusText = "● В сети";
                            StatusColor = Colors.Green;
                        }
                        else if (chatInfo.LastSeen.HasValue)
                        {
                            var timeAgo = GetTimeAgo(chatInfo.LastSeen.Value);
                            StatusText = $"Был(а) {timeAgo}";
                            StatusColor = Colors.Gray;
                        }
                        else
                        {
                            StatusText = "Не в сети";
                            StatusColor = Colors.Gray;
                        }
                        
                        OnPropertyChanged(nameof(StatusText));
                        OnPropertyChanged(nameof(StatusColor));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = "Не в сети";
            StatusColor = Colors.Gray;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
        }
    }
    
    private string GetTimeAgo(DateTime time)
    {
        var localTime = time.ToLocalTime();
        var diff = DateTime.Now - localTime;
        
        if (diff.TotalMinutes < 1)
            return "только что";
        if (diff.TotalMinutes < 60)
            return $"{Math.Floor(diff.TotalMinutes)} мин назад";
        if (diff.TotalHours < 24)
            return $"{Math.Floor(diff.TotalHours)} ч назад";
        if (diff.TotalDays < 7)
            return $"{Math.Floor(diff.TotalDays)} дн назад";
        
        return localTime.ToString("dd.MM.yyyy");
    }
    
    private async Task MarkMessagesAsReadAsync()
    {
        try
        {
            var request = new { ChatId = _chat.Id };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            await _httpClient.PostAsync($"{_serverUrl}/api/messages/markasread", content);
            WeakReferenceMessenger.Default.Send(new RefreshChatsMessage());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error marking messages as read: {ex.Message}");
        }
    }
    
    private async void OnSendMessage(object sender, EventArgs e)
    {
        var text = MessageEntry.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;
        
        MessageEntry.Text = "";
        
        var messageData = new
        {
            ChatId = _chat.Id,
            Text = text
        };
        
        var json = JsonSerializer.Serialize(messageData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await _httpClient.PostAsync($"{_serverUrl}/api/messages", content);
            var responseText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var newMessage = JsonSerializer.Deserialize<Message>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (newMessage != null)
                {
                    newMessage.Timestamp = newMessage.Timestamp.ToLocalTime();
                    
                    var newViewModel = new MessageViewModel
                    {
                        Id = newMessage.Id,
                        Text = newMessage.Text,
                        Timestamp = newMessage.Timestamp,
                        IsMine = true,
                        IsEdited = false
                    };
                    
                    var updatedMessages = new List<MessageViewModel>(Messages);
                    updatedMessages.Add(newViewModel);
                    Messages = updatedMessages;
                    
                    await Task.Delay(100);
                    MessagesCollection.ScrollTo(newViewModel, position: ScrollToPosition.End, animate: true);
                    
                    WeakReferenceMessenger.Default.Send(new RefreshChatsMessage());
                }
            }
            else
            {
                await DisplayAlert("Ошибка", $"Не удалось отправить сообщение\n{response.StatusCode}\n{responseText}", "OK");
                MessageEntry.Text = text;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"{ex.Message}", "OK");
            MessageEntry.Text = text;
        }
    }
    
    private async void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
            return;
        
        if (!_isTypingSent)
        {
            await SendTypingStatusAsync(true);
            _isTypingSent = true;
            
            _typingTimer?.Dispose();
            _typingTimer = new Timer(async _ =>
            {
                await SendTypingStatusAsync(false);
                _isTypingSent = false;
            }, null, 3000, Timeout.Infinite);
        }
    }
    
    private async Task SendTypingStatusAsync(bool isTyping)
    {
        try
        {
            var request = new { ChatId = _chat.Id, IsTyping = isTyping };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            await _httpClient.PostAsync($"{_serverUrl}/api/messages/typing", content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending typing status: {ex.Message}");
        }
    }
    
    private async void OnMenuClicked(object sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Опции", "Отмена", null, 
            "Информация о пользователе", "Очистить чат", "Удалить чат");
        
        switch (action)
        {
            case "Информация о пользователе":
                await DisplayAlert("Информация", $"Пользователь: {_chat.Name}\nТип: {(_chat.Type == ChatType.Private ? "Личный чат" : "Групповой чат")}", "OK");
                break;
            case "Очистить чат":
                var confirm = await DisplayAlert("Очистка", "Вы уверены, что хотите очистить историю сообщений?", "Да", "Нет");
                if (confirm)
                {
                    await DisplayAlert("Очистка", "Функция будет добавлена позже", "OK");
                }
                break;
            case "Удалить чат":
                var confirmDelete = await DisplayAlert("Удаление", "Вы уверены, что хотите удалить этот чат?", "Да", "Нет");
                if (confirmDelete)
                {
                    await DisplayAlert("Удаление", "Функция будет добавлена позже", "OK");
                }
                break;
        }
    }
    
    private async void OnMessageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MessageViewModel selectedMessage)
        {
            ((CollectionView)sender).SelectedItem = null;
            
            if (selectedMessage.IsMine)
            {
                var action = await DisplayActionSheet("Действия", "Отмена", null, 
                    "Редактировать", "Удалить");
                
                switch (action)
                {
                    case "Редактировать":
                        await EditMessage(selectedMessage);
                        break;
                    case "Удалить":
                        await DeleteMessage(selectedMessage);
                        break;
                }
            }
            else
            {
                var action = await DisplayActionSheet("Действия", "Отмена", null, 
                    "Ответить", "Пожаловаться");
                
                switch (action)
                {
                    case "Ответить":
                        await DisplayAlert("Ответ", "Функция будет добавлена позже", "OK");
                        break;
                    case "Пожаловаться":
                        await DisplayAlert("Жалоба", "Функция будет добавлена позже", "OK");
                        break;
                }
            }
        }
    }
    
    private async Task EditMessage(MessageViewModel message)
    {
        var newText = await DisplayPromptAsync("Редактирование", 
            "Введите новый текст", 
            initialValue: message.Text,
            maxLength: 500);
        
        if (string.IsNullOrWhiteSpace(newText))
            return;
        
        try
        {
            var request = new { Text = newText };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync($"{_serverUrl}/api/messages/{message.Id}", content);
            var responseText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var updatedMessage = JsonSerializer.Deserialize<Message>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (updatedMessage != null)
                {
                    for (int i = 0; i < Messages.Count; i++)
                    {
                        if (Messages[i].Id == message.Id)
                        {
                            Messages[i].Text = updatedMessage.Text;
                            Messages[i].IsEdited = true;
                            break;
                        }
                    }
                    
                    var updatedList = new List<MessageViewModel>(Messages);
                    Messages = updatedList;
                }
                
                await DisplayAlert("Успех", "Сообщение отредактировано", "OK");
            }
            else
            {
                await DisplayAlert("Ошибка", $"Не удалось отредактировать сообщение: {responseText}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }
    
    private async Task DeleteMessage(MessageViewModel message)
    {
        var confirm = await DisplayAlert("Удаление", 
            "Вы уверены, что хотите удалить это сообщение?", 
            "Да", "Нет");
        
        if (!confirm) return;
        
        try
        {
            var response = await _httpClient.DeleteAsync($"{_serverUrl}/api/messages/{message.Id}");
            
            if (response.IsSuccessStatusCode)
            {
                Messages.Remove(message);
                OnPropertyChanged(nameof(Messages));
                await DisplayAlert("Успех", "Сообщение удалено", "OK");
            }
            else
            {
                await DisplayAlert("Ошибка", "Не удалось удалить сообщение", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }
    
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.InvokeAsync("LeaveChat", _chat.Id.ToString());
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting from SignalR: {ex.Message}");
            }
        }
    }
}

public class MessageViewModel
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsMine { get; set; }
    public bool IsEdited { get; set; }
    
    public LayoutOptions HorizontalAlignment => IsMine ? LayoutOptions.End : LayoutOptions.Start;
    public Color BackgroundColor => IsMine ? Color.FromArgb("#2A6B9F") : Color.FromArgb("#E4E6EB");
    public Color TextColor => IsMine ? Colors.White : Colors.Black;
    public Color TimeColor => IsMine ? Colors.LightGray : (Application.Current.UserAppTheme == AppTheme.Dark ? Colors.LightGray : Colors.Gray);
}

public class ChatInfoResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChatType Type { get; set; }
    public List<Guid> ParticipantIds { get; set; } = new();
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    public Guid UserId { get; set; }
}