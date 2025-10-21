using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DromHub.Data;
using DromHub.Models;
using DromHub.ViewModels;
using DromHub.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OfficeOpenXml;
using WinRT;
using WinRT.Interop;

namespace DromHub
{
    public partial class App : Application
    {
        private static IServiceProvider _serviceProvider;
        public static ApplicationDbContext DbContext { get; private set; }
        public static Window MainWindow { get; private set; }
        private Window m_window;
        public static nint MainHwnd { get; private set; }
        private WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        private MicaController m_micaController;
        private SystemBackdropConfiguration m_configuration;
        public static IServiceProvider ServiceProvider => _serviceProvider;
        public static IConfiguration Configuration { get; private set; } = default!;

        public App()
        {
            this.InitializeComponent();
            Configuration = BuildConfiguration();
            ConfigureServices();
            ConfigureEpplusLicense();
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(Configuration);

            var connectionString = Configuration.GetConnectionString("DromHub");
            if (string.IsNullOrWhiteSpace(connectionString) ||
                connectionString.IndexOf("CHANGE_ME", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(
                    "The required database connection string 'ConnectionStrings:DromHub' is missing or still uses the " +
                    "placeholder value. Provide a valid Npgsql connection string via appsettings.json, environment variables, " +
                    "or user secrets. See README.md for configuration options.");
            }

            // Регистрация контекста базы данных
            services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Регистрация ViewModels
            services.AddTransient<PartViewModel>();
            services.AddTransient<BrandOverviewViewModel>();
            services.AddTransient<BrandsIndexViewModel>();
            services.AddTransient<BrandMergeWizardViewModel>();
            services.AddTransient<BrandsHomeViewModel>();
            services.AddTransient<BrandShellViewModel>();
            services.AddTransient<MailParserViewModel>();

            // ДОБАВЬТЕ ЭТУ СТРОКУ - регистрация CartViewModel
            services.AddTransient<CartViewModel>();

            // Регистрация MainWindow
            services.AddTransient<MainWindow>();

            // Добавьте это в конфигурацию сервисов
            services.AddLogging(builder =>
            {
                builder.AddDebug();
            });

            _serviceProvider = services.BuildServiceProvider();
        }

        private static IConfiguration BuildConfiguration()
        {
            var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);

            if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                builder.AddUserSecrets<App>(optional: true);
            }

            return builder
                .AddEnvironmentVariables()
                .AddEnvironmentVariables("DROMHUB_")
                .Build();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
            MainWindow = m_window;
            MainHwnd = WindowNative.GetWindowHandle(m_window);
            try
            {
                using (var scope = ServiceProvider.CreateScope())
                {
                    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                    await using var dbContext = await dbFactory.CreateDbContextAsync();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();
                    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                    var forceResetRequested = DatabaseResetGuard.IsResetRequested(configuration, args.Arguments, logger);

                    // Инициализация БД
                    await DatabaseInitializer.InitializeAsync(dbContext, forceResetRequested);

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

            TrySetMicaBackdrop();
            m_window.Activate();
        }

        private static void ConfigureEpplusLicense()
        {
            // EPPlus 8+: ExcelPackage.License (статическое свойство)
            // EPPlus ≤7: ExcelPackage.LicenseContext (старое свойство)
            var prop = typeof(ExcelPackage).GetProperty("License", BindingFlags.Public | BindingFlags.Static);
            if (prop != null)
            {
                // у EPPlus 8 тип тот же (LicenseContext)
                prop.SetValue(null, LicenseContext.NonCommercial);
            }
            else
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            }
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
                    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                    await using var dbContext = await dbFactory.CreateDbContextAsync();
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