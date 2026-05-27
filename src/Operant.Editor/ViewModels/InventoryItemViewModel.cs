using System.Text.Json.Nodes;

namespace Operant.Editor.ViewModels;

/// <summary>
/// Wraps a single inventory item from the save's <c>Inventory.Items</c> JsonArray.
/// Property setters mutate the underlying JsonNode directly so edits propagate
/// to disk on save without an explicit flush step.
/// </summary>
public class InventoryItemViewModel : ViewModelBase
{
    private readonly JsonObject _node;
    private readonly Action _markDirty;

    /// <summary>
    /// InventoryStatus enum from ZAUM.FELD.C4.Inventory.Management.InventoryStatus
    /// (verified in the IL2CPP dump). Missing=0 is excluded from the editable set
    /// since clicking it once would functionally remove the item.
    /// </summary>
    public static readonly IReadOnlyList<StatusOption> StatusOptions = new[]
    {
        new StatusOption(1, "In inventory"),
        new StatusOption(2, "Equipped"),
    };

    public InventoryItemViewModel(JsonObject node, Action markDirty)
    {
        _node = node;
        _markDirty = markDirty;
    }

    public string EntityId => (string?)_node["EntityID"] ?? string.Empty;

    public int ArrayIdx => (int?)_node["ArrayIDX"] ?? 0;

    public int Amount
    {
        get => (int?)_node["Amount"] ?? 0;
        set
        {
            if (Amount == value) return;
            if (value < 0) value = 0;
            _node["Amount"] = value;
            OnPropertyChanged();
            _markDirty();
        }
    }

    public int Status
    {
        get => (int?)_node["Status"] ?? 0;
        set
        {
            if (Status == value) return;
            _node["Status"] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(IsEquipped));
            _markDirty();
        }
    }

    public string StatusLabel => Status switch
    {
        0 => "Missing",
        1 => "In inventory",
        2 => "Equipped",
        _ => $"Unknown ({Status})",
    };

    public bool IsEquipped => Status == 2;
}

public record StatusOption(int Value, string Label)
{
    public override string ToString() => Label;
}
