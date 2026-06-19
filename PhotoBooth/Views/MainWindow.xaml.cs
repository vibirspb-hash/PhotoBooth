using System;
using System.Windows;
using System.Windows.Threading;

namespace PhotoBooth.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _countdownTimer;
    private int _countdownValue = 3;

    public MainWindow()
    {
        InitializeComponent();

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _countdownTimer.Tick += CountdownTimer_Tick;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        _countdownValue = 3;
        CountdownText.Text = _countdownValue.ToString();
        CountdownText.FontSize = 180;
        CountdownCaption.Text = "Приготовьтесь";

        HomePanel.Visibility = Visibility.Collapsed;
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
