using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
    private readonly ICameraService _cameraService;
    private readonly ImageComposer _imageComposer;
    private readonly PrintHistoryService _printHistoryService;
    private readonly IPrinterService _printerService;
    private readonly SessionManager _sessionManager;
    private readonly TemplateDefinitionService _templateDefinitionService;
    private readonly TemplateManager _templateManager;
    private readonly string _outputRootPath;
    private int _countdownValue = InitialCountdownValue;
    private int _copyCount = 1;
    private int _currentShotNumber;
    private bool _isCursorHidden;
    private bool _isFullscreen;
    private bool _isHistoryPreview;
    private string _currentCaptureId = string.Empty;
    private string _currentResultPath = string.Empty;
    private PhotoSession? _activeSession;
    private IReadOnlyList<string> _preparedShots = [];
    private PhotoSession? _recoverableSession;
    private TemplateDefinition? _selectedDefinition;
    private TemplateInfo? _selectedTemplate;

    public MainWindow()
    {
        InitializeComponent();

        _config = new ConfigService().Load();
        _cameraService = new DemoPhotoService();
        _imageComposer = new ImageComposer();
        _printHistoryService = new PrintHistoryService();
        _printerService = new DemoPrinterService();
        _sessionManager = new SessionManager();
        _templateDefinitionService = new TemplateDefinitionService();
        _templateManager = new TemplateManager();
        _outputRootPath = ResolveAppPath(_config.OutputPath);
        SetFullscreen(_config.Fullscreen);
        SetCursorHidden(_config.HideCursor);

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

        ShowSessionStartup();
    }

    private void ShowSessionStartup()
    {
        _recoverableSession = _sessionManager.LoadActiveSession(_outputRootPath);
        SessionErrorText.Text = string.Empty;
        HomePanel.Visibility = Visibility.Collapsed;
        TemplatesPanel.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Collapsed;
        PrintProgressPanel.Visibility = Visibility.Collapsed;
        PreviewPanel.Visibility = Visibility.Collapsed;
        SessionPanel.Visibility = Visibility.Visible;

        if (_recoverableSession is null)
        {
            ShowNewSessionForm();
            return;
        }

        CurrentSessionNameText.Text =
            $"{_recoverableSession.Name}\n" +
            $"с {_recoverableSession.StartedAt:dd.MM.yyyy HH:mm}";
        NewSessionPanel.Visibility = Visibility.Collapsed;
        CurrentSessionPanel.Visibility = Visibility.Visible;
    }

    private void ContinueSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recoverableSession is null)
        {
            ShowNewSessionForm();
            return;
        }

        ActivateSession(_recoverableSession);
    }

    private void ShowNewSessionButton_Click(object sender, RoutedEventArgs e)
    {
        ShowNewSessionForm();
    }

    private void ShowNewSessionForm()
    {
        CurrentSessionPanel.Visibility = Visibility.Collapsed;
        NewSessionPanel.Visibility = Visibility.Visible;
        SessionNameTextBox.Text = string.Empty;
        SessionErrorText.Text = string.Empty;
        SessionNameTextBox.Focus();
    }

    private void CreateSessionButton_Click(object sender, RoutedEventArgs e)
    {
        CreateAndActivateSession();
    }

    private void SessionNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CreateAndActivateSession();
        }
    }

    private void CreateAndActivateSession()
    {
        try
        {
            PhotoSession session = _sessionManager.CreateSession(
                _outputRootPath,
                SessionNameTextBox.Text);
            ActivateSession(session);
        }
        catch (Exception exception)
        {
            SessionErrorText.Text = exception.Message;
        }
    }

    private void ActivateSession(PhotoSession session)
    {
        _activeSession = session;
        _recoverableSession = session;
        ActiveSessionText.Text = $"Сессия: {session.Name}";
        SessionPanel.Visibility = Visibility.Collapsed;
        HomePanel.Visibility = Visibility.Visible;
    }

    private void ChangeSessionButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSessionStartup();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        string templatesPath = ResolveAppPath(_config.TemplatesPath);
        IReadOnlyList<TemplateInfo> templates = _templateManager.GetTemplates(templatesPath);

        TemplatesList.ItemsSource = templates;
        TemplatesList.SelectedIndex = templates.Count > 0 ? 0 : -1;
        TemplatesList.Visibility = templates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoTemplatesText.Visibility = templates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TemplateSelectionErrorText.Text = string.Empty;
        TemplatePreviewOverlay.Visibility = Visibility.Collapsed;
        TemplatePreviewImage.Source = null;
        TemplateSelectionContinueButton.IsEnabled = templates.Count > 0;
        TemplatePreviewButton.IsEnabled = templates.Count > 0;

        HomePanel.Visibility = Visibility.Collapsed;
        TemplatesPanel.Visibility = Visibility.Visible;
    }

    private void PrintHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPrintHistory();
    }

    private void ShowPrintHistory()
    {
        if (_activeSession is null)
        {
            ShowSessionStartup();
            return;
        }

        IReadOnlyList<PrintHistoryItem> history = _printHistoryService.GetItems(_activeSession);
        HistoryList.ItemsSource = history;
        HistoryList.Visibility = history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoHistoryText.Visibility = history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistorySessionNameText.Text = $"Сессия: {_activeSession.Name}";

        HomePanel.Visibility = Visibility.Collapsed;
        TemplatesPanel.Visibility = Visibility.Collapsed;
        PreviewPanel.Visibility = Visibility.Collapsed;
        PrintProgressPanel.Visibility = Visibility.Collapsed;
        ResultPreviewImage.Source = null;
        HistoryPanel.Visibility = Visibility.Visible;
    }

    private void HistoryItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PrintHistoryItem item })
        {
            return;
        }

        _currentResultPath = item.FilePath;
        _isHistoryPreview = true;
        ShowResultPreview(item.FilePath);
    }

    private void HistoryBackButton_Click(object sender, RoutedEventArgs e)
    {
        ShowHomeScreen();
    }

    private void TemplateSelectionContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (TemplatesList.SelectedItem is not TemplateInfo template)
        {
            return;
        }

        SelectTemplateAndShowCapture(template);
    }

    private void TemplatePreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (TemplatesList.SelectedItem is not TemplateInfo { PreviewPath: not null } template)
        {
            return;
        }

        TemplatePreviewImage.Source = LoadImage(template.PreviewPath);
        TemplatePreviewOverlay.Visibility = Visibility.Visible;
    }

    private void CloseTemplatePreviewButton_Click(object sender, RoutedEventArgs e)
    {
        TemplatePreviewOverlay.Visibility = Visibility.Collapsed;
        TemplatePreviewImage.Source = null;
    }

    private void BackToHomeButton_Click(object sender, RoutedEventArgs e)
    {
        ShowHomeScreen();
    }

    private void CaptureBackButton_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer.Stop();
        _completionTimer.Stop();
        CountdownPanel.Visibility = Visibility.Collapsed;
        TemplatePreviewOverlay.Visibility = Visibility.Collapsed;
        TemplatesPanel.Visibility = Visibility.Visible;
    }

    private void StartCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        StartCurrentShotCountdown();
    }

    private void SelectTemplateAndShowCapture(TemplateInfo template)
    {
        if (string.IsNullOrWhiteSpace(template.JsonPath))
        {
            TemplateSelectionErrorText.Text = "JSON-файл выбранного шаблона не найден.";
            return;
        }

        try
        {
            TemplateDefinition definition = _templateDefinitionService.Load(template.JsonPath);
            string? templateError = GetTemplateError(template, definition);

            if (templateError is not null)
            {
                TemplateSelectionErrorText.Text = templateError;
                return;
            }

            _selectedTemplate = template;
            _selectedDefinition = definition;
            TemplateSelectionErrorText.Text = string.Empty;
            PrepareCaptureScreen();
        }
        catch (Exception exception)
        {
            TemplateSelectionErrorText.Text = $"Не удалось прочитать шаблон: {exception.Message}";
        }
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

    private void PrepareCaptureScreen()
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

        if (_activeSession is null)
        {
            ShowSessionStartup();
            return;
        }

        try
        {
            _currentCaptureId = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string originalsPath = Path.Combine(
                _activeSession.FolderPath,
                "Photos",
                _currentCaptureId);

            _preparedShots = _cameraService.PrepareShots(
                ResolveAppPath(_config.DemoPhotosPath),
                originalsPath,
                _selectedDefinition.RequiredShotCount);
            _currentShotNumber = 1;

            DemoPreviewBadge.Visibility = _cameraService.IsDemo
                ? Visibility.Visible
                : Visibility.Collapsed;
            LivePreviewImage.Source = LoadImage(_preparedShots[0]);
            CaptureReadyTitleText.Text = _selectedTemplate.Name;
            UpdateCaptureProgress();
        }
        catch (Exception exception)
        {
            ShowCaptureError(exception.Message);
            return;
        }

        HomePanel.Visibility = Visibility.Collapsed;
        TemplatesPanel.Visibility = Visibility.Collapsed;
        PreviewPanel.Visibility = Visibility.Collapsed;
        CaptureReadyOverlay.Visibility = Visibility.Visible;
        CountdownOverlay.Visibility = Visibility.Collapsed;
        CountdownPanel.Visibility = Visibility.Visible;
    }

    private void StartCurrentShotCountdown()
    {
        _countdownValue = InitialCountdownValue;
        CountdownText.Text = _countdownValue.ToString();
        CountdownText.FontSize = 112;
        CountdownCaption.Text = "Смотрите в объектив";
        UpdateCaptureProgress();
        LivePreviewImage.Source = LoadImage(_preparedShots[_currentShotNumber - 1]);

        CaptureReadyOverlay.Visibility = Visibility.Collapsed;
        CountdownOverlay.Visibility = Visibility.Visible;
        PreviewPanel.Visibility = Visibility.Collapsed;
        CountdownPanel.Visibility = Visibility.Visible;

        _countdownTimer.Start();
    }

    private void UpdateCaptureProgress()
    {
        int shotCount = _selectedDefinition?.RequiredShotCount ?? 0;
        CaptureProgressText.Text = $"СЪЁМКА {_currentShotNumber} ИЗ {shotCount}";
        CaptureStatusPhotoText.Text = $"{_currentShotNumber} из {shotCount}";
        CaptureProgressDots.ItemsSource = Enumerable
            .Range(1, shotCount)
            .Select(shotNumber => shotNumber <= _currentShotNumber)
            .ToList();
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
            string overlayPath = Path.Combine(_selectedTemplate.FolderPath, _selectedDefinition.Overlay!);
            string resultPath = Path.Combine(
                _activeSession!.FolderPath,
                "Prints",
                $"{_currentCaptureId}.png");

            _imageComposer.Compose(
                _selectedDefinition,
                overlayPath,
                _preparedShots,
                resultPath);

            _currentResultPath = resultPath;
            _isHistoryPreview = false;
            CountdownPanel.Visibility = Visibility.Collapsed;
            ShowResultPreview(resultPath);
        }
        catch (Exception exception)
        {
            ShowCaptureError(exception.Message);
        }
    }

    private void RetakeButton_Click(object sender, RoutedEventArgs e)
    {
        PrepareCaptureScreen();
    }

    private void PreviewHomeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHistoryPreview)
        {
            ShowPrintHistory();
            return;
        }

        ShowHomeScreen();
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        PrintButton.IsEnabled = false;
        CopyOptionsPanel.IsEnabled = false;
        PrintResult result;

        try
        {
            result = _printerService.Print(_currentResultPath, _copyCount);
        }
        catch (Exception exception)
        {
            result = new PrintResult(false, $"Ошибка печати: {exception.Message}");
        }

        PrintStatusText.Text = result.Message;

        if (_activeSession is not null)
        {
            _printHistoryService.RecordPrintJob(
                _activeSession,
                _currentResultPath,
                _copyCount,
                _printerService.DisplayName,
                result);
        }

        if (result.Success)
        {
            ShowPrintProgress();
            _printCompletionTimer.Start();
            return;
        }

        PrintButton.IsEnabled = true;
        CopyOptionsPanel.IsEnabled = true;
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
        if (PrintButtonText is null || _config is null)
        {
            return;
        }

        string prefix = _printerService.IsDemo ? "Печать (демо)" : "Печатать";
        PrintButtonText.Text = $"{prefix}: {_copyCount}";
    }

    private void PrintCompletionTimer_Tick(object? sender, EventArgs e)
    {
        _printCompletionTimer.Stop();
        PrintProgressPercentText.Text = "100%";
        PrintProgressArc.Data = Geometry.Parse(
            "M 195,18 A 177,177 0 1 1 195,372 A 177,177 0 1 1 195,18");
        PrintProgressStatusText.Text = "Задание передано принтеру";
        PrintProgressTimeText.Text = "00:00";
        PrintProgressPhaseText.Text = "Завершено";
        PrintProgressPhaseText.Foreground = new SolidColorBrush(
            Color.FromRgb(123, 97, 255));
        PrintCompletionPhaseText.Text = "Завершено";
        PrintCompletionPhaseText.Foreground = new SolidColorBrush(
            Color.FromRgb(123, 97, 255));
    }

    private void ShowPrintProgress()
    {
        PrintProgressPrinterNameText.Text = _printerService.DisplayName;
        PrintProgressTemplateText.Text = _isHistoryPreview
            ? "Повторная печать"
            : _selectedTemplate?.Name ?? "Фотография";
        PrintProgressCopiesText.Text =
            _copyCount == 1 ? "1 копия" : $"{_copyCount} копии";
        PrintProgressPercentText.Text = "68%";
        PrintProgressArc.Data = Geometry.Parse(
            "M 195,18 A 177,177 0 1 1 52,302");
        PrintProgressStatusText.Text = "Печатаем ваш макет";
        PrintProgressTimeText.Text = _printerService.IsDemo ? "00:03" : "00:20";
        PrintProgressPhaseText.Text = "В процессе";
        PrintProgressPhaseText.Foreground = new SolidColorBrush(
            Color.FromRgb(123, 97, 255));
        PrintCompletionPhaseText.Text = "Ожидание";
        PrintCompletionPhaseText.Foreground = new SolidColorBrush(
            Color.FromRgb(104, 117, 140));
        PrintNextPhotoButton.Content = _isHistoryPreview
            ? "К истории"
            : "Следующее фото";

        PreviewPanel.Visibility = Visibility.Collapsed;
        PrintProgressPanel.Visibility = Visibility.Visible;
    }

    private void PrintNextPhotoButton_Click(object sender, RoutedEventArgs e)
    {
        _printCompletionTimer.Stop();

        if (_isHistoryPreview)
        {
            ShowPrintHistory();
            return;
        }

        ShowHomeScreen();
    }

    private void PrintProgressBackButton_Click(object sender, RoutedEventArgs e)
    {
        PrintNextPhotoButton_Click(sender, e);
    }

    private void ShowResultPreview(string resultPath)
    {
        ResultPreviewImage.Source = LoadImage(resultPath);
        _copyCount = 1;
        CopyOneOption.IsChecked = true;
        CopyOptionsPanel.IsEnabled = true;
        PrintButton.IsEnabled = true;
        PrintStatusText.Text = string.Empty;
        UpdatePrintButtonText();

        PreviewTitleText.Text = _isHistoryPreview ? "Повторная печать" : "Предпросмотр";
        PreviewBackButtonText.Text = _isHistoryPreview ? "К истории" : "На главную";
        RetakeButton.Visibility = _isHistoryPreview ? Visibility.Collapsed : Visibility.Visible;

        HistoryPanel.Visibility = Visibility.Collapsed;
        PrintProgressPanel.Visibility = Visibility.Collapsed;
        PreviewPanel.Visibility = Visibility.Visible;
    }

    private void ShowCaptureError(string message)
    {
        _countdownTimer.Stop();
        _completionTimer.Stop();
        _printCompletionTimer.Stop();
        CountdownPanel.Visibility = Visibility.Collapsed;
        PreviewPanel.Visibility = Visibility.Collapsed;
        TemplatesPanel.Visibility = Visibility.Visible;
        TemplateSelectionErrorText.Text = $"Ошибка: {message}";
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            SetFullscreen(!_isFullscreen);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F10)
        {
            SetCursorHidden(!_isCursorHidden);
            e.Handled = true;
            return;
        }

        if (_config.DemoMode && e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void SetFullscreen(bool isFullscreen)
    {
        _isFullscreen = isFullscreen;
        WindowState = WindowState.Normal;

        if (isFullscreen)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            WindowState = WindowState.Maximized;
            return;
        }

        Topmost = false;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        Width = 1280;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void SetCursorHidden(bool isHidden)
    {
        _isCursorHidden = isHidden;
        Cursor = isHidden ? Cursors.None : Cursors.Arrow;
    }

    private void ShowHomeScreen()
    {
        _countdownTimer.Stop();
        _completionTimer.Stop();
        _printCompletionTimer.Stop();

        TemplatesPanel.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Collapsed;
        PrintProgressPanel.Visibility = Visibility.Collapsed;
        CountdownPanel.Visibility = Visibility.Collapsed;
        PreviewPanel.Visibility = Visibility.Collapsed;
        ResultPreviewImage.Source = null;
        LivePreviewImage.Source = null;
        TemplatePreviewImage.Source = null;
        TemplatePreviewOverlay.Visibility = Visibility.Collapsed;
        _isHistoryPreview = false;
        SessionPanel.Visibility = Visibility.Collapsed;
        ActiveSessionText.Text = _activeSession is null ? string.Empty : $"Сессия: {_activeSession.Name}";
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

    private static BitmapImage LoadImage(string path)
    {
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
