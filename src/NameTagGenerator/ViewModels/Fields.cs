using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using NameTagGenerator.Text;

namespace NameTagGenerator.ViewModels;

/// <summary>Hebrew text flows from the right; everything else from the left.</summary>
public sealed class TextFlowConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && TextOutliner.IsRightToLeft(text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class NotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    readonly Action<object?> _run;
    readonly Func<bool>? _can;
    public RelayCommand(Action<object?> run, Func<bool>? can = null) => (_run, _can) = (run, can);
    public bool CanExecute(object? parameter) => _can?.Invoke() ?? true;
    public void Execute(object? parameter) => _run(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

/// <summary>One setting on screen. Getter and setter connect it to wherever the value lives.</summary>
public abstract class FieldVm : Observable
{
    protected FieldVm(string label) => Label = label;
    public string Label { get; }
}

/// <summary>Number box. Keeps what was typed, so "1." can be on the way to "1.5".</summary>
public sealed class NumberFieldVm : FieldVm
{
    readonly Action<double> _set;
    readonly double _min;
    string _text;
    bool _valid = true;

    public NumberFieldVm(string label, double value, double min, Action<double> set) : base(label)
    {
        _set = set;
        _min = min;
        _text = value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Raise();
            IsValid = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number >= _min;
            if (IsValid) _set(number);
        }
    }

    /// <summary>Displays a value worked out by the app, without treating it as something the user typed.</summary>
    public void Show(double value)
    {
        _text = value.ToString("0.###", CultureInfo.InvariantCulture);
        Raise(nameof(Text));
        IsValid = true;
    }

    public bool IsValid
    {
        get => _valid;
        private set { if (_valid != value) { _valid = value; Raise(); } }
    }
}

public sealed class ToggleFieldVm : FieldVm
{
    readonly Action<bool> _set;
    bool _value;
    public ToggleFieldVm(string label, bool value, Action<bool> set) : base(label) => (_value, _set) = (value, set);

    public bool Value
    {
        get => _value;
        set { _value = value; Raise(); _set(value); }
    }
}

public sealed class ChoiceFieldVm : FieldVm
{
    readonly Action<int> _set;
    int _index;
    public ChoiceFieldVm(string label, IReadOnlyList<string> choices, int index, Action<int> set) : base(label) => (Choices, _index, _set) = (choices, index, set);

    public IReadOnlyList<string> Choices { get; }

    public int Index
    {
        get => _index;
        set { _index = value; Raise(); _set(value); }
    }
}
