using System;
using DromHub.Data;
using DromHub.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DromHub
{
    public partial class App : Application
    {
        private static IServiceProvider _serviceProvider;
        public static ApplicationDbContext DbContext { get; private set; }
        private Window m_window;

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

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = _serviceProvider.GetRequiredService<MainWindow>();

            // Инициализация БД с тестовыми данными
            // using (var scope = _serviceProvider.CreateScope())
            // {
                // var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                // await DatabaseInitializer.InitializeAsync(dbContext);
            // }

            m_window.Activate();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Регистрация контекста БД
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql("Host=localhost;Database=DromHubDB;Username=postgres;Password=admin");
            });

            // Регистрация ViewModels
            services.AddTransient<PartViewModel>();
            services.AddTransient<MainWindow>();
        }

        public static T GetService<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }
    }
}