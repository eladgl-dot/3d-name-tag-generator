using System.Windows;

namespace NameTagGenerator.Views;

/// <summary>Makes sure the user wants what is about to be exported when there are several names.</summary>
public partial class ExportDialog : Window
{
    readonly int _count;

    /// <param name="beds">Beds used by Fill the bed (columns is then 0). Each bed becomes a Bambu Studio plate.</param>
    /// <param name="separateObjects">The 3MF choice made last time.</param>
    public ExportDialog(int count, int columns, int rows, int beds = 1, bool separateObjects = true)
    {
        _count = count;
        InitializeComponent();
        Width = App.Sized(440);
        Heading.Text = $"Export {count} names";
        TogetherText.Text = columns == 0
            ? $"All names in one file, on {beds} {(beds == 1 ? "bed" : "beds (one Bambu plate each)")}"
            : $"All names in one file, arranged {columns} × {rows}";
        OneObjectText.Text = beds > 1 ? "One object per bed" : "One object";
        SeparateObjectsChoice.IsChecked = separateObjects;
        OneObjectChoice.IsChecked = !separateObjects;
        SeparateText.Text = "One STL file per name";
        UpdateSummary();
    }

    public bool OnePerName => Separate.IsChecked == true;

    /// <summary>3MF: every name its own object.</summary>
    public bool SeparateObjects => SeparateObjectsChoice.IsChecked == true;

    void OnChoice(object sender, RoutedEventArgs e) => UpdateSummary();

    void UpdateSummary()
    {
        if (Summary is null) return;
        // One STL per name has no objects to choose.
        if (ObjectsChoice is not null) ObjectsChoice.IsEnabled = !OnePerName;
        Summary.Text = OnePerName
            ? $"You will get {_count} files. You choose the folder next."
            : "You will get 1 file. You choose its name next.";
    }

    void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
