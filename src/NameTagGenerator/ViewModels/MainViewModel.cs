using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;
using NameTagGenerator.Items;
using NameTagGenerator.Output;
using NameTagGenerator.Text;
using NameTagGenerator.Views;

namespace NameTagGenerator.ViewModels;

public sealed class MainViewModel : Observable
{
    // PETG density; filament estimates assume solid prints, so slicers will usually report less.
    const double PetgGramsPerCm3 = 1.27;

    readonly AppState _state = AppState.Load();
    readonly TagBuilder _builder = new(FontCatalog.Resolve);
    readonly DispatcherTimer _rebuildTimer;
    readonly ObservableCollection<FontOption> _fonts = new();
    CancellationTokenSource? _rebuildCancel;
    BuildResult? _result;
    IItemType _item;
    string _newNames = "";
    int _selectedIndex;
    bool _previewAll;
    Model3D? _previewModel;
    string _status = "";
    string _warning = "";
    bool _busy;
    bool _fitViewAfterBuild;
    NumberFieldVm? _columnsField;

    public MainViewModel()
    {
        _item = ItemRegistry.All.FirstOrDefault(i => i.Id == _state.ActiveItem) ?? ItemRegistry.All[0];
        Names = new ObservableCollection<string>(_state.Names);
        Names.CollectionChanged += (_, _) => { ScheduleRebuild(); Raise(nameof(HasNames)); };

        FontsView = CollectionViewSource.GetDefaultView(_fonts);
        FontsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FontOption.Group)));

        _rebuildTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _rebuildTimer.Tick += (_, _) => { _rebuildTimer.Stop(); Rebuild(); };

        AddNamesCommand = new RelayCommand(_ => AddNames());
        RemoveNameCommand = new RelayCommand(p => { if (p is string s) Names.Remove(s); });
        ClearNamesCommand = new RelayCommand(_ => Names.Clear(), () => Names.Count > 0);
        AddFontCommand = new RelayCommand(_ => AddFont());
        SaveCommand = new RelayCommand(_ => Save(), () => _result is { Tags.Count: > 0 });
        SendToBambuCommand = new RelayCommand(_ => SendToBambu(), () => _result is { Tags.Count: > 0 });
        HelpCommand = new RelayCommand(p => HelpWindow.ShowTopic(Application.Current.MainWindow, p as string ?? "start"));
        ResetViewCommand = new RelayCommand(_ => ResetViewRequested?.Invoke());
        SwapColoursCommand = new RelayCommand(_ => SwapColours());
        ShowAllNamesCommand = new RelayCommand(_ => { if (!_previewAll) PreviewAll = true; });

        BuildFieldLists();
        LoadFontsInBackground();
        ScheduleRebuild();
    }

    public event Action? ResetViewRequested;

    public IReadOnlyList<IItemType> Items => ItemRegistry.All;
    public ObservableCollection<string> Names { get; }
    public ICollectionView FontsView { get; }
    public IReadOnlyList<FieldVm> TextFields { get; private set; } = Array.Empty<FieldVm>();
    public IReadOnlyList<FieldVm> ItemFields { get; private set; } = Array.Empty<FieldVm>();
    public IReadOnlyList<FieldVm> ConnectorFields { get; private set; } = Array.Empty<FieldVm>();
    public IReadOnlyList<FieldVm> ArrangeFields { get; private set; } = Array.Empty<FieldVm>();

    public IReadOnlyList<ConnectorOption> Connectors { get; } = new ConnectorOption[]
    {
        new("Hyphen (-)", "-"), new("En dash (–)", "–"), new("Em dash (—)", "—"), new("Underscore (_)", "_"),
        new("Dot (.)", "."), new("Tilde (~)", "~"), new("Just a space", " "),
    };

    public RelayCommand AddNamesCommand { get; }
    public RelayCommand RemoveNameCommand { get; }
    public RelayCommand ClearNamesCommand { get; }
    public RelayCommand AddFontCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SendToBambuCommand { get; }
    public RelayCommand HelpCommand { get; }
    public RelayCommand ResetViewCommand { get; }
    public RelayCommand SwapColoursCommand { get; }
    public RelayCommand ShowAllNamesCommand { get; }

    public bool HasNames => Names.Count > 0;

    public string BaseColour
    {
        get => _state.BaseColour;
        set { _state.BaseColour = value; ColoursChanged(); }
    }

    public string LetterColour
    {
        get => _state.LetterColour;
        set { _state.LetterColour = value; ColoursChanged(); }
    }

    void SwapColours()
    {
        (_state.BaseColour, _state.LetterColour) = (_state.LetterColour, _state.BaseColour);
        ColoursChanged();
    }

    // Colours are for the preview only, so the shapes do not need rebuilding.
    void ColoursChanged()
    {
        Raise(nameof(BaseColour));
        Raise(nameof(LetterColour));
        ShowPreview();
        _state.Save();
    }

    ItemSettings Settings
    {
        get
        {
            if (!_state.Items.TryGetValue(_item.Id, out var s))
            {
                s = ItemSettings.DefaultsFor(_item);
                _state.Items[_item.Id] = s;
            }
            s.Upgrade();
            // Settings added in a newer version get their default value.
            foreach (var (key, value) in _item.Defaults) s.Values.TryAdd(key, value);
            return s;
        }
    }

    public IItemType SelectedItem
    {
        get => _item;
        set
        {
            if (value is null || value == _item) return;
            _item = value;
            _state.ActiveItem = value.Id;
            Raise();
            Raise(nameof(SelectedFont));
            Raise(nameof(Bold));
            Raise(nameof(ConnectorValue));
            Raise(nameof(IsHollow));
            BuildFieldLists();
            // A different item has a different size, so the old zoom would not suit it.
            _fitViewAfterBuild = true;
            ScheduleRebuild();
        }
    }

    public string NewNames
    {
        get => _newNames;
        set { _newNames = value; Raise(); }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set { _selectedIndex = value; Raise(); if (!_previewAll) ShowPreview(); }
    }

    public FontOption? SelectedFont
    {
        get => _fonts.FirstOrDefault(f => f.Name == Settings.Font);
        set
        {
            // Null arrives while the list is being refilled; that is not the user choosing anything.
            if (value is null || value.Name == Settings.Font) return;
            Settings.Font = value.Name;
            Raise();
            ScheduleRebuild();
        }
    }

    public bool Bold
    {
        get => Settings.Bold;
        set { Settings.Bold = value; Raise(); ScheduleRebuild(); }
    }

    public string ConnectorValue
    {
        get => Settings.Connector;
        set
        {
            if (value is null) return;
            Settings.Connector = value;
            Raise();
            ScheduleRebuild();
        }
    }

    public bool IsHollow
    {
        get => Settings.Hollow;
        set { Settings.Hollow = value; Raise(); ScheduleRebuild(); }
    }

    public bool ShapeFit
    {
        get => _state.Arrange is { Shape: ArrangeShape.Columns, Columns: 0 };
        set
        {
            if (!value) return;
            _state.Arrange.Columns = 0;
            _columnsField?.Show(0);
            SetShape(ArrangeShape.Columns);
        }
    }

    public bool ShapeRow
    {
        get => _state.Arrange.Shape == ArrangeShape.OneRow;
        set { if (value) SetShape(ArrangeShape.OneRow); }
    }

    public bool ShapeColumn
    {
        get => _state.Arrange.Shape == ArrangeShape.OneColumn;
        set { if (value) SetShape(ArrangeShape.OneColumn); }
    }

    public bool ShapeSquare
    {
        get => _state.Arrange.Shape == ArrangeShape.Square;
        set { if (value) SetShape(ArrangeShape.Square); }
    }

    // A new shape can be much wider or taller than the old one, so the view has to refit.
    void SetShape(ArrangeShape shape)
    {
        _state.Arrange.Shape = shape;
        RaiseShapes();
        _fitViewAfterBuild = true;
        ScheduleRebuild();
    }

    void RaiseShapes()
    {
        Raise(nameof(ShapeFit));
        Raise(nameof(ShapeRow));
        Raise(nameof(ShapeColumn));
        Raise(nameof(ShapeSquare));
    }

    public bool OrderListed
    {
        get => _state.Arrange.Order == ArrangeOrder.AsListed;
        set { if (value) SetOrder(ArrangeOrder.AsListed); }
    }

    public bool OrderAlphabetical
    {
        get => _state.Arrange.Order == ArrangeOrder.Alphabetical;
        set { if (value) SetOrder(ArrangeOrder.Alphabetical); }
    }

    public bool OrderBySize
    {
        get => _state.Arrange.Order == ArrangeOrder.BySize;
        set { if (value) SetOrder(ArrangeOrder.BySize); }
    }

    void SetOrder(ArrangeOrder order)
    {
        _state.Arrange.Order = order;
        Raise(nameof(OrderListed));
        Raise(nameof(OrderAlphabetical));
        Raise(nameof(OrderBySize));
        ScheduleRebuild();
    }

    public bool PreviewAll
    {
        get => _previewAll;
        set
        {
            _previewAll = value;
            Raise();
            Raise(nameof(PreviewOne));
            ShowPreview();
            ResetViewRequested?.Invoke();
        }
    }

    public bool PreviewOne
    {
        get => !_previewAll;
        set => PreviewAll = !value;
    }

    public Model3D? PreviewModel
    {
        get => _previewModel;
        private set { _previewModel = value; Raise(); }
    }

    public string Status
    {
        get => _status;
        private set { _status = value; Raise(); }
    }

    public string Warning
    {
        get => _warning;
        private set { _warning = value; Raise(); Raise(nameof(HasWarning)); }
    }

    public bool HasWarning => _warning.Length > 0;

    public bool IsBusy
    {
        get => _busy;
        private set { _busy = value; Raise(); }
    }

    void BuildFieldLists()
    {
        TextFields = ItemRegistry.CommonFields.Select(MakeField).ToList();
        ItemFields = _item.Fields.Select(MakeField).ToList();
        ConnectorFields = new FieldVm[]
        {
            new NumberFieldVm("Extra connector outline (mm)", Settings[Keys.ConnectorOutline], -50, v => SetValue(Keys.ConnectorOutline, v)),
        };

        var a = _state.Arrange;
        _columnsField = new NumberFieldVm("Columns (0 = auto)", a.Shape == ArrangeShape.Columns ? a.Columns : _result?.Array.Columns ?? 0, 0, v =>
        {
            // Typing a number takes over from whichever shape button was on.
            a.Columns = (int)v;
            a.Shape = ArrangeShape.Columns;
            RaiseShapes();
            ScheduleRebuild();
        });
        ArrangeFields = new FieldVm[]
        {
            _columnsField,
            new NumberFieldVm("Gap (mm)", a.GapX, 0, v => { a.GapX = a.GapY = v; ScheduleRebuild(); }),
            new NumberFieldVm("Plate width (mm)", a.PlateWidth, 10, v => { a.PlateWidth = v; ScheduleRebuild(); }),
            new NumberFieldVm("Plate depth (mm)", a.PlateHeight, 10, v => { a.PlateHeight = v; ScheduleRebuild(); }),
        };

        Raise(nameof(TextFields));
        Raise(nameof(ItemFields));
        Raise(nameof(ConnectorFields));
        Raise(nameof(ArrangeFields));
    }

    FieldVm MakeField(Field f) => f.Kind switch
    {
        FieldKind.Toggle => new ToggleFieldVm(f.Label, Settings[f.Key] != 0, v => SetValue(f.Key, v ? 1 : 0)),
        FieldKind.Choice => new ChoiceFieldVm(f.Label, f.Choices ?? Array.Empty<string>(), (int)Settings[f.Key], v => SetValue(f.Key, v)),
        _ => new NumberFieldVm(f.Label, Settings[f.Key], f.Min, v => SetValue(f.Key, v)),
    };

    void SetValue(string key, double value)
    {
        Settings.Values[key] = value;
        ScheduleRebuild();
    }

    void AddNames()
    {
        // Excel puts one cell per line, and tabs between cells on the same row.
        var parts = NewNames.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var name in parts)
            if (!Names.Contains(name)) Names.Add(name);
        NewNames = "";
    }

    void AddFont()
    {
        var dialog = new OpenFileDialog { Title = "Add a font file", Filter = "Fonts (*.ttf;*.otf)|*.ttf;*.otf" };
        if (dialog.ShowDialog() != true) return;
        var name = FontCatalog.AddFontFile(dialog.FileName);
        if (name is null)
        {
            MessageBox.Show("This file could not be read as a font.", "Add font file", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        LoadFontsInBackground(selectAfter: name);
    }

    void LoadFontsInBackground(string? selectAfter = null)
    {
        if (_fonts.Count == 0) _fonts.Add(new FontOption(Settings.Font, FontCatalog.BuiltInBothGroup));
        Task.Run(FontCatalog.ListAll).ContinueWith(t =>
        {
            if (t.IsFaulted) return;
            var chosen = selectAfter ?? Settings.Font;
            _fonts.Clear();
            foreach (var option in t.Result) _fonts.Add(option);
            // Keep a font chosen earlier even if it is no longer on this PC, so it stays visible.
            if (_fonts.All(f => f.Name != chosen)) _fonts.Insert(0, new FontOption(chosen, FontCatalog.AddedGroup));
            Settings.Font = chosen;
            // The list control finishes reacting to the refill first, then gets the selection back.
            Application.Current.Dispatcher.BeginInvoke(() => Raise(nameof(SelectedFont)), DispatcherPriority.Background);
            ScheduleRebuild();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    void ScheduleRebuild()
    {
        _rebuildTimer.Stop();
        _rebuildTimer.Start();
    }

    void Rebuild()
    {
        _rebuildCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _rebuildCancel = cancel;
        var names = Names.ToList();
        var item = _item;
        var settings = Settings;
        var arrange = _state.Arrange;
        IsBusy = true;

        Task.Run(() => _builder.Build(names, item, settings, arrange, cancel.Token), cancel.Token).ContinueWith(t =>
        {
            if (cancel.IsCancellationRequested) return;
            IsBusy = false;
            if (t.IsFaulted)
            {
                CrashLog.Record(t.Exception!, "Build");
                _result = null;
                PreviewModel = null;
                Status = "";
                Warning = "Could not build this: " + t.Exception?.GetBaseException().Message;
                return;
            }
            _result = t.Result;
            // A shape button decides the columns, so the box shows what it worked out.
            if (arrange.Shape != ArrangeShape.Columns) _columnsField?.Show(t.Result.Array.Columns);
            UpdateStatus();
            try
            {
                ShowPreview();
                if (_fitViewAfterBuild) ResetViewRequested?.Invoke();
                _fitViewAfterBuild = false;
            }
            catch (Exception ex)
            {
                // An error inside this task would otherwise vanish, leaving an old preview on screen.
                CrashLog.Record(ex, "Preview");
                PreviewModel = null;
                Warning = "Could not draw the preview: " + ex.Message;
            }
            CommandManagerRefresh();
            _state.Names = names;
            _state.Save();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    static void CommandManagerRefresh() => System.Windows.Input.CommandManager.InvalidateRequerySuggested();

    void ShowPreview()
    {
        if (_result is null || _result.Tags.Count == 0)
        {
            PreviewModel = null;
            return;
        }
        if (_previewAll)
        {
            PreviewModel = PreviewBuilder.Build(_result.Tags, (_state.Arrange.PlateWidth, _state.Arrange.PlateHeight), BaseColour, LetterColour);
            return;
        }
        // The plate order can differ from the list, so find the selected name itself.
        var selected = _selectedIndex >= 0 && _selectedIndex < Names.Count ? Names[_selectedIndex] : null;
        var tag = _result.Tags.FirstOrDefault(t => t.Name == selected) ?? _result.Tags[0];
        PreviewModel = PreviewBuilder.Build(new[] { tag with { X = 0, Y = 0 } }, null, BaseColour, LetterColour);
    }

    void UpdateStatus()
    {
        if (_result is null || _result.Tags.Count == 0)
        {
            Status = "Add a name to see the preview";
            Warning = "";
            return;
        }

        var a = _result.Array;
        var grams = _result.VolumeMm3 / 1000 * PetgGramsPerCm3;
        var nameCount = _result.Tags.Count == 1 ? "1 name" : $"{_result.Tags.Count} names, {a.Columns} × {a.Rows}";
        Status = $"{a.TotalWidth:0} × {a.TotalHeight:0} × {_result.HeightMm:0.#} mm  ·  {nameCount}  ·  up to {grams:0.#} g PETG";

        var problems = new List<string>();
        if (!a.FitsPlate) problems.Add($"Does not fit the {_state.Arrange.PlateWidth:0} × {_state.Arrange.PlateHeight:0} mm plate");
        var loose = _result.NamesWithLooseParts.Take(3).ToList();
        if (loose.Count > 0) problems.Add("Loose parts in: " + string.Join(", ", loose));
        Warning = string.Join("  ·  ", problems);
    }

    void Save()
    {
        if (_result is null) return;
        var tags = _result.Tags;
        var separate = false;

        if (tags.Count > 1)
        {
            var dialog = new ExportDialog(tags.Count, _result.Array.Columns, _result.Array.Rows) { Owner = Application.Current.MainWindow };
            if (dialog.ShowDialog() != true) return;
            separate = dialog.OnePerName;
        }

        try
        {
            if (separate) SaveOnePerName(tags);
            else SaveTogether(tags);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Could not save the file: " + ex.Message, "Save", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void SaveTogether(IReadOnlyList<PlacedTag> tags)
    {
        var suggested = tags.Count == 1 ? Exporter.SafeFileName(tags[0].Name) : _item.Id + "_tags";
        var dialog = new SaveFileDialog
        {
            FileName = suggested,
            Filter = "STL file (*.stl)|*.stl|3MF, two colours (*.3mf)|*.3mf",
            AddExtension = true,
        };
        if (dialog.ShowDialog() != true) return;

        if (dialog.FilterIndex == 2) Exporter.Save3mf(dialog.FileName, suggested, tags);
        else Exporter.SaveStl(dialog.FileName, tags);
        Status = "Saved " + Path.GetFileName(dialog.FileName);
    }

    void SaveOnePerName(IReadOnlyList<PlacedTag> tags)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder for the STL files" };
        if (dialog.ShowDialog() != true) return;

        var paths = tags.Select(t => (Tag: t, Path: Path.Combine(dialog.FolderName, Exporter.SafeFileName(t.Name) + ".stl"))).ToList();
        var existing = paths.Count(p => File.Exists(p.Path));
        if (existing > 0 && MessageBox.Show($"{existing} of these files already exist in that folder. Replace them?", "Save",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        foreach (var (tag, path) in paths)
            Exporter.SaveStl(path, new[] { tag with { X = 0, Y = 0 } });
        Status = $"Saved {paths.Count} STL files";
    }

    void SendToBambu()
    {
        if (_result is null) return;
        var exe = BambuStudio.FindExe();
        if (exe is null)
        {
            MessageBox.Show("Bambu Studio was not found on this PC. Install it, or use Save instead.", "Send to Bambu Studio",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var tags = _result.Tags;
        var running = BambuStudio.IsRunning;
        if (running || tags.Count > 1)
        {
            var text = tags.Count > 1 ? $"Send all {tags.Count} names to Bambu Studio as one object?" : "Send this name to Bambu Studio?";
            if (running) text += "\n\nBambu Studio is already open. Save your work there first, because the new file may open in that window.";
            if (MessageBox.Show(text, "Send to Bambu Studio", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK)
                return;
        }

        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "3D name tag generator");
            Directory.CreateDirectory(folder);
            var title = tags.Count == 1 ? Exporter.SafeFileName(tags[0].Name) : _item.Id + "_tags";
            var file = Path.Combine(folder, title + ".3mf");
            Exporter.Save3mf(file, title, tags);
            BambuStudio.Open(exe, file);
            Status = "Sent to Bambu Studio";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show("Could not open Bambu Studio: " + ex.Message, "Send to Bambu Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void SaveState()
    {
        _state.Names = Names.ToList();
        _state.Save();
    }
}

public sealed record ConnectorOption(string Label, string Value);
