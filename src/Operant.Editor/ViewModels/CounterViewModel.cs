using System.Text.Json.Nodes;

namespace Operant.Editor.ViewModels;

/// <summary>
/// Wraps a single (key, value) entry in FELDState.m_countersValues. Because the
/// game stores this dictionary as two parallel arrays, the viewmodel holds the
/// arrays and an index rather than the value directly.
/// </summary>
public class CounterViewModel : ViewModelBase
{
    private readonly JsonArray _keys;
    private readonly JsonArray _values;
    private readonly int _index;
    private readonly Action<string, int> _onChanged;

    public CounterViewModel(JsonArray keys, JsonArray values, int index, Action<string, int> onChanged)
    {
        _keys = keys;
        _values = values;
        _index = index;
        _onChanged = onChanged;
    }

    public string Key => (string?)_keys[_index] ?? string.Empty;

    public string Group
    {
        get
        {
            int dot = Key.IndexOf('.');
            return dot >= 0 ? Key[..dot] : "(other)";
        }
    }

    public int Value
    {
        get => (int?)_values[_index] ?? 0;
        set
        {
            if (Value == value) return;
            _values[_index] = value;
            OnPropertyChanged();
            _onChanged(Key, value);
        }
    }
}
