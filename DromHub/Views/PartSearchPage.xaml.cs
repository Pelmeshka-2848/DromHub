using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using System;
using DromHub.Models;
using DromHub.Data;
using DromHub;
using Microsoft.EntityFrameworkCore;

namespace DromHub.Views
{
    /// <summary>
    /// Класс PartSearchPage отвечает за логику компонента PartSearchPage.
    /// </summary>
    public sealed partial class PartSearchPage : Page
    {
        /// <summary>
        /// Свойство ViewModel предоставляет доступ к данным ViewModel.
        /// </summary>
        public PartViewModel ViewModel { get; }
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        /// <summary>
        /// Конструктор PartSearchPage инициализирует экземпляр класса.
        /// </summary>

        public PartSearchPage()
        {
            this.InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<PartViewModel>();
            this.DataContext = ViewModel;
            _dbFactory = App.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        }
        /// <summary>
        /// Метод SearchTextBox_KeyDown выполняет основную операцию класса.
        /// </summary>

        private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _ = ViewModel.SearchPartsCommand.ExecuteAsync(null);
            }
        }
        /// <summary>
        /// Запускает сценарий создания новой запчасти через диалог, сохраняя текущий контекст поиска и обновляя список после успешного сохранения.
        /// Используйте для пользовательской кнопки «Создать запись», когда необходимо открыть форму ввода без перехода на отдельную страницу.
        /// Метод обрабатывает ошибки сохранения, показывая диалог с причиной сбоя.
        /// </summary>
        /// <param name="sender">Кнопка запуска добавления; допускается <see langword="null"/>.</param>
        /// <param name="e">Аргументы события нажатия; не используются напрямую.</param>
        /// <exception cref="InvalidOperationException">Передан сервис <see cref="PartViewModel"/>, не зарегистрированный в <see cref="App.ServiceProvider"/>.</exception>
        /// <remarks>
        /// Предусловия: контейнер внедрения зависимостей приложения сконфигурирован и содержит экземпляры <see cref="PartViewModel"/> и <see cref="IDbContextFactory{ApplicationDbContext}"/>.<para/>
        /// Постусловия: при подтверждении диалога список запчастей обновляется через <see cref="PartViewModel.SearchPartsCommand"/>.<para/>
        /// Побочные эффекты: открывает модальные диалоги WinUI и взаимодействует с базой данных через команды ViewModel.<para/>
        /// Потокобезопасность: метод должен выполняться в UI-потоке WinUI.<para/>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Программно вызвать создание новой запчасти:
        /// AddPart_Click(addButton, new RoutedEventArgs());
        /// </code>
        /// </example>
        private async void AddPart_Click(object sender, RoutedEventArgs e)
        {
            // Создаем новую VM для добавления запчасти
            var partVm = App.ServiceProvider.GetRequiredService<PartViewModel>();
            partVm.ResetPart();
            await partVm.LoadBrandsCommand.ExecuteAsync(null);

            var dialog = new AddPartDialog(partVm)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await partVm.SavePartCommand.ExecuteAsync(null);

                    // Обновляем список только если сохранение прошло успешно
                    await ViewModel.SearchPartsCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    // Показываем пользователю сообщение об ошибке
                    var errorDialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = ex.Message,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        /// <summary>
        /// Открывает историю изменений выбранной запчасти, чтобы оператор мог быстро свериться с журналом аудита без ручной навигации по меню.
        /// Применяйте обработчик на кнопке списка запчастей, когда требуется сохранить контекст текущего поиска и перейти к странице <see cref="PartChangesPage"/>.
        /// Игнорирует вызов, если элемент списка не содержит валидного идентификатора или главное окно недоступно.
        /// </summary>
        /// <param name="sender">Кнопка "История" внутри элемента списка; допускает <see langword="null"/>, но в этом случае метод ничего не делает.</param>
        /// <param name="e">Аргументы события клика; не используются.</param>
        /// <remarks>
        /// Предусловия: источник события должен хранить в <see cref="FrameworkElement.DataContext"/> экземпляр <see cref="Part"/> с ненулевым <see cref="Part.Id"/>.<para/>
        /// Постусловия: при выполнении предусловий вызывается <see cref="MainWindow.NavigateToPartChanges(Guid)"/>, что изменяет выбранную страницу приложения.<para/>
        /// Побочные эффекты: инициирует переход внутри главного окна и закрывает текущий контекст просмотра деталей.<para/>
        /// Потокобезопасность: метод не потокобезопасен и должен вызываться из UI-потока WinUI.<para/>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Программно смоделировать переход к истории конкретной запчасти из ViewModel:
        /// if (App.MainWindow is MainWindow mainWindow)
        /// {
        ///     mainWindow.NavigateToPartChanges(partId);
        /// }
        /// </code>
        /// </example>
        private void ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.DataContext is not Part part || part.Id == Guid.Empty)
            {
                return;
            }

            if (App.MainWindow is not MainWindow mainWindow)
            {
                return;
            }

            mainWindow.NavigateToPartChanges(part.Id);
        }
        /// <summary>
        /// Метод ViewPart_Click выполняет основную операцию класса.
        /// </summary>

        private async void ViewPart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Part part)
            {
                // Передаём ваш ApplicationDbContext (в вашем коде он называется ViewModel.Context)
                var dialog = new ViewPartDialog(part, _dbFactory)
                {
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
        }
        /// <summary>
        /// Метод EditPart_Click выполняет основную операцию класса.
        /// </summary>




        private async void EditPart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Part part)
            {
                var partVm = App.ServiceProvider.GetRequiredService<PartViewModel>();
                partVm.LoadFromPart(part);

                await partVm.LoadBrandsCommand.ExecuteAsync(null);

                var dialog = new EditPartDialog(partVm)
                {
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await partVm.SavePartCommand.ExecuteAsync(null);
                    await ViewModel.SearchPartsCommand.ExecuteAsync(null);
                }
            }
        }
        /// <summary>
        /// Метод DeletePart_Click выполняет основную операцию класса.
        /// </summary>

        private async void DeletePart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Part part)
            {
                var dialog = new ContentDialog
                {
                    Title = "Удаление записи",
                    Content = $"Вы действительно хотите удалить {part.Name}?",
                    PrimaryButtonText = "Да",
                    CloseButtonText = "Нет",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await using var context = await _dbFactory.CreateDbContextAsync();
                    var partToDelete = await context.Parts
                        .FirstOrDefaultAsync(p => p.Id == part.Id);

                    if (partToDelete != null)
                    {
                        context.Parts.Remove(partToDelete);
                        await context.SaveChangesAsync();
                        await ViewModel.SearchPartsCommand.ExecuteAsync(null);
                    }
                }
            }
        }
    }
}