using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BitmapToVector.Demo.Util
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T obj, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(obj, value))
            {
                return false;
            }

            obj = value;
            RaisePropertyChanged(propertyName);

            return true;
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
