using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{
    public class BrandsHomeViewModel : ObservableObject
    {
        public Task LoadAsync() => Task.CompletedTask;
    }
}