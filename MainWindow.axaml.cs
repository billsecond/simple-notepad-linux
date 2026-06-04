using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Notepad;

public partial class MainWindow : Window
{
    private const string AppName = "Notepad";

    private string? _path;                 // null => Untitled
    private Encoding _encoding = new UTF8Encoding(false);
    private bool _dirty;
    private bool _loading;
    private bool _confirmedClose;

    private FindReplaceWindow? _findWindow;
    private string _lastSearch = "";
    private bool _lastMatchCase;
    private bool _lastDown = true;

    private const double DefaultFontSize = 14;
    private double _baseFontSize = DefaultFontSize;   // the 100% size
    private int _zoom = 100;

    public MainWindow() : this(null) { }

    public MainWindow(string? startupFile)
    {
        InitializeComponent();

        Editor.PropertyChanged += Editor_PropertyChanged;
        Closing += MainWindow_Closing;

        // Ctrl +/-/0 zoom: handle on the tunnel so it works regardless of how
        // the platform reports the +/- keys, and before the editor sees them.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        ApplySavedSettings();

        if (!string.IsNullOrEmpty(startupFile) && File.Exists(startupFile))
            LoadFile(startupFile!);
        else
            UpdateTitle();

        UpdatePosition();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Focus the editor once the window is actually shown; focusing during
        // construction does not stick.
        Editor.Focus();
        Editor.CaretIndex = 0;
    }

    private static Control Check() => new TextBlock { Text = "✓" };

    private void ApplySavedSettings()
    {
        var s = Settings.Current;

        if (!string.IsNullOrWhiteSpace(s.FontFamily))
            Editor.FontFamily = new FontFamily(s.FontFamily);
        _baseFontSize = s.FontSize > 0 ? s.FontSize : DefaultFontSize;

        _zoom = Math.Clamp(s.Zoom, 10, 500);
        Editor.FontSize = _baseFontSize * _zoom / 100.0;
        ZoomText.Text = _zoom + "%";

        SetWordWrap(s.WordWrap, persist: false);

        StatusBar.IsVisible = s.StatusBar;
        StatusBarItem.Icon = s.StatusBar ? Check() : null;

        UpdateThemeChecks(s.Theme);   // theme variant itself is applied in App
    }

    // Avalonia treats a MenuItem's InputGesture as display-only when the item
    // uses a Click handler (rather than Command + HotKey), so the shortcuts
    // never fire on their own. We dispatch them here on the tunnel, before the
    // editor sees the keystroke. (Ctrl+Z/Y/X/C/V/A and Delete are handled
    // natively by the TextBox, so we leave those alone.)
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ea = new RoutedEventArgs();

        // Function keys (no modifier)
        if (e.Key == Key.F3) { OnFindNext(this, ea); e.Handled = true; return; }
        if (e.Key == Key.F5) { OnTimeDate(this, ea); e.Handled = true; return; }

        if (!ctrl) return;

        switch (e.Key)
        {
            case Key.N: OnNew(this, ea); e.Handled = true; break;
            case Key.O: OnOpen(this, ea); e.Handled = true; break;
            case Key.S: if (shift) OnSaveAs(this, ea); else OnSave(this, ea); e.Handled = true; break;
            case Key.F: OnFind(this, ea); e.Handled = true; break;
            case Key.H: OnReplace(this, ea); e.Handled = true; break;
            case Key.G: OnGoTo(this, ea); e.Handled = true; break;

            case Key.OemPlus:
            case Key.Add:
                SetZoom(_zoom + 10); e.Handled = true; break;
            case Key.OemMinus:
            case Key.Subtract:
                SetZoom(_zoom - 10); e.Handled = true; break;
            case Key.D0:
            case Key.NumPad0:
                SetZoom(100); e.Handled = true; break;
        }
    }

    private void Editor_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty)
        {
            if (!_loading) { _dirty = true; UpdateTitle(); }
            UpdatePosition();
        }
        else if (e.Property == TextBox.CaretIndexProperty ||
                 e.Property == TextBox.SelectionStartProperty ||
                 e.Property == TextBox.SelectionEndProperty)
        {
            UpdatePosition();
        }
    }

    // ------------------------------------------------------------------
    // File
    // ------------------------------------------------------------------

    private async void OnNew(object? s, RoutedEventArgs e)
    {
        if (!await ConfirmDiscard()) return;
        _loading = true;
        Editor.Text = "";
        _loading = false;
        _path = null;
        _encoding = new UTF8Encoding(false);
        _dirty = false;
        UpdateTitle();
        UpdateEncodingLabel();
    }

    private async void OnOpen(object? s, RoutedEventArgs e)
    {
        if (!await ConfirmDiscard()) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Text Documents") { Patterns = new[] { "*.txt" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0 && files[0].Path.IsAbsoluteUri)
            LoadFile(files[0].Path.LocalPath);
    }

    private void LoadFile(string path)
    {
        try
        {
            using var reader = new StreamReader(path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();
            _encoding = reader.CurrentEncoding;

            LineEndingText.Text = !text.Contains('\n') || text.Contains("\r\n")
                ? "Windows (CRLF)" : "Unix (LF)";

            _loading = true;
            Editor.Text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            _loading = false;

            _path = path;
            _dirty = false;
            UpdateTitle();
            UpdateEncodingLabel();
        }
        catch (Exception ex)
        {
            _ = Dialogs.MessageBox(this, "Cannot open file:\n" + ex.Message, AppName, MsgButtons.Ok);
        }
    }

    private async void OnSave(object? s, RoutedEventArgs e) => await Save();
    private async void OnSaveAs(object? s, RoutedEventArgs e) => await SaveAs();

    private async Task<bool> Save()
    {
        if (_path == null) return await SaveAs();
        return WriteFile(_path);
    }

    private async Task<bool> SaveAs()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            DefaultExtension = "txt",
            SuggestedFileName = _path != null ? Path.GetFileName(_path) : "Untitled.txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text Documents") { Patterns = new[] { "*.txt" } },
                FilePickerFileTypes.All
            }
        });

        if (file != null && file.Path.IsAbsoluteUri)
            return WriteFile(file.Path.LocalPath);
        return false;
    }

    private bool WriteFile(string path)
    {
        try
        {
            var text = (Editor.Text ?? "").Replace("\n", Environment.NewLine);
            File.WriteAllText(path, text, _encoding);
            _path = path;
            _dirty = false;
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            _ = Dialogs.MessageBox(this, "Cannot save file:\n" + ex.Message, AppName, MsgButtons.Ok);
            return false;
        }
    }

    private async Task<bool> ConfirmDiscard()
    {
        if (!_dirty) return true;
        var name = _path != null ? Path.GetFileName(_path) : "Untitled";
        var r = await Dialogs.MessageBox(this,
            $"Do you want to save changes to {name}?", AppName, MsgButtons.YesNoCancel);
        return r switch
        {
            MsgResult.Yes => await Save(),
            MsgResult.No => true,
            _ => false
        };
    }

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_confirmedClose || !_dirty) return;
        e.Cancel = true;
        if (await ConfirmDiscard())
        {
            _confirmedClose = true;
            Close();
        }
    }

    private void OnExit(object? s, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------
    // Edit
    // ------------------------------------------------------------------

    private void OnUndo(object? s, RoutedEventArgs e) => Editor.Undo();
    private void OnRedo(object? s, RoutedEventArgs e) => Editor.Redo();
    private void OnCut(object? s, RoutedEventArgs e) => Editor.Cut();
    private void OnCopy(object? s, RoutedEventArgs e) => Editor.Copy();
    private void OnPaste(object? s, RoutedEventArgs e) => Editor.Paste();
    private void OnSelectAll(object? s, RoutedEventArgs e) => Editor.SelectAll();

    private void OnDelete(object? s, RoutedEventArgs e)
    {
        if (Editor.SelectionStart != Editor.SelectionEnd)
            Editor.SelectedText = "";
    }

    private void OnTimeDate(object? s, RoutedEventArgs e)
    {
        var stamp = DateTime.Now.ToString("h:mm tt M/d/yyyy");
        Editor.SelectedText = stamp;
    }

    // ------------------------------------------------------------------
    // Find / Replace
    // ------------------------------------------------------------------

    private void OnFind(object? s, RoutedEventArgs e) => ShowFind(false);
    private void OnReplace(object? s, RoutedEventArgs e) => ShowFind(true);

    private void ShowFind(bool replaceMode)
    {
        if (_findWindow == null)
        {
            _findWindow = new FindReplaceWindow
            {
                FindNext = (text, mc, down) => FindFrom(text, mc, down),
                ReplaceOne = ReplaceOne,
                ReplaceAll = ReplaceAll
            };
        }

        var sel = SelectedText();
        if (!string.IsNullOrEmpty(sel) && !sel.Contains('\n'))
            _findWindow.FindText = sel;
        else
            _findWindow.FindText = _lastSearch;

        _findWindow.SetReplaceMode(replaceMode);
        if (!_findWindow.IsVisible)
            _findWindow.Show(this);
        _findWindow.Activate();
        _findWindow.FocusInput();
    }

    private void OnFindNext(object? s, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastSearch)) { ShowFind(false); return; }
        FindFrom(_lastSearch, _lastMatchCase, _lastDown);
    }

    private bool FindFrom(string text, bool matchCase, bool down)
    {
        _lastSearch = text;
        _lastMatchCase = matchCase;
        _lastDown = down;
        if (string.IsNullOrEmpty(text)) return false;

        var haystack = Editor.Text ?? "";
        var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int caret = Math.Max(Editor.SelectionStart, Editor.SelectionEnd);
        int anchor = Math.Min(Editor.SelectionStart, Editor.SelectionEnd);
        int index;

        if (down)
            index = haystack.IndexOf(text, Math.Min(caret, haystack.Length), cmp);
        else
            index = anchor > 0 ? haystack.LastIndexOf(text, Math.Min(anchor - 1, haystack.Length - 1), cmp) : -1;

        if (index < 0)
        {
            _ = Dialogs.MessageBox(this, $"Cannot find \"{text}\"", AppName, MsgButtons.Ok);
            return false;
        }

        // Focus the editor so the match is highlighted with the active
        // selection colour and scrolled into view; this also lets F3 repeat
        // the search while typing continues in the document.
        Editor.Focus();
        Editor.SelectionStart = index;
        Editor.SelectionEnd = index + text.Length;
        Editor.CaretIndex = index + text.Length;
        return true;
    }

    private void ReplaceOne(string find, string replace, bool matchCase)
    {
        var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var sel = SelectedText();
        if (sel.Equals(find, cmp) && !string.IsNullOrEmpty(find))
            Editor.SelectedText = replace;
        FindFrom(find, matchCase, true);
    }

    private void ReplaceAll(string find, string replace, bool matchCase)
    {
        if (string.IsNullOrEmpty(find)) return;
        var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var text = Editor.Text ?? "";
        var sb = new StringBuilder();
        int i = 0;
        while (i < text.Length)
        {
            int next = text.IndexOf(find, i, cmp);
            if (next < 0) { sb.Append(text, i, text.Length - i); break; }
            sb.Append(text, i, next - i).Append(replace);
            i = next + find.Length;
        }
        Editor.Text = sb.ToString();
    }

    private string SelectedText()
    {
        var text = Editor.Text ?? "";
        int a = Math.Min(Editor.SelectionStart, Editor.SelectionEnd);
        int b = Math.Max(Editor.SelectionStart, Editor.SelectionEnd);
        a = Math.Clamp(a, 0, text.Length);
        b = Math.Clamp(b, 0, text.Length);
        return text.Substring(a, b - a);
    }

    // ------------------------------------------------------------------
    // Go To
    // ------------------------------------------------------------------

    private async void OnGoTo(object? s, RoutedEventArgs e)
    {
        if (Editor.TextWrapping == TextWrapping.Wrap) return; // disabled under word wrap

        var text = Editor.Text ?? "";
        int currentLine = 1;
        for (int i = 0; i < Editor.CaretIndex && i < text.Length; i++)
            if (text[i] == '\n') currentLine++;

        var line = await Dialogs.GoToLine(this, currentLine);
        if (line == null) return;

        int target = Math.Max(1, line.Value);
        int idx = 0, ln = 1;
        while (ln < target && idx < text.Length)
        {
            int nl = text.IndexOf('\n', idx);
            if (nl < 0) { idx = text.Length; break; }
            idx = nl + 1;
            ln++;
        }
        Editor.CaretIndex = Math.Min(idx, text.Length);
        Editor.SelectionStart = Editor.SelectionEnd = Editor.CaretIndex;
        Editor.Focus();
    }

    // ------------------------------------------------------------------
    // Format
    // ------------------------------------------------------------------

    private void OnToggleWordWrap(object? s, RoutedEventArgs e)
        => SetWordWrap(Editor.TextWrapping != TextWrapping.Wrap, persist: true);

    private void SetWordWrap(bool wrap, bool persist)
    {
        Editor.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        WordWrapItem.Icon = wrap ? Check() : null;
        GoToItem.IsEnabled = !wrap;
        if (persist) { Settings.Current.WordWrap = wrap; Settings.Current.Save(); }
    }

    private async void OnChooseFont(object? s, RoutedEventArgs e)
    {
        var result = await Dialogs.ChooseFont(this, Editor.FontFamily, _baseFontSize);
        if (result is { } r)
        {
            Editor.FontFamily = r.family;
            _baseFontSize = r.size;
            _zoom = 100;
            Editor.FontSize = _baseFontSize;
            ZoomText.Text = "100%";

            var st = Settings.Current;
            st.FontFamily = r.family.Name;
            st.FontSize = r.size;
            st.Zoom = 100;
            st.Save();
        }
    }

    // ------------------------------------------------------------------
    // View
    // ------------------------------------------------------------------

    private void OnZoomIn(object? s, RoutedEventArgs e) => SetZoom(_zoom + 10);
    private void OnZoomOut(object? s, RoutedEventArgs e) => SetZoom(_zoom - 10);
    private void OnZoomReset(object? s, RoutedEventArgs e) => SetZoom(100);

    private void SetZoom(int percent)
    {
        _zoom = Math.Clamp(percent, 10, 500);
        Editor.FontSize = _baseFontSize * _zoom / 100.0;
        ZoomText.Text = _zoom + "%";
        Settings.Current.Zoom = _zoom;
        Settings.Current.Save();
    }

    private void OnToggleStatusBar(object? s, RoutedEventArgs e)
    {
        StatusBar.IsVisible = !StatusBar.IsVisible;
        StatusBarItem.Icon = StatusBar.IsVisible ? Check() : null;
        Settings.Current.StatusBar = StatusBar.IsVisible;
        Settings.Current.Save();
    }

    // ----- Theme -----

    private void OnThemeSystem(object? s, RoutedEventArgs e) => SetTheme("System");
    private void OnThemeLight(object? s, RoutedEventArgs e) => SetTheme("Light");
    private void OnThemeDark(object? s, RoutedEventArgs e) => SetTheme("Dark");

    private void SetTheme(string name)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = App.ThemeFromName(name);
        Settings.Current.Theme = name;
        Settings.Current.Save();
        UpdateThemeChecks(name);
    }

    private void UpdateThemeChecks(string name)
    {
        ThemeSystemItem.Icon = name == "System" ? Check() : null;
        ThemeLightItem.Icon = name == "Light" ? Check() : null;
        ThemeDarkItem.Icon = name == "Dark" ? Check() : null;
    }

    // ------------------------------------------------------------------
    // Help
    // ------------------------------------------------------------------

    private async void OnAbout(object? s, RoutedEventArgs e)
    {
        await Dialogs.MessageBox(this,
            "Simple Notepad\nVersion 1.0\n\nA lightweight Notepad clone built with .NET 10 and Avalonia (XAML), " +
            "running natively on Linux, Windows and macOS.\n\n" +
            "Created by William Daugherty (Created with AI)",
            "About Notepad", MsgButtons.Ok);
    }

    // ------------------------------------------------------------------
    // Status / title
    // ------------------------------------------------------------------

    private void UpdateTitle()
    {
        var name = _path != null ? Path.GetFileName(_path) : "Untitled";
        Title = (_dirty ? "*" : "") + name + " - " + AppName;
    }

    private void UpdateEncodingLabel()
    {
        EncodingText.Text = _encoding switch
        {
            UTF8Encoding => "UTF-8",
            _ when _encoding.Equals(Encoding.Unicode) => "UTF-16 LE",
            _ when _encoding.Equals(Encoding.BigEndianUnicode) => "UTF-16 BE",
            _ => _encoding.WebName.ToUpperInvariant()
        };
    }

    private void UpdatePosition()
    {
        var text = Editor.Text ?? "";
        int caret = Math.Clamp(Editor.CaretIndex, 0, text.Length);
        int line = 1, col = 1;
        for (int i = 0; i < caret; i++)
        {
            if (text[i] == '\n') { line++; col = 1; }
            else col++;
        }
        PositionText.Text = $"Ln {line}, Col {col}";
    }
}
