using DromHub.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Graphics;
using WinRT.Interop;

namespace DromHub
{
    public sealed partial class MainWindow : Window
    {
        private const double CinemaAspect = 21.0 / 9.0; // ≈ 2.333...  1.618
        private bool _isAdjusting;                      // защита от рекурсии

        public MainWindow()
        {
            InitializeComponent();

            // 1) Стартовый максимальный 21:9 внутри рабочей области (учитывает DPI/панель задач)
            ResizeCinemaToWorkArea();
            CenterOnScreen();

            // 2) Держим аспект 21:9 — подписка на изменение размера ИМЕННО ОКНА
            var appWindow = GetAppWindow();
            appWindow.Changed += AppWindow_Changed;

            // 3) Стартовая страница
            contentFrame.Navigate(typeof(MainPage));
            nvSample.SelectedItem = MainPageItem;
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected) return;

            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
                NavigateByTag(tag);
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked) return;

            if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
                NavigateByTag(tag);
        }

        private void NavigateByTag(string tag, object parameter = null)
        {
            Type pageType = tag switch
            {
                "MainPage" => typeof(MainPage),
                "PartPage" => typeof(PartSearchPage),
                "PartSearchPage" => typeof(PartSearchPage),
                "BrandsOverviewPage" => typeof(BrandsHomePage),
                "BrandsListPage" => typeof(BrandsIndexPage),
                "BrandMergePage" => typeof(BrandMergeWizardPage),
                _ => null
            };

            if (pageType != null && (contentFrame.CurrentSourcePageType != pageType || parameter != null))
                contentFrame.Navigate(pageType, parameter);
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!args.DidSizeChange || _isAdjusting) return;

            try
            {
                _isAdjusting = true;

                var displayArea = DisplayArea.GetFromWindowId(sender.Id, DisplayAreaFallback.Nearest);
                var work = displayArea.WorkArea;

                int w = sender.Size.Width;
                int h = sender.Size.Height;

                // текущий аспект и целевая высота под 21:9
                double targetH = w / CinemaAspect;

                // если заметно ушли от 21:9 — корректируем
                if (Math.Abs(h - targetH) > 1.0)
                {
                    int newW = w;
                    int newH = (int)Math.Round(targetH);

                    // в границах рабочей области
                    if (newH > work.Height)
                    {
                        newH = work.Height;
                        newW = (int)Math.Round(newH * CinemaAspect);
                    }
                    if (newW > work.Width)
                    {
                        newW = work.Width;
                        newH = (int)Math.Round(newW / CinemaAspect);
                    }

                    // (необязательно) минимальный размер 21:9, чтобы окно не превращали в «спичку»
                    const int minW = 3200; // подставь своё желаемое
                    int minH = (int)Math.Round(minW / CinemaAspect);
                    if (newW < minW) { newW = minW; newH = minH; }

                    sender.Resize(new SizeInt32(newW, newH));
                }
            }
            finally
            {
                _isAdjusting = false;
            }
        }

        private void ResizeCinemaToWorkArea()
        {
            var appWindow = GetAppWindow();
            if (appWindow == null) return;

            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
            var work = displayArea.WorkArea; // уже в нужных единицах с учётом DPI

            // Максимально возможная ширина в пределах рабочей области
            const double MaxWidthRatio = 0.90; // 90% от рабочей ширины
            int targetW = (int)Math.Round(work.Width * MaxWidthRatio);
            int targetH = (int)Math.Round(targetW / CinemaAspect);

            if (targetH > work.Height)
            {
                targetH = work.Height;
                targetW = (int)Math.Round(targetH * CinemaAspect);
            }

            appWindow.Resize(new SizeInt32(targetW, targetH));
        }

        private AppWindow GetAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        private void CenterOnScreen()
        {
            var appWindow = GetAppWindow();
            if (appWindow == null) return;

            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
            var work = displayArea.WorkArea;

            int x = work.X + (work.Width - appWindow.Size.Width) / 2;
            int y = work.Y + (work.Height - appWindow.Size.Height) / 2;
            appWindow.Move(new PointInt32(x, y));
        }

        // Удалить подписку на Root.SizeChanged и сам обработчик Root_SizeChanged
    }
}