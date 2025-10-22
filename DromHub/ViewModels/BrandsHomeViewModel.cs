using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{
    /// <summary>
    /// Класс BrandsHomeViewModel отвечает за логику компонента BrandsHomeViewModel.
    /// </summary>
    public class BrandsHomeViewModel : ObservableObject
    {
        /// <summary>
        /// Метод LoadAsync выполняет основную операцию класса.
        /// </summary>
        public Task LoadAsync() => Task.CompletedTask;
    }
}