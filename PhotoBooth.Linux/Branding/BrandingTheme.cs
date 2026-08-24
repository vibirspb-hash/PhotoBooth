using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PhotoBooth.Linux.Branding;

internal static class BrandingTheme
{
    private static readonly Dictionary<string, string> DefaultColors = new(StringComparer.Ordinal)
    {
        ["window"] = "#5F72E9",
        ["textOnColor"] = "#FFFFFFFF",
        ["textOnColorMuted"] = "#D8FFFFFF",
        ["textDark"] = "#FF172238",
        ["textDarkMuted"] = "#FF6B758A",
        ["warningText"] = "#FFFFFFD8",
        ["accent"] = "#FF7B61FF",
        ["accentDark"] = "#FF6F55E8",
        ["glass"] = "#20FFFFFF",
        ["glassStrong"] = "#42FFFFFF",
        ["glassOpaque"] = "#E8FFFFFF",
        ["glassBorder"] = "#BFFFFFFF",
        ["glassBorderStrong"] = "#B8FFFFFF",
        ["glassBorderMedium"] = "#90FFFFFF",
        ["glassBorderLight"] = "#70FFFFFF",
        ["glassBorderSoft"] = "#8AFFFFFF",
        ["glassBadge"] = "#28FFFFFF",
        ["glassSubtle"] = "#24FFFFFF",
        ["glassPanel"] = "#25FFFFFF",
        ["glassFaint"] = "#1AFFFFFF",
        ["glassLine"] = "#55FFFFFF",
        ["iconBorder"] = "#C8FFFFFF",
        ["sessionBorder"] = "#80FFFFFF",
        ["field"] = "#EFFFFFFF",
        ["fieldBorder"] = "#D8FFFFFF",
        ["cardBorder"] = "#FFDDE2EC",
        ["controlBorder"] = "#FFD6DCE8",
        ["primaryBorder"] = "#A8FFFFFF",
        ["selection"] = "#887B61FF",
        ["surfaceSoft"] = "#FFF7F9FC",
        ["inkStrong"] = "#FF141B2A",
        ["textDarkSecondary"] = "#FF65718A",
        ["overlay"] = "#D0101525",
        ["overlaySoft"] = "#B8101525",
        ["overlayPanel"] = "#B0202740",
        ["overlayDeep"] = "#260C1732",
        ["panelDark"] = "#F02B3150",
        ["panelDarkSoft"] = "#EE2B3150",
        ["blueGlowStart"] = "#805AA7FF",
        ["blueGlowEnd"] = "#005AA7FF",
        ["pinkGlowStart"] = "#78FF6EC7",
        ["pinkGlowEnd"] = "#00FF6EC7",
        ["homeGradient1"] = "#B05AA7FF",
        ["homeGradient2"] = "#B0B16CFF",
        ["homeGradient3"] = "#B0FF6EC7",
        ["homeGradient4"] = "#B0FFB278",
        ["captureGradient1"] = "#FFBED8FF",
        ["captureGradient2"] = "#FFD2C9FF",
        ["captureGradient3"] = "#FFE6CEFF",
        ["captureGradient4"] = "#FFFFD3EA",
        ["captureGradient5"] = "#FFFFE3C5",
        ["captureText"] = "#FF536078",
        ["captureBackground"] = "#FF101525",
        ["captureOverlay"] = "#220A1020",
        ["capturePanel1"] = "#A0182038",
        ["capturePanel2"] = "#B0182038",
        ["capturePanel3"] = "#88182038",
        ["capturePanel4"] = "#C0182038",
        ["previewBorder"] = "#FFE0E5EF",
        ["previewText"] = "#FF59657B",
        ["accentSurface"] = "#FFECE9FF",
        ["textOnColorSoft"] = "#E5FFFFFF",
        ["textOnColorBright"] = "#E8FFFFFF",
        ["homeCard"] = "#24FFFFFF",
        ["homeCardBorder"] = "#B8FFFFFF",
        ["homeSettings"] = "#18FFFFFF",
        ["homeInstruction"] = "#B8FFFFFF",
        ["backgroundGradient1"] = "#FF5AA7FF",
        ["backgroundGradient2"] = "#FF7B61FF",
        ["backgroundGradient3"] = "#FFB16CFF",
        ["backgroundGradient4"] = "#FFFF6EC7",
        ["backgroundGradient5"] = "#FFFFB278",
        ["primaryGradient1"] = "#FF9B70FF",
        ["primaryGradient2"] = "#FFC378E8",
        ["primaryGradient3"] = "#FFF37EC9"
    };

    private static readonly Dictionary<string, string> DefaultTexts = new(StringComparer.Ordinal)
    {
        ["brand"] = "P H O T O   S T A R  ✦",
        ["homeBrand"] = "P H O T O  S T A R✦",
        ["continueWorkTitle"] = "Продолжить работу",
        ["unfinishedSession"] = "Найдена незавершенная сессия",
        ["continueSession"] = "Продолжить сессию",
        ["startNewSession"] = "＋  Начать новую сессию",
        ["newSessionTitle"] = "Новая сессия",
        ["newSessionSubtitle"] = "Создайте папку для фотографий мероприятия",
        ["eventName"] = "Название мероприятия",
        ["createSession"] = "Создать сессию",
        ["chooseSession"] = "Выберите сессию",
        ["savedSessionsSubtitle"] = "Сохранённые сесии с этой флешки",
        ["settings"] = "⚙  Настройки",
        ["startPhotoSession"] = "Начать фотосессию",
        ["homeSubtitle"] = "Создавайте яркие воспоминания",
        ["instructions"] = "ⓘ  Инструкция по использованию",
        ["back"] = "‹  Назад",
        ["templateTitle"] = "Выбор макета",
        ["templateSubtitle"] = "Выберите подходящий макет для печати",
        ["continue"] = "Продолжить  →",
        ["cameraName"] = "CANON EOS",
        ["capturePrepare"] = "Проверьте кадр и приготовьтесь",
        ["lookAtCamera"] = "Смотрите в объектив",
        ["takePhoto"] = "Снять фото",
        ["retake"] = "↻  Переснять",
        ["approve"] = "Нравится  ✓",
        ["home"] = "‹  На главную",
        ["previewTitle"] = "Предпросмотр",
        ["previewSubtitle"] = "Проверьте фото перед печатью",
        ["copies"] = "Количество копий",
        ["print"] = "Печать",
        ["printing"] = "Печать фотографий",
        ["doNotDisconnect"] = "Пожалуйста, не отключайте устройство",
        ["preparingJob"] = "Подготовка задания",
        ["nextPhoto"] = "Следующее фото",
        ["history"] = "История печати",
        ["historyEmpty"] = "История пока пуста",
        ["historyEmptySubtitle"] = "Здесь появятся готовые макеты всех сесий",
        ["closedTitle"] = "Фотобудка сейчас не работает",
        ["helpTitle"] = "Как пользоваться фотобудкой",
        ["helpSteps"] = "1. Нажмите «Начать фотосессию».\n2. Выберите рамку.\n3. Встаньте перед камерой и нажмите «Снять фото».\n4. Подтвердите каждый удачный снимок.\n5. Выберите количество копий и нажмите «Печать».",
        ["understood"] = "Понятно",
        ["settingsTitle"] = "Настройки",
        ["enterPin"] = "Введите код доступа",
        ["controlPanel"] = "Панель управления",
        ["chooseSection"] = "Выберите раздел",
        ["cameraSettings"] = "Настройки Canon",
        ["iso"] = "ISO",
        ["aperture"] = "Диафрагма",
        ["shutter"] = "Выдержка",
        ["whiteBalance"] = "Баланс белого",
        ["printCalibration"] = "Калибровка печати",
        ["offsetX"] = "Смещение по X",
        ["offsetY"] = "Смещение по Y",
        ["scale"] = "Масштаб",
        ["quality"] = "Качество",
        ["cut"] = "Рез",
        ["scheduleTitle"] = "Таймер работы",
        ["scheduleSubtitle"] = "Автоматическое включение и выключение будки",
        ["scheduleStart"] = "Начало работы",
        ["scheduleEnd"] = "Завершение работы",
        ["dateTime"] = "Дата и время",
        ["day"] = "День",
        ["month"] = "Месяц",
        ["year"] = "Год",
        ["hour"] = "Час",
        ["minutes"] = "Минуты",
        ["hardwareClockHint"] = "Время будет записано и в аппаратные часы компьютера",
        ["enableSchedule"] = "Включить расписание работы",
        ["shutdownOnSchedule"] = "Выключать после окончания и включать к началу",
        ["menuHistory"] = "История печати",
        ["menuCamera"] = "Камера",
        ["menuPrinter"] = "Принтер",
        ["menuCalibration"] = "Калибровка экрана",
        ["menuSchedule"] = "Таймер работы",
        ["menuTime"] = "Дата и время",
        ["menuRestart"] = "Перезагрузка",
        ["menuShutdown"] = "Выключить будку",
        ["menuSession"] = "Сменить сессию"
    };

    public static Bitmap? LogoImage { get; private set; }
    public static Bitmap? BackgroundImage { get; private set; }

    public static void Load(Application application)
    {
        Dictionary<string, string> colors = new(DefaultColors, StringComparer.Ordinal);
        Dictionary<string, string> texts = new(DefaultTexts, StringComparer.Ordinal);
        Dictionary<string, double> numbers = DefaultNumbers();
        string fontFamily = "Inter, Arial, sans-serif";
        string logoFile = string.Empty;
        string backgroundFile = string.Empty;

        string brandingDirectory = Path.Combine(AppContext.BaseDirectory, "Branding");
        string themePath = Environment.GetEnvironmentVariable("PHOTOBOOTH_THEME_PATH")
            ?? Path.Combine(brandingDirectory, "theme.json");

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(themePath));
            JsonElement root = document.RootElement;
            ReadStrings(root, "colors", colors);
            ReadStrings(root, "texts", texts);
            ReadNumbers(root, "sizes", numbers);
            if (root.TryGetProperty("font", out JsonElement font) &&
                font.TryGetProperty("family", out JsonElement family) &&
                family.ValueKind == JsonValueKind.String)
            {
                fontFamily = family.GetString() ?? fontFamily;
            }
            if (root.TryGetProperty("branding", out JsonElement branding))
            {
                logoFile = ReadString(branding, "logo", string.Empty);
                backgroundFile = ReadString(branding, "background", string.Empty);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Theme was not loaded; built-in design is used: {exception.Message}");
        }

        foreach ((string name, string value) in colors)
        {
            Color color;
            try
            {
                color = Color.Parse(value);
            }
            catch
            {
                color = Color.Parse(DefaultColors[name]);
            }
            application.Resources[$"Theme.{name}.Color"] = color;
            application.Resources[$"Theme.{name}.Brush"] = new SolidColorBrush(color);
        }

        foreach ((string name, string value) in texts)
        {
            application.Resources[$"Text.{name}"] = value;
        }

        foreach ((string name, double value) in numbers)
        {
            application.Resources[$"Theme.{name}"] = name.StartsWith("corner", StringComparison.Ordinal)
                ? (object)new CornerRadius(value)
                : value;
        }

        application.Resources["Theme.FontFamily"] = new FontFamily(fontFamily);
        application.Resources["Theme.ButtonPadding"] = new Thickness(numbers["buttonPaddingX"], numbers["buttonPaddingY"]);
        application.Resources["Theme.ButtonCornerRadius"] = new CornerRadius(numbers["buttonCornerRadius"]);
        application.Resources["Theme.FieldCornerRadius"] = new CornerRadius(numbers["fieldCornerRadius"]);
        application.Resources["Theme.CardCornerRadius"] = new CornerRadius(numbers["cardCornerRadius"]);
        application.Resources["Theme.ScreenMargin"] = new Thickness(numbers["screenMarginX"], numbers["screenMarginY"]);
        application.Resources["Theme.SessionMargin"] = new Thickness(numbers["sessionMarginX"], numbers["sessionMarginY"]);
        application.Resources["Theme.MainPanelPadding"] = new Thickness(numbers["panelPaddingX"], numbers["panelPaddingY"]);
        application.Resources["Theme.FieldPadding"] = new Thickness(numbers["fieldPaddingX"], numbers["fieldPaddingY"]);
        application.Resources["Theme.KeyboardPadding"] = new Thickness(numbers["keyboardPaddingX"], numbers["keyboardPaddingY"]);
        application.Resources["Theme.OptionPadding"] = new Thickness(numbers["optionPaddingX"], numbers["optionPaddingY"]);
        application.Resources["Theme.ComboPadding"] = new Thickness(numbers["comboPaddingX"], numbers["comboPaddingY"]);
        application.Resources["Theme.TemplateCardMargin"] = new Thickness(numbers["templateCardMargin"]);
        application.Resources["Theme.CopyOptionMargin"] = new Thickness(numbers["copyOptionMargin"]);
        application.Resources["Theme.HistoryCardMargin"] = new Thickness(numbers["historyCardMargin"]);
        application.Resources["Theme.PinPadding"] = new Thickness(numbers["pinPadding"]);
        application.Resources["Theme.TemplateCardPadding"] = new Thickness(numbers["templateCardPadding"]);
        application.Resources["Theme.HistoryCardPadding"] = new Thickness(numbers["historyCardPadding"]);

        LogoImage = LoadBrandingImage(brandingDirectory, logoFile);
        BackgroundImage = LoadBrandingImage(brandingDirectory, backgroundFile);
    }

    private static Dictionary<string, double> DefaultNumbers() => new(StringComparer.Ordinal)
    {
        ["buttonFontSize"] = 20,
        ["buttonMinHeight"] = 58,
        ["buttonPaddingX"] = 28,
        ["buttonPaddingY"] = 13,
        ["buttonCornerRadius"] = 29,
        ["fieldFontSize"] = 21,
        ["fieldHeight"] = 64,
        ["fieldPaddingX"] = 22,
        ["fieldPaddingY"] = 14,
        ["fieldCornerRadius"] = 22,
        ["actionButtonHeight"] = 92,
        ["actionButtonMinWidth"] = 320,
        ["actionButtonFontSize"] = 25,
        ["cardCornerRadius"] = 30,
        ["titleFontSize"] = 36,
        ["subtitleFontSize"] = 18,
        ["backgroundImageOpacity"] = 1,
        ["logoMaxHeight"] = 54,
        ["keyboardHeight"] = 58,
        ["keyboardMinWidth"] = 62,
        ["keyboardPaddingX"] = 12,
        ["keyboardPaddingY"] = 6,
        ["pinWidth"] = 124,
        ["pinHeight"] = 68,
        ["pinPadding"] = 8,
        ["optionMinHeight"] = 52,
        ["optionPaddingX"] = 18,
        ["optionPaddingY"] = 9,
        ["comboMinHeight"] = 58,
        ["comboPaddingX"] = 18,
        ["comboPaddingY"] = 10,
        ["templateCardWidth"] = 220,
        ["templateCardHeight"] = 382,
        ["templateCardMargin"] = 10,
        ["templateCardPadding"] = 18,
        ["copyOptionWidth"] = 106,
        ["copyOptionHeight"] = 58,
        ["copyOptionMargin"] = 7,
        ["historyCardWidth"] = 230,
        ["historyCardHeight"] = 350,
        ["historyCardMargin"] = 10,
        ["historyCardPadding"] = 12,
        ["homeSettingsWidth"] = 230,
        ["homeSettingsHeight"] = 78,
        ["homeCardWidth"] = 800,
        ["homeCardMinHeight"] = 500,
        ["homeOrbSize"] = 230,
        ["homeCameraIconSize"] = 92,
        ["font15"] = 15,
        ["font16"] = 16,
        ["font17"] = 17,
        ["font18"] = 18,
        ["font19"] = 19,
        ["font20"] = 20,
        ["font21"] = 21,
        ["font22"] = 22,
        ["font23"] = 23,
        ["font24"] = 24,
        ["font25"] = 25,
        ["font27"] = 27,
        ["font32"] = 32,
        ["font34"] = 34,
        ["font36"] = 36,
        ["font38"] = 38,
        ["font40"] = 40,
        ["font42"] = 42,
        ["font44"] = 44,
        ["font50"] = 50,
        ["font58"] = 58,
        ["font70"] = 70,
        ["font72"] = 72,
        ["font128"] = 128,
        ["corner12"] = 12,
        ["corner18"] = 18,
        ["corner20"] = 20,
        ["corner22"] = 22,
        ["corner24"] = 24,
        ["corner26"] = 26,
        ["corner27"] = 27,
        ["corner28"] = 28,
        ["corner29"] = 29,
        ["corner30"] = 30,
        ["corner32"] = 32,
        ["corner36"] = 36,
        ["corner46"] = 46,
        ["corner54"] = 54,
        ["corner60"] = 60,
        ["corner95"] = 95,
        ["corner195"] = 195,
        ["corner215"] = 215,
        ["screenMarginX"] = 44,
        ["screenMarginY"] = 36,
        ["sessionMarginX"] = 42,
        ["sessionMarginY"] = 32,
        ["panelPaddingX"] = 36,
        ["panelPaddingY"] = 22
    };

    private static void ReadStrings(JsonElement root, string sectionName, Dictionary<string, string> destination)
    {
        if (!root.TryGetProperty(sectionName, out JsonElement section) || section.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        foreach (JsonProperty property in section.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String && destination.ContainsKey(property.Name))
            {
                destination[property.Name] = property.Value.GetString() ?? destination[property.Name];
            }
        }
    }

    private static void ReadNumbers(JsonElement root, string sectionName, Dictionary<string, double> destination)
    {
        if (!root.TryGetProperty(sectionName, out JsonElement section) || section.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        foreach (JsonProperty property in section.EnumerateObject())
        {
            if (destination.ContainsKey(property.Name) && property.Value.TryGetDouble(out double value) &&
                double.IsFinite(value) && value >= 0 && value <= 2000)
            {
                destination[property.Name] = value;
            }
        }
    }

    private static string ReadString(JsonElement section, string name, string fallback) =>
        section.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static Bitmap? LoadBrandingImage(string brandingDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            string root = Path.GetFullPath(brandingDirectory) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(brandingDirectory, relativePath));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
            {
                return null;
            }
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }
}
