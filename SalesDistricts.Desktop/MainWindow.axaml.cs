using Avalonia.Controls;

namespace SalesDistricts.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var apiUrl = Environment.GetEnvironmentVariable("SALESDISTRICTS_API") ?? "http://localhost:8080/";
        var viewModel = new MainWindowViewModel(new ApiClient(new HttpClient { BaseAddress = new Uri(apiUrl) }));
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
