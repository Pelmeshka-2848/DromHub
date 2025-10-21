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
    public sealed partial class ViewPartDialog : ContentDialog, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public Part Part { get; private set; }
        public ObservableCollection<object> Crosses { get; } = new();

        public bool HasImage => Part?.Images?.Any() == true;

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ViewPartDialog(Part part, IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            this.InitializeComponent();
            _contextFactory = contextFactory;
            Part = part ?? throw new ArgumentNullException(nameof(part));

            this.DataContext = this;
            this.Loaded += ViewPartDialog_Loaded;
        }

        private async void ViewPartDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPartDataAsync();
            await LoadCrossesAsync();
            LoadImage();
        }

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

        private void PartImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Image failed: {e.ErrorMessage}");
            // При ошибке загрузки показываем заглушку
            ShowNoImagePlaceholder();
        }

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

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}