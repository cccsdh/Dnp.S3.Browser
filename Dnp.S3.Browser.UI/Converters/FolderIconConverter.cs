using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Dnp.S3.Browser.UI.Converters
{
    public class FolderIconConverter : IValueConverter
    {
        // Returns a folder emoji for true, file emoji for false
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFolder && isFolder)
                return "📁";
            return "📄";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
