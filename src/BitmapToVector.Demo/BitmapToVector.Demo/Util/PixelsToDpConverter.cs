using System;
using System.Globalization;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace BitmapToVector.Demo.Util
{
    public class PixelsToDpConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Convert.ToDouble(value) / DeviceDisplay.MainDisplayInfo.Density;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Convert.ToDouble(value) * DeviceDisplay.MainDisplayInfo.Density;
        }
    }
}
