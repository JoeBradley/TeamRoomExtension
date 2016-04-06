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
    public sealed class ImageUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (UserWorker.Instance.ProfileImages.Keys.Contains(value.ToString()))
                {
                    var bytes = UserWorker.Instance.ProfileImages[value.ToString()];

                    // Create source.
                    var bi = new BitmapImage();
                    // BitmapImage.UriSource must be in a BeginInit/EndInit block.
                    bi.BeginInit();
                    bi.StreamSource = new MemoryStream(bytes);
                    bi.EndInit();
                    return bi;
                }
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
