using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using NetworksCeW.Structures;
using NetworksCeW.UnitWorkers;

namespace NetworksCeW.Windows
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
        public int Frames { get; set; }
        public int BytesSent { get; set; }
        public int BytesResent { get; set; }

    }

    /// <summary>
    /// Interaction logic for TerminalWindow.xaml
    /// </summary>
    public partial class TerminalWindow
    {
        private Unit _unit;
        private readonly List<BufferBusy> _listOfBuffers;
        private readonly UnitWorker _unitWorker;

        public TerminalWindow()
        {
            InitializeComponent();
        }

        public TerminalWindow(Unit unit, UnitWorker unitWorker)
        {
            InitializeComponent();
            _unitWorker = unitWorker;
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
            if (!_listOfBuffers.Exists(t => t.ToUnit == item.ToUnit))
            {
                AddItem(item);
            }

            var buffRecord = _listOfBuffers.Find(t => t.ToUnit == item.ToUnit);

            buffRecord.Frames = item.Frames;
            buffRecord.BytesSent = item.BytesSent;
            buffRecord.BytesResent = item.BytesResent;

            UpdateListOfBuffers();
        }

        public void UpdateUnit(Unit unit)
        {
            _unit = unit;
            UpdateListOfBuffers();
        }

        private void UpdateListOfBuffers()
        {
            var listOfBuffers = new List<BufferBusy>(_listOfBuffers);
            _listOfBuffers.Clear();

            Dispatcher.Invoke(() =>
            {
                ListViewBuffers.ItemsSource = null;
            });
            
            foreach (var index in _unit.ListBindsIndexes)
            {
                var toUnit = MainWindow.ListOfBinds.Find(
                        bind => bind.Index == index
                        ).GetSecondUnitIndex(_unit.Index);
                var buffRecord = listOfBuffers.Find(item => item.ToUnit == toUnit);

                AddItem(new BufferBusy()
                {
                    ToUnit = toUnit,
                    Frames = buffRecord?.Frames ?? 0,
                    BytesSent = buffRecord?.BytesSent ?? 0,
                    BytesResent = buffRecord?.BytesResent ?? 0
                });
            }

            Dispatcher.Invoke(() =>
            {
                ListViewBuffers.ItemsSource = _listOfBuffers;
            });
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
            Dispatcher.Invoke(UpdateLayout);
        }

        public void UpdateDestinatioinVariants(List<byte> destinations)
        {
            Dispatcher.Invoke((() =>
            {
                ComboChoose.Items.Clear();
                foreach (var dest in destinations)
                {
                    ComboChoose.Items.Add(dest);
                }

            }));
            Update();
        }

        private void ButtonSend_Click(object sender, RoutedEventArgs e)
        {
            if (RadioTCP.IsChecked == null)
            {
                MessageBox.Show("Protocol is not specified!");
                return;
            }
            if (ComboChoose.SelectedIndex == -1)
            {
                MessageBox.Show("Destination unit is not chosen!");
                return;
            }
            if (TextBoxMessage.Text.Length == 0)
            {
                MessageBox.Show("Message is empty!");
                return;
            }

            _unitWorker.SendMessage(
                (bool) RadioTCP.IsChecked ? (byte)6 : (byte)17,
                MessageToList(TextBoxMessage.Text),
                byte.Parse(ComboChoose.Text)
                );

            MessageBox.Show("Message is sent!");
        }

        private static List<byte> MessageToList(string message)
        {
            return message.Select(ch => (byte) ch).ToList();
        }
    }
}
