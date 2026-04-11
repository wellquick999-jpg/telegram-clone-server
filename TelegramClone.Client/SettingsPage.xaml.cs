using System.Text;
using System.Text.Json;
using TelegramClone.Shared.Models;

namespace TelegramClone.Client;

public partial class SettingsPage : ContentPage
{
    private const string ThemePreferenceKey = "app_theme";
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _serverUrl;
    private bool _isDarkTheme;
    
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            _isDarkTheme = value;
            OnPropertyChanged(nameof(IsDarkTheme));
        }
    }
    
    public SettingsPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        _token = "";
        _serverUrl = "";
        BindingContext = this;
        
        // Загружаем сохраненную тему
        var savedTheme = Preferences.Get(ThemePreferenceKey, "light");
        IsDarkTheme = savedTheme == "dark";
        ThemeSwitch.IsToggled = IsDarkTheme;
    }
    
    public SettingsPage(string token, string serverUrl) : this()
    {
        _token = token;
        _serverUrl = serverUrl;
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        
        LoadUserData();
    }
    
    private async void LoadUserData()
    {
        try
        {
            UsernameLabel.Text = "Загрузка...";
            
            var response = await _httpClient.GetAsync($"{_serverUrl}/api/users/me");
            var responseText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var user = JsonSerializer.Deserialize<User>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (user != null)
                {
                    UsernameLabel.Text = user.Username;
                    PhoneLabel.Text = user.PhoneNumber;
                    AvatarLabel.Text = user.Username[0].ToString().ToUpper();
                }
                else
                {
                    UsernameLabel.Text = "Пользователь";
                    AvatarLabel.Text = "👤";
                }
            }
            else
            {
                UsernameLabel.Text = "Пользователь";
                PhoneLabel.Text = "+7 XXX XXX-XX-XX";
                AvatarLabel.Text = "👤";
            }
        }
        catch (Exception ex)
        {
            UsernameLabel.Text = "Пользователь";
            PhoneLabel.Text = "+7 XXX XXX-XX-XX";
            AvatarLabel.Text = "👤";
        }
    }
    
    private async void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        IsDarkTheme = e.Value;
        
        if (IsDarkTheme)
        {
            Preferences.Set(ThemePreferenceKey, "dark");
            Application.Current.UserAppTheme = AppTheme.Dark;
        }
        else
        {
            Preferences.Set(ThemePreferenceKey, "light");
            Application.Current.UserAppTheme = AppTheme.Light;
        }
    }
    
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Выход", "Вы уверены, что хотите выйти?", "Да", "Нет");
        if (confirm)
        {
            await Navigation.PopToRootAsync();
        }
    }
    
    private async void OnAccountClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Аккаунт", "Редактирование профиля будет доступно в следующей версии", "OK");
    }
    
    private async void OnChatSettingsClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Настройки чатов", "Обои, режимы, анимации будут добавлены позже", "OK");
    }
    
    private async void OnPrivacyClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Конфиденциальность", "Настройки приватности будут добавлены позже", "OK");
    }
    
    private async void OnNotificationsClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Уведомления", "Настройки уведомлений будут добавлены позже", "OK");
    }
    
    private async void OnAboutClicked(object sender, EventArgs e)
    {
        await DisplayAlert("О приложении", 
            "Telegram Clone\nВерсия 1.0\n\nРазработчик: Telegram Clone Team\n\nМессенджер с открытым исходным кодом", 
            "OK");
    }
}