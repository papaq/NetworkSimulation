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
    /// Structure for outputting log information
    /// </summary>
    public class LogInfo
    {
        public string Time { get; set; }
        public string Info { get; set; }
    }
        
    /// <summary>
    /// Structure for outputting all unit buffers' statuses
    /// </summary>
    public class BufferBusy
    {
        public int ToUnit { get; set; }
        public string Utilization { get; set; }
    }

    /// <summary>
    /// Interaction logic for TerminalWindow.xaml
    /// </summary>
    public partial class TerminalWindow : Window
    {
        private Unit _unit;
        private List<BufferBusy> _listOfBuffers;

        public TerminalWindow()
        {
            InitializeComponent();
        }

        public TerminalWindow(Unit unit)
        {
            InitializeComponent();
            _listOfBuffers = new List<BufferBusy>();
            InitInterfaceFill(unit);
        }

        private void InitInterfaceFill(Unit unit)
        {
            TextBlockUnitN.Text = unit.Index.ToString();
            UpdateUnit(unit);
        }

        public void UpdateItem(BufferBusy item)
        {
            if (_listOfBuffers.Exists(t => t.ToUnit == item.ToUnit))
            {
                _listOfBuffers.Find(t => t.ToUnit == item.ToUnit).Utilization = item.Utilization;
            }
            else
            {
                AddItem(item);
            }
            UpdateListOfBuffers();
        }

        public void UpdateUnit(Unit unit)
        {
            _unit = unit;
            UpdateListOfBuffers();
        }

        private void UpdateListOfBuffers()
        {
            List<BufferBusy> listOfBuffers = new List<BufferBusy>(_listOfBuffers);
            _listOfBuffers.Clear();

            ListViewBuffers.ItemsSource = null;
            foreach (var index in _unit.ListBindsIndexes)
            {
                int toUnit = MainWindow.ListOfBinds.Find(
                        bind => bind.Index == index
                        ).GetSecondUnitIndex(_unit.Index);
                AddItem(new BufferBusy()
                {
                    ToUnit = toUnit,
                    Utilization = listOfBuffers.Exists(item => item.ToUnit == toUnit) 
                    ? listOfBuffers.Find(item => item.ToUnit == toUnit).Utilization
                    : "0%"
                });
            }
            ListViewBuffers.ItemsSource = _listOfBuffers;
        }

        public void AddItem(BufferBusy item)
        {
            if (!_listOfBuffers.Exists(t => t.ToUnit == item.ToUnit))
            {
                _listOfBuffers.Add(item);
            }
        }

        public void RemoveItem(BufferBusy item)
        {
            if (_listOfBuffers.FindIndex(t => t.ToUnit == item.ToUnit) >= 0)
            {
                _listOfBuffers.Remove(item);
            }
        }

        public void WriteLog(DateTime time, string log)
        {
            Dispatcher.Invoke((() => {
                ListViewLogs.Items.Add(new LogInfo()
                {
                    Time = time.Hour.ToString() + ":" + time.Minute.ToString() + ":" + time.Second.ToString(),
                    Info = log
                });
            }));        
        }

        public void Update()
        {
            Dispatcher.Invoke(() => {
                UpdateLayout();
            });
        }
    }
}
