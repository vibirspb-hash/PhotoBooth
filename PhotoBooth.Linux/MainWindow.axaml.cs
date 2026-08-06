using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PhotoBooth.Models;
using PhotoBooth.Services;

namespace PhotoBooth.Linux;

public sealed partial class MainWindow : Window
{
    private const string SettingsPin = "2016";
    private const int CountdownStart = 3;

    private readonly ConfigService _configService = new();
    private readonly AppConfig _config;
    private readonly SessionManager _sessionManager = new();
    private readonly TemplateManager _templateManager = new();
    private readonly TemplateDefinitionService _templateDefinitionService = new();
    private IPhotoCaptureService _cameraService = new DemoPhotoCaptureService();
    private readonly SkiaImageComposer _imageComposer = new();
    private readonly PrinterCalibrationService _printerCalibrationService = new();
    private readonly PrintAuditService _printAuditService = new();
    private IPrinterService _printerService = new DemoPrinterService();
    private string _printerStatus = "Деморежим.";
    private readonly string _outputRootPath;
    private readonly string _templatesPath;
    private readonly string _demoPhotosPath;
    private readonly List<Button> _templateButtons = [];
    private readonly List<string> _acceptedShots = [];

    private readonly TextBlock _activeSessionText;
    private readonly TextBlock _currentSessionNameText;
    private readonly StackPanel _currentSessionPanel;
    private readonly Grid _homePanel;
    private readonly TextBlock _homeSubtitleText;
    private readonly Border _homeCameraStatusBadge;
    private readonly Border _homePrinterStatusBadge;
    private readonly StackPanel _newSessionPanel;
    private readonly StackPanel _savedSessionsPanel;
    private readonly StackPanel _savedSessionsListPanel;
    private readonly TextBlock _sessionErrorText;
    private readonly StackPanel _sessionKeyboardPanel;
    private readonly TextBox _sessionNameTextBox;
    private readonly Grid _sessionPanel;
    private readonly TextBlock _selectedTemplateText;
    private readonly Button _templateContinueButton;
    private readonly TextBlock _templatesCountText;
    private readonly TextBlock _templatesEmptyText;
    private readonly WrapPanel _templatesItemsPanel;
    private readonly Grid _templatesPanel;
    private readonly Grid _capturePanel;
    private readonly Image _livePreviewImage;
    private readonly TextBlock _captureProgressText;
    private readonly Border _captureDeviceBadge;
    private readonly TextBlock _captureDeviceBadgeText;
    private readonly Border _acceptedShotsRail;
    private readonly StackPanel _acceptedShotsPanel;
    private readonly StackPanel _captureReadyOverlay;
    private readonly Button _captureReadyActions;
    private readonly TextBlock _captureReadyTitleText;
    private readonly Border _countdownOverlay;
    private readonly TextBlock _countdownText;
    private readonly TextBlock _countdownCaption;
    private readonly StackPanel _shotReviewOverlay;
    private readonly StackPanel _shotReviewActions;
    private readonly TextBlock _shotReviewProgressText;
    private readonly Border _shotFlyCard;
    private readonly Image _shotFlyImage;
    private readonly TextBlock _captureFooterText;
    private readonly Grid _previewPanel;
    private readonly Image _resultPreviewImage;
    private readonly TextBlock _previewBackButtonText;
    private readonly TextBlock _previewTitleText;
    private readonly TextBlock _previewSubtitleText;
    private readonly Button _copyOneButton;
    private readonly Button _copyTwoButton;
    private readonly Button _copyThreeButton;
    private readonly TextBlock _printButtonText;
    private readonly Grid _printProgressPanel;
    private readonly TextBlock _printProgressStatusText;
    private readonly TextBlock _printProgressPercentText;
    private readonly TextBlock _printProgressDetailsText;
    private readonly Button _printNextPhotoButton;
    private readonly TextBlock _printNextPhotoButtonText;
    private readonly Grid _historyPanel;
    private readonly TextBlock _historySessionNameText;
    private readonly StackPanel _noHistoryPanel;
    private readonly WrapPanel _historyItemsPanel;
    private readonly StackPanel _historySessionFilterPanel;
    private readonly Grid _instructionOverlay;
    private readonly Grid _settingsOverlay;
    private readonly StackPanel _settingsPinPanel;
    private readonly TextBlock _settingsPinTitleText;
    private readonly TextBlock _settingsPinSubtitleText;
    private readonly TextBlock _settingsPinDisplay;
    private readonly TextBlock _settingsPinErrorText;
    private readonly Button _settingsPinCancelButton;
    private readonly StackPanel _settingsMenuPanel;
    private readonly TextBlock _settingsSectionStatusText;
    private readonly StackPanel _printerSettingsPanel;
    private readonly TextBlock _printerSettingsStatusText;
    private readonly TextBlock _printerOffsetXText;
    private readonly TextBlock _printerOffsetYText;
    private readonly TextBlock _printerScaleText;
    private readonly Button _printerQualityFastButton;
    private readonly Button _printerQualityHighButton;
    private readonly Button _printerCutStandardButton;
    private readonly Button _printerCutTwoInchButton;
    private readonly StackPanel _scheduleSettingsPanel;
    private readonly CheckBox _scheduleEnabledCheckBox;
    private readonly CheckBox _scheduleShutdownCheckBox;
    private readonly TextBlock _scheduleStartHourText;
    private readonly TextBlock _scheduleStartMinuteText;
    private readonly TextBlock _scheduleEndHourText;
    private readonly TextBlock _scheduleEndMinuteText;
    private readonly TextBlock _scheduleStatusText;
    private readonly Grid _offHoursPanel;
    private readonly TextBlock _offHoursScheduleText;
    private readonly StackPanel _cameraSettingsPanel;
    private readonly ComboBox _cameraIsoComboBox;
    private readonly ComboBox _cameraApertureComboBox;
    private readonly ComboBox _cameraShutterComboBox;
    private readonly ComboBox _cameraWhiteBalanceComboBox;
    private readonly TextBlock _cameraSettingsStatusText;
    private readonly StackPanel _systemTimeSettingsPanel;
    private readonly TextBlock _systemTimeDayText;
    private readonly TextBlock _systemTimeMonthText;
    private readonly TextBlock _systemTimeYearText;
    private readonly TextBlock _systemTimeHourText;
    private readonly TextBlock _systemTimeMinuteText;
    private readonly TextBlock _systemTimeStatusText;

    private readonly DispatcherTimer _countdownTimer;
    private readonly DispatcherTimer _reviewDelayTimer;
    private readonly DispatcherTimer _flyTimer;
    private readonly DispatcherTimer _finalPreviewTimer;
    private readonly DispatcherTimer _printProgressTimer;
    private readonly DispatcherTimer _printCompletionTimer;
    private readonly DispatcherTimer _livePreviewTimer;
    private readonly DispatcherTimer _scheduleTimer;

    private PhotoSession? _activeSession;
    private PhotoSession? _recoverableSession;
    private TemplateInfo? _selectedTemplate;
    private TemplateDefinition? _selectedDefinition;
    private int _currentShotNumber = 1;
    private int _countdownValue = CountdownStart;
    private int _copyCount = 1;
    private int _printProgress;
    private string _captureId = string.Empty;
    private string _resultPath = string.Empty;
    private string _currentCapturedPath = string.Empty;
    private string? _historyFilterSessionPath;
    private string _pinEntry = string.Empty;
    private bool _isHistoryPreview;
    private bool _isStartupLocked = true;
    private bool _livePreviewBusy;
    private int _printerOffsetXDraft;
    private int _printerOffsetYDraft;
    private double _printerScaleDraft = 100;
    private string _printerQualityDraft = "Fast";
    private string _printerCutModeDraft = "Standard";
    private int _scheduleStartHourDraft;
    private int _scheduleStartMinuteDraft;
    private int _scheduleEndHourDraft;
    private int _scheduleEndMinuteDraft;
    private bool _schedulePowerOffRequested;
    private readonly Dictionary<string, CameraSettingDefinition> _cameraSettingDefinitions = [];
    private DateTime _systemTimeDraft;
    private byte[]? _lastLivePreviewFrame;
    private DateTime _flyStartedAt;
    private readonly ScaleTransform _flyScale = new();
    private readonly TranslateTransform _flyTranslate = new();
    private readonly bool _persistentStorageRequired;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        _activeSessionText = Find<TextBlock>("ActiveSessionText");
        _currentSessionNameText = Find<TextBlock>("CurrentSessionNameText");
        _currentSessionPanel = Find<StackPanel>("CurrentSessionPanel");
        _homePanel = Find<Grid>("HomePanel");
        _homeSubtitleText = Find<TextBlock>("HomeSubtitleText");
        _homeCameraStatusBadge = Find<Border>("HomeCameraStatusBadge");
        _homePrinterStatusBadge = Find<Border>("HomePrinterStatusBadge");
        _newSessionPanel = Find<StackPanel>("NewSessionPanel");
        _savedSessionsPanel = Find<StackPanel>("SavedSessionsPanel");
        _savedSessionsListPanel = Find<StackPanel>("SavedSessionsListPanel");
        _sessionErrorText = Find<TextBlock>("SessionErrorText");
        _sessionKeyboardPanel = Find<StackPanel>("SessionKeyboardPanel");
        _sessionNameTextBox = Find<TextBox>("SessionNameTextBox");
        _sessionPanel = Find<Grid>("SessionPanel");
        _selectedTemplateText = Find<TextBlock>("SelectedTemplateText");
        _templateContinueButton = Find<Button>("TemplateContinueButton");
        _templatesCountText = Find<TextBlock>("TemplatesCountText");
        _templatesEmptyText = Find<TextBlock>("TemplatesEmptyText");
        _templatesItemsPanel = Find<WrapPanel>("TemplatesItemsPanel");
        _templatesPanel = Find<Grid>("TemplatesPanel");
        _capturePanel = Find<Grid>("CapturePanel");
        _livePreviewImage = Find<Image>("LivePreviewImage");
        _captureProgressText = Find<TextBlock>("CaptureProgressText");
        _captureDeviceBadge = Find<Border>("CaptureDeviceBadge");
        _captureDeviceBadgeText = Find<TextBlock>("CaptureDeviceBadgeText");
        _acceptedShotsRail = Find<Border>("AcceptedShotsRail");
        _acceptedShotsPanel = Find<StackPanel>("AcceptedShotsPanel");
        _captureReadyOverlay = Find<StackPanel>("CaptureReadyOverlay");
        _captureReadyActions = Find<Button>("CaptureReadyActions");
        _captureReadyTitleText = Find<TextBlock>("CaptureReadyTitleText");
        _countdownOverlay = Find<Border>("CountdownOverlay");
        _countdownText = Find<TextBlock>("CountdownText");
        _countdownCaption = Find<TextBlock>("CountdownCaption");
        _shotReviewOverlay = Find<StackPanel>("ShotReviewOverlay");
        _shotReviewActions = Find<StackPanel>("ShotReviewActions");
        _shotReviewProgressText = Find<TextBlock>("ShotReviewProgressText");
        _shotFlyCard = Find<Border>("ShotFlyCard");
        _shotFlyImage = Find<Image>("ShotFlyImage");
        _captureFooterText = Find<TextBlock>("CaptureFooterText");
        _previewPanel = Find<Grid>("PreviewPanel");
        _resultPreviewImage = Find<Image>("ResultPreviewImage");
        _previewBackButtonText = Find<TextBlock>("PreviewBackButtonText");
        _previewTitleText = Find<TextBlock>("PreviewTitleText");
        _previewSubtitleText = Find<TextBlock>("PreviewSubtitleText");
        _copyOneButton = Find<Button>("CopyOneButton");
        _copyTwoButton = Find<Button>("CopyTwoButton");
        _copyThreeButton = Find<Button>("CopyThreeButton");
        _printButtonText = Find<TextBlock>("PrintButtonText");
        _printProgressPanel = Find<Grid>("PrintProgressPanel");
        _printProgressStatusText = Find<TextBlock>("PrintProgressStatusText");
        _printProgressPercentText = Find<TextBlock>("PrintProgressPercentText");
        _printProgressDetailsText = Find<TextBlock>("PrintProgressDetailsText");
        _printNextPhotoButton = Find<Button>("PrintNextPhotoButton");
        _printNextPhotoButtonText = Find<TextBlock>("PrintNextPhotoButtonText");
        _historyPanel = Find<Grid>("HistoryPanel");
        _historySessionNameText = Find<TextBlock>("HistorySessionNameText");
        _noHistoryPanel = Find<StackPanel>("NoHistoryPanel");
        _historyItemsPanel = Find<WrapPanel>("HistoryItemsPanel");
        _historySessionFilterPanel = Find<StackPanel>("HistorySessionFilterPanel");
        _instructionOverlay = Find<Grid>("InstructionOverlay");
        _settingsOverlay = Find<Grid>("SettingsOverlay");
        _settingsPinPanel = Find<StackPanel>("SettingsPinPanel");
        _settingsPinTitleText = Find<TextBlock>("SettingsPinTitleText");
        _settingsPinSubtitleText = Find<TextBlock>("SettingsPinSubtitleText");
        _settingsPinDisplay = Find<TextBlock>("SettingsPinDisplay");

        BuildSessionKeyboard();
        _settingsPinErrorText = Find<TextBlock>("SettingsPinErrorText");
        _settingsPinCancelButton = Find<Button>("SettingsPinCancelButton");
        _settingsMenuPanel = Find<StackPanel>("SettingsMenuPanel");
        _settingsSectionStatusText = Find<TextBlock>("SettingsSectionStatusText");
        _printerSettingsPanel = Find<StackPanel>("PrinterSettingsPanel");
        _printerSettingsStatusText = Find<TextBlock>("PrinterSettingsStatusText");
        _printerOffsetXText = Find<TextBlock>("PrinterOffsetXText");
        _printerOffsetYText = Find<TextBlock>("PrinterOffsetYText");
        _printerScaleText = Find<TextBlock>("PrinterScaleText");
        _printerQualityFastButton = Find<Button>("PrinterQualityFastButton");
        _printerQualityHighButton = Find<Button>("PrinterQualityHighButton");
        _printerCutStandardButton = Find<Button>("PrinterCutStandardButton");
        _printerCutTwoInchButton = Find<Button>("PrinterCutTwoInchButton");
        _scheduleSettingsPanel = Find<StackPanel>("ScheduleSettingsPanel");
        _scheduleEnabledCheckBox = Find<CheckBox>("ScheduleEnabledCheckBox");
        _scheduleShutdownCheckBox = Find<CheckBox>("ScheduleShutdownCheckBox");
        _scheduleStartHourText = Find<TextBlock>("ScheduleStartHourText");
        _scheduleStartMinuteText = Find<TextBlock>("ScheduleStartMinuteText");
        _scheduleEndHourText = Find<TextBlock>("ScheduleEndHourText");
        _scheduleEndMinuteText = Find<TextBlock>("ScheduleEndMinuteText");
        _scheduleStatusText = Find<TextBlock>("ScheduleStatusText");
        _offHoursPanel = Find<Grid>("OffHoursPanel");
        _offHoursScheduleText = Find<TextBlock>("OffHoursScheduleText");
        _cameraSettingsPanel = Find<StackPanel>("CameraSettingsPanel");
        _cameraIsoComboBox = Find<ComboBox>("CameraIsoComboBox");
        _cameraApertureComboBox = Find<ComboBox>("CameraApertureComboBox");
        _cameraShutterComboBox = Find<ComboBox>("CameraShutterComboBox");
        _cameraWhiteBalanceComboBox = Find<ComboBox>("CameraWhiteBalanceComboBox");
        _cameraSettingsStatusText = Find<TextBlock>("CameraSettingsStatusText");
        _systemTimeSettingsPanel = Find<StackPanel>("SystemTimeSettingsPanel");
        _systemTimeDayText = Find<TextBlock>("SystemTimeDayText");
        _systemTimeMonthText = Find<TextBlock>("SystemTimeMonthText");
        _systemTimeYearText = Find<TextBlock>("SystemTimeYearText");
        _systemTimeHourText = Find<TextBlock>("SystemTimeHourText");
        _systemTimeMinuteText = Find<TextBlock>("SystemTimeMinuteText");
        _systemTimeStatusText = Find<TextBlock>("SystemTimeStatusText");

        TransformGroup flyTransforms = new();
        flyTransforms.Children.Add(_flyScale);
        flyTransforms.Children.Add(_flyTranslate);
        _shotFlyCard.RenderTransform = flyTransforms;
        _shotFlyCard.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        _countdownTimer = CreateTimer(TimeSpan.FromSeconds(1), CountdownTimer_OnTick);
        _reviewDelayTimer = CreateTimer(TimeSpan.FromMilliseconds(450), ReviewDelayTimer_OnTick);
        _flyTimer = CreateTimer(TimeSpan.FromMilliseconds(16), FlyTimer_OnTick);
        _finalPreviewTimer = CreateTimer(TimeSpan.FromMilliseconds(650), FinalPreviewTimer_OnTick);
        _printProgressTimer = CreateTimer(TimeSpan.FromMilliseconds(90), PrintProgressTimer_OnTick);
        _printCompletionTimer = CreateTimer(TimeSpan.FromMilliseconds(1200), PrintCompletionTimer_OnTick);
        _livePreviewTimer = CreateTimer(TimeSpan.FromMilliseconds(80), LivePreviewTimer_OnTick);
        _scheduleTimer = CreateTimer(TimeSpan.FromSeconds(15), ScheduleTimer_OnTick);

        _config = _configService.Load();
        _persistentStorageRequired =
            Environment.GetEnvironmentVariable("PHOTOBOOTH_STORAGE_REQUIRED") == "1";
        _outputRootPath = ResolveOutputPath(_config.OutputPath);
        _templatesPath = ResolvePath(_config.TemplatesPath);
        _demoPhotosPath = ResolvePath(_config.DemoPhotosPath);

        bool windowedTestMode =
            Environment.GetEnvironmentVariable("PHOTOBOOTH_WINDOWED") == "1";

        if (windowedTestMode)
        {
            WindowDecorations = WindowDecorations.Full;
            Topmost = true;
        }
        else if (_config.Fullscreen)
        {
            WindowState = WindowState.FullScreen;
        }

        ShowStartupLock();
        _scheduleTimer.Start();
        Opened += async (_, _) => await InitializeHardwareAsync();
    }

    private T Find<T>(string name) where T : Control =>
        this.FindControl<T>(name) ??
        throw new InvalidOperationException($"Элемент интерфейса не найден: {name}");

    private static DispatcherTimer CreateTimer(TimeSpan interval, EventHandler handler)
    {
        DispatcherTimer timer = new() { Interval = interval };
        timer.Tick += handler;
        return timer;
    }

    private void StopWorkflowTimers()
    {
        _countdownTimer.Stop();
        _reviewDelayTimer.Stop();
        _flyTimer.Stop();
        _finalPreviewTimer.Stop();
        _printProgressTimer.Stop();
        _printCompletionTimer.Stop();
        _livePreviewTimer.Stop();
    }

    private async Task InitializeHardwareAsync()
    {
        if (_config.DemoMode)
        {
            _cameraService = new DemoPhotoCaptureService();
            _printerService = new DemoPrinterService();
            _printerStatus = "Деморежим включён в config.json.";
            UpdatePublicHardwareStatus();
            return;
        }

        (GPhotoCameraService? camera, string cameraError) =
            await GPhotoCameraService.TryCreateAsync(_config.GPhotoCommand);
        _cameraService = camera is not null
            ? camera
            : new DemoPhotoCaptureService(cameraError);

        (CupsPrinterService? printer, string printerError) =
            await CupsPrinterService.TryCreateAsync(
                _config.CupsLpCommand,
                _config.CupsLpStatCommand,
                _config.PrinterName,
                _config.PrinterMedia);
        _printerService = printer is not null
            ? printer
            : new DemoPrinterService();
        printer?.Configure(_config.PrinterQuality, _config.PrinterCutMode);
        _printerStatus = printer?.Status ?? $"Демо-печать: {printerError}.";
        UpdatePublicHardwareStatus();
    }

    private void UpdatePublicHardwareStatus()
    {
        UpdateHardwareBadge(
            _homeCameraStatusBadge,
            !_cameraService.IsDemo,
            _cameraService.IsDemo
                ? "Камера не найдена"
                : $"Камера: {_cameraService.DisplayName}");
        UpdateHardwareBadge(
            _homePrinterStatusBadge,
            !_printerService.IsDemo,
            _printerService.IsDemo
                ? "Принтер не найден"
                : $"Принтер: {_printerService.DisplayName}");

        _captureDeviceBadge.IsVisible = true;
        _captureDeviceBadgeText.Text = _cameraService.IsDemo
            ? "КАМЕРА НЕ НАЙДЕНА"
            : _cameraService.DisplayName.ToUpperInvariant();
    }

    private static void UpdateHardwareBadge(
        Border badge,
        bool connected,
        string tooltip)
    {
        badge.Background = Brush.Parse(connected ? "#3A55D9A5" : "#48FF6B7A");
        badge.BorderBrush = Brush.Parse(connected ? "#90BFFFE9" : "#B8FFD6DC");
        ToolTip.SetTip(badge, tooltip);
    }

    private void HidePrimaryPanels()
    {
        _sessionPanel.IsVisible = false;
        _homePanel.IsVisible = false;
        _templatesPanel.IsVisible = false;
        _capturePanel.IsVisible = false;
        _previewPanel.IsVisible = false;
        _printProgressPanel.IsVisible = false;
        _historyPanel.IsVisible = false;
        _offHoursPanel.IsVisible = false;
    }

    private void ShowSessionStartup()
    {
        StopWorkflowTimers();
        HidePrimaryPanels();
        _instructionOverlay.IsVisible = false;
        _settingsOverlay.IsVisible = false;
        _recoverableSession = _sessionManager.LoadActiveSession(_outputRootPath);
        _sessionErrorText.Text = string.Empty;
        _sessionPanel.IsVisible = true;
        _savedSessionsPanel.IsVisible = false;

        if (_recoverableSession is null)
        {
            ShowNewSessionForm();
            return;
        }

        _currentSessionNameText.Text =
            $"{_recoverableSession.Name}\n" +
            $"с {_recoverableSession.StartedAt:dd.MM.yyyy HH:mm}";
        _newSessionPanel.IsVisible = false;
        _currentSessionPanel.IsVisible = true;
    }

    private void ShowStartupLock()
    {
        StopWorkflowTimers();
        HidePrimaryPanels();
        _instructionOverlay.IsVisible = false;
        _isStartupLocked = true;
        ShowPinScreen();
    }

    private void ContinueSessionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_recoverableSession is null)
        {
            ShowNewSessionForm();
            return;
        }

        ActivateSession(_recoverableSession);
    }

    private void ShowNewSessionButton_OnClick(object? sender, RoutedEventArgs e) =>
        ShowNewSessionForm();

    private void ShowNewSessionForm()
    {
        _currentSessionPanel.IsVisible = false;
        _savedSessionsPanel.IsVisible = false;
        _newSessionPanel.IsVisible = true;
        _sessionNameTextBox.Text = string.Empty;
        _sessionErrorText.Text = string.Empty;
        _sessionNameTextBox.Focus();
    }

    private void CreateSessionButton_OnClick(object? sender, RoutedEventArgs e) =>
        CreateAndActivateSession();

    private void SessionNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CreateAndActivateSession();
        }
    }

    private void BuildSessionKeyboard()
    {
        string[] rows =
        [
            "ЙЦУКЕНГШЩЗХЪ",
            "ФЫВАПРОЛДЖЭ",
            "ЯЧСМИТЬБЮ"
        ];

        foreach (string letters in rows)
        {
            StackPanel row = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 6
            };

            foreach (char letter in letters)
            {
                Button key = CreateKeyboardButton(letter.ToString(), 66);
                key.Click += (_, _) => AppendSessionName(letter.ToString());
                row.Children.Add(key);
            }

            _sessionKeyboardPanel.Children.Add(row);
        }

        StackPanel controls = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8
        };

        Button hyphen = CreateKeyboardButton("-", 100);
        hyphen.Click += (_, _) => AppendSessionName("-");
        controls.Children.Add(hyphen);

        Button space = CreateKeyboardButton("Пробел", 500);
        space.Click += (_, _) => AppendSessionName(" ");
        controls.Children.Add(space);

        Button backspace = CreateKeyboardButton("⌫", 145);
        backspace.Click += (_, _) => RemoveLastSessionNameCharacter();
        controls.Children.Add(backspace);

        _sessionKeyboardPanel.Children.Add(controls);
    }

    private static Button CreateKeyboardButton(string text, double width)
    {
        Button button = new()
        {
            Content = text,
            Width = width
        };
        button.Classes.Add("keyboardKey");
        return button;
    }

    private void AppendSessionName(string value)
    {
        string current = _sessionNameTextBox.Text ?? string.Empty;
        if (current.Length + value.Length <= 80)
        {
            _sessionNameTextBox.Text = current + value;
            _sessionNameTextBox.CaretIndex = _sessionNameTextBox.Text.Length;
        }
    }

    private void RemoveLastSessionNameCharacter()
    {
        string current = _sessionNameTextBox.Text ?? string.Empty;
        if (current.Length == 0)
        {
            return;
        }

        _sessionNameTextBox.Text = current[..^1];
        _sessionNameTextBox.CaretIndex = _sessionNameTextBox.Text.Length;
    }

    private void CreateAndActivateSession()
    {
        if (_persistentStorageRequired &&
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("PHOTOBOOTH_DATA_ROOT")))
        {
            _sessionErrorText.Text =
                "Постоянное хранилище PHOTOBOOTH не подключено. " +
                "Перезагрузите будку или перезапишите флешку.";
            return;
        }

        try
        {
            PhotoSession session = _sessionManager.CreateSession(
                _outputRootPath,
                _sessionNameTextBox.Text ?? string.Empty);
            ActivateSession(session);
        }
        catch (Exception exception)
        {
            _sessionErrorText.Text = exception.Message;
        }
    }

    private void ActivateSession(PhotoSession session)
    {
        _sessionManager.SetActiveSession(_outputRootPath, session);
        _activeSession = session;
        _recoverableSession = session;
        _activeSessionText.Text = $"Сессия: {session.Name}";
        ShowHomeScreen();
    }

    private void ShowHomeScreen()
    {
        StopWorkflowTimers();
        HidePrimaryPanels();
        _instructionOverlay.IsVisible = false;
        _settingsOverlay.IsVisible = false;
        _isHistoryPreview = false;
        _homeSubtitleText.Text = "Создавайте яркие воспоминания";
        _homePanel.IsVisible = true;
        EvaluateWorkSchedule();
    }

    private void StartButton_OnClick(object? sender, RoutedEventArgs e) =>
        ShowTemplates();

    private void ShowTemplates()
    {
        IReadOnlyList<TemplateInfo> templates =
            _templateManager.GetTemplates(_templatesPath);
        string singleTemplateError = string.Empty;

        _selectedTemplate = null;
        _selectedDefinition = null;
        _templateButtons.Clear();
        _templatesItemsPanel.Children.Clear();
        _templateContinueButton.IsEnabled = false;
        _selectedTemplateText.Text = string.Empty;

        if (templates.Count == 1 && _activeSession is not null)
        {
            try
            {
                OpenTemplateForCapture(templates[0]);
                return;
            }
            catch (Exception exception)
            {
                singleTemplateError = $"Ошибка макета: {exception.Message}";
            }
        }

        _templatesCountText.Text = $"Макетов: {templates.Count}";
        _templatesEmptyText.IsVisible = templates.Count == 0;
        _templatesEmptyText.Text = templates.Count == 0
            ? $"Шаблоны не найдены\n{_templatesPath}"
            : string.Empty;

        foreach (TemplateInfo template in templates)
        {
            Button card = CreateTemplateCard(template);
            _templateButtons.Add(card);
            _templatesItemsPanel.Children.Add(card);
        }

        if (_templateButtons.Count > 0 && string.IsNullOrEmpty(singleTemplateError))
        {
            SelectTemplateCard(_templateButtons[0]);
        }
        else
        {
            _selectedTemplateText.Text = singleTemplateError;
        }

        HidePrimaryPanels();
        _templatesPanel.IsVisible = true;
    }

    private Button CreateTemplateCard(TemplateInfo template)
    {
        Image preview = new()
        {
            Height = 270,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Stretch = Stretch.Uniform
        };

        if (!string.IsNullOrWhiteSpace(template.PreviewPath) &&
            File.Exists(template.PreviewPath))
        {
            preview.Source = LoadBitmap(template.PreviewPath);
        }

        StackPanel content = new() { Spacing = 8 };
        content.Children.Add(preview);
        content.Children.Add(new TextBlock
        {
            Text = template.PhotoCountText,
            FontSize = 23,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#172238"),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = template.Name,
            FontSize = 16,
            Foreground = Brush.Parse("#65718A"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        Button card = new() { Content = content, Tag = template };
        card.Classes.Add("templateCard");
        card.Click += TemplateCard_OnClick;
        return card;
    }

    private void TemplateCard_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button selectedButton)
        {
            return;
        }

        SelectTemplateCard(selectedButton);
    }

    private void SelectTemplateCard(Button selectedButton)
    {
        if (selectedButton.Tag is not TemplateInfo template)
        {
            return;
        }

        foreach (Button button in _templateButtons)
        {
            button.Classes.Remove("selected");
        }

        selectedButton.Classes.Add("selected");
        _selectedTemplate = template;
        _selectedTemplateText.Text = $"{template.Name} · {template.PhotoCountText}";
        _templateContinueButton.IsEnabled = template.RequiredShotCount > 0;
    }

    private void TemplatesBackButton_OnClick(object? sender, RoutedEventArgs e) =>
        ShowHomeScreen();

    private void TemplateContinueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedTemplate?.JsonPath is null || _activeSession is null)
        {
            return;
        }

        try
        {
            OpenTemplateForCapture(_selectedTemplate);
        }
        catch (Exception exception)
        {
            _selectedTemplateText.Text = $"Ошибка макета: {exception.Message}";
        }
    }

    private void OpenTemplateForCapture(TemplateInfo template)
    {
        if (template.JsonPath is null)
        {
            throw new InvalidOperationException("Файл JSON не найден.");
        }

        TemplateDefinition definition =
            _templateDefinitionService.Load(template.JsonPath);
        ValidateTemplate(template, definition);
        _selectedTemplate = template;
        _selectedDefinition = definition;
        PrepareCapture();
    }

    private static void ValidateTemplate(
        TemplateInfo template,
        TemplateDefinition definition)
    {
        if (definition.Width <= 0 || definition.Height <= 0)
        {
            throw new InvalidOperationException("Не указаны размеры итогового изображения.");
        }

        if (definition.RequiredShotCount <= 0)
        {
            throw new InvalidOperationException("В JSON не указаны позиции фотографий.");
        }

        if (string.IsNullOrWhiteSpace(definition.Overlay))
        {
            throw new InvalidOperationException("В JSON не указана PNG-рамка.");
        }

        string overlayPath = Path.Combine(template.FolderPath, definition.Overlay);
        if (!File.Exists(overlayPath))
        {
            throw new InvalidOperationException($"PNG-рамка не найдена: {definition.Overlay}");
        }

        bool invalidSlot = definition.Photos.Any(slot =>
            slot.Shoot <= 0 ||
            slot.Width <= 0 ||
            slot.Height <= 0 ||
            slot.X < 0 ||
            slot.Y < 0 ||
            slot.X + slot.Width > definition.Width ||
            slot.Y + slot.Height > definition.Height);

        if (invalidSlot)
        {
            throw new InvalidOperationException("Одна из областей фото выходит за границы макета.");
        }
    }

    private void PrepareCapture()
    {
        if (_activeSession is null || _selectedDefinition is null || _selectedTemplate is null)
        {
            return;
        }

        StopWorkflowTimers();
        _captureId = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string originalsPath = Path.Combine(
            _activeSession.FolderPath,
            "Photos",
            _captureId);
        _cameraService.PrepareCapture(
            _demoPhotosPath,
            originalsPath,
            _selectedDefinition.RequiredShotCount);

        _acceptedShots.Clear();
        _currentCapturedPath = string.Empty;
        _acceptedShotsPanel.Children.Clear();
        _acceptedShotsRail.IsVisible = false;
        _currentShotNumber = 1;
        _copyCount = 1;
        UpdateCopyButtons();
        ShowCurrentShotPreview();
        _captureReadyTitleText.Text = "Готовы к съёмке?";
        _captureReadyOverlay.IsVisible = true;
        _captureReadyActions.IsVisible = true;
        _countdownOverlay.IsVisible = false;
        _shotReviewOverlay.IsVisible = false;
        _shotReviewActions.IsVisible = false;
        _shotFlyCard.IsVisible = false;
        _captureFooterText.Text = "Снимки сохраняются в текущей сессии";
        HidePrimaryPanels();
        _capturePanel.IsVisible = true;
        StartLivePreview();
    }

    private void ShowCurrentShotPreview()
    {
        if (_selectedDefinition is null)
        {
            return;
        }

        _captureProgressText.Text =
            $"СЪЁМКА {_currentShotNumber} ИЗ {_selectedDefinition.RequiredShotCount}";
        _currentCapturedPath = string.Empty;
        StartLivePreview();
    }

    private void StartLivePreview()
    {
        _livePreviewTimer.Stop();
        _lastLivePreviewFrame = null;
        if (!_cameraService.IsDemo)
        {
            _livePreviewTimer.Start();
        }
        _ = RefreshLivePreviewAsync();
    }

    private async void LivePreviewTimer_OnTick(object? sender, EventArgs e) =>
        await RefreshLivePreviewAsync();

    private async Task RefreshLivePreviewAsync()
    {
        if (_livePreviewBusy || !_capturePanel.IsVisible)
        {
            return;
        }

        _livePreviewBusy = true;
        try
        {
            byte[]? frame = await _cameraService.CapturePreviewAsync(_currentShotNumber);
            if (frame is not null &&
                !ReferenceEquals(frame, _lastLivePreviewFrame))
            {
                SetLivePreview(frame);
                _lastLivePreviewFrame = frame;
            }
        }
        catch (Exception exception)
        {
            _captureFooterText.Text = $"Live View: {exception.Message}";
        }
        finally
        {
            _livePreviewBusy = false;
        }
    }

    private void StartCaptureButton_OnClick(object? sender, RoutedEventArgs e) =>
        StartCountdown();

    private void StartCountdown()
    {
        if (_selectedDefinition is null)
        {
            return;
        }

        _captureReadyOverlay.IsVisible = false;
        _captureReadyActions.IsVisible = false;
        _shotReviewOverlay.IsVisible = false;
        _shotReviewActions.IsVisible = false;
        _countdownValue = CountdownStart;
        _countdownText.Text = _countdownValue.ToString();
        _countdownCaption.Text = "Смотрите в объектив";
        _countdownOverlay.IsVisible = true;
        StartLivePreview();
        _countdownTimer.Start();
    }

    private async void CountdownTimer_OnTick(object? sender, EventArgs e)
    {
        _countdownValue--;

        if (_countdownValue > 0)
        {
            _countdownText.Text = _countdownValue.ToString();
            return;
        }

        _countdownTimer.Stop();
        _livePreviewTimer.Stop();
        _countdownText.Text = "●";
        _countdownCaption.Text = "Снимаем...";

        try
        {
            _currentCapturedPath =
                await _cameraService.CapturePhotoAsync(_currentShotNumber);
            SetLivePreview(_currentCapturedPath);
            _countdownText.Text = "✓";
            _countdownCaption.Text = "Снимок сделан";
            _reviewDelayTimer.Start();
        }
        catch (Exception exception)
        {
            _countdownOverlay.IsVisible = false;
            _captureFooterText.Text = $"Ошибка камеры: {exception.Message}";
            _captureReadyTitleText.Text = "Попробовать ещё раз";
            _captureReadyOverlay.IsVisible = true;
            _captureReadyActions.IsVisible = true;
            StartLivePreview();
        }
    }

    private void ReviewDelayTimer_OnTick(object? sender, EventArgs e)
    {
        _reviewDelayTimer.Stop();
        _countdownOverlay.IsVisible = false;
        _shotReviewProgressText.Text =
            $"Снимок {_currentShotNumber} готов";
        _shotReviewOverlay.IsVisible = true;
        _shotReviewActions.IsVisible = true;
    }

    private void RetakeCurrentShotButton_OnClick(object? sender, RoutedEventArgs e) =>
        StartCountdown();

    private void AcceptCurrentShotButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentCapturedPath) ||
            !File.Exists(_currentCapturedPath))
        {
            return;
        }

        string acceptedPath = _currentCapturedPath;
        _shotReviewOverlay.IsVisible = false;
        _shotReviewActions.IsVisible = false;
        _shotFlyImage.Source = LoadBitmap(acceptedPath);
        _shotFlyCard.IsVisible = true;
        _shotFlyCard.Opacity = 1;
        _flyScale.ScaleX = 1;
        _flyScale.ScaleY = 1;
        _flyTranslate.X = 0;
        _flyTranslate.Y = 0;
        _flyStartedAt = DateTime.UtcNow;
        _flyTimer.Start();
    }

    private void FlyTimer_OnTick(object? sender, EventArgs e)
    {
        double progress = Math.Clamp(
            (DateTime.UtcNow - _flyStartedAt).TotalMilliseconds / 520d,
            0,
            1);
        double eased = 1 - Math.Pow(1 - progress, 3);
        double targetX = -Math.Max(320, _capturePanel.Bounds.Width * 0.38);
        double targetY = -170 + (_acceptedShots.Count * 88);

        _flyScale.ScaleX = 1 - (0.78 * eased);
        _flyScale.ScaleY = 1 - (0.78 * eased);
        _flyTranslate.X = targetX * eased;
        _flyTranslate.Y = targetY * eased;
        _shotFlyCard.Opacity = 1 - (0.18 * eased);

        if (progress < 1)
        {
            return;
        }

        _flyTimer.Stop();
        _shotFlyCard.IsVisible = false;
        string acceptedPath = _currentCapturedPath;
        _acceptedShots.Add(acceptedPath);
        AddAcceptedThumbnail(acceptedPath, _currentShotNumber);
        AdvanceAfterAcceptedShot();
    }

    private void AddAcceptedThumbnail(string photoPath, int number)
    {
        Grid thumbnailGrid = new();
        thumbnailGrid.Children.Add(new Image
        {
            Source = LoadBitmap(photoPath),
            Stretch = Stretch.UniformToFill
        });

        Border badge = new()
        {
            Background = Brush.Parse("#7B61FF"),
            CornerRadius = new CornerRadius(14),
            Width = 28,
            Height = 28,
            Margin = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = number.ToString(),
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        thumbnailGrid.Children.Add(badge);

        Border thumbnail = new()
        {
            Width = 112,
            Height = 82,
            CornerRadius = new CornerRadius(14),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 10),
            Child = thumbnailGrid
        };

        _acceptedShotsPanel.Children.Add(thumbnail);
        _acceptedShotsRail.IsVisible = true;
    }

    private void AdvanceAfterAcceptedShot()
    {
        if (_selectedDefinition is null)
        {
            return;
        }

        if (_currentShotNumber >= _selectedDefinition.RequiredShotCount)
        {
            _captureFooterText.Text = "Все снимки готовы";
            _finalPreviewTimer.Start();
            return;
        }

        _currentShotNumber++;
        ShowCurrentShotPreview();
        _captureReadyTitleText.Text = $"Снимок {_currentShotNumber}";
        _captureReadyOverlay.IsVisible = true;
        _captureReadyActions.IsVisible = true;
    }

    private void FinalPreviewTimer_OnTick(object? sender, EventArgs e)
    {
        _finalPreviewTimer.Stop();
        ComposeAndShowPreview();
    }

    private void ComposeAndShowPreview()
    {
        if (_activeSession is null ||
            _selectedTemplate is null ||
            _selectedDefinition is null ||
            string.IsNullOrWhiteSpace(_selectedDefinition.Overlay))
        {
            return;
        }

        try
        {
            string overlayPath = Path.Combine(
                _selectedTemplate.FolderPath,
                _selectedDefinition.Overlay);
            _resultPath = Path.Combine(
                _activeSession.FolderPath,
                "Prints",
                $"{_captureId}.png");
            _imageComposer.Compose(
                _selectedDefinition,
                overlayPath,
                _acceptedShots,
                _resultPath);
            _isHistoryPreview = false;
            _resultPreviewImage.Source = LoadBitmap(_resultPath);
            ConfigurePreviewTexts();
            _printButtonText.Text = "Печать";
            HidePrimaryPanels();
            _previewPanel.IsVisible = true;
        }
        catch (Exception exception)
        {
            _captureFooterText.Text = $"Не удалось собрать макет: {exception.Message}";
            _captureReadyTitleText.Text = "Вернуться к макетам";
            _captureReadyOverlay.IsVisible = true;
            _captureReadyActions.IsVisible = true;
        }
    }

    private void CaptureBackButton_OnClick(object? sender, RoutedEventArgs e)
    {
        StopWorkflowTimers();
        _ = _cameraService.StopPreviewAsync();

        if (_templateManager.GetTemplates(_templatesPath).Count <= 1)
        {
            ShowHomeScreen();
            return;
        }

        ShowTemplates();
    }

    private void CopyCountButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            int.TryParse(button.Tag?.ToString(), out int copies) &&
            copies is >= 1 and <= 3)
        {
            _copyCount = copies;
            UpdateCopyButtons();
        }
    }

    private void UpdateCopyButtons()
    {
        UpdateSelectedClass(_copyOneButton, _copyCount == 1);
        UpdateSelectedClass(_copyTwoButton, _copyCount == 2);
        UpdateSelectedClass(_copyThreeButton, _copyCount == 3);
    }

    private static void UpdateSelectedClass(Button button, bool selected)
    {
        if (selected)
        {
            button.Classes.Add("selected");
        }
        else
        {
            button.Classes.Remove("selected");
        }
    }

    private void PreviewHomeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isHistoryPreview)
        {
            ShowPrintHistory();
            return;
        }

        ShowHomeScreen();
    }

    private void PrintButton_OnClick(object? sender, RoutedEventArgs e)
    {
        PrintResult result;
        string? adjustedPath = null;
        try
        {
            adjustedPath = _printerCalibrationService.CreateAdjustedCopy(
                _resultPath,
                _config.PrinterOffsetX,
                _config.PrinterOffsetY,
                _config.PrinterScalePercent);
            result = _printerService.Print(adjustedPath, _copyCount);
        }
        catch (Exception exception)
        {
            result = new PrintResult(false, $"Не удалось подготовить печать: {exception.Message}");
        }
        finally
        {
            DeleteTemporaryFile(adjustedPath);
        }

        if (!result.Success)
        {
            _printButtonText.Text = result.Message;
            return;
        }

        RecordSuccessfulPrint();

        _printProgress = 0;
        _printProgressStatusText.Text = "Подготовка к печати";
        _printProgressPercentText.Text = "0%";
        _printProgressDetailsText.Text = _printerService.IsDemo
            ? $"Копий: {_copyCount}"
            : $"{_printerService.DisplayName} · копий: {_copyCount}";
        _printNextPhotoButtonText.Text =
            _isHistoryPreview ? "К истории" : "Следующее фото";
        _printNextPhotoButton.IsVisible = false;
        HidePrimaryPanels();
        _printProgressPanel.IsVisible = true;
        _printProgressTimer.Start();
    }

    private void PrintProgressTimer_OnTick(object? sender, EventArgs e)
    {
        _printProgress = Math.Min(100, _printProgress + 4);
        _printProgressPercentText.Text = $"{_printProgress}%";

        if (_printProgress < 35)
        {
            _printProgressStatusText.Text = "Подготовка файла";
        }
        else if (_printProgress < 75)
        {
            _printProgressStatusText.Text = "Передача в принтер";
        }
        else if (_printProgress < 100)
        {
            _printProgressStatusText.Text = "Печать фотографий";
        }
        else
        {
            _printProgressTimer.Stop();
            _printProgressStatusText.Text = "Печать завершена";
            _printProgressDetailsText.Text =
                "Файл сохранён. Готовимся к следующей фотосессии";
            _printNextPhotoButton.IsVisible = false;
            _printCompletionTimer.Start();
        }
    }

    private void PrintCompletionTimer_OnTick(object? sender, EventArgs e)
    {
        _printCompletionTimer.Stop();
        ShowHomeScreen();
    }

    private void PrintNextPhotoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isHistoryPreview)
        {
            ShowPrintHistory();
            return;
        }

        ShowHomeScreen();
    }

    private void ShowPrintHistory()
    {
        IReadOnlyList<PhotoSession> sessions =
            _sessionManager.ListSessions(_outputRootPath);

        if (_historyFilterSessionPath is null)
        {
            _historyFilterSessionPath = sessions.FirstOrDefault(session =>
                _activeSession is not null &&
                session.FolderPath.Equals(
                    _activeSession.FolderPath,
                    StringComparison.OrdinalIgnoreCase))?.FolderPath;
        }

        BuildHistorySessionFilters(sessions);
        RenderPrintHistory(sessions);

        HidePrimaryPanels();
        _settingsOverlay.IsVisible = false;
        _historyPanel.IsVisible = true;
    }

    private void BuildHistorySessionFilters(IReadOnlyList<PhotoSession> sessions)
    {
        _historySessionFilterPanel.Children.Clear();
        _historySessionFilterPanel.Children.Add(CreateHistoryFilterButton(
            "Все",
            string.Empty,
            string.IsNullOrEmpty(_historyFilterSessionPath)));
        foreach (PhotoSession session in sessions)
        {
            _historySessionFilterPanel.Children.Add(CreateHistoryFilterButton(
                session.Name,
                session.FolderPath,
                session.FolderPath.Equals(
                    _historyFilterSessionPath,
                    StringComparison.OrdinalIgnoreCase)));
        }
    }

    private Button CreateHistoryFilterButton(
        string title,
        string sessionPath,
        bool selected)
    {
        Button button = new()
        {
            Content = title,
            Tag = sessionPath,
            MinWidth = 130,
            MinHeight = 48,
            Padding = new Thickness(22, 8)
        };
        button.Classes.Add("historyFilter");
        if (selected)
        {
            button.Classes.Add("selected");
        }

        button.Click += HistorySessionFilterButton_OnClick;
        return button;
    }

    private void HistorySessionFilterButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string sessionPath)
        {
            return;
        }

        _historyFilterSessionPath = string.IsNullOrEmpty(sessionPath)
            ? string.Empty
            : sessionPath;
        IReadOnlyList<PhotoSession> sessions =
            _sessionManager.ListSessions(_outputRootPath);
        BuildHistorySessionFilters(sessions);
        RenderPrintHistory(sessions);
    }

    private void RenderPrintHistory(IReadOnlyList<PhotoSession> sessions)
    {
        IReadOnlyList<PhotoSession> visibleSessions =
            string.IsNullOrEmpty(_historyFilterSessionPath)
                ? sessions
                : sessions.Where(session => session.FolderPath.Equals(
                        _historyFilterSessionPath,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

        _historyItemsPanel.Children.Clear();
        foreach (PhotoSession session in visibleSessions)
        {
            string printsPath = Path.Combine(session.FolderPath, "Prints");
            IReadOnlyList<string> files = Directory.Exists(printsPath)
                ? Directory.EnumerateFiles(printsPath, "*.png")
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToList()
                : [];

            if (files.Count == 0)
            {
                _historyItemsPanel.Children.Add(CreateEmptyHistoryCard(session));
                continue;
            }

            foreach (string file in files)
            {
                _historyItemsPanel.Children.Add(CreateHistoryCard(file, session));
            }
        }

        int totalCopies = visibleSessions.Sum(session =>
            _printAuditService.GetTotalCopies(session.FolderPath));
        _historySessionNameText.Text = visibleSessions.Count == 1
            ? $"{visibleSessions[0].Name} · Отпечатано: {totalCopies} копий"
            : $"Все сессии: {sessions.Count} · Отпечатано: {totalCopies} копий";
        _noHistoryPanel.IsVisible = sessions.Count == 0;
    }

    private Button CreateHistoryCard(string filePath, PhotoSession session)
    {
        StackPanel content = new() { Spacing = 10 };
        content.Children.Add(new Border
        {
            Height = 230,
            Padding = new Thickness(5),
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#DDE2EC"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = new Image
            {
                Source = LoadBitmap(filePath),
                Stretch = Stretch.Uniform
            }
        });
        content.Children.Add(new TextBlock
        {
            Text = session.Name,
            Foreground = Brush.Parse("#172238"),
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            TextAlignment = TextAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Отпечатано копий: " +
                   _printAuditService.GetCopiesForImage(session.FolderPath, filePath),
            Foreground = Brush.Parse("#7B61FF"),
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextAlignment = TextAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = File.GetLastWriteTime(filePath).ToString("dd.MM.yyyy HH:mm:ss"),
            Foreground = Brush.Parse("#68758C"),
            FontSize = 14,
            TextAlignment = TextAlignment.Center
        });

        Button card = new() { Content = content, Tag = filePath };
        card.Classes.Add("historyCard");
        card.Click += HistoryItemButton_OnClick;
        return card;
    }

    private void RecordSuccessfulPrint()
    {
        try
        {
            PhotoSession? session = _sessionManager
                .ListSessions(_outputRootPath)
                .FirstOrDefault(candidate => _resultPath.StartsWith(
                    candidate.FolderPath + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
            if (session is not null)
            {
                _printAuditService.Record(
                    session.FolderPath,
                    _resultPath,
                    _copyCount);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"{DateTime.Now:O} Print audit was not saved: {exception.Message}");
        }
    }

    private static Border CreateEmptyHistoryCard(PhotoSession session)
    {
        StackPanel content = new()
        {
            Spacing = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(new TextBlock
        {
            Text = session.Name,
            Foreground = Brush.Parse("#172238"),
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = "Печати не было",
            Foreground = Brush.Parse("#68758C"),
            FontSize = 16,
            TextAlignment = TextAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = session.StartedAt.ToString("dd.MM.yyyy HH:mm"),
            Foreground = Brush.Parse("#68758C"),
            FontSize = 14,
            TextAlignment = TextAlignment.Center
        });

        return new Border
        {
            Width = 280,
            Height = 180,
            Margin = new Thickness(10),
            Padding = new Thickness(22),
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#DDE2EC"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = content
        };
    }

    private void HistoryItemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string filePath } || !File.Exists(filePath))
        {
            return;
        }

        _resultPath = filePath;
        _isHistoryPreview = true;
        _copyCount = 1;
        UpdateCopyButtons();
        _resultPreviewImage.Source = LoadBitmap(filePath);
        _printButtonText.Text = "Печать ещё раз";
        ConfigurePreviewTexts();
        HidePrimaryPanels();
        _previewPanel.IsVisible = true;
    }

    private void ConfigurePreviewTexts()
    {
        _previewBackButtonText.Text =
            _isHistoryPreview ? "‹  К истории" : "‹  На главную";
        _previewTitleText.Text =
            _isHistoryPreview ? "Повторная печать" : "Предпросмотр";
        _previewSubtitleText.Text = _isHistoryPreview
            ? "Выберите количество копий"
            : "Проверьте фото перед печатью";
    }

    private void HistoryBackButton_OnClick(object? sender, RoutedEventArgs e) =>
        ShowSettingsMenu();

    private void InstructionButton_OnClick(object? sender, RoutedEventArgs e) =>
        _instructionOverlay.IsVisible = true;

    private void InstructionCloseButton_OnClick(object? sender, RoutedEventArgs e) =>
        _instructionOverlay.IsVisible = false;

    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _isStartupLocked = false;
        ShowPinScreen();
    }

    private void ShowPinScreen()
    {
        _pinEntry = string.Empty;
        _settingsPinDisplay.Text = "○  ○  ○  ○";
        _settingsPinErrorText.Text = string.Empty;
        _settingsSectionStatusText.Text = string.Empty;
        _settingsPinTitleText.Text = _isStartupLocked
            ? "Фотобудка заблокирована"
            : "Настройки";
        _settingsPinSubtitleText.Text = _isStartupLocked
            ? "Введите код для начала работы"
            : "Введите код доступа";
        _settingsPinCancelButton.IsVisible = !_isStartupLocked;
        _settingsPinPanel.IsVisible = true;
        _settingsMenuPanel.IsVisible = false;
        _printerSettingsPanel.IsVisible = false;
        _scheduleSettingsPanel.IsVisible = false;
        _cameraSettingsPanel.IsVisible = false;
        _systemTimeSettingsPanel.IsVisible = false;
        _settingsOverlay.IsVisible = true;
    }

    private void ShowSettingsMenu()
    {
        StopWorkflowTimers();
        HidePrimaryPanels();
        _instructionOverlay.IsVisible = false;
        _settingsPinPanel.IsVisible = false;
        _printerSettingsPanel.IsVisible = false;
        _scheduleSettingsPanel.IsVisible = false;
        _cameraSettingsPanel.IsVisible = false;
        _systemTimeSettingsPanel.IsVisible = false;
        _settingsSectionStatusText.Text = string.Empty;
        _settingsMenuPanel.IsVisible = true;
        _settingsOverlay.IsVisible = true;
    }

    private void SettingsPinDigitButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || _pinEntry.Length >= 4)
        {
            return;
        }

        _pinEntry += button.Tag?.ToString();
        UpdatePinDisplay();

        if (_pinEntry.Length != 4)
        {
            return;
        }

        if (_pinEntry == SettingsPin)
        {
            _settingsPinErrorText.Text = string.Empty;
            if (_isStartupLocked)
            {
                _isStartupLocked = false;
                ShowSessionStartup();
                return;
            }

            _settingsPinPanel.IsVisible = false;
            _settingsMenuPanel.IsVisible = true;
            return;
        }

        _settingsPinErrorText.Text = "Неверный код";
        _pinEntry = string.Empty;
        UpdatePinDisplay();
    }

    private void SettingsPinBackspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_pinEntry.Length > 0)
        {
            _pinEntry = _pinEntry[..^1];
        }

        _settingsPinErrorText.Text = string.Empty;
        UpdatePinDisplay();
    }

    private void SettingsPinClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _pinEntry = string.Empty;
        _settingsPinErrorText.Text = string.Empty;
        UpdatePinDisplay();
    }

    private void UpdatePinDisplay()
    {
        _settingsPinDisplay.Text = string.Join(
            "  ",
            Enumerable.Range(0, 4).Select(index =>
                index < _pinEntry.Length ? "●" : "○"));
    }

    private async void SettingsSectionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        switch (button.Tag?.ToString())
        {
            case "history":
                ShowPrintHistory();
                break;
            case "camera":
                await ShowCameraSettingsAsync();
                break;
            case "printer":
                ShowPrinterSettings();
                break;
            case "calibration":
                await CalibrateTouchscreenAsync();
                break;
            case "schedule":
                ShowScheduleSettings();
                break;
            case "time":
                ShowSystemTimeSettings();
                break;
            case "restart":
                await RestartComputerAsync();
                break;
            case "shutdown":
                await ShutdownComputerAsync();
                break;
            case "session":
                _settingsOverlay.IsVisible = false;
                ShowSessionSelection();
                break;
        }
    }

    private void ShowPrinterSettings()
    {
        _printerOffsetXDraft = Math.Clamp(_config.PrinterOffsetX, -20, 20);
        _printerOffsetYDraft = Math.Clamp(_config.PrinterOffsetY, -20, 20);
        _printerScaleDraft = Math.Clamp(_config.PrinterScalePercent, 98, 102);
        _printerQualityDraft = _config.PrinterQuality == "High" ? "High" : "Fast";
        _printerCutModeDraft = _config.PrinterCutMode == "TwoInch"
            ? "TwoInch"
            : "Standard";

        _settingsPinPanel.IsVisible = false;
        _settingsMenuPanel.IsVisible = false;
        _cameraSettingsPanel.IsVisible = false;
        _scheduleSettingsPanel.IsVisible = false;
        _systemTimeSettingsPanel.IsVisible = false;
        _printerSettingsPanel.IsVisible = true;
        _settingsOverlay.IsVisible = true;
        UpdatePrinterSettingsControls();

        if (_printerService is CupsPrinterService cups)
        {
            _printerQualityFastButton.IsEnabled = cups.SupportsQualitySelection;
            _printerQualityHighButton.IsEnabled = cups.SupportsQualitySelection;
            _printerCutStandardButton.IsEnabled = cups.SupportsTwoInchCut;
            _printerCutTwoInchButton.IsEnabled = cups.SupportsTwoInchCut;
            _printerSettingsStatusText.Text =
                $"{cups.DisplayName}. {cups.DriverOptionsSummary}";
        }
        else
        {
            _printerQualityFastButton.IsEnabled = false;
            _printerQualityHighButton.IsEnabled = false;
            _printerCutStandardButton.IsEnabled = false;
            _printerCutTwoInchButton.IsEnabled = false;
            _printerSettingsStatusText.Text =
                $"Принтер не подключён. Геометрию можно настроить и сохранить заранее.";
        }
    }

    private void PrinterAdjustmentButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag)
        {
            return;
        }

        string[] parts = tag.Split(':');
        if (parts.Length != 2 ||
            !double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double amount))
        {
            return;
        }

        switch (parts[0])
        {
            case "x":
                _printerOffsetXDraft = Math.Clamp(
                    _printerOffsetXDraft + (int)amount,
                    -20,
                    20);
                break;
            case "y":
                _printerOffsetYDraft = Math.Clamp(
                    _printerOffsetYDraft + (int)amount,
                    -20,
                    20);
                break;
            case "scale":
                _printerScaleDraft = Math.Round(
                    Math.Clamp(_printerScaleDraft + amount, 98, 102),
                    1);
                break;
        }

        UpdatePrinterSettingsControls();
    }

    private void PrinterOptionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag)
        {
            return;
        }

        if (tag is "Fast" or "High")
        {
            _printerQualityDraft = tag;
        }
        else if (tag is "Standard" or "TwoInch")
        {
            _printerCutModeDraft = tag;
        }

        UpdatePrinterSettingsControls();
    }

    private void UpdatePrinterSettingsControls()
    {
        _printerOffsetXText.Text = $"{_printerOffsetXDraft:+0;-0;0} px";
        _printerOffsetYText.Text = $"{_printerOffsetYDraft:+0;-0;0} px";
        _printerScaleText.Text = $"{_printerScaleDraft:0.0}%";
        UpdateSelectedClass(
            _printerQualityFastButton,
            _printerQualityDraft == "Fast");
        UpdateSelectedClass(
            _printerQualityHighButton,
            _printerQualityDraft == "High");
        UpdateSelectedClass(
            _printerCutStandardButton,
            _printerCutModeDraft == "Standard");
        UpdateSelectedClass(
            _printerCutTwoInchButton,
            _printerCutModeDraft == "TwoInch");
    }

    private void PrinterSaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _config.PrinterOffsetX = _printerOffsetXDraft;
        _config.PrinterOffsetY = _printerOffsetYDraft;
        _config.PrinterScalePercent = _printerScaleDraft;
        _config.PrinterQuality = _printerQualityDraft;
        _config.PrinterCutMode = _printerCutModeDraft;
        try
        {
            _configService.Save(_config);
            if (_printerService is CupsPrinterService cups)
            {
                cups.Configure(_config.PrinterQuality, _config.PrinterCutMode);
            }

            _printerSettingsStatusText.Text = "Настройки печати сохранены.";
        }
        catch (Exception exception)
        {
            _printerSettingsStatusText.Text =
                $"Не удалось сохранить настройки: {exception.Message}";
        }
    }

    private void PrinterResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _printerOffsetXDraft = 0;
        _printerOffsetYDraft = 0;
        _printerScaleDraft = 100;
        _printerQualityDraft = "Fast";
        _printerCutModeDraft = "Standard";
        UpdatePrinterSettingsControls();
        _printerSettingsStatusText.Text =
            "Значения сброшены. Нажмите «Сохранить», чтобы применить.";
    }

    private void PrinterTestButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string? testPagePath = null;
        string? adjustedPath = null;
        try
        {
            testPagePath = _printerCalibrationService.CreateTestPage();
            adjustedPath = _printerCalibrationService.CreateAdjustedCopy(
                testPagePath,
                _printerOffsetXDraft,
                _printerOffsetYDraft,
                _printerScaleDraft);
            if (_printerService is CupsPrinterService cups)
            {
                cups.Configure(_printerQualityDraft, _printerCutModeDraft);
            }

            PrintResult result = _printerService.Print(adjustedPath, 1);
            _printerSettingsStatusText.Text = result.Success
                ? "Тестовая страница отправлена в принтер."
                : result.Message;
        }
        catch (Exception exception)
        {
            _printerSettingsStatusText.Text =
                $"Тестовая печать не выполнена: {exception.Message}";
        }
        finally
        {
            if (_printerService is CupsPrinterService cups)
            {
                cups.Configure(_config.PrinterQuality, _config.PrinterCutMode);
            }

            DeleteTemporaryFile(adjustedPath);
            DeleteTemporaryFile(testPagePath);
        }
    }

    private void PrinterSettingsBackButton_OnClick(object? sender, RoutedEventArgs e) =>
        ShowSettingsMenu();

    private void ShowScheduleSettings()
    {
        _scheduleStartHourDraft = NormalizeHour(_config.WorkStartHour);
        _scheduleStartMinuteDraft = NormalizeMinute(_config.WorkStartMinute);
        _scheduleEndHourDraft = NormalizeHour(_config.WorkEndHour);
        _scheduleEndMinuteDraft = NormalizeMinute(_config.WorkEndMinute);
        _scheduleEnabledCheckBox.IsChecked = _config.WorkScheduleEnabled;
        _scheduleShutdownCheckBox.IsChecked = _config.ShutdownOutsideWorkHours;
        _scheduleStatusText.Text = string.Empty;
        UpdateScheduleControls();

        _settingsPinPanel.IsVisible = false;
        _settingsMenuPanel.IsVisible = false;
        _printerSettingsPanel.IsVisible = false;
        _cameraSettingsPanel.IsVisible = false;
        _systemTimeSettingsPanel.IsVisible = false;
        _scheduleSettingsPanel.IsVisible = true;
        _settingsOverlay.IsVisible = true;
    }

    private void ScheduleStepButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string command)
        {
            return;
        }

        string[] parts = command.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out int delta))
        {
            return;
        }

        switch (parts[0])
        {
            case "start-hour":
                _scheduleStartHourDraft = Wrap(_scheduleStartHourDraft + delta, 24);
                break;
            case "start-minute":
                _scheduleStartMinuteDraft = Wrap(_scheduleStartMinuteDraft + delta, 60);
                break;
            case "end-hour":
                _scheduleEndHourDraft = Wrap(_scheduleEndHourDraft + delta, 24);
                break;
            case "end-minute":
                _scheduleEndMinuteDraft = Wrap(_scheduleEndMinuteDraft + delta, 60);
                break;
        }

        _scheduleStatusText.Text = string.Empty;
        UpdateScheduleControls();
    }

    private void UpdateScheduleControls()
    {
        _scheduleStartHourText.Text = _scheduleStartHourDraft.ToString("00");
        _scheduleStartMinuteText.Text = _scheduleStartMinuteDraft.ToString("00");
        _scheduleEndHourText.Text = _scheduleEndHourDraft.ToString("00");
        _scheduleEndMinuteText.Text = _scheduleEndMinuteDraft.ToString("00");
    }

    private void ScheduleSaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _config.WorkScheduleEnabled = _scheduleEnabledCheckBox.IsChecked == true;
        _config.WorkStartHour = _scheduleStartHourDraft;
        _config.WorkStartMinute = _scheduleStartMinuteDraft;
        _config.WorkEndHour = _scheduleEndHourDraft;
        _config.WorkEndMinute = _scheduleEndMinuteDraft;
        _config.ShutdownOutsideWorkHours =
            _scheduleShutdownCheckBox.IsChecked == true;

        try
        {
            _configService.Save(_config);
            _schedulePowerOffRequested = false;
            _scheduleStatusText.Text = _config.WorkScheduleEnabled
                ? "Расписание сохранено. Пароль при включении останется активным."
                : "Расписание отключено.";
        }
        catch (Exception exception)
        {
            _scheduleStatusText.Text =
                $"Не удалось сохранить расписание: {exception.Message}";
        }
    }

    private void ScheduleSettingsBackButton_OnClick(object? sender, RoutedEventArgs e) =>
        ShowSettingsMenu();

    private void ScheduleTimer_OnTick(object? sender, EventArgs e) =>
        EvaluateWorkSchedule();

    private void EvaluateWorkSchedule()
    {
        if (!_config.WorkScheduleEnabled)
        {
            _schedulePowerOffRequested = false;
            _offHoursPanel.IsVisible = false;
            return;
        }

        if (_settingsOverlay.IsVisible)
        {
            return;
        }

        if (IsWithinWorkHours(DateTime.Now.TimeOfDay))
        {
            _schedulePowerOffRequested = false;
            if (_offHoursPanel.IsVisible)
            {
                _offHoursPanel.IsVisible = false;
                _homePanel.IsVisible = true;
            }

            return;
        }

        if (!_homePanel.IsVisible && !_offHoursPanel.IsVisible)
        {
            return;
        }

        HidePrimaryPanels();
        _offHoursScheduleText.Text =
            $"Время работы: {_config.WorkStartHour:00}:{_config.WorkStartMinute:00}–" +
            $"{_config.WorkEndHour:00}:{_config.WorkEndMinute:00}";
        _offHoursPanel.IsVisible = true;

        if (_config.ShutdownOutsideWorkHours && !_schedulePowerOffRequested)
        {
            _schedulePowerOffRequested = true;
            _ = RequestScheduledPowerOffAsync();
        }
    }

    private async Task RequestScheduledPowerOffAsync()
    {
        const string helper = "/usr/local/sbin/photobooth-schedule-poweroff";
        if (!CommandRunner.Exists(helper))
        {
            _offHoursScheduleText.Text +=
                "\nАвтовключение доступно только в образе фотобудки Linux.";
            return;
        }

        _offHoursScheduleText.Text +=
            $"\nВыключение. Следующий запуск в " +
            $"{_config.WorkStartHour:00}:{_config.WorkStartMinute:00}.";
        CommandResult result = await CommandRunner.RunAsync(
            "sudo",
            [
                "-n",
                helper,
                NormalizeHour(_config.WorkStartHour).ToString(),
                NormalizeMinute(_config.WorkStartMinute).ToString()
            ],
            TimeSpan.FromSeconds(30));
        if (!result.Success)
        {
            _offHoursScheduleText.Text +=
                $"\nКомпьютер оставлен включённым: {result.CombinedOutput}";
        }
    }

    private bool IsWithinWorkHours(TimeSpan currentTime)
    {
        TimeSpan start = new(
            NormalizeHour(_config.WorkStartHour),
            NormalizeMinute(_config.WorkStartMinute),
            0);
        TimeSpan end = new(
            NormalizeHour(_config.WorkEndHour),
            NormalizeMinute(_config.WorkEndMinute),
            0);
        if (start == end)
        {
            return true;
        }

        return start < end
            ? currentTime >= start && currentTime < end
            : currentTime >= start || currentTime < end;
    }

    private static int NormalizeHour(int value) =>
        value is >= 0 and <= 23 ? value : 0;

    private static int NormalizeMinute(int value) =>
        value is >= 0 and <= 59 ? value : 0;

    private static int Wrap(int value, int maximum) =>
        (value % maximum + maximum) % maximum;

    private void OffHoursSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _offHoursPanel.IsVisible = false;
        SettingsButton_OnClick(sender, e);
    }

    private async Task ShowCameraSettingsAsync()
    {
        _settingsPinPanel.IsVisible = false;
        _settingsMenuPanel.IsVisible = false;
        _printerSettingsPanel.IsVisible = false;
        _scheduleSettingsPanel.IsVisible = false;
        _systemTimeSettingsPanel.IsVisible = false;
        _cameraSettingsPanel.IsVisible = true;
        _settingsOverlay.IsVisible = true;
        _cameraSettingsStatusText.Text = "Читаем настройки Canon...";
        SetCameraControlsEnabled(false);

        if (_cameraService is not GPhotoCameraService camera)
        {
            _cameraSettingsStatusText.Text =
                "Canon не подключён. Настройки камеры недоступны.";
            return;
        }

        try
        {
            IReadOnlyList<CameraSettingDefinition> settings =
                await camera.GetSettingsAsync();
            _cameraSettingDefinitions.Clear();
            foreach (CameraSettingDefinition setting in settings)
            {
                _cameraSettingDefinitions[setting.Key] = setting;
            }

            SetCameraControlsEnabled(true);
            PopulateCameraCombo(_cameraIsoComboBox, "iso");
            PopulateCameraCombo(_cameraApertureComboBox, "aperture");
            PopulateCameraCombo(_cameraShutterComboBox, "shutterspeed");
            PopulateCameraCombo(_cameraWhiteBalanceComboBox, "whitebalance");

            string[] errors = settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Error))
                .Select(setting => setting.Error)
                .ToArray();
            _cameraSettingsStatusText.Text = errors.Length == 0
                ? $"{camera.DisplayName}. Выберите значения и нажмите «Применить»."
                : string.Join(" ", errors);
        }
        catch (Exception exception)
        {
            _cameraSettingsStatusText.Text =
                $"Не удалось прочитать Canon: {exception.Message}";
        }
    }

    private void PopulateCameraCombo(ComboBox comboBox, string key)
    {
        if (!_cameraSettingDefinitions.TryGetValue(key, out CameraSettingDefinition? setting) ||
            setting.Choices.Count == 0)
        {
            comboBox.ItemsSource = new[] { "Недоступно" };
            comboBox.SelectedIndex = 0;
            comboBox.IsEnabled = false;
            return;
        }

        comboBox.ItemsSource = setting.Choices;
        comboBox.SelectedItem = setting.Choices.FirstOrDefault(value =>
            value.Equals(setting.CurrentValue, StringComparison.OrdinalIgnoreCase)) ??
            setting.Choices[0];
        comboBox.IsEnabled = true;
    }

    private void SetCameraControlsEnabled(bool enabled)
    {
        _cameraIsoComboBox.IsEnabled = enabled;
        _cameraApertureComboBox.IsEnabled = enabled;
        _cameraShutterComboBox.IsEnabled = enabled;
        _cameraWhiteBalanceComboBox.IsEnabled = enabled;
    }

    private async void CameraApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_cameraService is not GPhotoCameraService camera)
        {
            return;
        }

        Dictionary<string, string> changes = [];
        AddCameraChange(changes, "iso", _cameraIsoComboBox);
        AddCameraChange(changes, "aperture", _cameraApertureComboBox);
        AddCameraChange(changes, "shutterspeed", _cameraShutterComboBox);
        AddCameraChange(changes, "whitebalance", _cameraWhiteBalanceComboBox);
        if (changes.Count == 0)
        {
            _cameraSettingsStatusText.Text = "Изменений нет.";
            return;
        }

        _cameraSettingsStatusText.Text = "Применяем настройки Canon...";
        SetCameraControlsEnabled(false);
        try
        {
            IReadOnlyList<string> errors = await camera.ApplySettingsAsync(changes);
            _cameraSettingsStatusText.Text = errors.Count == 0
                ? "Настройки Canon применены."
                : string.Join(" ", errors);
            if (errors.Count == 0)
            {
                foreach ((string key, string value) in changes)
                {
                    CameraSettingDefinition current = _cameraSettingDefinitions[key];
                    _cameraSettingDefinitions[key] = current with { CurrentValue = value };
                }
            }
        }
        catch (Exception exception)
        {
            _cameraSettingsStatusText.Text =
                $"Canon не принял настройки: {exception.Message}";
        }
        finally
        {
            RestoreCameraControlAvailability();
        }
    }

    private void AddCameraChange(
        Dictionary<string, string> changes,
        string key,
        ComboBox comboBox)
    {
        if (!_cameraSettingDefinitions.TryGetValue(key, out CameraSettingDefinition? setting) ||
            comboBox.SelectedItem is not string selected ||
            selected.Equals(setting.CurrentValue, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        changes[key] = selected;
    }

    private void RestoreCameraControlAvailability()
    {
        _cameraIsoComboBox.IsEnabled = HasCameraChoices("iso");
        _cameraApertureComboBox.IsEnabled = HasCameraChoices("aperture");
        _cameraShutterComboBox.IsEnabled = HasCameraChoices("shutterspeed");
        _cameraWhiteBalanceComboBox.IsEnabled = HasCameraChoices("whitebalance");
    }

    private bool HasCameraChoices(string key) =>
        _cameraSettingDefinitions.TryGetValue(key, out CameraSettingDefinition? setting) &&
        setting.Choices.Count > 0;

    private async void CameraRefreshButton_OnClick(object? sender, RoutedEventArgs e) =>
        await ShowCameraSettingsAsync();

    private void CameraSettingsBackButton_OnClick(object? sender, RoutedEventArgs e) =>
        ShowSettingsMenu();

    private void ShowSystemTimeSettings()
    {
        _systemTimeDraft = DateTime.Now;
        UpdateSystemTimeControls();
        _systemTimeStatusText.Text =
            $"Текущее системное время · часовой пояс Москва (UTC+3)";
        _settingsPinPanel.IsVisible = false;
        _settingsMenuPanel.IsVisible = false;
        _printerSettingsPanel.IsVisible = false;
        _scheduleSettingsPanel.IsVisible = false;
        _cameraSettingsPanel.IsVisible = false;
        _systemTimeSettingsPanel.IsVisible = true;
        _settingsOverlay.IsVisible = true;
    }

    private void SystemTimeStepButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string command)
        {
            return;
        }

        string[] parts = command.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out int delta))
        {
            return;
        }

        try
        {
            _systemTimeDraft = parts[0] switch
            {
                "day" => _systemTimeDraft.AddDays(delta),
                "month" => _systemTimeDraft.AddMonths(delta),
                "year" => _systemTimeDraft.AddYears(delta),
                "hour" => _systemTimeDraft.AddHours(delta),
                "minute" => _systemTimeDraft.AddMinutes(delta),
                _ => _systemTimeDraft
            };
            if (_systemTimeDraft.Year is < 2024 or > 2099)
            {
                _systemTimeDraft = DateTime.Now;
            }

            _systemTimeStatusText.Text = string.Empty;
            UpdateSystemTimeControls();
        }
        catch (ArgumentOutOfRangeException)
        {
            _systemTimeStatusText.Text = "Дата находится вне допустимого диапазона.";
        }
    }

    private void UpdateSystemTimeControls()
    {
        _systemTimeDayText.Text = _systemTimeDraft.Day.ToString("00");
        _systemTimeMonthText.Text = _systemTimeDraft.Month.ToString("00");
        _systemTimeYearText.Text = _systemTimeDraft.Year.ToString("0000");
        _systemTimeHourText.Text = _systemTimeDraft.Hour.ToString("00");
        _systemTimeMinuteText.Text = _systemTimeDraft.Minute.ToString("00");
    }

    private async void SystemTimeSaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        const string helper = "/usr/local/sbin/photobooth-set-time";
        if (!CommandRunner.Exists(helper))
        {
            _systemTimeStatusText.Text =
                "Установка времени доступна только в образе фотобудки Linux.";
            return;
        }

        _systemTimeStatusText.Text = "Устанавливаем системное время...";
        CommandResult result = await CommandRunner.RunAsync(
            "sudo",
            [
                "-n",
                helper,
                _systemTimeDraft.Year.ToString(),
                _systemTimeDraft.Month.ToString(),
                _systemTimeDraft.Day.ToString(),
                _systemTimeDraft.Hour.ToString(),
                _systemTimeDraft.Minute.ToString()
            ],
            TimeSpan.FromSeconds(15));
        _systemTimeStatusText.Text = result.Success
            ? result.CombinedOutput
            : $"Время не изменено: {result.CombinedOutput}";
        if (result.Success)
        {
            _systemTimeDraft = DateTime.Now;
            UpdateSystemTimeControls();
        }
    }

    private void SystemTimeBackButton_OnClick(object? sender, RoutedEventArgs e) =>
        ShowSettingsMenu();

    private static void DeleteTemporaryFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void ShowSessionSelection()
    {
        StopWorkflowTimers();
        HidePrimaryPanels();
        _settingsOverlay.IsVisible = false;
        _currentSessionPanel.IsVisible = false;
        _newSessionPanel.IsVisible = false;
        _savedSessionsListPanel.Children.Clear();

        IReadOnlyList<PhotoSession> sessions =
            _sessionManager.ListSessions(_outputRootPath);
        foreach (PhotoSession session in sessions)
        {
            StackPanel content = new() { Spacing = 3 };
            content.Children.Add(new TextBlock
            {
                Text = session.Name,
                FontSize = 21,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center
            });
            content.Children.Add(new TextBlock
            {
                Text = session.StartedAt.ToString("dd.MM.yyyy HH:mm"),
                FontSize = 15,
                Foreground = Brush.Parse("#D8FFFFFF"),
                TextAlignment = TextAlignment.Center
            });

            Button button = new()
            {
                Content = content,
                Tag = session,
                MinHeight = 76
            };
            button.Classes.Add("glass");
            button.Click += SavedSessionButton_OnClick;
            _savedSessionsListPanel.Children.Add(button);
        }

        _savedSessionsPanel.IsVisible = true;
        _sessionPanel.IsVisible = true;
    }

    private void SavedSessionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PhotoSession session })
        {
            ActivateSession(session);
        }
    }

    private void SettingsCloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_isStartupLocked)
        {
            ShowHomeScreen();
        }
    }

    private async Task CalibrateTouchscreenAsync()
    {
        const string command = "/usr/local/bin/photobooth-touch-calibrate";
        if (!CommandRunner.Exists(command))
        {
            _settingsSectionStatusText.Text =
                "Мастер калибровки доступен только в образе фотобудки Linux.";
            return;
        }

        _settingsSectionStatusText.Text =
            "Коснитесь четырёх крестиков на системном экране калибровки...";
        CommandResult result = await CommandRunner.RunAsync(
            command,
            [],
            TimeSpan.FromMinutes(3));
        _settingsSectionStatusText.Text = result.Success
            ? result.CombinedOutput
            : $"Калибровка не сохранена: {result.CombinedOutput}";
    }

    private async Task RestartComputerAsync()
    {
        _settingsSectionStatusText.Text = "Перезагружаем компьютер...";

        CommandResult result = await CommandRunner.RunAsync(
            "systemctl",
            ["reboot"],
            TimeSpan.FromSeconds(5));

        if (!result.Success && CommandRunner.Exists("sudo"))
        {
            result = await CommandRunner.RunAsync(
                "sudo",
                ["-n", "systemctl", "reboot"],
                TimeSpan.FromSeconds(5));
        }

        if (!result.Success)
        {
            _settingsSectionStatusText.Text =
                $"Не удалось перезагрузить: {result.CombinedOutput}";
        }
    }

    private async Task ShutdownComputerAsync()
    {
        _settingsSectionStatusText.Text = "Выключаем фотобудку...";

        CommandResult result = await CommandRunner.RunAsync(
            "systemctl",
            ["poweroff"],
            TimeSpan.FromSeconds(5));

        if (!result.Success && CommandRunner.Exists("sudo"))
        {
            result = await CommandRunner.RunAsync(
                "sudo",
                ["-n", "systemctl", "poweroff"],
                TimeSpan.FromSeconds(5));
        }

        if (!result.Success)
        {
            _settingsSectionStatusText.Text =
                $"Не удалось выключить будку: {result.CombinedOutput}";
        }
    }

    private void SetLivePreview(string path)
    {
        Bitmap bitmap = LoadBitmap(path);
        if (_livePreviewImage.Source is IDisposable previous)
        {
            previous.Dispose();
        }

        _livePreviewImage.Source = bitmap;
    }

    private void SetLivePreview(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, writable: false);
        Bitmap bitmap = new(stream);
        if (_livePreviewImage.Source is IDisposable previous)
        {
            previous.Dispose();
        }

        _livePreviewImage.Source = bitmap;
    }

    private static Bitmap LoadBitmap(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return new Bitmap(stream);
    }

    private static string ResolvePath(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);
    }

    private static string ResolveOutputPath(string configuredPath)
    {
        string? dataRoot = Environment.GetEnvironmentVariable("PHOTOBOOTH_DATA_ROOT");
        return string.IsNullOrWhiteSpace(dataRoot)
            ? ResolvePath(configuredPath)
            : Path.Combine(dataRoot, "Output");
    }
}
