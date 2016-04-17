using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TeamRoomExtension.ServiceHelpers
{
    public sealed class ToLocalTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                DateTime? dt = value as DateTime?;
                return dt.HasValue ? dt.Value.ToLocalTime().ToString("d MMM, h:mm tt") : "";
            }
            catch (Exception ex)
            {

            }
            return null;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
