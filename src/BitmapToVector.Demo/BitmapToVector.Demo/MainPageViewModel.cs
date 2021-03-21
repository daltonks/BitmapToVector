using System;
using BitmapToVector.Demo.Util;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;

namespace BitmapToVector.Demo
{
    public class MainPageViewModel : BaseViewModel
    {
        private readonly SKPaint _pathPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            Color = SKColors.Black
        };

        private readonly Action _invalidateSurfaceAction;

        public MainPageViewModel(Action invalidateSurfaceAction)
        {
            _invalidateSurfaceAction = invalidateSurfaceAction;

            BrowseCommand = new Command(() => {

            });
        }

        public Command BrowseCommand { get; }

        private SKBitmap _bitmap;
        private SKBitmap Bitmap
        {
            get => _bitmap;
            set
            {
                _bitmap?.Dispose();
                SetProperty(ref _bitmap, value);
                InvalidateSurface();
                if (value != null)
                {
                    CanvasWidth = value.Width;
                    CanvasHeight = value.Height;
                }
            }
        }

        private SKPath _path;
        private SKPath Path
        {
            get => _path;
            set
            {
                _path?.Dispose();
                SetProperty(ref _path, value);
                InvalidateSurface();
            }
        }

        private int _canvasWidth = 100;
        public int CanvasWidth
        {
            get => _canvasWidth;
            set => SetProperty(ref _canvasWidth, value);
        }

        private int _canvasHeight = 100;
        public int CanvasHeight
        {
            get => _canvasHeight;
            set => SetProperty(ref _canvasHeight, value);
        }

        private void InvalidateSurface()
        {
            _invalidateSurfaceAction();
        }

        public void PaintCanvas(SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            if (Bitmap != null)
            {
                canvas.DrawBitmap(Bitmap, 0, 0);
            }

            if (Path != null)
            {
                canvas.DrawPath(Path, _pathPaint);
            }
        }
    }
}
