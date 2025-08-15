using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DromHub.Data;
using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        public static IServiceProvider ServiceProvider => _serviceProvider;

        public App()
        {
            this.InitializeComponent();
            ConfigureServices();
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Регистрация контекста базы данных
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=DromHubDB;Username=postgres;Password=admin"));

            // Регистрация ViewModels
            services.AddScoped<PartSearchViewModel>();
            services.AddTransient<PartViewModel>();

            // Регистрация MainWindow
            services.AddTransient<MainWindow>();

            // Добавьте это в конфигурацию сервисов
            services.AddLogging(); // Добавляет систему логгирования


            _serviceProvider = services.BuildServiceProvider();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            try
            {
                using (var scope = ServiceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Инициализация БД
                    await DatabaseInitializer.InitializeAsync(dbContext, forceReset: true);

                    // Дополнительная проверка (на всякий случай)
                    await EnsureTestPartExists(dbContext);

                    // Финальная проверка
                    var testPart = await dbContext.Parts
                        .Include(p => p.Brand)
                        .FirstOrDefaultAsync(p => p.CatalogNumber == "04152-YZZA1");

                    Debug.WriteLine(testPart != null
                        ? $"Деталь найдена: {testPart.Name}"
                        : "Деталь не найдена!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка инициализации: {ex.Message}");
            }

            m_window.Activate();
        }

        private static async Task EnsureTestPartExists(ApplicationDbContext context)
        {
            try
            {
                var partExists = await context.Parts
                    .AnyAsync(p => p.CatalogNumber == "04152-YZZA1");

                if (!partExists)
                {
                    var toyota = await context.Brands
                        .FirstOrDefaultAsync(b => b.Name == "Toyota");

                    if (toyota != null)
                    {
                        await context.Parts.AddAsync(new Part
                        {
                            Id = Guid.NewGuid(),
                            BrandId = toyota.Id,
                            CatalogNumber = "04152-YZZA1",
                            Name = "Масляный фильтр Toyota Corolla",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                        await context.SaveChangesAsync();
                        Debug.WriteLine("Деталь 04152-YZZA1 добавлена через резервный метод");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при проверке детали: {ex.Message}");
            }
        }

        private async void InitializeDatabaseAsync()
        {
            try
            {
                using (var scope = ServiceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await DatabaseInitializer.InitializeAsync(dbContext);
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                Debug.WriteLine($"Ошибка инициализации БД: {ex.Message}");
            }
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