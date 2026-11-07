using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NameTagGenerator.Output;

namespace NameTagGenerator;

public partial class App : Application
{
    /// <summary>Follows the Windows "Text size" setting: 1 at the default size, 1.75 at about 180%.</summary>
    public static double TextScale { get; } = Math.Max(1, SystemFonts.MessageFontSize / 12);

    /// <summary>A width or height for a design at the default text size, grown to fit larger text.</summary>
    public static double Sized(double designSize) => designSize * Math.Pow(TextScale, 0.55);

    public App()
    {
        EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnComboWheel));

        DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
            OnUiError(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception error) CrashLog.Record(error, "Unhandled");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLog.Record(e.Exception, "Background task");
            e.SetObserved();
        };
    }

    // A failure that repeats on every frame must not bury the user in pop-ups.
    static readonly TimeSpan MinTimeBetweenPopups = TimeSpan.FromMinutes(1);
    DateTime _lastPopup = DateTime.MinValue;
    bool _showingPopup;

    void OnUiError(Exception error)
    {
        CrashLog.Record(error, "UI");

        var outOfMemory = CrashLog.IsOutOfMemory(error);
        // Drawing without the graphics card is slower, but needs none of its memory.
        if (outOfMemory) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        if (_showingPopup || DateTime.Now - _lastPopup < MinTimeBetweenPopups) return;
        _showingPopup = true;
        try
        {
            var text = outOfMemory
                ? "The PC ran short of memory for the 3D preview, so the preview now draws without the graphics card. It may be a little slower until you restart the app.\n\nYour names and settings are safe."
                : "Something went wrong:\n\n" + error.Message;
            MessageBox.Show(text + "\n\nDetails were saved to:\n" + CrashLog.FilePath, "3D name tag generator", MessageBoxButton.OK,
                outOfMemory ? MessageBoxImage.Warning : MessageBoxImage.Error);
        }
        finally
        {
            _showingPopup = false;
            _lastPopup = DateTime.Now;
        }
    }

    // Scrolling the panel past a closed list must not silently change the font or the item.
    static void OnComboWheel(object sender, MouseWheelEventArgs e)
    {
        var box = (ComboBox)sender;
        if (box.IsDropDownOpen) return;
        e.Handled = true;
        if (VisualTreeHelper.GetParent(box) is UIElement parent)
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent });
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Resources["LabelFontSize"] = 12 * TextScale;
        Resources["HeadingFontSize"] = 18 * TextScale;
        Resources["SidePanelWidth"] = new GridLength(Sized(340));
        Resources["ArrangeMenuWidth"] = 340 * TextScale;
        // About six rows, whatever the text size.
        Resources["NamesListHeight"] = 180 * TextScale;
        base.OnStartup(e);
    }
}
