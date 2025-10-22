using System;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;

namespace DromHub.Views
{
    /// <summary>
    /// Класс BrandSettingsPage отвечает за логику компонента BrandSettingsPage.
    /// </summary>
    public sealed partial class BrandSettingsPage : Page
    {
        private readonly IReadOnlyList<Expander> _accordion;

        /// <summary>
        /// Gets the page view model.
        /// </summary>
        public BrandSettingsViewModel ViewModel { get; }

        /// <summary>
        /// Конструктор BrandSettingsPage инициализирует экземпляр класса.
        /// </summary>
        public BrandSettingsPage()
        {
            InitializeComponent();

            ViewModel = App.ServiceProvider.GetRequiredService<BrandSettingsViewModel>();
            DataContext = ViewModel;

            _accordion = new[] { ExpCore, ExpAliases, ExpPricing, ExpUser };

            foreach (var ex in _accordion)
            {
                ex.Expanding += Accordion_Expanding;
            }

            Loaded += (_, __) =>
            {
                // открыть первый, остальные закрыть
                ExpCore.IsExpanded = true;
                for (int i = 1; i < _accordion.Count; i++)
                {
                    _accordion[i].IsExpanded = false;
                }
            };
        }

        /// <summary>
        /// Handles navigation to the page and triggers loading of the brand data.
        /// </summary>
        /// <param name="e">Navigation arguments.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var brandId = e?.Parameter switch
            {
                Guid g => g,
                string s when Guid.TryParse(s, out var parsed) => parsed,
                _ => Guid.Empty
            };

            if (brandId != Guid.Empty)
            {
                await ViewModel.InitializeAsync(brandId, XamlRoot);
            }
        }

        /// <summary>
        /// Метод Accordion_Expanding выполняет основную операцию класса.
        /// </summary>
        private void Accordion_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            foreach (var ex in _accordion)
            {
                if (!ReferenceEquals(ex, sender))
                {
                    ex.IsExpanded = false;
                }
            }
        }
    }
}
