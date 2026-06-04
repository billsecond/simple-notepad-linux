using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Notepad;

/// <summary>Modeless Find / Replace tool window driven by callbacks into the editor.</summary>
public class FindReplaceWindow : Window
{
    private readonly TextBox _findBox = new() { Width = 220 };
    private readonly TextBox _replaceBox = new() { Width = 220 };
    private readonly CheckBox _matchCase = new() { Content = "Match case" };
    private readonly CheckBox _down = new() { Content = "Search down", IsChecked = true };
    private readonly Control _replaceRow;
    private readonly Button _replaceBtn;
    private readonly Button _replaceAllBtn;

    public Func<string, bool, bool, bool>? FindNext;          // (text, matchCase, down) => found
    public Action<string, string, bool>? ReplaceOne;          // (find, replace, matchCase)
    public Action<string, string, bool>? ReplaceAll;          // (find, replace, matchCase)

    public FindReplaceWindow()
    {
        Title = "Find";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var findBtn = new Button { Content = "Find Next", MinWidth = 90, IsDefault = true };
        findBtn.Click += (_, _) => FindNext?.Invoke(_findBox.Text ?? "", _matchCase.IsChecked == true, _down.IsChecked == true);

        _replaceBtn = new Button { Content = "Replace", MinWidth = 90 };
        _replaceBtn.Click += (_, _) => ReplaceOne?.Invoke(_findBox.Text ?? "", _replaceBox.Text ?? "", _matchCase.IsChecked == true);

        _replaceAllBtn = new Button { Content = "Replace All", MinWidth = 90 };
        _replaceAllBtn.Click += (_, _) => ReplaceAll?.Invoke(_findBox.Text ?? "", _replaceBox.Text ?? "", _matchCase.IsChecked == true);

        var closeBtn = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
        closeBtn.Click += (_, _) => Hide();

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
        };
        grid.Children.Add(Label("Find what:", 0, 0));
        Grid.SetColumn(_findBox, 1); Grid.SetRow(_findBox, 0);
        grid.Children.Add(_findBox);

        var replLabel = Label("Replace with:", 1, 0);
        Grid.SetColumn(_replaceBox, 1); Grid.SetRow(_replaceBox, 1);
        grid.Children.Add(replLabel);
        grid.Children.Add(_replaceBox);
        _replaceRow = _replaceBox;
        _replaceRow.IsVisible = false;
        replLabel.IsVisible = false;
        _replaceLabel = replLabel;

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                grid,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { _matchCase, _down } },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { findBtn, _replaceBtn, _replaceAllBtn, closeBtn }
                }
            }
        };

        // Hide instead of close so search state survives.
        Closing += (_, e) => { e.Cancel = true; Hide(); };

        Opened += (_, _) => FocusInput();
    }

    /// <summary>Put keyboard focus in the "Find what" box (posted so it lands after layout).</summary>
    public void FocusInput()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _findBox.Focus();
            _findBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private Control _replaceLabel = null!;

    private static TextBlock Label(string text, int row, int col)
    {
        var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
        Grid.SetRow(t, row);
        Grid.SetColumn(t, col);
        return t;
    }

    public string FindText
    {
        get => _findBox.Text ?? "";
        set => _findBox.Text = value;
    }

    public void SetReplaceMode(bool replace)
    {
        Title = replace ? "Replace" : "Find";
        _replaceRow.IsVisible = replace;
        _replaceLabel.IsVisible = replace;
        _replaceBtn.IsVisible = replace;
        _replaceAllBtn.IsVisible = replace;
        _down.IsVisible = !replace;
        _findBox.Focus();
    }
}
