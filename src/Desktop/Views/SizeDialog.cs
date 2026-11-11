using System.Windows;
using System.Windows.Controls;
using TokenPet.Infrastructure.Configuration;

namespace TokenPet.Desktop.Views
{
    internal sealed class SizeDialog : Window
    {
        private readonly Slider slider;

        internal SizeDialog(Window owner, double scale)
        {
            Title = "调整浮窗大小";
            Owner = owner;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Height;
            Width = 320;

            slider = new Slider {
                Minimum = Preferences.MinScale * 100,
                Maximum = Preferences.MaxScale * 100,
                Value = scale * 100,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 8, 0, 12)
            };
            var label = new TextBlock { Text = FormatScale(slider.Value), FontSize = 16 };
            slider.ValueChanged += (s, e) => label.Text = FormatScale(e.NewValue);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "取消", IsCancel = true, Width = 72, Margin = new Thickness(0, 0, 8, 0) };
            var confirm = new Button { Content = "保存", IsDefault = true, Width = 72 };
            confirm.Click += (s, e) => DialogResult = true;
            buttons.Children.Add(cancel);
            buttons.Children.Add(confirm);
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(label);
            panel.Children.Add(slider);
            panel.Children.Add(buttons);
            Content = panel;
        }

        internal double SelectedScale { get { return slider.Value / 100; } }

        private static string FormatScale(double value) { return string.Format("{0:0}%", value); }
    }
}
