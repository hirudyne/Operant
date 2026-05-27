using System;
using System.IO;
using System.Windows;
using Operant.Editor.ViewModels;

namespace Operant.Editor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 2 && File.Exists(args[1]) && DataContext is MainViewModel vm)
            {
                vm.LoadSave(args[1]);
            }
        };

        Closing += (_, e) =>
        {
            if (DataContext is MainViewModel vm && vm.IsDirty)
            {
                if (!vm.ConfirmDiscardChanges()) e.Cancel = true;
            }
        };
    }


    private void ExitClick(object sender, RoutedEventArgs e) => Close();

    private void AboutClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Operant - Zero Parades Save Editor\n\n" +
            "Edits Sol, inventory amounts/equip state, and FELDState counters " +
            "in Zero Parades save files. Handles AES decryption, Hash128 " +
            "checksums, and re-encryption transparently.",
            "About",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
