using System;
using Microsoft.Win32;
using ServiceBench.App.Models;
using ServiceBench.App.Services;

namespace ServiceBench.App.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private AppSettings _settings;

    public SettingsViewModel()
        : this(new SettingsService())
    {
    }

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settings = settingsService.Load();
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        BrowseAidaCommand = new RelayCommand(() => BrowsePath(path => Settings.AidaPath = path));
        BrowseOcctCommand = new RelayCommand(() => BrowsePath(path => Settings.OcctPath = path));
        BrowseFurmarkCommand = new RelayCommand(() => BrowsePath(path => Settings.FurmarkPath = path));
        BrowseLogoCommand = new RelayCommand(() => BrowsePath(path => Settings.Brand.LogoPath = path, filter: "Image Files|*.png;*.jpg;*.jpeg"));
    }

    public event EventHandler<bool>? RequestClose;

    public AppSettings Settings
    {
        get => _settings;
        set => SetField(ref _settings, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand BrowseAidaCommand { get; }
    public RelayCommand BrowseOcctCommand { get; }
    public RelayCommand BrowseFurmarkCommand { get; }
    public RelayCommand BrowseLogoCommand { get; }

    private void Save()
    {
        _settingsService.Save(Settings);
        RequestClose?.Invoke(this, true);
    }

    private void BrowsePath(Action<string?> setter, string filter = "Executable|*.exe")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter
        };
        if (dialog.ShowDialog() == true)
        {
            setter(dialog.FileName);
            RaisePropertyChanged(nameof(Settings));
        }
    }
}
