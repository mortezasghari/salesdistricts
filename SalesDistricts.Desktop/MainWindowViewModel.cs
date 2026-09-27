using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SalesDistricts.Core;

namespace SalesDistricts.Desktop;

public partial class MainWindowViewModel(ApiClient api) : ObservableObject
{
    public ObservableCollection<District> Districts { get; } = [];

    [ObservableProperty] private District? _selectedDistrict;
    [ObservableProperty] private DistrictDetails? _details;
    [ObservableProperty] private string? _error;

    public async Task LoadAsync()
    {
        try
        {
            Error = null;
            Districts.Clear();
            foreach (var district in await api.GetDistrictsAsync())
            {
                Districts.Add(district);
            }
        }
        catch (Exception ex)
        {
            Error = $"Could not load districts: {ex.Message}";
        }
    }

    async partial void OnSelectedDistrictChanged(District? value)
    {
        Details = null;
        if (value is null)
        {
            return;
        }

        try
        {
            Error = null;
            Details = await api.GetDistrictAsync(value.Id);
        }
        catch (Exception ex)
        {
            Error = $"Could not load {value.Name}: {ex.Message}";
        }
    }
}
