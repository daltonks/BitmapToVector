using SkiaSharp.Views.Forms;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace BitmapToVector.Demo
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            BindingContext = new MainPageViewModel(
                () => MainThread.InvokeOnMainThreadAsync(CanvasView.InvalidateSurface)
            );
            InitializeComponent();
        }

        private MainPageViewModel ViewModel => (MainPageViewModel) BindingContext;

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            ViewModel.PaintCanvas(e);
        }
    }
}
