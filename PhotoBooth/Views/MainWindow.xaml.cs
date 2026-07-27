using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PhotoBooth.Models;
using PhotoBooth.Services;

namespace PhotoBooth.Views;

public partial class MainWindow : Window
{
    private const int InitialCountdownValue = 3;

    private readonly DispatcherTimer _countdownTimer;
    private readonly DispatcherTimer _completionTimer;
    private readonly DispatcherTimer _printCompletionTimer;
    private readonly AppConfig _config;
    private readonly DemoPhotoService _demoPhotoService;
    private readonly ImageComposer _imageComposer;
    private readonly TemplateDefinitionService _templateDefinitionService;
    private readonly TemplateManager _templateManager;
    private int _countdownValue = InitialCountdownValue;
    private int _copyCount = 1;
    private int _currentShotNumber;
    private IReadOnlyList<string> _preparedShots = [];
    private TemplateDefinition? _selectedDefinition;
    private TemplateInfo? _selectedTemplate;

    public MainWindow()
    {
        InitializeComponent();

        _config = new ConfigService().Load();
        _demoPhotoService = new DemoPhotoService();
        _imageComposer = new ImageComposer();
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

        _printCompletionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };

        _printCompletionTimer.Tick += PrintCompletionTimer_Tick;
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
        TemplateStatusText.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(template.JsonPath))
        {
            TemplateWidthText.Text = "Ширина: не указана";
            TemplateHeightText.Text = "Высота: не указана";
            TemplatePhotosText.Text = "Количество кадров: 0";
            TemplateOverlayText.Text = "Overlay: не указан";
            TemplateStatusText.Text = "Ошибка: JSON-файл шаблона не найден.";
            TemplateStatusText.Foreground = Brushes.LightSalmon;
            ContinueButton.IsEnabled = false;
        }
        else
        {
            TemplateDefinition definition = _templateDefinitionService.Load(template.JsonPath);
            string? templateError = GetTemplateError(template, definition);
            _selectedTemplate = template;
            _selectedDefinition = definition;

            TemplateWidthText.Text = $"Ширина: {definition.Width}";
            TemplateHeightText.Text = $"Высота: {definition.Height}";
            TemplatePhotosText.Text = $"Количество снимков: {definition.RequiredShotCount}";
            TemplateOverlayText.Text = $"Overlay: {definition.Overlay ?? "не указан"}";
            TemplateStatusText.Text = templateError ?? "Шаблон готов.";
            TemplateStatusText.Foreground = templateError is null ? Brushes.LightGreen : Brushes.LightSalmon;
            ContinueButton.IsEnabled = templateError is null;
        }

        TemplatesPanel.Visibility = Visibility.Collapsed;
        TemplateDetailsPanel.Visibility = Visibility.Visible;
    }

    private static string? GetTemplateError(TemplateInfo template, TemplateDefinition definition)
    {
        if (definition.Width <= 0 || definition.Height <= 0)
        {
            return "Ошибка: ширина или высота шаблона не указана.";
        }

        if (definition.RequiredShotCount <= 0)
        {
            return "Ошибка: в шаблоне нет снимков.";
        }

        if (string.IsNullOrWhiteSpace(definition.Overlay))
        {
            return "Ошибка: Overlay не указан.";
        }

        string overlayPath = Path.Combine(template.FolderPath, definition.Overlay);

        if (!File.Exists(overlayPath))
        {
            return $"Ошибка: файл Overlay не найден ({definition.Overlay}).";
        }

        foreach (TemplatePhotoSlot slot in definition.Photos)
        {
            if (slot.Shoot <= 0 ||
                slot.Width <= 0 ||
                slot.Height <= 0 ||
                slot.X < 0 ||
                slot.Y < 0 ||
                slot.X + slot.Width > definition.Width ||
                slot.Y + slot.Height > definition.Height)
            {
                return "Ошибка: координаты одного из снимков выходят за границы шаблона.";
            }
        }

        return null;
    }

    private void StartCountdown()
    {
        _completionTimer.Stop();
        _printCompletionTimer.Stop();
        PrintStatusText.Text = string.Empty;
        PrintButton.IsEnabled = true;
        CopyOptionsPanel.IsEnabled = true;

        if (_selectedTemplate is null || _selectedDefinition is null)
        {
            return;
        }

        try
        {
            string sessionPath = Path.Combine(
                ResolveAppPath(_config.OutputPath),
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            string originalsPath = Path.Combine(sessionPath, "Originals");

            _preparedShots = _demoPhotoService.PrepareShots(
                ResolveAppPath(_config.DemoPhotosPath),
                originalsPath,
                _selectedDefinition.RequiredShotCount);
            _currentShotNumber = 1;
        }
        catch (Exception exception)
        {
            ShowCaptureError(exception.Message);
            return;
        }

        StartCurrentShotCountdown();
    }

    private void StartCurrentShotCountdown()
    {
        _countdownValue = InitialCountdownValue;
        CountdownText.Text = _countdownValue.ToString();
        CountdownText.FontSize = 180;
        CountdownCaption.Text = $"Кадр {_currentShotNumber} из {_selectedDefinition!.RequiredShotCount}";

        TemplateDetailsPanel.Visibility = Visibility.Collapsed;
        PreviewPanel.Visibility = Visibility.Collapsed;
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
        CountdownText.Text = "СНЯТО";
        CountdownText.FontSize = 96;
        CountdownCaption.Text = $"Кадр {_currentShotNumber} сохранён";

        _completionTimer.Start();
    }

    private void CompletionTimer_Tick(object? sender, EventArgs e)
    {
        _completionTimer.Stop();

        if (_selectedDefinition is null)
        {
            ShowHomeScreen();
            return;
        }

        if (_currentShotNumber < _selectedDefinition.RequiredShotCount)
        {
            _currentShotNumber++;
            StartCurrentShotCountdown();
            return;
        }

        ShowComposedPreview();
    }

    private void ShowComposedPreview()
    {
        if (_selectedTemplate is null || _selectedDefinition is null)
        {
            return;
        }

        try
        {
            string originalsPath = Path.GetDirectoryName(_preparedShots[0])!;
            string sessionPath = Directory.GetParent(originalsPath)!.FullName;
            string overlayPath = Path.Combine(_selectedTemplate.FolderPath, _selectedDefinition.Overlay!);
            string resultPath = Path.Combine(sessionPath, "result.png");

            _imageComposer.Compose(
                _selectedDefinition,
                overlayPath,
                _preparedShots,
                resultPath);

            BitmapImage preview = new();
            preview.BeginInit();
            preview.CacheOption = BitmapCacheOption.OnLoad;
            preview.UriSource = new Uri(resultPath, UriKind.Absolute);
            preview.EndInit();
            preview.Freeze();

            ResultPreviewImage.Source = preview;
            _copyCount = 1;
            CopyOneOption.IsChecked = true;
            UpdatePrintButtonText();
            PrintStatusText.Text = string.Empty;
            CountdownPanel.Visibility = Visibility.Collapsed;
            PreviewPanel.Visibility = Visibility.Visible;
        }
        catch (Exception exception)
        {
            ShowCaptureError(exception.Message);
        }
    }

    private void RetakeButton_Click(object sender, RoutedEventArgs e)
    {
        StartCountdown();
    }

    private void PreviewHomeButton_Click(object sender, RoutedEventArgs e)
    {
        ShowHomeScreen();
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        PrintButton.IsEnabled = false;
        CopyOptionsPanel.IsEnabled = false;
        PrintStatusText.Text = _config.DemoMode
            ? $"Демо-печать: {GetCopiesText(_copyCount)}."
            : "Модуль принтера ещё не подключён.";

        _printCompletionTimer.Start();
    }

    private void CopyCount_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string copyCountText } &&
            int.TryParse(copyCountText, out int copyCount))
        {
            _copyCount = copyCount;
            UpdatePrintButtonText();
        }
    }

    private void UpdatePrintButtonText()
    {
        if (PrintButton is null || _config is null)
        {
            return;
        }

        string prefix = _config.DemoMode ? "Печать (демо)" : "Печатать";
        PrintButton.Content = $"{prefix}: {_copyCount}";
    }

    private static string GetCopiesText(int copyCount)
    {
        return copyCount == 1 ? "1 копия" : $"{copyCount} копии";
    }

    private void PrintCompletionTimer_Tick(object? sender, EventArgs e)
    {
        _printCompletionTimer.Stop();
        ShowHomeScreen();
    }

    private void ShowCaptureError(string message)
    {
        _countdownTimer.Stop();
        _completionTimer.Stop();
        _printCompletionTimer.Stop();
        CountdownPanel.Visibility = Visibility.Collapsed;
        PreviewPanel.Visibility = Visibility.Collapsed;
        TemplateDetailsPanel.Visibility = Visibility.Visible;
        TemplateStatusText.Text = $"Ошибка: {message}";
        TemplateStatusText.Foreground = Brushes.LightSalmon;
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
        _printCompletionTimer.Stop();

        TemplatesPanel.Visibility = Visibility.Collapsed;
        TemplateDetailsPanel.Visibility = Visibility.Collapsed;
        CountdownPanel.Visibility = Visibility.Collapsed;
        PreviewPanel.Visibility = Visibility.Collapsed;
        ResultPreviewImage.Source = null;
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
