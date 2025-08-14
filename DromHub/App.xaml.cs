using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DromHub.Data;
using DromHub.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using WinRT;

namespace DromHub
{
    public partial class App : Application
    {
        private static IServiceProvider _serviceProvider;
        public static ApplicationDbContext DbContext { get; private set; }
        private Window m_window;
        private WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        private MicaController m_micaController;
        private SystemBackdropConfiguration m_configuration;

        public App()
        {
            this.InitializeComponent();

            // Настройка DI контейнера
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Инициализация БД
            DbContext = _serviceProvider.GetRequiredService<ApplicationDbContext>();
            DbContext.Database.EnsureCreated();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = _serviceProvider.GetRequiredService<MainWindow>();

            // Инициализация Mica
            TrySetMicaBackdrop();

            m_window.Activate();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=DromHubDB;Username=postgres;Password=admin"));

            services.AddTransient<PartViewModel>();
            services.AddTransient<MainWindow>();
        }

        private void TrySetMicaBackdrop()
        {
            if (MicaController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                m_configuration = new SystemBackdropConfiguration
                {
                    IsInputActive = true
                };

                m_micaController = new MicaController();

                // Указываем тип Mica
                m_micaController.Kind = MicaKind.BaseAlt; // или MicaKind.Base

                // Включаем Mica для окна
                m_micaController.AddSystemBackdropTarget(m_window.As<ICompositionSupportsSystemBackdrop>());
                m_micaController.SetSystemBackdropConfiguration(m_configuration);

                // Обработчики активации окна
                m_window.Activated += Window_Activated;
                m_window.Closed += Window_Closed;
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (m_configuration != null)
            {
                m_configuration.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            m_window.Activated -= Window_Activated;
            m_configuration = null;
        }

        public static T GetService<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }
    }

    // Класс для работы с DispatcherQueue
    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        private object m_dispatcherQueueController = null;

        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2;  // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }
}