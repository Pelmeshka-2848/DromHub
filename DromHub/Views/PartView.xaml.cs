using DromHub.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DromHub.Views
{
    public sealed partial class PartView : Page
    {
        public PartViewModel ViewModel { get; }

        public PartView()
        {
            this.InitializeComponent();

            // �������� ViewModel ����� DI
            ViewModel = App.GetService<PartViewModel>();

            // ������������� DataContext
            this.DataContext = ViewModel;

            // ��������� ������ ��� �������������
            Loaded += async (s, e) => await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
        }
    }
}