using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NameTagGenerator.Text;
using NameTagGenerator.ViewModels;

namespace NameTagGenerator;

public partial class MainWindow : Window
{
    readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        var screen = SystemParameters.WorkArea;
        Width = Math.Min(App.Sized(1180), screen.Width * 0.96);
        Height = Math.Min(App.Sized(760), screen.Height * 0.96);
        DataContext = _viewModel;
        _viewModel.ResetViewRequested += Preview.ResetView;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.SaveState();
        base.OnClosing(e);
    }

    // The menu sits at the preview's right edge and the model moves over beside it, so the names stay visible while arranging.
    void OnArrangeOpened(object? sender, EventArgs e)
    {
        var menu = (FrameworkElement)ArrangePopup.Child;
        var menuWidth = menu.Width + menu.Margin.Left + menu.Margin.Right;
        ArrangePopup.HorizontalOffset = Math.Max(0, Preview.ActualWidth - menuWidth);
        ArrangePopup.VerticalOffset = PreviewBar.Margin.Top + PreviewBar.ActualHeight;
        // In a very narrow window the model keeps some room, even if the menu then overlaps it.
        Preview.ReserveRight(Math.Min(menuWidth, Preview.ActualWidth * 0.6));
        // The menu is its own little window, so keyboard users need focus moved into it.
        Dispatcher.BeginInvoke(() => menu.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)));
    }

    void OnArrangeClosed(object? sender, EventArgs e) => Preview.ReserveRight(0);

    void OnArrangeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        ArrangePopup.IsOpen = false;
        ArrangeButton.Focus();
        e.Handled = true;
    }

    // Hebrew typed into the box flows from the right, following whatever was typed.
    void OnNameBoxChanged(object sender, TextChangedEventArgs e)
    {
        var box = (TextBox)sender;
        box.FlowDirection = TextOutliner.IsRightToLeft(box.Text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
}
