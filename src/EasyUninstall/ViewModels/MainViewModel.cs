using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyUninstall.Contracts;
using EasyUninstall.Models;
using EasyUninstall.Services;

namespace EasyUninstall.ViewModels;

/// <summary>Main page view model: catalog loading, filtering, sorting, uninstall flow.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IAppCatalogService _catalog;
    private readonly IUninstallService _uninstallService;
    private IReadOnlyList<InstalledApp> _masterList = [];

    public enum CategoryFilter { All, Desktop, Store }

    public MainViewModel(IAppCatalogService catalog, IUninstallService uninstallService)
    {
        _catalog = catalog;
        _uninstallService = uninstallService;

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SearchText) or nameof(SelectedCategory)
                or nameof(SortField) or nameof(SortDescending))
                ApplyFilters();
        };

        LocalizationService.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty)); // re-trigger; status text is rewritten by window
        };
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CategoryFilter _selectedCategory = CategoryFilter.All;

    [ObservableProperty]
    private AppCatalogExtensions.SortField _sortField = AppCatalogExtensions.SortField.Name;

    [ObservableProperty]
    private bool _sortDescending;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    private InstalledApp? _selectedApp;

    /// <summary>Filtered/sorted view of the catalog shown in the UI.</summary>
    public ObservableCollection<InstalledApp> ViewApps { get; } = [];

    public bool IsEmpty => !IsBusy && ViewApps.Count == 0;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        HasError = false;
        StatusMessage = LocalizationService.Get("StatusLoading");
        try
        {
            var apps = await _catalog.GetCatalogAsync(cancellationToken);
            _masterList = apps;
            ApplyFilters();
            StatusMessage = LocalizationService.Format("StatusLoaded", apps.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = LocalizationService.Get("StatusLoadCancelled");
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = LocalizationService.Format("StatusLoadFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        HasError = false;
        StatusMessage = LocalizationService.Get("StatusRefreshing");
        try
        {
            var apps = await _catalog.RefreshAsync(cancellationToken);
            _masterList = apps;
            ApplyFilters();
            StatusMessage = LocalizationService.Format("StatusRefreshed", apps.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = LocalizationService.Get("StatusRefreshCancelled");
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = LocalizationService.Format("StatusRefreshFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanUninstallSelected))]
    public async Task UninstallAsync(CancellationToken cancellationToken)
    {
        if (SelectedApp is not { } app) return;
        if (!app.CanUninstall)
        {
            StatusMessage = app.UnavailableReason ?? LocalizationService.Get("CannotUninstall");
            return;
        }

        IsBusy = true;
        StatusMessage = LocalizationService.Format("StatusUninstalling", app.DisplayName);
        try
        {
            var result = await _uninstallService.UninstallAsync(app, cancellationToken);
            HasError = result.Outcome is UninstallOutcome.Failed;
            StatusMessage = result.Outcome switch
            {
                UninstallOutcome.Success => LocalizationService.Format("ResultSuccess", app.DisplayName, result.Message),
                UninstallOutcome.Cancelled => LocalizationService.Format("ResultCancelled", app.DisplayName),
                UninstallOutcome.Unknown => LocalizationService.Format("ResultUnknown", app.DisplayName, result.Message),
                _ => LocalizationService.Format("ResultFailed", app.DisplayName, result.Message)
            };

            if (result.Outcome is UninstallOutcome.Success or UninstallOutcome.Unknown)
            {
                var apps = await _catalog.RefreshAsync(cancellationToken);
                _masterList = apps;
                ApplyFilters();
                SelectedApp = null;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = LocalizationService.Get("StatusCancelled");
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = LocalizationService.Format("StatusUninstallFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUninstallSelected() => !IsBusy && SelectedApp?.CanUninstall == true;

    [RelayCommand(CanExecute = nameof(CanForceUninstallSelected))]
    public async Task ForceUninstallAsync(CancellationToken cancellationToken)
    {
        if (SelectedApp is not { } app) return;
        if (app.CanUninstall)
        {
            StatusMessage = LocalizationService.Get("WarnUnverifiable");
            return;
        }

        IsBusy = true;
        StatusMessage = LocalizationService.Format("StatusUninstalling", app.DisplayName);
        try
        {
            var result = await _uninstallService.ForceUninstallAsync(app, cancellationToken);
            HasError = result.Outcome is UninstallOutcome.Failed;
            StatusMessage = result.Outcome switch
            {
                UninstallOutcome.Success => LocalizationService.Format("ResultSuccess", app.DisplayName, result.Message),
                UninstallOutcome.Cancelled => LocalizationService.Format("ResultCancelled", app.DisplayName),
                UninstallOutcome.Unknown => LocalizationService.Format("ResultUnknown", app.DisplayName, result.Message),
                _ => LocalizationService.Format("ResultFailed", app.DisplayName, result.Message)
            };

            if (result.Outcome is UninstallOutcome.Success or UninstallOutcome.Unknown)
            {
                var apps = await _catalog.RefreshAsync(cancellationToken);
                _masterList = apps;
                ApplyFilters();
                SelectedApp = null;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = LocalizationService.Get("StatusCancelled");
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = LocalizationService.Format("StatusUninstallFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanForceUninstallSelected() => !IsBusy && SelectedApp is { CanUninstall: false };

    /// <summary>Shorthand for localized strings.</summary>
    private static string L(string key) => LocalizationService.Get(key);

    private void ApplyFilters()
    {
        ViewApps.Clear();
        foreach (var a in _masterList
                     .FilterByCategory(SelectedCategory switch
                     {
                         CategoryFilter.Desktop => AppCategory.Desktop,
                         CategoryFilter.Store => AppCategory.Store,
                         _ => null
                     })
                     .Search(SearchText)
                     .Sort(SortField, SortDescending))
            ViewApps.Add(a);
        OnPropertyChanged(nameof(IsEmpty));
    }
}
