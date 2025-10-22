using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Specialized;
using System.Linq;

namespace DromHub.Views
{
    /// <summary>
    /// Класс BrandsIndexPage отвечает за логику компонента BrandsIndexPage.
    /// </summary>
    public sealed partial class BrandsIndexPage : Page
    {
        /// <summary>
        /// Свойство ViewModel предоставляет доступ к данным ViewModel.
        /// </summary>
        public BrandsIndexViewModel ViewModel { get; }
        /// <summary>
        /// Конструктор BrandsIndexPage инициализирует экземпляр класса.
        /// </summary>

        public BrandsIndexPage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandsIndexViewModel>();
            DataContext = ViewModel;

            Loaded += async (_, __) =>
            {
                if (ViewModel.GroupedBrands is INotifyCollectionChanged oc)
                    oc.CollectionChanged += (_, ____) => BuildIndexBar();

                await ViewModel.LoadAsync();
                BuildIndexBar();
            };
        }
        /// <summary>
        /// Метод OnSearchKeyDown выполняет основную операцию класса.
        /// </summary>

        private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key.ToString() == "Enter")
                ViewModel.ApplyFilters();
        }
        /// <summary>
        /// Метод Refresh_Click выполняет основную операцию класса.
        /// </summary>

        private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel.ApplyFilters();

        // ===== A–Z индекс, растянутый по высоте
        /// <summary>
        /// Метод BuildIndexBar выполняет основную операцию класса.
        /// </summary>
        private void BuildIndexBar()
        {
            if (IndexHost == null || BrandsList == null) return;

            IndexHost.Children.Clear();
            IndexHost.RowDefinitions.Clear();

            var groups = ViewModel.GroupedBrands?
                .Where(g => g != null && g.Count > 0)
                .OrderBy(g => g.Key)
                .ToList();

            if (groups == null || groups.Count == 0) return;

            for (int i = 0; i < groups.Count; i++)
            {
                IndexHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var letter = groups[i].Key;
                var btn = new Button
                {
                    Content = letter,
                    Tag = letter,
                    Background = null,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                btn.Click += (s, e) =>
                {
                    var l = (string)((Button)s).Tag;
                    var group = ViewModel.GroupedBrands
                        .FirstOrDefault(g => string.Equals(g.Key, l, StringComparison.OrdinalIgnoreCase));
                    var first = group?.FirstOrDefault();
                    if (first != null)
                        BrandsList.ScrollIntoView(first, ScrollIntoViewAlignment.Leading);
                };

                Grid.SetRow(btn, i);
                IndexHost.Children.Add(btn);
            }
        }
        /// <summary>
        /// Метод OpenBrand_Click выполняет основную операцию класса.
        /// </summary>

        private void OpenBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand is Brand b)
                Frame?.Navigate(typeof(BrandShellPage), b.Id);
        }
        /// <summary>
        /// Метод CreatePart_Click выполняет основную операцию класса.
        /// </summary>

        private void CreatePart_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog
            {
                Title = "Создать запчасть",
                Content = "Откройте мастер создания запчасти для выбранного бренда.",
                CloseButtonText = "ОК",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
        /// <summary>
        /// Метод OpenMergeWizard_Click выполняет основную операцию класса.
        /// </summary>

        private void OpenMergeWizard_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(BrandMergeWizardPage));
        }
    }
}