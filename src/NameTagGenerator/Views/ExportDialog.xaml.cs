using System.Windows;

namespace NameTagGenerator.Views;

/// <summary>Makes sure the user wants what is about to be exported when there are several names.</summary>
public partial class ExportDialog : Window
{
    readonly int _count;

    public ExportDialog(int count, int columns, int rows)
    {
        _count = count;
        InitializeComponent();
        Width = App.Sized(440);
        Heading.Text = $"Export {count} names";
        TogetherText.Text = $"All names in one file, arranged {columns} × {rows}";
        SeparateText.Text = "One STL file per name";
        UpdateSummary();
    }

    public bool OnePerName => Separate.IsChecked == true;

    void OnChoice(object sender, RoutedEventArgs e) => UpdateSummary();

    void UpdateSummary()
    {
        if (Summary is null) return;
        Summary.Text = OnePerName
            ? $"You will get {_count} files. You choose the folder next."
            : "You will get 1 file. You choose its name next.";
    }

    void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
