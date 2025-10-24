using System;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Models;
using DromHub.Services;

namespace DromHub.ViewModels;

public partial class BrandChangesViewModel : ObservableObject
{
    private readonly BrandAuditService _service;

    public BrandChangesViewModel(BrandAuditService service)
    {
        _service = service;
        Items = new ObservableCollection<BrandAuditRow>();
        PageSizes = new ObservableCollection<int> { 10, 25, 50, 100 };

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, () => (PageIndex + 1) * PageSize < TotalCount);
        PrevPageCommand = new AsyncRelayCommand(PrevPageAsync, () => PageIndex > 0);
    }

    // Входной бренд
    [ObservableProperty] private Guid brandId;

    // Фильтры
    [ObservableProperty] private DateTimeOffset? fromDate = null;
    [ObservableProperty] private DateTimeOffset? toDate = null;
    [ObservableProperty] private AuditActionFilter action = AuditActionFilter.All;
    [ObservableProperty] private bool onlyChangedFields = false;
    [ObservableProperty] private string? search;


    // Пагинация
    public ObservableCollection<int> PageSizes { get; }
    [ObservableProperty] private int pageSize = 25;
    [ObservableProperty] private int pageIndex = 0;
    [ObservableProperty] private int totalCount = 0;

    // UI
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    public ObservableCollection<BrandAuditRow> Items { get; }

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PrevPageCommand { get; }

    partial void OnPageSizeChanged(int value)
    {
        PageIndex = 0;
        _ = LoadAsync();
    }

    partial void OnActionChanged(AuditActionFilter value) => _ = LoadAsync();

    public async Task InitializeAsync(Guid brandId)
    {
        BrandId = brandId;
        PageIndex = 0;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        DateTime? from = FromDate?.DateTime.Date;                         // начало дня (локально)
        DateTime? to = ToDate?.DateTime.Date.AddDays(1).AddTicks(-1);   // конец дня

        var filter = new BrandAuditFilter
        {
            BrandId = BrandId,
            From = from,
            To = to,
            Action = Action,
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search,
            OnlyChangedFields = OnlyChangedFields,
            PageIndex = PageIndex,
            PageSize = PageSize
        };

    }

    private Task NextPageAsync()
    {
        if ((PageIndex + 1) * PageSize >= TotalCount) return Task.CompletedTask;
        PageIndex++;
        return LoadAsync();
    }

    private Task PrevPageAsync()
    {
        if (PageIndex == 0) return Task.CompletedTask;
        PageIndex--;
        return LoadAsync();
    }
}
