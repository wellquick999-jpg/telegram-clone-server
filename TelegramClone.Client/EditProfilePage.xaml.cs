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
    private User _currentUser;
    
    public EditProfilePage(User currentUser, string token, string serverUrl)
    {
        InitializeComponent();
        _currentUser = currentUser;
        _token = token;
        _serverUrl = serverUrl;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        
        LoadUserData(currentUser);
    }
    
    private void LoadUserData(User user)
    {
        // Разделяем имя на имя и фамилию
        var fullName = user.Username;
        var spaceIndex = fullName.IndexOf(' ');
        
        if (spaceIndex > 0)
        {
            var firstName = fullName.Substring(0, spaceIndex);
            var lastName = fullName.Substring(spaceIndex + 1);
            
            DisplayFirstNameLabel.Text = firstName;
            DisplayLastNameLabel.Text = lastName;
            FirstNameEntry.Text = firstName;
            LastNameEntry.Text = lastName;
        }
        else
        {
            DisplayFirstNameLabel.Text = fullName;
            DisplayLastNameLabel.Text = "";
            FirstNameEntry.Text = fullName;
            LastNameEntry.Text = "";
        }
        
        UserTagEntry.Text = user.UserTag;
        PhoneEntry.Text = user.PhoneNumber;
        BioEntry.Text = user.Bio ?? "";
        AvatarLabel.Text = fullName[0].ToString().ToUpper();
    }
    
    private async void OnAvatarClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Аватар", "Функция будет добавлена позже", "OK");
    }
    
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var firstName = FirstNameEntry.Text?.Trim();
        var lastName = LastNameEntry.Text?.Trim();
        var userTag = UserTagEntry.Text?.Trim();
        var newBio = BioEntry.Text?.Trim();
        
        // Убираем @ если ввели
        if (userTag != null && userTag.StartsWith("@"))
        {
            userTag = userTag.Substring(1);
        }
        
        // Собираем полное имя
        var newDisplayName = firstName;
        if (!string.IsNullOrWhiteSpace(lastName))
        {
            newDisplayName = $"{firstName} {lastName}";
        }
        
        // Проверки
        if (string.IsNullOrWhiteSpace(newDisplayName))
        {
            await DisplayAlert("Ошибка", "Имя не может быть пустым", "OK");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(userTag))
        {
            await DisplayAlert("Ошибка", "Имя пользователя не может быть пустым", "OK");
            return;
        }
        
        if (!System.Text.RegularExpressions.Regex.IsMatch(userTag, @"^[a-zA-Z0-9_]+$"))
        {
            await DisplayAlert("Ошибка", "Имя пользователя может содержать только буквы, цифры и символы подчёркивания", "OK");
            return;
        }
        
        var updateData = new { DisplayName = newDisplayName, UserTag = userTag, Bio = newBio ?? "" };
        var json = JsonSerializer.Serialize(updateData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await _httpClient.PutAsync($"{_serverUrl}/api/users/profile", content);
            var responseText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var updatedUser = JsonSerializer.Deserialize<User>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (updatedUser != null)
                {
                    // Обновляем все поля на странице
                    LoadUserData(updatedUser);
                    _currentUser = updatedUser;
                }
                
                await DisplayAlert("Успех", "Профиль обновлен!", "OK");
                WeakReferenceMessenger.Default.Send(new RefreshChatsMessage());
            }
            else
            {
                await DisplayAlert("Ошибка", $"Не удалось обновить профиль: {responseText}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }
    
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}