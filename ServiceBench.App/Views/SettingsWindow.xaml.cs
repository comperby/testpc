using System;
using System.Windows;
using ServiceBench.App.ViewModels;

namespace ServiceBench.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        if (DataContext is SettingsViewModel vm)
        {
            vm.RequestClose += (_, result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
