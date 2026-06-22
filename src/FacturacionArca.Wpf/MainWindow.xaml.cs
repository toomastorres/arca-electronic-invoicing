using System.Windows;
using FacturacionArca.Wpf.ViewModels;

namespace FacturacionArca.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
