using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SalesDistricts.Core;

namespace SalesDistricts.Desktop;

public partial class MainWindowViewModel(ApiClient api) : ObservableObject
{
    public ObservableCollection<District> Districts { get; } = [];
    public ObservableCollection<Salesperson> AllSalespersons { get; } = [];

    [ObservableProperty] private District? _selectedDistrict;
    [ObservableProperty] private DistrictDetails? _details;
    [ObservableProperty] private Salesperson? _salespersonToAdd;
    [ObservableProperty] private bool _addAsPrimary;
    [ObservableProperty] private string? _error;

    public async Task LoadAsync()
    {
        await Try(async () =>
        {
            Districts.Clear();
            foreach (var district in await api.GetDistrictsAsync()) Districts.Add(district);

            AllSalespersons.Clear();
            foreach (var salesperson in await api.GetSalespersonsAsync()) AllSalespersons.Add(salesperson);
        });
    }

    partial void OnSelectedDistrictChanged(District? value) => _ = ReloadDetailsAsync();

    [RelayCommand]
    private async Task AddAsync()
    {
        if (SelectedDistrict is null || SalespersonToAdd is null) return;
        await Change(api.AssignAsync(SelectedDistrict.Id, SalespersonToAdd.Id, AddAsPrimary ? Role.Primary : Role.Secondary));
    }

    [RelayCommand]
    private async Task MakePrimaryAsync(DistrictSalesperson salesperson)
    {
        if (SelectedDistrict is null) return;
        await Change(api.AssignAsync(SelectedDistrict.Id, salesperson.Id, Role.Primary));
    }

    [RelayCommand]
    private async Task RemoveAsync(DistrictSalesperson salesperson)
    {
        if (SelectedDistrict is null) return;
        await Change(api.RemoveAsync(SelectedDistrict.Id, salesperson.Id));
    }

    private async Task Change(Task<string?> apiCall)
    {
        await Try(async () =>
        {
            Error = await apiCall;
            await ReloadDetailsAsync();
        });
    }

    private async Task ReloadDetailsAsync()
    {
        Details = null;
        if (SelectedDistrict is { } district)
        {
            await Try(async () => Details = await api.GetDistrictAsync(district.Id));
        }
    }

    private async Task Try(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
