using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BitmapToVector.Demo.Util;
using BitmapToVector.SkiaSharp;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace BitmapToVector.Demo
{
    public class MainPageViewModel : BaseViewModel
    {
        private readonly SKPaint _pathPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            Color = SKColors.Red
        };

        private readonly Action _invalidateSurfaceAction;

        public MainPageViewModel(Action invalidateSurfaceAction)
        {
            _invalidateSurfaceAction = invalidateSurfaceAction;

            BrowseCommand = new Command(() => _ = BrowseAsync());
        }
        
        public Command BrowseCommand { get; }

        private SKBitmap _bitmap;
        private SKBitmap Bitmap
        {
            get => _bitmap;
            set
            {
                _bitmap?.Dispose();
                _bitmap = value;
                if (value != null)
                {
                    CanvasWidthPixels = value.Width;
                    CanvasHeightPixels = value.Height;
                    Paths = value.Trace(new PotraceParam()).ToList();
                }
            }
        }

        private List<SKPath> _paths = new List<SKPath>();
        private List<SKPath> Paths
        {
            get => _paths;
            set
            {
                foreach (var path in _paths)
                {
                    path.Dispose();
                }
                _paths = value;
                InvalidateSurface();
            }
        }

        private int _canvasWidthPixels = 100;
        public int CanvasWidthPixels
        {
            get => _canvasWidthPixels;
            set => SetProperty(ref _canvasWidthPixels, value);
        }

        private int _canvasHeightPixels = 100;
        public int CanvasHeightPixels
        {
            get => _canvasHeightPixels;
            set => SetProperty(ref _canvasHeightPixels, value);
        }

        private async Task BrowseAsync()
        {
            var pickerResult = await FilePicker.PickAsync(
                new PickOptions {FileTypes = FilePickerFileType.Images}
            );
            if (pickerResult != null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () => {
                    using var stream = await pickerResult.OpenReadAsync();
                    Bitmap = SKBitmap.Decode(stream);
                });
            }
        }

        private void InvalidateSurface()
        {
            _invalidateSurfaceAction();
        }

        public void PaintCanvas(SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            canvas.Clear(SKColors.White);

            if (Bitmap != null)
            {
                canvas.DrawBitmap(Bitmap, 0, 0);
            }

            foreach(var path in Paths)
            {
                canvas.DrawPath(path, _pathPaint);
            }
        }
    }
}
