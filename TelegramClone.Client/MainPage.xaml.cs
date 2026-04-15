using System.Text;
using System.Text.Json;
using TelegramClone.Shared.Models;

namespace TelegramClone.Client;

public partial class MainPage : ContentPage
{
    private const string ServerUrl = "https://telegram-clone-server-2.onrender.com";
    private const string ThemePreferenceKey = "app_theme";
    private bool _isDarkTheme;
    
    public MainPage()
    {
        InitializeComponent();
        
        // Загружаем сохранённую тему
        _isDarkTheme = Preferences.Get(ThemePreferenceKey, "light") == "dark";
        UpdateThemeButton();
        ApplyTheme(_isDarkTheme);
    }
    
    private void UpdateThemeButton()
    {
        ThemeToggleButton.Text = _isDarkTheme ? "☀️" : "🌙";
    }
    
    private void ApplyTheme(bool isDark)
    {
        if (isDark)
        {
            Application.Current.UserAppTheme = AppTheme.Dark;
        }
        else
        {
            Application.Current.UserAppTheme = AppTheme.Light;
        }
    }
    
    private async void OnThemeToggleClicked(object sender, EventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        Preferences.Set(ThemePreferenceKey, _isDarkTheme ? "dark" : "light");
        UpdateThemeButton();
        ApplyTheme(_isDarkTheme);
        
        await ShowToast(_isDarkTheme ? "🌙 Темная тема включена" : "☀️ Светлая тема включена", Colors.Gray);
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
    
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var phoneNumber = PhoneNumberEntry.Text;
        var password = PasswordEntry.Text;
        
        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(password))
        {
            await ShowToast("Введите номер телефона и пароль", Colors.Red);
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
                    await ShowToast("Ошибка: токен не получен", Colors.Red);
                    return;
                }
                
                await ShowToast($"Добро пожаловать, {authResponse.User?.Username}!", Colors.Green);
                
                var chatsPage = new ChatsPage(authResponse.Token, ServerUrl);
                await Navigation.PushAsync(chatsPage);
            }
            else
            {
                if (responseText.Contains("Неверный") || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await ShowToast("Неверный номер телефона или пароль", Colors.Red);
                }
                else
                {
                    await ShowToast($"Ошибка: {responseText}", Colors.Red);
                }
            }
        }
        catch (Exception ex)
        {
            await ShowToast($"Ошибка подключения: {ex.Message}", Colors.Red);
        }
        
        StatusLabel.Text = "";
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
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await httpClient.PostAsync($"{ServerUrl}/api/auth/register", content);
            var responseText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                await ShowToast("✅ Вы успешно зарегистрированы!", Colors.Green);
            }
            else
            {
                if (responseText.Contains("уже существует"))
                {
                    await ShowToast("❌ Аккаунт с таким номером телефона уже зарегистрирован", Colors.Orange);
                }
                else
                {
                    await ShowToast($"❌ Ошибка: {responseText}", Colors.Red);
                }
            }
        }
        catch (Exception ex)
        {
            await ShowToast($"❌ Ошибка: {ex.Message}", Colors.Red);
        }
    }
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public User User { get; set; } = null!;
}