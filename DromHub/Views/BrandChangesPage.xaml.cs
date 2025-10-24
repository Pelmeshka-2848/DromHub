using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using DromHub.ViewModels;

namespace DromHub.Views
{
    public sealed partial class BrandChangesPage : Page
    {
        public BrandChangesViewModel VM { get; }

        public BrandChangesPage()
        {
            InitializeComponent();
            VM = App.ServiceProvider.GetRequiredService<BrandChangesViewModel>();
            DataContext = VM;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Guid id && id != Guid.Empty)
            {
                await VM.InitializeAsync(id);
            }
            else
            {
                // на всякий случай покажем понятный статус
                VM.BrandId = Guid.Empty;
                VM.Items.Clear();
                VM.TotalCount = 0;
                VM.ErrorMessage = "BrandId не передан — изменений нет.";
            }
        }
    }
}
