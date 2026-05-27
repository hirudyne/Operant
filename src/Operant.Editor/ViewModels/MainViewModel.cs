using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using Operant.SaveFormat;

namespace Operant.Editor.ViewModels;

public class MainViewModel : ViewModelBase
{
    private SaveFile? _save;
    private string? _currentPath;
    private bool _isDirty;
    private string _status = "No file loaded.";
    private string _inventoryFilter = "";
    private string _statsFilter = "";
    private bool _hideEquipped;
    private bool _showAllCounterGroups;
    private static readonly HashSet<string> CommonCounterGroups = new()
    {
        "stats", "faculties", "skills", "anchors", "political",
        "conditioning", "limits", "thought", "items",
    };

    public MainViewModel()
    {
        OpenCommand = new RelayCommand(_ => OpenFile());
        SaveCommand = new RelayCommand(_ => SaveCurrent(), _ => _save is not null && !string.IsNullOrEmpty(_currentPath));
        SaveAsCommand = new RelayCommand(_ => SaveAs(), _ => _save is not null);

        InventoryItems = new ObservableCollection<InventoryItemViewModel>();
        Counters = new ObservableCollection<CounterViewModel>();

        // CollectionViews give us cheap live filtering without rebuilding the lists.
        InventoryView = (ListCollectionView)CollectionViewSource.GetDefaultView(InventoryItems);
        InventoryView.Filter = InventoryFilterPredicate;
        InventoryView.SortDescriptions.Add(new SortDescription(nameof(InventoryItemViewModel.IsEquipped), ListSortDirection.Descending));
        InventoryView.SortDescriptions.Add(new SortDescription(nameof(InventoryItemViewModel.ArrayIdx), ListSortDirection.Ascending));

        CountersView = (ListCollectionView)CollectionViewSource.GetDefaultView(Counters);
        CountersView.Filter = CounterFilterPredicate;
        CountersView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CounterViewModel.Group)));
        CountersView.SortDescriptions.Add(new SortDescription(nameof(CounterViewModel.Group), ListSortDirection.Ascending));
        CountersView.SortDescriptions.Add(new SortDescription(nameof(CounterViewModel.Key), ListSortDirection.Ascending));
    }

    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }

    public ObservableCollection<InventoryItemViewModel> InventoryItems { get; }
    public ObservableCollection<CounterViewModel> Counters { get; }
    public ListCollectionView InventoryView { get; }
    public ListCollectionView CountersView { get; }

    public bool HasSave => _save is not null;
    public bool IsDirty => _isDirty;

    private CharacterCreationViewModel? _characterCreation;
    /// <summary>Set after a save is loaded. The Overview tab binds to this.</summary>
    public CharacterCreationViewModel? CharacterCreation
    {
        get => _characterCreation;
        private set => SetField(ref _characterCreation, value);
    }

    public string Title
    {
        get
        {
            var parts = new List<string> { "Operant - Zero Parades Save Editor" };
            if (_currentPath is not null) parts.Add(Path.GetFileName(_currentPath));
            if (_isDirty) parts.Add("*");
            return string.Join(" - ", parts);
        }
    }

    public string Status { get => _status; set => SetField(ref _status, value); }

    public string InventoryFilter
    {
        get => _inventoryFilter;
        set { if (SetField(ref _inventoryFilter, value)) InventoryView.Refresh(); }
    }

    public bool HideEquipped
    {
        get => _hideEquipped;
        set { if (SetField(ref _hideEquipped, value)) InventoryView.Refresh(); }
    }

    public string StatsFilter
    {
        get => _statsFilter;
        set { if (SetField(ref _statsFilter, value)) CountersView.Refresh(); }
    }

    public bool ShowAllCounterGroups
    {
        get => _showAllCounterGroups;
        set { if (SetField(ref _showAllCounterGroups, value)) CountersView.Refresh(); }
    }

    // --- Header / Overview accessors -------------------------------------

    public int Sol
    {
        get => _save?.Sol ?? 0;
        set
        {
            if (_save is null) return;
            if (_save.Sol == value) return;
            _save.Sol = value;
            // Reflect into the Counters list too if a row matches.
            foreach (var c in Counters)
                if (c.Key == "stats.money")
                    c.OnPropertyChangedExternal(nameof(CounterViewModel.Value));
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public string? SaveName
    {
        get => (string?)_save?.Header["SummaryData"]?["SaveName"];
        set { SetHeaderField("SaveName", value); OnPropertyChanged(); }
    }

    public string? SaveLabel
    {
        get => (string?)_save?.Header["SummaryData"]?["SaveLabel"];
        set { SetHeaderField("SaveLabel", value); OnPropertyChanged(); }
    }

    public long PlayTime
    {
        get => (long?)_save?.Header["SummaryData"]?["PlayTime"] ?? 0L;
        set { SetHeaderField("PlayTime", value); OnPropertyChanged(); }
    }

    public int? StoryYear
    {
        get => (int?)_save?.Header["SummaryData"]?["StoryTime"]?["Year"];
        set { SetStoryTimeField("Year", value); OnPropertyChanged(); }
    }
    public int? StoryDay
    {
        get => (int?)_save?.Header["SummaryData"]?["StoryTime"]?["Day"];
        set { SetStoryTimeField("Day", value); OnPropertyChanged(); }
    }
    public int? StoryHour
    {
        get => (int?)_save?.Header["SummaryData"]?["StoryTime"]?["Hour"];
        set { SetStoryTimeField("Hour", value); OnPropertyChanged(); }
    }
    public int? StoryMinute
    {
        get => (int?)_save?.Header["SummaryData"]?["StoryTime"]?["Minute"];
        set { SetStoryTimeField("Minute", value); OnPropertyChanged(); }
    }

    // --- File operations -------------------------------------------------

    private void OpenFile()
    {
        if (_isDirty && !ConfirmDiscardChanges()) return;

        var dlg = new OpenFileDialog
        {
            Title = "Open save file",
            Filter = "Zero Parades save (*.sav)|*.sav|All files (*.*)|*.*",
            InitialDirectory = ResolveInitialDirectory(),
        };
        if (dlg.ShowDialog() != true) return;

        LoadSave(dlg.FileName);
    }

    /// <summary>
    /// Picks the most sensible directory for the open/save dialogs:
    /// 1. The directory of the currently-loaded file, if any;
    /// 2. The Zero Parades saves folder under %LOCALAPPDATA_LOW%, choosing the
    ///    most recently modified per-user subdirectory if multiple exist;
    /// 3. Empty string (let Windows pick).
    /// </summary>
    private string ResolveInitialDirectory()
    {
        if (_currentPath is not null)
        {
            var dir = Path.GetDirectoryName(_currentPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }
        return ResolveDefaultSavesDirectory() ?? string.Empty;
    }

    /// <summary>
    /// Returns the Zero Parades root folder under %LOCALAPPDATA_LOW% if it
    /// exists, else null. The actual per-user Saves subdirectory is one or
    /// two levels deeper; we stop here deliberately so the user picks the
    /// correct account themselves if multiple are present.
    /// </summary>
    private static string? ResolveDefaultSavesDirectory()
    {
        // There is no SpecialFolder for LocalLow; derive from LocalApplicationData.
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(local)) return null;
        string localLow = local.Replace(
            @"\Local", @"\LocalLow", StringComparison.OrdinalIgnoreCase);
        string gameRoot = Path.Combine(localLow, "ZA UM", "Zero Parades");
        return Directory.Exists(gameRoot) ? gameRoot : null;
    }

    public void LoadSave(string path)
    {
        try
        {
            var loaded = SaveFile.Read(path);
            _save = loaded;
            _currentPath = path;
            _isDirty = false;
            OnPropertyChanged(nameof(IsDirty));
            CharacterCreation = new CharacterCreationViewModel(loaded, MarkDirty);
            RebuildCollections();
            RaiseAllForLoadedSave();
            Status = $"Loaded {Path.GetFileName(path)}.";
        }
        catch (SaveChecksumException ex)
        {
            var resp = MessageBox.Show(
                $"{ex.Message}\n\nLoad anyway? (The file may have already been edited.)",
                "Checksum mismatch", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (resp != MessageBoxResult.Yes) return;
            // Re-parse bypassing the Read() helper, which validated checksums.
            // Here we accept the file as-is by reading bytes and forcing through.
            // Since SaveFile.Parse() rethrows the same exception, the only path
            // is to fix the format library if the user genuinely wants to load
            // corrupt files; for now surface a clearer message and bail.
            Status = "Aborted: save format library does not currently support loading mismatched checksums.";
        }
        catch (SaveFormatException ex)
        {
            MessageBox.Show(ex.Message, "Cannot open file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{ex.GetType().Name}: {ex.Message}", "Cannot open file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveCurrent()
    {
        if (_save is null || _currentPath is null) return;
        WriteWithBackup(_currentPath);
    }

    private void SaveAs()
    {
        if (_save is null) return;
        var dlg = new SaveFileDialog
        {
            Title = "Save as",
            Filter = "Zero Parades save (*.sav)|*.sav",
            DefaultExt = ".sav",
            FileName = _currentPath is null ? "save.sav" : Path.GetFileName(_currentPath),
            InitialDirectory = ResolveInitialDirectory(),
        };
        if (dlg.ShowDialog() != true) return;
        WriteWithBackup(dlg.FileName);
        _currentPath = dlg.FileName;
        OnPropertyChanged(nameof(Title));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private void WriteWithBackup(string path)
    {
        if (_save is null) return;
        try
        {
            if (File.Exists(path))
            {
                try { File.Copy(path, path + ".bak", overwrite: true); }
                catch (IOException ioe)
                {
                    var resp = MessageBox.Show(
                        $"Could not back up the existing file: {ioe.Message}\n\nSave anyway?",
                        "Backup failed", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (resp != MessageBoxResult.Yes) return;
                }
            }
            _save.Write(path);
            _isDirty = false;
            OnPropertyChanged(nameof(IsDirty));
            Status = $"Wrote {Path.GetFileName(path)} (backup at .bak).";
            OnPropertyChanged(nameof(Title));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{ex.GetType().Name}: {ex.Message}", "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public bool ConfirmDiscardChanges()
    {
        return MessageBox.Show(
            "There are unsaved changes. Discard them?",
            "Discard changes?", MessageBoxButton.YesNo, MessageBoxImage.Question
        ) == MessageBoxResult.Yes;
    }

    // --- Internals -------------------------------------------------------

    private void RebuildCollections()
    {
        InventoryItems.Clear();
        Counters.Clear();
        if (_save is null) return;

        foreach (var item in _save.Inventory)
        {
            if (item is JsonObject obj)
                InventoryItems.Add(new InventoryItemViewModel(obj, MarkDirty));
        }

        var keys = (JsonArray?)_save.FeldState["m_countersValues"]?["m_keys"];
        var vals = (JsonArray?)_save.FeldState["m_countersValues"]?["m_values"];
        if (keys is not null && vals is not null)
        {
            int n = Math.Min(keys.Count, vals.Count);
            for (int i = 0; i < n; i++)
                Counters.Add(new CounterViewModel(keys, vals, i, OnCounterChanged));
        }
    }

    private void OnCounterChanged(string key, int value)
    {
        // If the counter that changed is stats.money, sync header Sol too.
        if (key == "stats.money" && _save is not null)
        {
            _save.Header["SummaryData"]!["Sol"] = value;
            OnPropertyChanged(nameof(Sol));
        }
        MarkDirty();
    }

    public void MarkDirty()
    {
        if (!_isDirty)
        {
            _isDirty = true;
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(Title));
        }
    }

    private void RaiseAllForLoadedSave()
    {
        OnPropertyChanged(nameof(HasSave));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Sol));
        OnPropertyChanged(nameof(SaveName));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(PlayTime));
        OnPropertyChanged(nameof(StoryYear));
        OnPropertyChanged(nameof(StoryDay));
        OnPropertyChanged(nameof(StoryHour));
        OnPropertyChanged(nameof(StoryMinute));
    }

    private void SetHeaderField<T>(string field, T value)
    {
        if (_save is null) return;
        var sd = _save.Header["SummaryData"] as JsonObject ?? throw new InvalidOperationException("SummaryData missing");
        sd[field] = JsonValue.Create(value);
        MarkDirty();
    }

    private void SetStoryTimeField<T>(string field, T value)
    {
        if (_save is null) return;
        var sd = _save.Header["SummaryData"] as JsonObject ?? throw new InvalidOperationException("SummaryData missing");
        if (sd["StoryTime"] is not JsonObject st)
        {
            st = new JsonObject();
            sd["StoryTime"] = st;
        }
        st[field] = JsonValue.Create(value);
        MarkDirty();
    }

    private bool InventoryFilterPredicate(object o)
    {
        if (o is not InventoryItemViewModel item) return false;
        if (_hideEquipped && item.IsEquipped) return false;
        if (string.IsNullOrEmpty(_inventoryFilter)) return true;
        return item.EntityId.Contains(_inventoryFilter, StringComparison.OrdinalIgnoreCase);
    }

    private bool CounterFilterPredicate(object o)
    {
        if (o is not CounterViewModel c) return false;
        if (!_showAllCounterGroups && !CommonCounterGroups.Contains(c.Group)) return false;
        if (string.IsNullOrEmpty(_statsFilter)) return true;
        return c.Key.Contains(_statsFilter, StringComparison.OrdinalIgnoreCase);
    }
}
