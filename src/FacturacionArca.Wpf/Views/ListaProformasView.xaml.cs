using System.Windows;
using System.Windows.Controls;
using FacturacionArca.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace FacturacionArca.Wpf.Views;

public partial class ListaProformasView : UserControl
{
    public ListaProformasView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ListaProformasViewModel vm)
        {
            vm.PropertyChanged += async (_, ev) =>
            {
                if (ev.PropertyName == nameof(ListaProformasViewModel.SelectedProforma) && vm.SelectedProforma is not null)
                {
                    var detalleVm = App.Host.Services.GetRequiredService<DetalleProformaViewModel>();
                    detalleVm.Cargar(vm.SelectedProforma);
                    var view = new DetalleProformaView { DataContext = detalleVm };
                    DetallePanel.Content = view;
                    await Task.Yield();
                }
            };
        }
    }

    private async void ImportarManualClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ListaProformasViewModel vm) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Proforma XML|*.xml;*.XML|Todos los archivos|*.*",
            Title = "Seleccionar proforma de Nápoles",
        };
        if (dlg.ShowDialog() == true)
            await vm.ImportarManualAsync(dlg.FileName);
    }
}
