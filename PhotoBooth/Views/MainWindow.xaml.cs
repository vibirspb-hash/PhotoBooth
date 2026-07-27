using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PhotoBooth.Models;
using PhotoBooth.Services;

namespace PhotoBooth.Views;

public partial class MainWindow : Window
{
    private const int InitialCountdownValue = 3;

    private readonly DispatcherTimer _countdownTimer;
    private readonly DispatcherTimer _completionTimer;
    private readonly AppConfig _config;
    private readonly TemplateDefinitionService _templateDefinitionService;
    private readonly TemplateManager _templateManager;
    private int _countdownValue = InitialCountdownValue;

    public MainWindow()
    {
        InitializeComponent();

        _config = new ConfigService().Load();
        _templateDefinitionService = new TemplateDefinitionService();
        _templateManager = new TemplateManager();

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _countdownTimer.Tick += CountdownTimer_Tick;

        _completionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };

        _completionTimer.Tick += CompletionTimer_Tick;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        string templatesPath = ResolveAppPath(_config.TemplatesPath);
        IReadOnlyList<TemplateInfo> templates = _templateManager.GetTemplates(templatesPath);

        TemplatesList.ItemsSource = templates;
        TemplatesList.Visibility = templates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoTemplatesText.Visibility = templates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        HomePanel.Visibility = Visibility.Collapsed;
        TemplatesPanel.Visibility = Visibility.Visible;
    }

    private void TemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TemplateInfo template })
        {
            return;
        }

        ShowTemplateDetails(template);
    }

    private void BackToHomeButton_Click(object sender, RoutedEventArgs e)
    {
        ShowHomeScreen();
    }

    private void BackToTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        TemplateDetailsPanel.Visibility = Visibility.Collapsed;
        TemplatesPanel.Visibility = Visibility.Visible;
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        StartCountdown();
    }

    private void ShowTemplateDetails(TemplateInfo template)
    {
        SelectedTemplateNameText.Text = template.Name;

        if (string.IsNullOrWhiteSpace(template.JsonPath))
        {
            TemplateWidthText.Text = "Ширина: не указана";
            TemplateHeightText.Text = "Высота: не указана";
            TemplatePhotosText.Text = "Количество кадров: 0";
        }
        else
        {
            TemplateDefinition definition = _templateDefinitionService.Load(template.JsonPath);

            TemplateWidthText.Text = $"Ширина: {definition.Width}";
            TemplateHeightText.Text = $"Высота: {definition.Height}";
            TemplatePhotosText.Text = $"Количество кадров: {definition.Photos.Count}";
        }

        TemplatesPanel.Visibility = Visibility.Collapsed;
        TemplateDetailsPanel.Visibility = Visibility.Visible;
    }

    private void StartCountdown()
    {
        _completionTimer.Stop();
        _countdownValue = InitialCountdownValue;
        CountdownText.Text = _countdownValue.ToString();
        CountdownText.FontSize = 180;
        CountdownCaption.Text = "Приготовьтесь";

        TemplateDetailsPanel.Visibility = Visibility.Collapsed;
        CountdownPanel.Visibility = Visibility.Visible;

        _countdownTimer.Start();
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _countdownValue--;

        if (_countdownValue > 0)
        {
            CountdownText.Text = _countdownValue.ToString();
            return;
        }

        _countdownTimer.Stop();
        CountdownText.Text = "ГОТОВО";
        CountdownText.FontSize = 96;
        CountdownCaption.Text = "Снимок сделан";

        _completionTimer.Start();
    }

    private void CompletionTimer_Tick(object? sender, EventArgs e)
    {
        _completionTimer.Stop();
        ShowHomeScreen();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_config.DemoMode && e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void ShowHomeScreen()
    {
        _countdownTimer.Stop();
        _completionTimer.Stop();

        TemplatesPanel.Visibility = Visibility.Collapsed;
        TemplateDetailsPanel.Visibility = Visibility.Collapsed;
        CountdownPanel.Visibility = Visibility.Collapsed;
        HomePanel.Visibility = Visibility.Visible;
    }

    private static string ResolveAppPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(AppContext.BaseDirectory, path);
    }
}
