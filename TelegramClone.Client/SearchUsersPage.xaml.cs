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
    private bool _isSearching;
    
    public SearchUsersPage(string token, string serverUrl)
    {
        InitializeComponent();
        _token = token;
        _serverUrl = serverUrl;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }
    
    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        if (_isSearching) return;
        
        var query = SearchEntry.Text;
        if (string.IsNullOrWhiteSpace(query))
            return;
        
        _isSearching = true;
        StatusLabel.Text = "Поиск...";
        StatusLabel.TextColor = Colors.Blue;
        
        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/api/users/search?query={Uri.EscapeDataString(query)}");
            var responseText = await response.Content.ReadAsStringAsync();
            
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
            else if (response.StatusCode == System.Net.HttpStatusCode.BadGateway || 
                     response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                StatusLabel.Text = "Сервер временно недоступен, попробуйте позже";
                StatusLabel.TextColor = Colors.Orange;
                await ShowToast("⚠️ Сервер перегружен, попробуйте позже", Colors.Orange);
            }
            else
            {
                StatusLabel.Text = $"Ошибка: {response.StatusCode}";
                StatusLabel.TextColor = Colors.Red;
            }
        }
        catch (TaskCanceledException)
        {
            StatusLabel.Text = "Превышено время ожидания, попробуйте позже";
            StatusLabel.TextColor = Colors.Orange;
            await ShowToast("⏱️ Сервер не отвечает, попробуйте позже", Colors.Orange);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Ошибка: {ex.Message}";
            StatusLabel.TextColor = Colors.Red;
        }
        finally
        {
            _isSearching = false;
        }
    }
    
    private async void OnUserSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is User selectedUser)
        {
            UsersCollection.SelectedItem = null;
            
            var json = JsonSerializer.Serialize(selectedUser.Id);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            StatusLabel.Text = "Создание чата...";
            StatusLabel.TextColor = Colors.Blue;
            
            try
            {
                var response = await _httpClient.PostAsync($"{_serverUrl}/api/chats/private", content);
                var responseText = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var newChat = JsonSerializer.Deserialize<Chat>(responseText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (newChat != null)
                    {
                        await ShowToast($"✅ Чат с {selectedUser.Username} создан!", Colors.Green);
                        WeakReferenceMessenger.Default.Send(new RefreshChatsMessage());
                        await Navigation.PopAsync();
                    }
                    else
                    {
                        await ShowToast("❌ Не удалось создать чат", Colors.Red);
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadGateway)
                {
                    await ShowToast("⚠️ Сервер временно недоступен, попробуйте позже", Colors.Orange);
                }
                else
                {
                    await ShowToast($"❌ Ошибка: {response.StatusCode}", Colors.Red);
                }
            }
            catch (TaskCanceledException)
            {
                await ShowToast("⏱️ Превышено время ожидания", Colors.Orange);
            }
            catch (Exception ex)
            {
                await ShowToast($"❌ Ошибка: {ex.Message}", Colors.Red);
            }
            finally
            {
                StatusLabel.Text = "";
            }
        }
    }
    
    private async Task ShowToast(string message, Color backgroundColor)
    {
        var toast = new Label
        {
            Text = message,
            BackgroundColor = backgroundColor,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Padding = new Thickness(20, 10),
            Margin = new Thickness(20, 0, 20, 30),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Opacity = 0
        };
        
        var grid = this.Content as Grid;
        if (grid == null)
        {
            grid = new Grid();
            if (this.Content is Layout layout)
            {
                var children = layout.Children.ToList();
                layout.Children.Clear();
                grid.Children.Add(layout);
                foreach (var child in children)
                {
                    layout.Children.Add(child);
                }
            }
            this.Content = grid;
        }
        
        grid.Children.Add(toast);
        Grid.SetRow(toast, grid.RowDefinitions.Count - 1);
        
        await toast.FadeTo(1, 300);
        await Task.Delay(2000);
        await toast.FadeTo(0, 300);
        
        grid.Children.Remove(toast);
    }
}