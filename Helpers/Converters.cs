using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Assistant.Models;

namespace Assistant.Helpers
{
    /// <summary>bool → Visibility (true = Visible, false = Collapsed)</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => value is Visibility.Visible;
    }

    /// <summary>bool → Visibility inverted (false = Visible, true = Collapsed)</summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => value is not Visibility.Visible;
    }

    /// <summary>bool → bool inverted</summary>
    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is bool b && !b;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => value is bool b && !b;
    }

    /// <summary>
    /// Maps a MessageRole to the horizontal alignment of the chat bubble:
    ///   User      → Right
    ///   Assistant → Left
    ///   System    → Center
    /// </summary>
    public sealed class MessageRoleToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is MessageRole role
                ? role switch
                {
                    MessageRole.User      => HorizontalAlignment.Right,
                    MessageRole.Assistant => HorizontalAlignment.Left,
                    _                     => HorizontalAlignment.Center
                }
                : HorizontalAlignment.Left;

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Maps a MessageRole to the brush key for the bubble background:
    ///   User      → UserBubbleBrush
    ///   Assistant → AssistantBubbleBrush
    ///   System    → SystemBubbleBrush
    /// </summary>
    public sealed class MessageRoleToBrushKeyConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is MessageRole role
                ? role switch
                {
                    MessageRole.User      => "UserBubbleBrush",
                    MessageRole.Assistant => "AssistantBubbleBrush",
                    _                     => "SystemBubbleBrush"
                }
                : "AssistantBubbleBrush";

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>DateTime → short time string "HH:mm"</summary>
    public sealed class DateTimeToShortTimeConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is DateTime dt ? dt.ToString("HH:mm") : string.Empty;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>double Temperature (0..1) → percentage string "70 %"</summary>
    public sealed class TemperatureToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is double d ? $"{d:P0}" : "0 %";
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }
}
