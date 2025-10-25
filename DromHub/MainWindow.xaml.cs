using DromHub.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using Windows.Graphics;
using WinRT.Interop;

namespace DromHub
{
    /// <summary>
    /// Класс MainWindow отвечает за логику компонента MainWindow.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const double CinemaAspect = 21.0 / 9.0;
        private bool _isAdjusting;
        private bool _suppressContentNavigationPrompt;
        /// <summary>
        /// Конструктор MainWindow инициализирует экземпляр класса.
        /// </summary>

        public MainWindow()
        {
            InitializeComponent();

            // 1) Стартовый максимальный 21:9 внутри рабочей области
            ResizeCinemaToWorkArea();
            CenterOnScreen();

            // 2) Держим аспект 21:9
            var appWindow = GetAppWindow();
            appWindow.Changed += AppWindow_Changed;

            contentFrame.Navigating += ContentFrame_Navigating;

            // 3) Стартовая страница
            contentFrame.Navigate(typeof(MainPage));
            nvSample.SelectedItem = MainPageItem;
        }
        /// <summary>
        /// Метод NavigationView_SelectionChanged выполняет основную операцию класса.
        /// </summary>

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer != null)
            {
                var tag = args.SelectedItemContainer.Tag?.ToString();
                System.Diagnostics.Debug.WriteLine($"Selection changed: {tag}");

                if (tag == "CartPage")
                {
                    contentFrame.Navigate(typeof(CartPage));
                }
                else if (tag == "MailParserView") // ИЗМЕНИЛ НА MailParserView
                {
                    contentFrame.Navigate(typeof(MailParserView));
                }
                else
                {
                    // Для остальных страниц используем NavigateByTag
                    NavigateByTag(tag);
                }
            }
        }
        /// <summary>
        /// Метод NavigationView_ItemInvoked выполняет основную операцию класса.
        /// </summary>

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked) return;

            if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
                NavigateByTag(tag);
        }
        /// <summary>
        /// Метод NavigateByTag выполняет основную операцию класса.
        /// </summary>

        private void NavigateByTag(string tag, object parameter = null)
        {
            try
            {
                Type pageType = tag switch
                {
                    "MainPage" => typeof(MainPage),
                    "PartPage" => typeof(PartSearchPage),
                    "PartSearchPage" => typeof(PartSearchPage),
                    "PartChangesPage" => typeof(PartChangesPage),
                    "BrandsOverviewPage" => typeof(BrandsHomePage),
                    "BrandsListPage" => typeof(BrandsIndexPage),
                    "BrandMergePage" => typeof(BrandMergeWizardPage),
                    "CartPage" => typeof(CartPage),
                    "MailParserView" => typeof(MailParserView), // ИЗМЕНИЛ НА MailParserView
                    _ => null
                };

                if (pageType == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Unknown tag: {tag}");
                    return;
                }

                if (contentFrame.CurrentSourcePageType != pageType || parameter != null)
                {
                    contentFrame.Navigate(pageType, parameter);
                    System.Diagnostics.Debug.WriteLine($"Navigated to: {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error for tag '{tag}': {ex.Message}");
            }
        }

        /// <summary>
        /// <para>Переключает главное окно на страницу истории изменений выбранной запчасти, прокидывая идентификатор в навигацию.</para>
        /// <para>Используйте из диалогов и других экранов, чтобы быстро открыть аудит без ручного выбора пунктов меню.</para>
        /// <para>Раскрывает раздел «Запчасть» в меню и активирует подпункт «Изменения».</para>
        /// </summary>
        /// <param name="partId">Идентификатор запчасти, история которой требуется; должен быть ненулевым GUID.</param>
        /// <exception cref="ArgumentException">Брошено, когда <paramref name="partId"/> равен <see cref="Guid.Empty"/>, потому что навигация без контекста бессмысленна.</exception>
        /// <remarks>
        /// Предусловия: вызывающий код работает в UI-потоке и приложение уже инициализировало <see cref="App.MainWindow"/>.<para/>
        /// Постусловия: основная рамка навигации загружает <see cref="Views.PartChangesPage"/> с переданным идентификатором детали.<para/>
        /// Побочные эффекты: изменяет выбранный пункт меню и контент фрейма.<para/>
        /// Потокобезопасность: метод не потокобезопасен; обращайтесь только из UI-потока WinUI.<para/>
        /// См. также: <see cref="NavigateByTag(string, object)"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Открыть аудит для запчасти из произвольного диалога:
        /// if (App.MainWindow is MainWindow main)
        /// {
        ///     main.NavigateToPartChanges(partId);
        /// }
        /// </code>
        /// </example>
        public void NavigateToPartChanges(Guid partId)
        {
            if (partId == Guid.Empty)
            {
                throw new ArgumentException("Идентификатор запчасти должен быть задан.", nameof(partId));
            }

            if (PartPageItem is not null)
            {
                PartPageItem.IsExpanded = true;
            }

            if (PartChangesPageItem is not null)
            {
                nvSample.SelectedItem = PartChangesPageItem;
            }

            NavigateByTag("PartChangesPage", partId);
        }

        private readonly struct PendingNavigationRequest
        {
            public PendingNavigationRequest(NavigatingCancelEventArgs args)
            {
                Mode = args.NavigationMode;
                TargetPageType = args.SourcePageType;
                Parameter = args.Parameter;
                TransitionInfo = args.NavigationTransitionInfo;
            }

            public NavigationMode Mode { get; }

            public Type? TargetPageType { get; }

            public object? Parameter { get; }

            public NavigationTransitionInfo? TransitionInfo { get; }
        }

        private async void ContentFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (_suppressContentNavigationPrompt)
            {
                _suppressContentNavigationPrompt = false;
                return;
            }

            if (contentFrame.Content is not BrandShellPage shellPage)
            {
                return;
            }

            if (!shellPage.HasPendingBrandSettingsChanges)
            {
                return;
            }

            var pendingNavigation = new PendingNavigationRequest(e);

            e.Cancel = true;

            if (await shellPage.TryHandleUnsavedBrandSettingsAsync())
            {
                ResumeContentNavigation(pendingNavigation);
            }
        }

        private void ResumeContentNavigation(in PendingNavigationRequest request)
        {
            _suppressContentNavigationPrompt = true;

            var resumed = false;

            switch (request.Mode)
            {
                case NavigationMode.Back:
                    if (contentFrame.CanGoBack)
                    {
                        contentFrame.GoBack();
                        resumed = true;
                    }

                    break;

                case NavigationMode.Forward:
                    if (contentFrame.CanGoForward)
                    {
                        contentFrame.GoForward();
                        resumed = true;
                    }

                    break;

                case NavigationMode.New:
                case NavigationMode.Refresh:
                default:
                    if (request.TargetPageType is not null)
                    {
                        if (request.TransitionInfo is NavigationTransitionInfo info)
                        {
                            contentFrame.Navigate(request.TargetPageType, request.Parameter, info);
                        }
                        else
                        {
                            contentFrame.Navigate(request.TargetPageType, request.Parameter);
                        }

                        resumed = true;
                    }

                    break;
            }

            if (!resumed)
            {
                _suppressContentNavigationPrompt = false;
            }
        }
        /// <summary>
        /// Метод AppWindow_Changed выполняет основную операцию класса.
        /// </summary>

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

                    // минимальный размер 21:9
                    const int minW = 3200;
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
        /// <summary>
        /// Метод ResizeCinemaToWorkArea выполняет основную операцию класса.
        /// </summary>

        private void ResizeCinemaToWorkArea()
        {
            var appWindow = GetAppWindow();
            if (appWindow == null) return;

            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
            var work = displayArea.WorkArea;

            // Максимально возможная ширина в пределах рабочей области
            const double MaxWidthRatio = 0.90;
            int targetW = (int)Math.Round(work.Width * MaxWidthRatio);
            int targetH = (int)Math.Round(targetW / CinemaAspect);

            if (targetH > work.Height)
            {
                targetH = work.Height;
                targetW = (int)Math.Round(targetH * CinemaAspect);
            }

            appWindow.Resize(new SizeInt32(targetW, targetH));
        }
        /// <summary>
        /// Метод GetAppWindow выполняет основную операцию класса.
        /// </summary>

        private AppWindow GetAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }
        /// <summary>
        /// Метод CenterOnScreen выполняет основную операцию класса.
        /// </summary>

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

        // Добавьте свойство для доступа к Frame извне (если нужно)
        /// <summary>
        /// Свойство ContentFrame предоставляет доступ к данным ContentFrame.
        /// </summary>
        public Frame ContentFrame => contentFrame;
    }
}