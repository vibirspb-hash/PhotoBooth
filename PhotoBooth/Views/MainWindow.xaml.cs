using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private readonly DispatcherTimer _finalPreviewTimer;
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
    private readonly List<string> _acceptedShots = [];
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

        _finalPreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };

        _finalPreviewTimer.Tick += FinalPreviewTimer_Tick;

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
        _finalPreviewTimer.Stop();
        CountdownPanel.IsHitTestVisible = true;
        CountdownPanel.Visibility = Visibility.Collapsed;
        ShotReviewOverlay.Visibility = Visibility.Collapsed;
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
        _finalPreviewTimer.Stop();
        _printCompletionTimer.Stop();
        _acceptedShots.Clear();
        AcceptedShotsPanel.Children.Clear();
        AcceptedShotsRail.Visibility = Visibility.Collapsed;
        ShotFlyCard.Visibility = Visibility.Collapsed;
        ShotReviewOverlay.IsHitTestVisible = true;
        CountdownPanel.IsHitTestVisible = true;
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
        ShotReviewOverlay.Visibility = Visibility.Collapsed;
        CountdownPanel.Visibility = Visibility.Visible;
    }

    private void StartCurrentShotCountdown()
    {
        _countdownValue = InitialCountdownValue;
        CountdownText.Text = _countdownValue.ToString();
        CountdownText.FontSize = 112;
        CountdownCaption.Text = "Смотрите в объектив";
        UpdateCountdownProgressRing();
        UpdateCaptureProgress();
        LivePreviewImage.Source = LoadImage(_preparedShots[_currentShotNumber - 1]);

        CaptureReadyOverlay.Visibility = Visibility.Collapsed;
        CountdownOverlay.Visibility = Visibility.Visible;
        ShotReviewOverlay.Visibility = Visibility.Collapsed;
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
        UpdateCountdownProgressRing();

        if (_countdownValue > 0)
        {
            CountdownText.Text = _countdownValue.ToString();
            return;
        }

        _countdownTimer.Stop();
        CountdownText.Text = "СНЯТО";
        CountdownText.FontSize = 96;
        CountdownCaption.Text = $"Снимок {_currentShotNumber} готов";

        _completionTimer.Start();
    }

    private void UpdateCountdownProgressRing()
    {
        if (_countdownValue <= 1)
        {
            CountdownProgressRing.Visibility = Visibility.Hidden;
            return;
        }

        CountdownProgressRing.Visibility = Visibility.Visible;
        CountdownProgressArc.Data = _countdownValue >= 3
            ? Geometry.Parse("M 280,25 A 255,255 0 0 1 280,535")
            : Geometry.Parse("M 280,25 A 255,255 0 0 1 535,280");
    }

    private void CompletionTimer_Tick(object? sender, EventArgs e)
    {
        _completionTimer.Stop();

        if (_selectedDefinition is null)
        {
            ShowHomeScreen();
            return;
        }

        ShowCurrentShotReview();
    }

    private void ShowCurrentShotReview()
    {
        int shotCount = _selectedDefinition?.RequiredShotCount ?? 0;
        ShotReviewProgressText.Text = $"Фото {_currentShotNumber} из {shotCount}";
        CaptureReadyOverlay.Visibility = Visibility.Collapsed;
        CountdownOverlay.Visibility = Visibility.Collapsed;
        ShotReviewOverlay.Visibility = Visibility.Visible;
    }

    private void RetakeCurrentShotButton_Click(object sender, RoutedEventArgs e)
    {
        ShotReviewOverlay.Visibility = Visibility.Collapsed;
        StartCurrentShotCountdown();
    }

    private void AcceptCurrentShotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDefinition is null)
        {
            ShowHomeScreen();
            return;
        }

        AnimateAcceptedShot();
    }

    private void AnimateAcceptedShot()
    {
        int shotIndex = _currentShotNumber - 1;

        if (shotIndex < 0 || shotIndex >= _preparedShots.Count)
        {
            ShowCaptureError("Не удалось найти текущий снимок.");
            return;
        }

        string shotPath = _preparedShots[shotIndex];
        Duration duration = new(TimeSpan.FromMilliseconds(480));
        QuadraticEase easing = new()
        {
            EasingMode = EasingMode.EaseInOut
        };

        ShotReviewOverlay.IsHitTestVisible = false;
        CountdownPanel.IsHitTestVisible = false;
        ShotFlyImage.Source = LoadImage(shotPath);
        ShotFlyCard.Opacity = 1;
        ShotFlyScale.ScaleX = 1;
        ShotFlyScale.ScaleY = 1;
        ShotFlyTranslate.X = 0;
        ShotFlyTranslate.Y = 0;
        ShotFlyCard.Visibility = Visibility.Visible;

        double targetX = -Math.Max(300, CountdownPanel.ActualWidth * 0.38);
        double targetY = -180 + (Math.Min(_acceptedShots.Count, 4) * 88);

        DoubleAnimation scaleXAnimation = new(1, 0.19, duration)
        {
            EasingFunction = easing
        };
        DoubleAnimation scaleYAnimation = new(1, 0.19, duration)
        {
            EasingFunction = easing
        };
        DoubleAnimation translateXAnimation = new(0, targetX, duration)
        {
            EasingFunction = easing
        };
        DoubleAnimation translateYAnimation = new(0, targetY, duration)
        {
            EasingFunction = easing
        };
        DoubleAnimation opacityAnimation = new(1, 0.78, duration)
        {
            EasingFunction = easing
        };

        opacityAnimation.Completed += (_, _) =>
        {
            ShotFlyCard.Visibility = Visibility.Collapsed;
            ShotFlyCard.BeginAnimation(OpacityProperty, null);
            ShotFlyScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ShotFlyScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            ShotFlyTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ShotFlyTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            ShotFlyImage.Source = null;
            ShotReviewOverlay.IsHitTestVisible = true;
            CountdownPanel.IsHitTestVisible = true;

            _acceptedShots.Add(shotPath);
            AddAcceptedShotThumbnail(shotPath, _currentShotNumber);
            AdvanceAfterAcceptedShot();
        };

        ShotFlyScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
        ShotFlyScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
        ShotFlyTranslate.BeginAnimation(TranslateTransform.XProperty, translateXAnimation);
        ShotFlyTranslate.BeginAnimation(TranslateTransform.YProperty, translateYAnimation);
        ShotFlyCard.BeginAnimation(OpacityProperty, opacityAnimation);
    }

    private void AddAcceptedShotThumbnail(string shotPath, int shotNumber)
    {
        Grid thumbnailGrid = new();
        thumbnailGrid.Children.Add(new Image
        {
            Source = LoadImage(shotPath),
            Stretch = Stretch.UniformToFill
        });

        Border numberBadge = new()
        {
            Width = 28,
            Height = 28,
            Margin = new Thickness(6),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(220, 123, 97, 255)),
            CornerRadius = new CornerRadius(14),
            Child = new TextBlock
            {
                Text = shotNumber.ToString(),
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        thumbnailGrid.Children.Add(numberBadge);

        AcceptedShotsPanel.Children.Add(new Border
        {
            Width = 106,
            Height = 76,
            Margin = new Thickness(0, 0, 0, 10),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = thumbnailGrid
        });

        AcceptedShotsRail.Visibility = Visibility.Visible;
    }

    private void AdvanceAfterAcceptedShot()
    {
        ShotReviewOverlay.Visibility = Visibility.Collapsed;

        if (_selectedDefinition is null)
        {
            ShowHomeScreen();
            return;
        }

        if (_currentShotNumber >= _selectedDefinition.RequiredShotCount)
        {
            CountdownPanel.IsHitTestVisible = false;
            _finalPreviewTimer.Start();
            return;
        }

        _currentShotNumber++;
        StartCurrentShotCountdown();
    }

    private void FinalPreviewTimer_Tick(object? sender, EventArgs e)
    {
        _finalPreviewTimer.Stop();
        CountdownPanel.IsHitTestVisible = true;
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
        PrintNextPhotoButtonText.Text = _isHistoryPreview
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
        _finalPreviewTimer.Stop();
        _printCompletionTimer.Stop();
        CountdownPanel.IsHitTestVisible = true;
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
        _finalPreviewTimer.Stop();
        _printCompletionTimer.Stop();
        CountdownPanel.IsHitTestVisible = true;

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
