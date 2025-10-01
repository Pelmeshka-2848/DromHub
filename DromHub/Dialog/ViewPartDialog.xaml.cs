using DromHub.Data;
using DromHub.Models;
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

        private readonly ApplicationDbContext _context;

        public ViewPartDialog(Part part, ApplicationDbContext context)
        {
            this.InitializeComponent();
            _context = context;
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

                var partWithData = await _context.Parts
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

                    PartImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                        new Uri(imageUrl));

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
                if (_context == null || Part?.Id == null) return;

                var partId = Part.Id;
                var crosses = await _context.OemCrosses
                    .Where(c => c.AftermarketPartId == partId)
                    .Join(_context.Parts.Include(p => p.Brand),
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

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}