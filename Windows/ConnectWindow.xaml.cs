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

namespace NetworksCeW
{
    /// <summary>
    /// Interaction logic for ConnectWindow.xaml
    /// </summary>
    public partial class ConnectWindow : Window
    {
        public int Weight = 1;
        public bool Satellite;
        public bool Duplex = true;
        public bool Disabled;

        private string[] _listNs = { "1", "3", "4", "6", "7", "10", "12", "15", "18", "21", "26" };
        private string[] _listS = new string[11];

        public ConnectWindow()
        {
            InitializeComponent();
            InitListS();
        }

        public ConnectWindow(int weight, bool sat, bool dup, bool disabled, bool noEn)
        {
            InitializeComponent();
            InitListS();

            Weight = weight;
            Satellite = sat;
            Duplex = dup;
            Disabled = disabled;

            checkBoxDisabled.IsChecked = Disabled;

            if (noEn)
            {
                checkBoxDisabled.IsEnabled = false;
            }

            InitControls();
        }

        private void InitListS()
        {
            for (int i = 0; i < 11; i++)
            {
                _listS[i] = (Convert.ToUInt16(_listNs[i]) * 3).ToString();
            }
        }

        private void InitControls()
        {
            if (Satellite)
            {
                RadioButtonS.IsChecked = true;
                SetComboItems(_listS);
                ConnectionWeight.SelectedIndex = ConnectionWeight.Items.IndexOf(Weight.ToString());
            }
            else
            {
                RadioButtonNonS.IsChecked = true;
                SetComboItems(_listNs);
                ConnectionWeight.SelectedIndex = ConnectionWeight.Items.IndexOf(Weight.ToString());
            }
            if (Duplex)
                RadioButtonDuplex.IsChecked = true;
            else
                RadioButtonHalfDuplex.IsChecked = true;
        }

        private void Rectangle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SetIsPressed();
        }

        private void SetIsPressed()
        {
            Weight = Convert.ToInt16(ConnectionWeight.Text);
            Close();
        }

        private void SetComboItems(string[] arr)
        {
            ConnectionWeight.Items.Clear();
            foreach (var el in arr)
            {
                ConnectionWeight.Items.Add(el);
            }
            ConnectionWeight.SelectedItem = arr[0];
        }

        private void RadioButtonS_Checked(object sender, RoutedEventArgs e)
        {
            SetComboItems(_listS);
            Satellite = true;
        }

        private void RadioButtonNonS_Checked(object sender, RoutedEventArgs e)
        {
            SetComboItems(_listNs);
            Satellite = false;
        }

        private void RadioButtonDuplex_Checked(object sender, RoutedEventArgs e)
        {
            Duplex = true;
        }

        private void RadioButtonHalfDuplex_Checked(object sender, RoutedEventArgs e)
        {
            Duplex = false;
        }

        private void RectangleButton_MouseEnter(object sender, MouseEventArgs e)
        {
            RectangleButton.Fill = Brushes.LightGray;
        }

        private void RectangleButton_MouseLeave(object sender, MouseEventArgs e)
        {
            RectangleButton.Fill = Brushes.White;
        }

        private void _KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetIsPressed();
            }
        }

        private void checkBoxDisabled_Checked(object sender, RoutedEventArgs e)
        {
            Disabled = true;
        }

        private void checkBoxDisabled_Unchecked(object sender, RoutedEventArgs e)
        {
            Disabled = false;
        }
    }
}
