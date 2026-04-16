using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using TelegramClone.Client.Messages;
using TelegramClone.Shared.Models;

namespace TelegramClone.Client;

public partial class EditProfilePage : ContentPage
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _serverUrl;
    private readonly User _currentUser;
    
    public EditProfilePage(User currentUser, string token, string serverUrl)
    {
        InitializeComponent();
        _currentUser = currentUser;
        _token = token;
        _serverUrl = serverUrl;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        
        // Загружаем данные пользователя
        UsernameEntry.Text = currentUser.Username;
        PhoneEntry.Text = currentUser.PhoneNumber;
        BioEntry.Text = currentUser.Bio ?? "";
        AvatarLabel.Text = currentUser.Username[0].ToString().ToUpper();
    }
    
    private async void OnAvatarClicked(object sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Выберите действие", "Отмена", null, 
            "Выбрать из галереи", "Сделать фото");
        
        switch (action)
        {
            case "Выбрать из галереи":
                await SelectPhoto();
                break;
            case "Сделать фото":
                await TakePhoto();
                break;
        }
    }
    
    private async Task SelectPhoto()
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Выберите фото"
            });
            
            if (result != null)
            {
                await ShowToast("Функция будет добавлена позже", Colors.Orange);
            }
        }
        catch (Exception ex)
        {
            await ShowToast($"Ошибка: {ex.Message}", Colors.Red);
        }
    }
    
    private async Task TakePhoto()
    {
        try
        {
            var result = await MediaPicker.CapturePhotoAsync();
            
            if (result != null)
            {
                await ShowToast("Функция будет добавлена позже", Colors.Orange);
            }
        }
        catch (Exception ex)
        {
            await ShowToast($"Ошибка: {ex.Message}", Colors.Red);
        }
    }
    
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var newUsername = UsernameEntry.Text?.Trim();
        var newBio = BioEntry.Text?.Trim();
        
        // Отладка
        System.Diagnostics.Debug.WriteLine($"Saving - Username: {newUsername}, Bio: {newBio}");
        
        if (string.IsNullOrWhiteSpace(newUsername))
        {
            await ShowToast("Имя пользователя не может быть пустым", Colors.Red);
            return;
        }
        
        var updateData = new
        {
            Username = newUsername,
            Bio = newBio ?? ""
        };
        
        var json = JsonSerializer.Serialize(updateData);
        System.Diagnostics.Debug.WriteLine($"Sending JSON: {json}");
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await _httpClient.PutAsync($"{_serverUrl}/api/users/profile", content);
            var responseText = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Response: {response.StatusCode} - {responseText}");
            
            if (response.IsSuccessStatusCode)
            {
                await ShowToast("✅ Профиль обновлен!", Colors.Green);
                
                WeakReferenceMessenger.Default.Send(new RefreshChatsMessage());
                await Navigation.PopAsync();
            }
            else
            {
                await ShowToast($"❌ Ошибка: {responseText}", Colors.Red);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            await ShowToast($"❌ Ошибка: {ex.Message}", Colors.Red);
        }
    }
    
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
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