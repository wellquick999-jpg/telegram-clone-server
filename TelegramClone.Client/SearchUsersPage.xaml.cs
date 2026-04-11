using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using TelegramClone.Client.Messages;
using TelegramClone.Shared.Models;

namespace TelegramClone.Client;

public partial class SearchUsersPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _serverUrl;
    
    public SearchUsersPage(string token, string serverUrl)
    {
        InitializeComponent();
        _token = token;
        _serverUrl = serverUrl;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
    }
    
    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        var query = SearchEntry.Text;
        if (string.IsNullOrWhiteSpace(query))
            return;
        
        StatusLabel.Text = "Поиск...";
        StatusLabel.TextColor = Colors.Blue;
        
        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/api/users/search?query={Uri.EscapeDataString(query)}");
            var responseText = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Search response: {responseText}");
            
            if (response.IsSuccessStatusCode)
            {
                var users = JsonSerializer.Deserialize<List<User>>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (users != null && users.Any())
                {
                    UsersCollection.ItemsSource = users;
                    StatusLabel.Text = $"Найдено {users.Count} пользователей";
                    StatusLabel.TextColor = Colors.Green;
                }
                else
                {
                    UsersCollection.ItemsSource = null;
                    StatusLabel.Text = "Пользователи не найдены";
                    StatusLabel.TextColor = Colors.Red;
                }
            }
            else
            {
                StatusLabel.Text = $"Ошибка: {response.StatusCode}";
                StatusLabel.TextColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Ошибка: {ex.Message}";
            StatusLabel.TextColor = Colors.Red;
        }
    }
    
    private async void OnUserSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is User selectedUser)
        {
            UsersCollection.SelectedItem = null;
            
            var json = JsonSerializer.Serialize(selectedUser.Id);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"Creating chat with user: {selectedUser.Id} - {selectedUser.Username}");
                
                var response = await _httpClient.PostAsync($"{_serverUrl}/api/chats/private", content);
                var responseText = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"Create chat response status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Create chat response: {responseText}");
                
                if (response.IsSuccessStatusCode)
                {
                    // Десериализуем ответ в Chat объект
                    var newChat = JsonSerializer.Deserialize<Chat>(responseText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (newChat != null)
                    {
                        await DisplayAlert("Успех", $"Чат с {selectedUser.Username} создан!", "OK");
                        
                        // Отправляем уведомление об обновлении чатов
                        WeakReferenceMessenger.Default.Send(new RefreshChatsMessage());
                        
                        // Возвращаемся к списку чатов
                        await Navigation.PopAsync();
                    }
                    else
                    {
                        await DisplayAlert("Ошибка", "Не удалось создать чат", "OK");
                    }
                }
                else
                {
                    await DisplayAlert("Ошибка", $"Не удалось создать чат: {responseText}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", ex.Message, "OK");
            }
        }
    }
}