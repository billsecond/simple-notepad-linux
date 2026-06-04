using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Notepad;

public enum MsgButtons { Ok, YesNo, YesNoCancel }
public enum MsgResult { Ok, Yes, No, Cancel }

/// <summary>
/// Lightweight modal dialogs (message box, line prompt, font picker) since
/// Avalonia ships no equivalents out of the box.
/// </summary>
public static class Dialogs
{
    public static async Task<MsgResult> MessageBox(Window owner, string text, string title, MsgButtons buttons)
    {
        var result = MsgResult.Cancel;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = text, MaxWidth = 380, TextWrapping = TextWrapping.Wrap });

        var buttonBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        panel.Children.Add(buttonBar);

        var dlg = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        void Add(string caption, MsgResult r, bool isDefault = false)
        {
            var b = new Button { Content = caption, MinWidth = 80, IsDefault = isDefault };
            b.Click += (_, _) => { result = r; dlg.Close(); };
            buttonBar.Children.Add(b);
        }

        switch (buttons)
        {
            case MsgButtons.Ok:
                Add("OK", MsgResult.Ok, true);
                break;
            case MsgButtons.YesNo:
                Add("Yes", MsgResult.Yes, true);
                Add("No", MsgResult.No);
                break;
            case MsgButtons.YesNoCancel:
                Add("Yes", MsgResult.Yes, true);
                Add("No", MsgResult.No);
                Add("Cancel", MsgResult.Cancel);
                break;
        }

        await dlg.ShowDialog(owner);
        return result;
    }

    /// <summary>Prompts for a line number; returns null if cancelled.</summary>
    public static async Task<int?> GoToLine(Window owner, int current)
    {
        int? result = null;

        var box = new TextBox { Text = current.ToString(), Width = 240 };
        var ok = new Button { Content = "Go To", MinWidth = 75, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 75, IsCancel = true };

        var dlg = new Window
        {
            Title = "Go To Line",
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        ok.Click += (_, _) =>
        {
            if (int.TryParse(box.Text, out var n)) result = n;
            dlg.Close();
        };
        cancel.Click += (_, _) => dlg.Close();
        dlg.Opened += (_, _) => { box.Focus(); box.SelectAll(); };

        dlg.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Line number:" },
                box,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { ok, cancel }
                }
            }
        };

        await dlg.ShowDialog(owner);
        return result;
    }

    /// <summary>Font picker. Returns (family, size) or null if cancelled.</summary>
    public static async Task<(FontFamily family, double size)?> ChooseFont(
        Window owner, FontFamily currentFamily, double currentSize)
    {
        (FontFamily, double)? result = null;

        var names = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n)
            .ToList();
        if (!names.Contains(currentFamily.Name))
            names.Insert(0, currentFamily.Name);

        var list = new ListBox
        {
            ItemsSource = names,
            SelectedItem = currentFamily.Name,
            Height = 240,
            Width = 260
        };

        var sizes = new List<double> { 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 36, 48, 72 };
        var sizeBox = new ComboBox { ItemsSource = sizes, SelectedItem = sizes.Contains(currentSize) ? currentSize : 14.0, Width = 80 };

        var preview = new TextBlock
        {
            Text = "AaBbYyZz 0123",
            FontFamily = currentFamily,
            FontSize = currentSize,
            Margin = new Thickness(0, 8, 0, 0),
            Height = 40
        };

        void UpdatePreview()
        {
            if (list.SelectedItem is string fam) preview.FontFamily = new FontFamily(fam);
            if (sizeBox.SelectedItem is double s) preview.FontSize = s;
        }
        list.SelectionChanged += (_, _) => UpdatePreview();
        sizeBox.SelectionChanged += (_, _) => UpdatePreview();

        var ok = new Button { Content = "OK", MinWidth = 75, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 75, IsCancel = true };

        var dlg = new Window
        {
            Title = "Font",
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        ok.Click += (_, _) =>
        {
            var fam = list.SelectedItem is string f ? new FontFamily(f) : currentFamily;
            var size = sizeBox.SelectedItem is double s ? s : currentSize;
            result = (fam, size);
            dlg.Close();
        };
        cancel.Click += (_, _) => dlg.Close();
        dlg.Opened += (_, _) => list.Focus();

        dlg.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Font:" }, list } },
                        new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Size:" }, sizeBox } }
                    }
                },
                preview,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { ok, cancel }
                }
            }
        };

        await dlg.ShowDialog(owner);
        return result;
    }
}
