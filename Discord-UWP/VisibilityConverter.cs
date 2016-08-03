using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Discord_UWP
{
    class VisibilityConverter : IValueConverter
    {
        public Visibility TrueValue { get; set; }

        public Visibility Falsevalue { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (targetType != typeof(Visibility))
            {
                throw new InvalidOperationException($"Visbility converter cannot convert to {targetType.FullName}");
            }

            try
            {
                if (value != null && (bool)value)
                {
                    return TrueValue;
                }
                else
                {
                    return Falsevalue;
                }
            }
            catch (InvalidCastException)
            {
                // If this happened it wasn't a bool, but it wasn't null either
                return TrueValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
