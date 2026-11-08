using System.Windows;
using System.Windows.Controls;

namespace NameTagGenerator.Views;

public sealed record HelpTopic(string Id, string Title, string Text);

public partial class HelpWindow : Window
{
    public static IReadOnlyList<HelpTopic> AllTopics { get; } = new HelpTopic[]
    {
        new("start", "Quick start",
            "1.  Choose an item: keychain, pencil top, bookmark, desk name plate, cup marker or wine glass marker.\n" +
            "2.  Type names, or paste a column from Excel, then press Ctrl+Enter.\n" +
            "3.  Change the settings and rotate the 3D preview. No file is written until step 4.\n" +
            "4.  Click Save, or Send to Bambu Studio.\n\n" +
            "Letter size stays the same for every name. A longer name simply makes a wider tag."),
        new("settings", "What each setting does",
            "Font size: letter height in mm, roughly.\n" +
            "Text weight: makes the letters thicker.\n" +
            "Base height: thickness of the blue layer.\n" +
            "Letter height: how far letters rise above the base.\n" +
            "Outline: how far the base extends around the letters.\n" +
            "Fill gaps up to: closes narrow gaps and pockets in the base between letters. 0 turns this off.\n\n" +
            "Between two words: the connector joins two-word names on one line. Solid raises it like a letter. Hollow leaves it in the base colour only, and you can make its outline larger.\n\n" +
            "Desk name plate: Base height is its thickness at the top. It prints standing up, as it will sit on the desk.\n\n" +
            "Cup marker: a saddle that sits on top of the rim, with the name facing up. Set Cup diameter (across the outside) and Rim thickness to your cup, and the curved groove grips that rim. Inner and Outer grip depth set how far each wall reaches down the inside and the outside of the cup. In Bambu Studio it appears upside down and mirrored, because it prints name side down with no supports. Do not flip it.\n\n" +
            "Wine glass marker: set Stem size to your glass. The opening is narrower than the stem, so the clip snaps on and holds.\n\n" +
            "Base and Letters above the preview choose the colours you see. They change only the preview, and you pick the real filaments in Bambu Studio.\n\n" +
            "Each item remembers its own settings."),
        new("arrange", "Arranging an array",
            "Click Arrange above the preview. It shows every name on the plate, and changes appear as you make them.\n\n" +
            "Fit plate, One row, One column and Square lay the names out in one click, and they keep working as you add names.\n\n" +
            "Columns: how many names per row. 0 (auto) fits as many as the plate allows. Typing a number here overrides the buttons.\n" +
            "Gap: space between tags.\n" +
            "Plate width and depth: your printer's plate. 256 × 256 mm suits Bambu X1, P1 and A1 printers. A warning appears if the array does not fit.\n\n" +
            "Order: as listed, by the Hebrew alphabet, or widest first. It changes the plate only, not your list.\n\n" +
            "The first name goes top right, following Hebrew reading order. Click outside the menu or press Esc to close it."),
        new("fonts", "Hebrew and fonts",
            "Names always read in the correct right-to-left order.\n\n" +
            "The font list comes from your PC, with Hebrew fonts first. Four Hebrew fonts are built into this app and work on every PC: Gan CLM, MigdalHaemeq, Noot and Suez One.\n\n" +
            "Add font file loads any .ttf or .otf file and remembers it.\n\n" +
            "If you see the wrong letter shapes, that font probably has no Hebrew. Choose one from the Hebrew groups."),
        new("bambu", "Printing in two colours",
            "Send to Bambu Studio opens your tags there as one object with two parts: Base on filament 1 and Letters on filament 2. Choose your two colours and slice.\n\n" +
            "If Bambu Studio is already open, save your work there first. The app warns you before sending.\n\n" +
            "Save offers STL or 3MF. An STL has no colours, so use 3MF to keep the two parts ready for colouring.\n\n" +
            "The filament figure assumes solid PETG, so it is an upper limit. Bambu Studio gives the real amount after slicing."),
        new("keys", "Mouse and keyboard",
            "Left-drag: rotate\nMouse wheel: zoom\nRight-drag: pan\nCtrl+Enter: add the names you typed\nF1: this help"),
        new("trouble", "Troubleshooting",
            "Loose parts warning: part of the tag is not attached. Move the ring closer with Distance adjust, or increase the outline.\n\n" +
            "Does not fit the plate: reduce columns or gaps, or split the list.\n\n" +
            "Bambu Studio not found: install it, or use Save and open the file yourself.\n\n" +
            "On another PC, Windows may ask to install the .NET Desktop Runtime the first time. Accept, install it and start the app again.\n\n" +
            "Memory message: the preview switches to a slower mode that does not use the graphics card, and restarting the app returns it to normal. Closing other 3D programs helps. Details are saved in crash.log, in the folder %LOCALAPPDATA%\\3D name tag generator."),
        new("about", "About",
            "3D name tag generator, version 0.1 (prototype). Works offline.\n\n" +
            "Built-in fonts:\n" +
            "Gan CLM, © 2004 Maxim Iorsh, GNU GPL v2.\n" +
            "MigdalHaemeq, © 2010 Elad Mizrahi, SIL Open Font License 1.1.\n" +
            "Noot, © 2012 Amos Jerbi, SIL Open Font License 1.1.\n" +
            "Suez One, © 2016 Michal Sahar, SIL Open Font License 1.1.\n\n" +
            "Libraries: Clipper2 by Angus Johnson (Boost Software License), LibTessDotNet (SGI Free Software License B)."),
    };

    HelpWindow(string topicId)
    {
        InitializeComponent();
        var screen = SystemParameters.WorkArea;
        Width = Math.Min(App.Sized(720), screen.Width * 0.9);
        Height = Math.Min(App.Sized(520), screen.Height * 0.9);
        TopicColumn.Width = new GridLength(App.Sized(210));
        Topics.ItemsSource = AllTopics;
        Topics.SelectedItem = AllTopics.FirstOrDefault(t => t.Id == topicId) ?? AllTopics[0];
    }

    public static void ShowTopic(Window? owner, string topicId) => new HelpWindow(topicId) { Owner = owner }.ShowDialog();

    void OnTopicChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Topics.SelectedItem is not HelpTopic topic) return;
        Heading.Text = topic.Title;
        Body.Text = topic.Text;
    }
}
