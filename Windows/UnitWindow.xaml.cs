using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NetworksCeW.Windows
{
    /// <summary>
    /// Interaction logic for UnitWindow.xaml
    /// </summary>
    public partial class UnitWindow
    {
        public int BuffSize;
        public bool Disabled;

        public UnitWindow()
        {
            InitializeComponent();
        }

        public UnitWindow(int buffSize, bool disabled)
        {
            InitializeComponent();

            textBoxBufferRangeFrom.Text = (BuffSize = buffSize).ToString();
            checkBoxDisabled.IsChecked = Disabled = disabled;
        }

        private void checkBoxDisabled_Checked(object sender, RoutedEventArgs e)
        {
            Disabled = true;
        }

        private void _KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetIsPressed();
            }
        }

        private void SetIsPressed()
        {
            if (Encoding.ASCII.GetBytes(textBoxBufferRangeFrom.Text).Where(b => b < 48 || b > 57).Any() ||
                textBoxBufferRangeFrom.Text.Length < 2 || Convert.ToInt16(textBoxBufferRangeFrom.Text) < 10)
                textBoxBufferRangeFrom.Text = "2000";
            else
            {
                BuffSize = Convert.ToInt16(textBoxBufferRangeFrom.Text);
                Close();
            }
        }

        private void checkBoxDisabled_Unchecked(object sender, RoutedEventArgs e)
        {
            Disabled = false;
        }

        private void RectangleButton_MouseEnter(object sender, MouseEventArgs e)
        {
            RectangleButton.Fill = Brushes.LightGray;
        }

        private void RectangleButton_MouseLeave(object sender, MouseEventArgs e)
        {
            RectangleButton.Fill = Brushes.White;
        }

        private void Rectangle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SetIsPressed();
        }

    }
}
