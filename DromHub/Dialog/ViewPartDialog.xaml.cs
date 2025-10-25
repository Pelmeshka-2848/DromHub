using DromHub;
using DromHub.Data;
using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace DromHub.Views
{
    /// <summary>
    /// Класс ViewPartDialog отвечает за логику компонента ViewPartDialog.
    /// </summary>
    public sealed partial class ViewPartDialog : ContentDialog, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Свойство Part предоставляет доступ к данным Part.
        /// </summary>

        public Part Part { get; private set; }
        /// <summary>
        /// Свойство Crosses предоставляет доступ к данным Crosses.
        /// </summary>
        public ObservableCollection<object> Crosses { get; } = new();
        /// <summary>
        /// Свойство HasImage предоставляет доступ к данным HasImage.
        /// </summary>

        public bool HasImage => Part?.Images?.Any() == true;

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        /// <summary>
        /// Конструктор ViewPartDialog инициализирует экземпляр класса.
        /// </summary>

        public ViewPartDialog(Part part, IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            this.InitializeComponent();
            _contextFactory = contextFactory;
            Part = part ?? throw new ArgumentNullException(nameof(part));

            this.DataContext = this;
            this.Loaded += ViewPartDialog_Loaded;
        }
        /// <summary>
        /// Метод ViewPartDialog_Loaded выполняет основную операцию класса.
        /// </summary>

        private async void ViewPartDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPartDataAsync();
            await LoadCrossesAsync();
            LoadImage();
        }
        /// <summary>
        /// Метод LoadPartDataAsync выполняет основную операцию класса.
        /// </summary>

        private async Task LoadPartDataAsync()
        {
            try
            {
                if (Part?.Id == null) return;

                await using var context = await _contextFactory.CreateDbContextAsync();
                var partWithData = await context.Parts
                    .Include(p => p.Brand)
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == Part.Id);

                if (partWithData != null)
                {
                    Part = partWithData;
                    OnPropertyChanged(nameof(HasImage));

                    System.Diagnostics.Debug.WriteLine($"=== DEBUG INFO ===");
                    System.Diagnostics.Debug.WriteLine($"Part: {Part.CatalogNumber}");
                    System.Diagnostics.Debug.WriteLine($"Images count: {Part.Images?.Count ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"HasImage: {HasImage}");
                    System.Diagnostics.Debug.WriteLine($"=== END DEBUG ===");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading part data: {ex.Message}");
            }
        }
        /// <summary>
        /// Метод LoadImage выполняет основную операцию класса.
        /// </summary>

        private void LoadImage()
        {
            try
            {
                if (HasImage)
                {
                    // Есть изображение в базе - показываем его и скрываем заглушку
                    var imageUrl = Part.Images.First().Url;
                    System.Diagnostics.Debug.WriteLine($"Loading image from database: {imageUrl}");

                    var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage
                    {
                        DecodePixelWidth = 400,
                        DecodePixelHeight = 400,
                        CreateOptions = Microsoft.UI.Xaml.Media.Imaging.BitmapCreateOptions.IgnoreImageCache
                    };

                    bitmap.UriSource = new Uri(imageUrl);

                    PartImage.Source = bitmap;

                    // Скрываем заглушку
                    NoImagePlaceholder.Visibility = Visibility.Collapsed;
                    PartImage.Visibility = Visibility.Visible;
                }
                else
                {
                    // Нет изображения в базе - показываем заглушку и скрываем картинку
                    System.Diagnostics.Debug.WriteLine("No image in database - showing placeholder");

                    PartImage.Source = null;
                    PartImage.Visibility = Visibility.Collapsed;
                    NoImagePlaceholder.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
                // При ошибке показываем заглушку
                ShowNoImagePlaceholder();
            }
        }
        /// <summary>
        /// Метод PartImage_ImageFailed выполняет основную операцию класса.
        /// </summary>

        private void PartImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Image failed: {e.ErrorMessage}");
            // При ошибке загрузки показываем заглушку
            ShowNoImagePlaceholder();
        }
        /// <summary>
        /// Метод ShowNoImagePlaceholder выполняет основную операцию класса.
        /// </summary>

        private void ShowNoImagePlaceholder()
        {
            try
            {
                PartImage.Source = null;
                PartImage.Visibility = Visibility.Collapsed;
                NoImagePlaceholder.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine("Showing 'no image' placeholder");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing placeholder: {ex.Message}");
            }
        }
        /// <summary>
        /// Метод LoadCrossesAsync выполняет основную операцию класса.
        /// </summary>

        private async Task LoadCrossesAsync()
        {
            try
            {
                if (Part?.Id == null) return;

                await using var context = await _contextFactory.CreateDbContextAsync();

                var partId = Part.Id;
                var crosses = await context.OemCrosses
                    .Where(c => c.AftermarketPartId == partId)
                    .Join(context.Parts.Include(p => p.Brand),
                        cross => cross.OemPartId,
                        part => part.Id,
                        (cross, oemPart) => new
                        {
                            CatalogNumber = oemPart.CatalogNumber ?? "N/A",
                            Brand = oemPart.Brand != null ? oemPart.Brand.Name : "Unknown",
                            Note = cross.Note ?? string.Empty,
                            RelationType = "OEM Alternative"
                        })
                    .ToListAsync();

                Crosses.Clear();
                foreach (var cross in crosses)
                {
                    Crosses.Add(cross);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading crosses: {ex.Message}");
                Crosses.Clear();
            }
        }
        /// <summary>
        /// <para>Открывает страницу истории изменений текущей запчасти в главном окне, передавая контекст выбранного объекта.</para>
        /// <para>Позволяет администраторам быстро перейти к аудиту без ручного поиска нужной записи.</para>
        /// </summary>
        /// <param name="sender">Кнопка, инициировавшая обработчик; значение не используется напрямую.</param>
        /// <param name="e">Аргументы события нажатия; не содержат дополнительных данных.</param>
        /// <remarks>
        /// Предусловия: <see cref="Part"/> не равен <see langword="null"/> и его идентификатор отличен от <see cref="Guid.Empty"/>.<para/>
        /// Постусловия: при успешном выполнении диалог закрыт методом <see cref="ContentDialog.Hide"/>, а главное окно отобразило <see cref="PartChangesPage"/> с аудитом детали.<para/>
        /// Побочные эффекты: обращается к <see cref="App.MainWindow"/> и вызывает <see cref="MainWindow.NavigateToPartChanges(Guid)"/>; меняет текущую страницу приложения.<para/>
        /// Потокобезопасность: метод не потокобезопасен и должен вызываться в UI-потоке WinUI.<para/>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Кнопка в XAML привязана к обработчику:
        /// // <Button Click="ViewPartAudit_Click" Content="История изменений" />
        /// </code>
        /// </example>
        private void ViewPartAudit_Click(object sender, RoutedEventArgs e)
        {
            if (Part is null || Part.Id == Guid.Empty)
            {
                return;
            }

            if (App.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateToPartChanges(Part.Id);
                Hide();
            }
        }
        /// <summary>
        /// Метод AddToCartButton_Click выполняет основную операцию класса.
        /// </summary>

        private async void AddToCartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Add to Cart Button Clicked ===");

                if (Part == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Part is null");
                    return;
                }

                // Используем статический экземпляр
                var cartViewModel = CartViewModel.Instance;
                System.Diagnostics.Debug.WriteLine("Using CartViewModel singleton instance");

                System.Diagnostics.Debug.WriteLine($"Adding part {Part.CatalogNumber} to cart...");
                await cartViewModel.AddToCartAsync(Part);
                System.Diagnostics.Debug.WriteLine("Part added to cart successfully");

                ShowSuccessMessage($"Part {Part.CatalogNumber} added to cart!");

                System.Diagnostics.Debug.WriteLine("=== Add to Cart Completed ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in AddToCart: {ex.Message}");
                ShowErrorMessage($"Failed to add part to cart: {ex.Message}");
            }
        }
        /// <summary>
        /// Метод ShowSuccessMessage выполняет основную операцию класса.
        /// </summary>


        private void ShowSuccessMessage(string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Showing success message: {message}");

                // Если используете TeachingTip
                if (MessageTip != null)
                {
                    MessageTip.Title = "Success";
                    MessageTip.Subtitle = message;
                    MessageTip.IsOpen = true;
                    System.Diagnostics.Debug.WriteLine("TeachingTip shown successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("MessageTip is null, using fallback");
                    // Fallback - просто логируем
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in ShowSuccessMessage: {ex.Message}");
            }
        }
        /// <summary>
        /// Метод ShowErrorMessage выполняет основную операцию класса.
        /// </summary>

        private void ShowErrorMessage(string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Showing error message: {message}");

                if (MessageTip != null)
                {
                    MessageTip.Title = "Error";
                    MessageTip.Subtitle = message;
                    MessageTip.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in ShowErrorMessage: {ex.Message}");
            }
        }
        /// <summary>
        /// Метод ViewPartDialog_Closed выполняет основную операцию класса.
        /// </summary>

        private void ViewPartDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            try
            {
                this.Loaded -= ViewPartDialog_Loaded;

                if (MessageTip != null)
                {
                    MessageTip.IsOpen = false;
                }

                Crosses.Clear();

                if (PartImage != null)
                {
                    PartImage.Source = null;
                }

                Part = null;
                DataContext = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in ViewPartDialog_Closed: {ex.Message}");
            }
        }
        /// <summary>
        /// Метод OnPropertyChanged выполняет основную операцию класса.
        /// </summary>

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}