using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace DromHub.Views
{
    public sealed partial class BrandPage : Page
    {
        public BrandViewModel ViewModel { get; }

        private DispatcherTimer _undoTimer;

        public BrandPage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandViewModel>();
            DataContext = ViewModel;

            Loaded += async (_, __) =>
            {
                ViewModel.XamlRoot = this.XamlRoot;

                if (ViewModel.GroupedBrands is INotifyCollectionChanged oc)
                    oc.CollectionChanged += (_, ____) => DispatcherQueue.TryEnqueue(BuildIndexBar);

                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
                BuildIndexBar();
            };
        }

        #region Левый столбец: бренды

        private async void AddBrand_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetBrand();
            var dialog = new AddBrandDialog(ViewModel) { XamlRoot = this.XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.SaveBrandCommand.ExecuteAsync(null);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
                BuildIndexBar();
            }
        }

        private async void EditBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand is Brand b)
            {
                await ViewModel.EditBrand(b);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
                BuildIndexBar();
            }
        }

        private async void DeleteBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand != null)
            {
                await ViewModel.DeleteBrandCommand.ExecuteAsync(this.XamlRoot);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
                BuildIndexBar();
            }
        }

        // Индекс-полоса справа от списка
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
                btn.Click += (s, e) =>
                {
                    var l = (string)((Button)s).Tag;
                    var group = ViewModel.GroupedBrands.FirstOrDefault(g => string.Equals(g.Key, l, StringComparison.OrdinalIgnoreCase));
                    var first = group?.FirstOrDefault();
                    if (first != null)
                        BrandsList.ScrollIntoView(first, ScrollIntoViewAlignment.Leading);
                };

                Grid.SetRow(btn, i);
                IndexHost.Children.Add(btn);
            }
        }

        // контекстные действия
        private void CreatePart_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog
            {
                Title = "Создать запчасть",
                Content = "Откройте мастер создания запчасти для выбранного бренда.",
                CloseButtonText = "Ок",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }

        private void MergeBrands_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog
            {
                Title = "Слияние брендов",
                Content = "Запустите мастер объединения брендов (перенос деталей и алиасов).",
                CloseButtonText = "Ок",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }

        #endregion

        #region Правая колонка: наценка

        // пресеты значения %
        private void PresetMarkup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && double.TryParse(b.Tag?.ToString(), out var preset))
                ViewModel.BrandMarkupPercent = preset;
        }


        #endregion

        #region Синонимы + Undo

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

            var toRestore = ViewModel.SelectedAlias; // для проверки успеха
            await ViewModel.DeleteAliasCommand.ExecuteAsync(this.XamlRoot);
            await ViewModel.LoadAliasesCommand.ExecuteAsync(null);

            // если синоним реально удалился — показываем undo
            if (ViewModel.Aliases.FirstOrDefault(a => a.Id == toRestore.Id) == null &&
                ViewModel.LastDeletedAlias != null)
            {
                ShowUndoBar();
            }
        }

        private void ShowUndoBar()
        {
            UndoBar.IsOpen = true;

            _undoTimer?.Stop();
            _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _undoTimer.Tick += (_, __) =>
            {
                _undoTimer.Stop();
                UndoBar.IsOpen = false;
                // если пользователь не нажал "Отменить", просто скрываем
            };
            _undoTimer.Start();
        }

        private async void UndoDeleteAlias_Click(object sender, RoutedEventArgs e)
        {
            _undoTimer?.Stop();
            UndoBar.IsOpen = false;

            var alias = ViewModel.LastDeletedAlias;
            await ViewModel.UndoDeleteAliasCommand.ExecuteAsync(alias);
        }

        #endregion
    }
}