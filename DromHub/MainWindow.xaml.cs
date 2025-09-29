using DromHub.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DromHub
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Стартовая страница
            contentFrame.Navigate(typeof(MainPage));
            nvSample.SelectedItem = MainPageItem;
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                // Если будет страница настроек:
                // contentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                NavigateByTag(tag);
            }
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked) return;

            // Обработка кликов по дочерним пунктам (надёжно для MenuItems)
            if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
            {
                NavigateByTag(tag);
            }
            else if (args.InvokedItem is string header) // fallback по заголовку
            {
                // Не обязательно, но оставлю на случай, если Tag забудут
            }
        }

        private void NavigateByTag(string tag, object parameter = null)
        {
            Type pageType = tag switch
            {
                // Главная
                "MainPage" => typeof(MainPage),

                // Запчасти
                "PartPage" => typeof(PartSearchPage),      // родитель ведёт на поиск
                "PartSearchPage" => typeof(PartSearchPage),

                // Бренды
                "BrandsOverviewPage" => typeof(BrandsHomePage),
                "BrandsListPage" => typeof(BrandsIndexPage),
                "BrandMergePage" => typeof(BrandMergeWizardPage),

                // по умолчанию ничего не делаем
                _ => null
            };

            if (pageType != null)
            {
                if (contentFrame.CurrentSourcePageType != pageType || parameter != null)
                {
                    contentFrame.Navigate(pageType, parameter);
                }
            }
        }
    }
}