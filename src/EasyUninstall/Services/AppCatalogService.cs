using EasyUninstall.Contracts;
using EasyUninstall.Models;

namespace EasyUninstall.Services;

/// <summary>Merges app sources with dedup, filtering, sorting and refresh.</summary>
public sealed class AppCatalogService : IAppCatalogService
{
    private readonly IReadOnlyList<IAppSource> _sources;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<InstalledApp>? _cache;

    public AppCatalogService(IEnumerable<IAppSource> sources)
        => _sources = sources.ToArray();

    public async Task<IReadOnlyList<InstalledApp>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache is null)
                _cache = await ScanAsync(cancellationToken);
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Forces a rescan of all sources.</summary>
    public async Task<IReadOnlyList<InstalledApp>> RefreshAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _cache = await ScanAsync(cancellationToken);
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<InstalledApp>> ScanAsync(CancellationToken cancellationToken)
    {
        var all = new List<InstalledApp>();
        foreach (var source in _sources)
        {
            try
            {
                all.AddRange(await source.GetAppsAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // One failing source must not break the whole catalog; skip it.
            }
        }
        return all
            .DistinctBy(a => (a.Category, a.Id))
            .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

/// <summary>Sort/filter helpers (pure, testable).</summary>
public static class AppCatalogExtensions
{
    public enum SortField { Name, InstallDate, Publisher }

    public static IEnumerable<InstalledApp> FilterByCategory(
        this IEnumerable<InstalledApp> apps, AppCategory? category)
        => category is null ? apps : apps.Where(a => a.Category == category);

    public static IEnumerable<InstalledApp> Search(
        this IEnumerable<InstalledApp> apps, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return apps;
        var q = query.Trim();
        return apps.Where(a => a.DisplayName.Contains(q, StringComparison.CurrentCultureIgnoreCase));
    }

    public static IEnumerable<InstalledApp> Sort(
        this IEnumerable<InstalledApp> apps, SortField field, bool descending = false)
    {
        var ordered = field switch
        {
            SortField.InstallDate => apps.OrderBy(a => a.InstallDate ?? DateTime.MinValue),
            SortField.Publisher => apps.OrderBy(a => a.Publisher, StringComparer.CurrentCultureIgnoreCase),
            _ => apps.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        };
        return descending ? ordered.Reverse() : ordered;
    }
}
