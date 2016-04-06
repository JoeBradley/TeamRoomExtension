using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Ink;
using System.Windows.Media;

namespace TeamRoomExtension.ServiceHelpers
{
    public sealed class ProfileColorConverter : IValueConverter
    {
        private string defaultColor = "#ff1F8A70";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            SolidColorBrush brush = new SolidColorBrush();
            try
            {
                if (UserWorker.Instance.ProfileColors.Keys.Contains(value.ToString()))
                {
                    brush.Color = UserWorker.Instance.ProfileColors[value.ToString()];
                }
                else
                {
                    brush.Color = (Color)ColorConverter.ConvertFromString(defaultColor);
                }
            }
            catch (Exception ex)
            {
                brush.Color = (Color)ColorConverter.ConvertFromString(defaultColor);
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
