using System;
using DromHub.Models;
using DromHub.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DromHub
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // «агружаем стартовую страницу
            contentFrame.Navigate(typeof(MainPage));
        }

        private void NavigationView_SelectionChanged(NavigationView sender,
                                                   NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer != null)
            {
                string tag = args.SelectedItemContainer.Tag.ToString();
                Type pageType = null;

                // ќпредел€ем тип страницы по тегу
                switch (tag)
                {
                    case "MainPage":
                        pageType = typeof(MainPage);
                        break;
                    case "PartSearchPage":
                        pageType = typeof(PartSearchPage);
                        break;
                }

                // ≈сли тип страницы определен и это не текуща€ страница
                if (pageType != null && contentFrame.CurrentSourcePageType != pageType)
                {
                    contentFrame.Navigate(pageType);
                }
            }
        }
    }
}