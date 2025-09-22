using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Specialized;
using System.Linq;

namespace DromHub.Views
{
    public sealed partial class BrandPage : Page
    {
        public BrandViewModel ViewModel { get; }

        public BrandPage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandViewModel>();
            DataContext = ViewModel;

            Loaded += async (_, __) =>
            {
                ViewModel.XamlRoot = this.XamlRoot;

                // реагируем на перестройку групп
                if (ViewModel.GroupedBrands is INotifyCollectionChanged oc)
                    oc.CollectionChanged += GroupedBrands_CollectionChanged;

                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
                BuildIndexBar(); // первичная постройка
            };

            Unloaded += (_, __) =>
            {
                if (ViewModel?.GroupedBrands is INotifyCollectionChanged oc)
                    oc.CollectionChanged -= GroupedBrands_CollectionChanged;
            };
        }

        // ===== БРЕНДЫ =====
        private async void AddBrand_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetBrand();
            var dialog = new AddBrandDialog(ViewModel) { XamlRoot = this.XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.SaveBrandCommand.ExecuteAsync(null);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
            }
        }

        private async void EditBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand is Brand b)
            {
                ViewModel.XamlRoot = this.XamlRoot;
                await ViewModel.EditBrand(b);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
            }
        }

        private async void DeleteBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand != null)
            {
                await ViewModel.DeleteBrandCommand.ExecuteAsync(this.XamlRoot);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
            }
        }

        private void GroupedBrands_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            BuildIndexBar();
        }

        /// <summary>
        /// Строим индекс-полосу из фактических групп (равномерное растяжение).
        /// </summary>
        private void BuildIndexBar()
        {
            if (IndexHost == null) return;

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
                btn.Click += JumpIndex_Click;

                Grid.SetRow(btn, i);
                IndexHost.Children.Add(btn);
            }
        }

        private void JumpIndex_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string letter && !string.IsNullOrWhiteSpace(letter))
            {
                var group = ViewModel.GroupedBrands?
                    .FirstOrDefault(g => string.Equals(g.Key, letter, StringComparison.OrdinalIgnoreCase));

                var first = group?.FirstOrDefault();
                if (first != null)
                {
                    BrandsList.ScrollIntoView(first, ScrollIntoViewAlignment.Leading);
                }
            }
        }

        // ===== СИНОНИМЫ =====
        private async void AddAlias_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand == null) return;

            var dialog = new AddAliasDialog { XamlRoot = this.XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.SaveAliasAsync(dialog.AliasName);
                await ViewModel.LoadAliasesCommand.ExecuteAsync(null);
            }
        }

        private async void EditAlias_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedAlias == null) return;
            await ViewModel.EditAliasCommand.ExecuteAsync(this.XamlRoot);
            await ViewModel.LoadAliasesCommand.ExecuteAsync(null);
        }

        private async void DeleteAlias_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedAlias == null) return;
            await ViewModel.DeleteAliasCommand.ExecuteAsync(this.XamlRoot);
            await ViewModel.LoadAliasesCommand.ExecuteAsync(null);
        }

        // ===== НАЦЕНКА (пресеты) =====
        private void PresetMarkup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && double.TryParse(b.Tag?.ToString(), out var preset))
            {
                ViewModel.BrandMarkupPercent = preset;
                // Переключатель применения не трогаем
            }
        }
    }
}