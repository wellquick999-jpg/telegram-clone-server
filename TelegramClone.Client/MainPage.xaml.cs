using System.Text;
using System.Text.Json;
using TelegramClone.Shared.Models;

namespace TelegramClone.Client;

public partial class MainPage : ContentPage
{
    private const string ServerUrl = "http://192.168.1.48:5276";
    
    public MainPage()
    {
        InitializeComponent();
    }
    
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var phoneNumber = PhoneNumberEntry.Text;
        var password = PasswordEntry.Text;
        
        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(password))
        {
            StatusLabel.Text = "Введите номер телефона и пароль";
            return;
        }
        
        StatusLabel.Text = "Вход...";
        
        var loginData = new
        {
            PhoneNumber = phoneNumber,
            Password = password
        };
        
        var json = JsonSerializer.Serialize(loginData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            // Создаем новый HttpClient для каждого запроса
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await httpClient.PostAsync($"{ServerUrl}/api/auth/login", content);
            var responseText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (authResponse?.Token == null)
                {
                    StatusLabel.Text = "Ошибка: токен не получен";
                    return;
                }
                
                StatusLabel.Text = $"Добро пожаловать, {authResponse.User?.Username}!";
                StatusLabel.TextColor = Colors.Green;
                
                var chatsPage = new ChatsPage(authResponse.Token, ServerUrl);
                await Navigation.PushAsync(chatsPage);
            }
            else
            {
                StatusLabel.Text = $"Ошибка: {responseText}";
                StatusLabel.TextColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Ошибка: {ex.Message}\nПроверьте: {ServerUrl}";
            StatusLabel.TextColor = Colors.Red;
        }
    }
    
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var username = await DisplayPromptAsync("Регистрация", "Введите имя пользователя:");
        if (string.IsNullOrWhiteSpace(username)) return;
        
        var phoneNumber = await DisplayPromptAsync("Регистрация", "Введите номер телефона:");
        if (string.IsNullOrWhiteSpace(phoneNumber)) return;
        
        var password = await DisplayPromptAsync("Регистрация", "Введите пароль:");
        if (string.IsNullOrWhiteSpace(password)) return;
        
        var registerData = new
        {
            Username = username,
            PhoneNumber = phoneNumber,
            Password = password
        };
        
        var json = JsonSerializer.Serialize(registerData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            // Создаем новый HttpClient для каждого запроса
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await httpClient.PostAsync($"{ServerUrl}/api/auth/register", content);
            
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Успех", "Регистрация прошла успешно! Теперь войдите.", "OK");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Ошибка", error, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public User User { get; set; } = null!;
}