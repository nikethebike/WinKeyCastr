using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace WinKeyCastr.Localization;

public enum AppLanguage
{
    System,
    English,
    Russian,
}

/// <summary>
/// UI strings in English and Russian. Bindings go through the indexer (<c>{l:Tr Key}</c>), so switching
/// language updates open windows immediately.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    // Not initialised from English here: static initialisers run in source order, so the
    // dictionaries below do not exist yet when Instance is constructed.
    private Dictionary<string, string>? _table;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>"en" or "ru": the language actually in use.</summary>
    public string Language { get; private set; } = "en";

    public string this[string key] =>
        (_table ?? English).TryGetValue(key, out var value) ? value
        : English.TryGetValue(key, out var fallback) ? fallback
        : key;

    public static string T(string key) => Instance[key];

    public void SetLanguage(AppLanguage language)
    {
        bool russian = language switch
        {
            AppLanguage.Russian => true,
            AppLanguage.English => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru",
        };
        var code = russian ? "ru" : "en";
        if (code == Language)
            return;
        Language = code;
        _table = russian ? Russian : English;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    private static readonly Dictionary<string, string> English = new()
    {
        ["Pane_General"] = "General",
        ["Pane_Display"] = "Display",

        ["DisplayIcon"] = "Display KeyCastr icon:",
        ["Icon_Tray"] = "In the Notification Area",
        ["Icon_Taskbar"] = "In the Taskbar",
        ["Icon_Both"] = "In the Notification Area and Taskbar",
        ["ToggleCapturing"] = "Toggle capturing:",
        ["ModifierKeys"] = "Modifier keys:",
        ["Modifiers_Symbols"] = "Symbols  ⌃ ⌥ ⇧ ⊞",
        ["Modifiers_Names"] = "Names  Ctrl Alt Shift Win",
        ["Language"] = "Language:",
        ["Language_System"] = "System default",
        ["ShowPrefsAtLaunch"] = "Show preferences at launch",

        ["SelectedVisualizer"] = "Selected Visualizer:",
        ["Vis_Default"] = "Default",
        ["Vis_Svelte"] = "Svelte",
        ["Vis_Minimal"] = "Minimal",
        ["MouseEvents"] = "Display Mouse Events:",
        ["Mouse_None"] = "None",
        ["Mouse_Pointer"] = "With Mouse Pointer",
        ["Mouse_Visualizer"] = "With Current Visualizer",
        ["Mouse_Both"] = "With Pointer and Visualizer",
        ["BezelMaterial"] = "Bezel Material:",
        ["Acrylic"] = "Acrylic (blur what's behind)",

        ["DisplayMode"] = "Display Mode:",
        ["Mode_Command"] = "Command Keys Only",
        ["Mode_Modified"] = "All Modified Keys",
        ["Mode_All"] = "All Keys",
        ["ApplyModifiers"] = "Apply Modifiers",
        ["ApplyModifiers_Tip"] = "Show the character a chord produces (e.g. €) instead of the modifiers and key cap",
        ["FontSize"] = "Font Size:",
        ["FontSize_Caption"] = "Size of the keystrokes on the bezel",
        ["Tiny"] = "Tiny",
        ["Huge"] = "Huge",
        ["LineBreak"] = "Line Break Delay:",
        ["LineBreak_Caption"] = "Length of time before the line breaks",
        ["Short"] = "Short",
        ["Long"] = "Long",
        ["Linger"] = "Linger Time:",
        ["Linger_Caption"] = "Length of time before the text fades away",
        ["FadeDuration"] = "Fade Duration:",
        ["FadeDuration_Caption"] = "Duration of the fade effect",
        ["Instant"] = "Instant",
        ["FastIsh"] = "Fast-ish",
        ["BezelColor"] = "Bezel Color:",
        ["TextColor"] = "Text Color:",

        ["DisplayAll"] = "Display All Keystrokes:",

        ["DisplayedKeys"] = "Displayed Keys:",
        ["Minimal_Modifiers"] = "Modifier keys (⌃ ⌥ ⇧ ⊞)",
        ["Minimal_Special"] = "Special & navigation keys",
        ["Minimal_All"] = "All keys",
        ["Anchor"] = "Anchor:",
        ["Anchor_Caption"] = "Direction the bezel expands away from",
        ["Left"] = "Left",
        ["Right"] = "Right",
        ["BezelSize"] = "Bezel Size:",
        ["BezelSize_Caption"] = "Size of the bezel frame",
        ["Small"] = "Small",
        ["Large"] = "Large",
        ["BorderRadius"] = "Border Radius:",
        ["BorderRadius_Caption"] = "Roundness of the bezel edges",
        ["Sharp"] = "Sharp",
        ["Rounded"] = "Rounded",
        ["TextShadowColor"] = "Text Shadow Color:",

        ["Menu_About"] = "About KeyCastr",
        ["Menu_Preferences"] = "Preferences…",
        ["Menu_Start"] = "Start Casting",
        ["Menu_Stop"] = "Stop Casting",
        ["Menu_Quit"] = "Quit KeyCastr",
        ["Tray_Casting"] = "KeyCastr — casting",
        ["Tray_NotCasting"] = "KeyCastr — not casting",

        ["Recorder_Type"] = "Type shortcut",
        ["Recorder_Empty"] = "Click to record shortcut",
        ["Recorder_Clear"] = "Clear shortcut",

        ["About_Title"] = "About KeyCastr",
        ["About_Version"] = "Version {0}",
        ["About_Port"] = "A Windows port of KeyCastr.",

        ["HookError"] = "KeyCastr could not install its keyboard and mouse hooks, so keystrokes cannot be displayed.",
    };

    private static readonly Dictionary<string, string> Russian = new()
    {
        ["Pane_General"] = "Основные",
        ["Pane_Display"] = "Отображение",

        ["DisplayIcon"] = "Значок KeyCastr:",
        ["Icon_Tray"] = "В области уведомлений",
        ["Icon_Taskbar"] = "На панели задач",
        ["Icon_Both"] = "В области уведомлений и на панели задач",
        ["ToggleCapturing"] = "Вкл./выкл. показ:",
        ["ModifierKeys"] = "Модификаторы:",
        ["Modifiers_Symbols"] = "Символы  ⌃ ⌥ ⇧ ⊞",
        ["Modifiers_Names"] = "Названия  Ctrl Alt Shift Win",
        ["Language"] = "Язык:",
        ["Language_System"] = "Как в системе",
        ["ShowPrefsAtLaunch"] = "Показывать настройки при запуске",

        ["SelectedVisualizer"] = "Визуализатор:",
        ["Vis_Default"] = "Стандартный",
        ["Vis_Svelte"] = "Svelte",
        ["Vis_Minimal"] = "Минимальный",
        ["MouseEvents"] = "Показывать клики мыши:",
        ["Mouse_None"] = "Не показывать",
        ["Mouse_Pointer"] = "Кольцом у указателя",
        ["Mouse_Visualizer"] = "В визуализаторе",
        ["Mouse_Both"] = "У указателя и в визуализаторе",
        ["BezelMaterial"] = "Материал плашек:",
        ["Acrylic"] = "Акрил (размытие фона)",

        ["DisplayMode"] = "Что показывать:",
        ["Mode_Command"] = "Только сочетания-команды",
        ["Mode_Modified"] = "Все клавиши с модификаторами",
        ["Mode_All"] = "Все клавиши",
        ["ApplyModifiers"] = "Применять модификаторы",
        ["ApplyModifiers_Tip"] = "Показывать символ, который даёт сочетание (например, €), вместо модификаторов и клавиши",
        ["FontSize"] = "Размер шрифта:",
        ["FontSize_Caption"] = "Размер текста на плашке",
        ["Tiny"] = "Крошечный",
        ["Huge"] = "Огромный",
        ["LineBreak"] = "Задержка переноса:",
        ["LineBreak_Caption"] = "Пауза, после которой начинается новая строка",
        ["Short"] = "Коротко",
        ["Long"] = "Долго",
        ["Linger"] = "Время показа:",
        ["Linger_Caption"] = "Сколько текст виден до начала затухания",
        ["FadeDuration"] = "Затухание:",
        ["FadeDuration_Caption"] = "Длительность исчезновения",
        ["Instant"] = "Мгновенно",
        ["FastIsh"] = "Плавно",
        ["BezelColor"] = "Цвет плашки:",
        ["TextColor"] = "Цвет текста:",

        ["DisplayAll"] = "Показывать все нажатия:",

        ["DisplayedKeys"] = "Показывать:",
        ["Minimal_Modifiers"] = "Модификаторы (⌃ ⌥ ⇧ ⊞)",
        ["Minimal_Special"] = "Специальные и навигационные клавиши",
        ["Minimal_All"] = "Все клавиши",
        ["Anchor"] = "Привязка:",
        ["Anchor_Caption"] = "Край, от которого растёт плашка",
        ["Left"] = "Слева",
        ["Right"] = "Справа",
        ["BezelSize"] = "Размер плашки:",
        ["BezelSize_Caption"] = "Размер рамки плашки",
        ["Small"] = "Маленький",
        ["Large"] = "Большой",
        ["BorderRadius"] = "Скругление:",
        ["BorderRadius_Caption"] = "Насколько скруглены углы плашки",
        ["Sharp"] = "Острые",
        ["Rounded"] = "Круглые",
        ["TextShadowColor"] = "Цвет тени текста:",

        ["Menu_About"] = "О программе KeyCastr",
        ["Menu_Preferences"] = "Настройки…",
        ["Menu_Start"] = "Начать показ",
        ["Menu_Stop"] = "Остановить показ",
        ["Menu_Quit"] = "Выйти из KeyCastr",
        ["Tray_Casting"] = "KeyCastr — показ включён",
        ["Tray_NotCasting"] = "KeyCastr — показ выключен",

        ["Recorder_Type"] = "Нажмите сочетание",
        ["Recorder_Empty"] = "Нажмите, чтобы задать",
        ["Recorder_Clear"] = "Очистить сочетание",

        ["About_Title"] = "О программе KeyCastr",
        ["About_Version"] = "Версия {0}",
        ["About_Port"] = "Порт KeyCastr для Windows.",

        ["HookError"] = "KeyCastr не удалось установить перехват клавиатуры и мыши, поэтому нажатия не будут отображаться.",
    };
}

/// <summary><c>Text="{l:Tr Key}"</c> — a live binding to a localized string.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}

/// <summary>Visualizer name → localized display name; the second binding re-evaluates on language change.</summary>
public sealed class VisualizerNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length > 0 && values[0] is string name ? Loc.T("Vis_" + name) : "";

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
