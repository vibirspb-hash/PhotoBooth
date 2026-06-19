using System;
using System.Windows;
using System.Windows.Threading;
using PhotoBooth.Models;
using PhotoBooth.Services;

namespace PhotoBooth.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _countdownTimer;
    private readonly AppConfig _config;
    private readonly TemplateManager _templateManager;
    private int _countdownValue = 3;

    public MainWindow()
    {
        InitializeComponent();

        _config = new ConfigService().Load();
        _templateManager = new TemplateManager();

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _countdownTimer.Tick += CountdownTimer_Tick;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<TemplateInfo> templates = _templateManager.GetTemplates(_config.TemplatesPath);

        TemplatesList.ItemsSource = templates;
        TemplatesList.Visibility = templates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoTemplatesText.Visibility = templates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        HomePanel.Visibility = Visibility.Collapsed;
        TemplatesPanel.Visibility = Visibility.Visible;
    }

    private void TemplateButton_Click(object sender, RoutedEventArgs e)
    {
        StartCountdown();
    }

    private void BackToHomeButton_Click(object sender, RoutedEventArgs e)
    {
        TemplatesPanel.Visibility = Visibility.Collapsed;
        HomePanel.Visibility = Visibility.Visible;
    }

    private void StartCountdown()
    {
        _countdownValue = 3;
        CountdownText.Text = _countdownValue.ToString();
        CountdownText.FontSize = 180;
        CountdownCaption.Text = "Приготовьтесь";

        TemplatesPanel.Visibility = Visibility.Collapsed;
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
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
