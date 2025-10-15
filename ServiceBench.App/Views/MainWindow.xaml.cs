using System;
using System.ComponentModel;
using System.Windows;
using ServiceBench.App.ViewModels;

namespace ServiceBench.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ViewModel.AttachHotkey(this);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        ViewModel.DetachHotkey();
    }
}
