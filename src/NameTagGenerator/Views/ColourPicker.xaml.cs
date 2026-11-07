using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace NameTagGenerator.Views;

public sealed record ColourSwatch(string Name, string Hex)
{
    public Brush Brush { get; } = MakeBrush(Hex);

    static Brush MakeBrush(string hex)
    {
        var brush = new SolidColorBrush(PreviewBuilder.ParseColour(hex, Colors.Gray));
        brush.Freeze();
        return brush;
    }
}

/// <summary>A small button showing a colour. It opens a palette, with the Windows colour dialog for anything else.</summary>
public partial class ColourPicker : UserControl
{
    public static readonly DependencyProperty HexProperty = DependencyProperty.Register(
        nameof(Hex), typeof(string), typeof(ColourPicker),
        new FrameworkPropertyMetadata("#808080", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColourPicker)d).ShowHex()));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ColourPicker));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(ColourPicker));

    /// <summary>Colours close to common filaments.</summary>
    public static IReadOnlyList<ColourSwatch> Palette { get; } = new ColourSwatch[]
    {
        new("White", "#F4F6F8"), new("Black", "#1B1D20"), new("Grey", "#8A8F96"), new("Red", "#D0312D"), new("Orange", "#F28C28"), new("Yellow", "#F5D327"),
        new("Green", "#2E9E4F"), new("Mint", "#7FD8BE"), new("Blue", "#1E90FF"), new("Navy", "#1F3A7A"), new("Purple", "#7B4FB5"), new("Pink", "#F29BC0"),
    };

    static readonly int[] CustomColours = new int[16];

    public ColourPicker()
    {
        InitializeComponent();
        Swatches.ItemsSource = Palette;
        ShowHex();
    }

    public string Hex
    {
        get => (string)GetValue(HexProperty);
        set => SetValue(HexProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    void ShowHex() => Chip.Background = new SolidColorBrush(PreviewBuilder.ParseColour(Hex, Colors.Gray));

    void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ColourSwatch swatch) Hex = swatch.Hex;
        Opener.IsChecked = false;
    }

    void OnAnyColourClick(object sender, RoutedEventArgs e)
    {
        Opener.IsChecked = false;
        var current = PreviewBuilder.ParseColour(Hex, Colors.Gray);
        var owner = Window.GetWindow(this);
        var chosen = Native.Choose(owner is null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle, current, CustomColours);
        if (chosen is { } c) Hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    static class Native
    {
        const int FullOpen = 0x2, RgbInit = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        struct ChooseColorInfo
        {
            public int Size;
            public IntPtr Owner;
            public IntPtr Instance;
            public int Rgb;
            public IntPtr CustomColours;
            public int Flags;
            public IntPtr CustomData;
            public IntPtr Hook;
            public IntPtr Template;
        }

        [DllImport("comdlg32.dll", EntryPoint = "ChooseColorW")]
        static extern bool ChooseColor(ref ChooseColorInfo info);

        public static Color? Choose(IntPtr owner, Color start, int[] custom)
        {
            var pin = GCHandle.Alloc(custom, GCHandleType.Pinned);
            try
            {
                var info = new ChooseColorInfo
                {
                    Size = Marshal.SizeOf<ChooseColorInfo>(),
                    Owner = owner,
                    Rgb = start.R | start.G << 8 | start.B << 16,
                    CustomColours = pin.AddrOfPinnedObject(),
                    Flags = FullOpen | RgbInit,
                };
                if (!ChooseColor(ref info)) return null;
                return Color.FromRgb((byte)info.Rgb, (byte)(info.Rgb >> 8), (byte)(info.Rgb >> 16));
            }
            finally
            {
                pin.Free();
            }
        }
    }
}
