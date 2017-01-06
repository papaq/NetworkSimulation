using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

using NetworksCeW.Structures;
using NetworksCeW.UnitWorkers;
using NetworksCeW.File;

namespace NetworksCeW.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        // C O N S T A N T S

        private const int Grgr = 20;
        private const int MyFontSize = 10;
        private const int Buffer = 2000;

        //private readonly SolidColorBrush _seaBrush = new SolidColorBrush(Color.FromArgb(255, 00, 255, 255));
        private readonly SolidColorBrush _blueMenuSelectBrush = new SolidColorBrush(Color.FromArgb(255, 55, 153, 255));
        private readonly SolidColorBrush _pinkyBrush = new SolidColorBrush(Color.FromArgb(255, 255, 0, 116));

        private const string Folders = "\\Resources\\Recover";

        // F L A G S

        private int _enterUnit = -1; // Index of Unit in ListOfUnits
        private int _rightClickUnit = -1;
        private int _leftClickUnit = -1;

        private int _moveItem = -1;  // Index of Unit, we move, in ListOfUnits

        private int _connectItem1 = -1;
        private int _connectItem2 = -1;

        private int _enterLine; // Index of line in CollectionOfLines
        private int _rightClickLine = -1;
        private int _leftClickLine = -1;

        private int _currentUnitIndex = -1;
        private int _currentBindIndex = -1;

        private Point _currentPosition;

        private bool _randomOpen;

        // C O N T E X T   M E N U S

        private ContextMenu _contextMenuUnit = new ContextMenu();
        private ContextMenu _contextMenuBinding = new ContextMenu();

        // L I S T S

        private List<Unit> _listOfUnits = new List<Unit>();
        public static List<Bind> ListOfBinds = new List<Bind>();
        private List<Grid> _collectionOfUnitGrids = new List<Grid>();
        private List<Line> _collectionOfLines = new List<Line>();

        private List<string> _listOfFiles = new List<string>() { "Manual", "Random" };

        public List<UnitTerminal> ListOfTerminals = new List<UnitTerminal>();

        //private List<YouCanGetThere> _listOfRouteChoices = new List<YouCanGetThere>();

        // F I L E S   D I R E C T O R Y
        private readonly string _path;

        // S C R E E N   S I Z E

        private double _screenWidth;
        private double _screenHeight;

        private double _canvasHeight;
        private double _canvasWidth;


        // C O U N T E R S

        //private int _counterRecursion;


        public MainWindow()
        {
            InitializeComponent();

            _contextMenuBinding.Style = (Style)FindResource("ContextMenuStyle");
            _contextMenuUnit.Style = (Style)FindResource("ContextMenuStyle");

            _path = Directory.GetParent("..") + Folders;
            _screenHeight = MainWindow1.Height;

            _canvasHeight = MyCanvas.Height;
            _canvasWidth = MyCanvas.Width;
            _screenWidth = MainWindow1.Width;
            FillComboCreate();
        }

        private void FillComboCreate()
        {
            string[] filesBnd;
            string[] filesUnt;

            try
            {
                filesBnd = Directory.GetFiles(_path, "*.bnd");
                filesUnt = Directory.GetFiles(_path, "*.unt");
                for (var i = 0; i < filesBnd.Length; i++)
                    filesBnd[i] = filesBnd[i].Substring(filesBnd[i].LastIndexOf("\\") + 1,
                        filesBnd[i].Length - filesBnd[i].LastIndexOf("\\") - 5);
                for (var i = 0; i < filesUnt.Length; i++)
                    filesUnt[i] = filesUnt[i].Substring(filesUnt[i].LastIndexOf("\\") + 1,
                        filesUnt[i].Length - filesUnt[i].LastIndexOf("\\") - 5);
            }
            catch
            {
                return;
            }

            _listOfFiles.Clear();
            ComboChooseCreate.ItemsSource = null;
            ComboChooseCreate.Items.Clear();
            _listOfFiles.Add("Manual");
            _listOfFiles.Add("Random");

            if (filesBnd.Length > 0)
                foreach (var fileT in filesBnd)
                    if (filesUnt.Contains(fileT))
                        _listOfFiles.Add(fileT);

            ComboChooseCreate.ItemsSource = _listOfFiles;
            ComboChooseCreate.SelectedIndex = 0;

            button_Create.Content = "SAVE";
        }

        private void ClearWindow()
        {
            MyCanvas.Children.Clear();
            _listOfUnits.Clear();
            ListOfBinds.Clear();
            //_listOfRouteChoices.Clear();

            foreach (var grid in _collectionOfUnitGrids)
                UnregisterName(grid.Name);

            foreach (var line in _collectionOfLines)
                UnregisterName(line.Name);

            _collectionOfUnitGrids.Clear();
            _collectionOfLines.Clear();

            _currentUnitIndex = -1;
            _currentBindIndex = -1;

            ListViewInfo.ItemsSource = null;
            ListViewInfo.Items.Clear();
        }

        private MenuItem AddNewItem(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "New unit",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += AddUnit_Click;
            return item;
        }

        private MenuItem SetEditItem(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Edit unit",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += EditUnit_Click;
            return item;
        }

        private MenuItem SetDisabledItem(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Disable unit",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += DisableUnit_Click;
            return item;
        }

        private MenuItem SetEnabledItem(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Enable unit",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += EnableUnit_Click;
            return item;
        }

        private MenuItem DeleteItem(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Delete unit",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += DeleteUnit_Click;
            return item;
        }

        private MenuItem ConnectItem(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Connect",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += ConnectUnit_Click;
            return item;
        }

        private MenuItem OpenTerminal(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Terminal",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += OpenTerminal_Click;
            return item;
        }

        private MenuItem EditBinding(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Edit",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += EditBinding_Click;
            return item;
        }

        private MenuItem SetDisabledBinding(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Disable binding",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += DisableBinding_Click;
            return item;
        }

        private MenuItem SetEnabledBinding(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Enable binding",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += EnableBinding_Click;
            return item;
        }

        private MenuItem DeleteBinding(bool enabled)
        {
            var item = new MenuItem
            {
                Header = "Delete",
                IsEnabled = enabled,
                Style = (Style)FindResource("ContextMenuItem")
            };
            item.Click += DeleteBinding_Click;
            return item;
        }

        private void Set_cmUnit(Point position)
        {
            var addItem = CheckIfAdd(position);
            _contextMenuUnit.Items.Add(AddNewItem(addItem));

            _contextMenuUnit.Items.Add(SetEditItem(_rightClickUnit != -1));

            if (_rightClickUnit != -1 && _listOfUnits[_rightClickUnit].Disabled)
                _contextMenuUnit.Items.Add(SetEnabledItem(true));
            else
            {
                _contextMenuUnit.Items.Add(SetDisabledItem(_rightClickUnit != -1));
            }

            _contextMenuUnit.Items.Add(DeleteItem(_rightClickUnit != -1));

            _contextMenuUnit.Items.Add(ConnectItem(SetConnectItems()));

            _contextMenuUnit.Items.Add(OpenTerminal(_rightClickUnit != -1));
        }

        private void Set_cmBinding()
        {
            _contextMenuBinding.Items.Add(EditBinding(true));

            if (!ListOfBinds[_rightClickLine].Disabled)
                _contextMenuBinding.Items.Add(SetDisabledBinding(true));
            else
            {
                var listOfCoUnitsIndexes = ListOfBinds[_rightClickLine].ListOfBothUnitsIndexes;
                if (!GetUnitByIndex(listOfCoUnitsIndexes[0]).Disabled
                    && !GetUnitByIndex(listOfCoUnitsIndexes[1]).Disabled)
                    _contextMenuBinding.Items.Add(SetEnabledBinding(true));
                else
                    _contextMenuBinding.Items.Add(SetEnabledBinding(false));
            }

            _contextMenuBinding.Items.Add(DeleteBinding(true));
        }

        private bool SetConnectItems()
        {
            if (_rightClickUnit != -1 && _listOfUnits.Count > 1)
                if (_connectItem1 != -1)
                {
                    _connectItem2 = _rightClickUnit;
                    if (_listOfUnits[_connectItem1].GetListOfConnectedUnitsIndexes(ListOfBinds)
                        .Contains(_listOfUnits[_connectItem2].Index) || _connectItem1 == _connectItem2)
                        _connectItem1 = _connectItem2 = -1;
                }
                else
                    _connectItem1 = _rightClickUnit;
            else
                _connectItem1 = _connectItem2 = -1;
            return _connectItem1 != -1;
        }

        private Unit GetUnitByIndex(int idx)
        {
            return _listOfUnits.Find(unit => unit.Index == idx);
        }

        private double GetDistanceBetweenPoints(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        private double GetDistanceToLine(Point dot, Line line)
        {
            var distance = Math.Abs((line.Y2 - line.Y1) * dot.X - (line.X2 - line.X1) * dot.Y
                                       + line.X2 * line.Y1 - line.Y2 * line.X1)
                              / Math.Sqrt(Math.Pow(line.Y2 - line.Y1, 2) + Math.Pow(line.X2 - line.X1, 2));
            return distance;
        }

        private int FindCircleInside(Point position)
        {
            foreach (var unit in _listOfUnits)
                if (GetDistanceBetweenPoints(unit.Position, position) < Grgr * .5)
                    return _listOfUnits.IndexOf(unit);
            return -1;
        }

        private int FindLineClose(Point pos)
        {
            foreach (var line in _collectionOfLines)
            {
                if (GetDistanceToLine(pos, line) < 20
                    && Math.Abs(pos.X - line.X1)
                    + Math.Abs(pos.X - line.X2) < Math.Abs(line.X1 - line.X2) + 1
                    && Math.Abs(pos.Y - line.Y1)
                    + Math.Abs(pos.Y - line.Y2) < Math.Abs(line.Y1 - line.Y2) + 1)
                    return _collectionOfLines.IndexOf(line);
            }
            return -1;
        }

        private bool CheckIfAdd(Point position)
        {
            return _listOfUnits.All(unit => !(GetDistanceBetweenPoints(unit.Position, position) <= Grgr + 5));
        }

        private bool CheckIfAlone(Point position, int myIndex)
        {
            return _listOfUnits.All(unit => !(GetDistanceBetweenPoints(unit.Position, position) <= Grgr + 5) || unit.Index == myIndex);
        }

        private bool CheckIfNotCloseToBorder(Point position, int myIndex)
        {
            if (2 > position.X - Grgr * .5 || MyCanvas.ActualWidth < position.X + Grgr * .5 + 2
                || 2 > position.Y - Grgr * .5 || MyCanvas.ActualHeight < position.Y + Grgr * .5 + 2)
                return false;
            return true;
        }

        private void MyCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _rightClickLine = _enterLine;
            _rightClickUnit = _enterUnit;

            _currentPosition = e.GetPosition(MyCanvas);

            if (_enterLine != -1 && _rightClickUnit == -1)
            {
                _contextMenuBinding.Items.Clear();
                Set_cmBinding();
                _contextMenuBinding.IsOpen = true;
            }
            else
            {
                _contextMenuUnit.Items.Clear();
                Set_cmUnit(_currentPosition);
                _contextMenuUnit.IsOpen = true;
            }

        }

        private int GridNameIndex()
        {
            return (++_currentUnitIndex);
        }

        private string LineNameIndex()
        {
            return "line" + (++_currentBindIndex);
        }

        private Unit PutEllipse(Point position, int nameIndex, int buffSize)
        {
            var ellipseGrid = new Grid
            {
                Height = Grgr,
                Width = Grgr,
                Name = "grid" + nameIndex,
                //Background = Brushes.AliceBlue,
                Margin = new Thickness(position.X - Grgr * .5, position.Y - Grgr * .5, 0, 0),
            };
            ellipseGrid.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(EllipseGrid_Click));
            RegisterName(ellipseGrid.Name, ellipseGrid);
            Panel.SetZIndex(ellipseGrid, 1);

            var ellipse = new Ellipse
            {
                Height = Grgr,
                Width = Grgr,
                StrokeThickness = 1,
                Stroke = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = Brushes.White
            };

            var textBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = MyFontSize,
                Text = nameIndex.ToString()
            };

            ellipseGrid.Children.Add(ellipse);
            ellipseGrid.Children.Add(textBlock);
            MyCanvas.Children.Add(ellipseGrid);

            _collectionOfUnitGrids.Add(ellipseGrid);

            var unit = new Unit()
            {
                Index = nameIndex,
                Position = position,
                Buffer = buffSize,
            };

            ListOfTerminals.Add(new UnitTerminal(unit, ListOfTerminals));

            return unit;
        }

        private void CreateBinding(Unit unit1, Unit unit2, int weight, bool satellite, bool duplex, bool disabled)
        {
            PutLine(unit1.Position, unit2.Position, satellite, LineNameIndex(), disabled);

            var bind = new Bind()
            {
                A = unit1.Position,
                B = unit2.Position,
                ListOfBothUnitsIndexes = new List<int>()
                {
                    unit1.Index,
                    unit2.Index,
                },
                Weight = weight,
                Satellite = satellite,
                Duplex = duplex,
                Index = _currentBindIndex,
                Disabled = disabled,
            };

            unit1.AddBind(bind);
            unit2.AddBind(bind);

            ListOfBinds.Add(bind);
        }

        private void PutLine(Point p1, Point p2, bool satellite, string name, bool disabled)
        {
            var line = new Line()
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                StrokeThickness = satellite ? 1 : 2,
                Stroke = disabled ? Brushes.DarkGray : _blueMenuSelectBrush,
                Name = name,
            };
            RegisterName(line.Name, line);

            Panel.SetZIndex(line, 0);
            MyCanvas.Children.Add(line);

            _collectionOfLines.Add(line);
        }

        private Grid FindGridByIndex(int ind)
        {
            var grid = (Grid)MyCanvas.FindName("grid" + ind);
            return grid;
        }

        private void DeleteGrid(int whichUnit)
        {
            var mygrd = FindGridByIndex(_listOfUnits[whichUnit].Index);
            if (mygrd == null)
                return;
            UnregisterName(mygrd.Name);
            _collectionOfUnitGrids.Remove(mygrd);
            mygrd.Children.RemoveRange(0, 2);
            MyCanvas.Children.Remove(mygrd);
        }

        private void DeleteLine(int lineNumber)
        {
            if (_collectionOfLines.Count == 0 || lineNumber == -1)
                return;
            var line = _collectionOfLines[lineNumber];
            UnregisterName(line.Name);
            _collectionOfLines.RemoveAt(lineNumber);
            MyCanvas.Children.Remove(line);
        }

        private void AddUnit_Click(object sender, RoutedEventArgs e)
        {
            AddUnit(_currentPosition);
            UpdateInfoBox();
        }

        private void AddUnit(Point where)
        {
            _listOfUnits.Add(PutEllipse(where, GridNameIndex(), Buffer));
            _moveItem = -1;
            _connectItem1 = _connectItem2 = -1;
        }

        private void EditUnit_Click(object sender, RoutedEventArgs e)
        {
            var unit = _listOfUnits[_rightClickUnit];
            var dialog = new UnitWindow(unit.Buffer, unit.Disabled);
            dialog.ShowDialog();

            unit.Buffer = dialog.BuffSize;
            unit.Disabled = dialog.Disabled;

            UpdateTerminal(_listOfUnits[_rightClickUnit]);
        }

        private void DisableUnit_Click(object sender, RoutedEventArgs e)
        {
            _listOfUnits[_rightClickUnit].Disabled = true;
            _collectionOfUnitGrids[_rightClickUnit].Children.OfType<Ellipse>().First().Stroke = Brushes.DarkGray;

            foreach (var bindingIndex in _listOfUnits[_rightClickUnit].ListBindsIndexes)
            {
                var binding = ListOfBinds.Find(bind => bind.Index == bindingIndex);
                var line = (Line)MyCanvas.FindName("line" + binding.Index);
                binding.Disabled = true;
                if (line != null)
                    line.Stroke = Brushes.DarkGray;
            }

            _connectItem1 = _connectItem2 = -1;
            UpdateInfoBox();
            UpdateTerminal(_listOfUnits[_rightClickUnit]);
        }

        private void EnableUnit_Click(object sender, RoutedEventArgs e)
        {
            _listOfUnits[_rightClickUnit].Disabled = false;
            _collectionOfUnitGrids[_rightClickUnit].Children.OfType<Ellipse>().First().Stroke = Brushes.Black;

            /*foreach (var binding in _listOfUnits[_rightClickUnit].GetListOfBindings())
                if (!binding.GetSecondUNit(_listOfUnits[_rightClickUnit]).Disabled)
                {
                    binding.Disabled = true;
                    var line = (Line)MyCanvas.FindName("line" + binding.Index);
                    if (line != null)
                        line.Stroke = Brushes.DarkGray;
                }*/

            _connectItem1 = _connectItem2 = -1;
            UpdateInfoBox();
            UpdateTerminal(_listOfUnits[_rightClickUnit]);
        }

        private void DeleteUnit_Click(object sender, RoutedEventArgs e)
        {
            // delete bindings in all units

            /*
            var listOfMyBindings = _listOfUnits[_rightClickUnit].GetListOfBindings();
            while (listOfMyBindings.Count > 0)
            {
                DeleteBinding(_listOfBinds.IndexOf(listOfMyBindings[0]));
            }
            */

            DeleteUnit(_rightClickUnit);
            UpdateInfoBox();
        }

        private void DeleteUnit(int whichUnit)
        {
            if (whichUnit == -1)
            {
                return;
            }

            DeleteTerminal(_listOfUnits[whichUnit]);

            var listOfMyBindingsIndexes = _listOfUnits[whichUnit].ListBindsIndexes;
            while (listOfMyBindingsIndexes.Count > 0)
            {
                DeleteBinding(ListOfBinds.IndexOf(ListOfBinds.Find(unit => unit.Index == listOfMyBindingsIndexes[0])));
            }

            DeleteGrid(whichUnit);
            _listOfUnits.RemoveAt(whichUnit);

            _moveItem = -1;
            _connectItem1 = _connectItem2 = -1;
            _leftClickUnit = _rightClickUnit = _enterUnit = -1;
        }

        private void ConnectUnit_Click(object sender, RoutedEventArgs e)
        {
            _leftClickLine = -1;
            _leftClickUnit = -1;

            // First time click
            if (_connectItem2 == -1)
                return;

            // Second time cliсk
            var unit1 = _listOfUnits[_connectItem1];
            var unit2 = _listOfUnits[_connectItem2];

            var dialog = new ConnectWindow(1, false, true,
                unit1.Disabled || unit2.Disabled,
                unit1.Disabled || unit2.Disabled);
            dialog.ShowDialog();

            CreateBinding(unit1, unit2, dialog.Weight, dialog.Satellite, dialog.Duplex, dialog.Disabled);

            _connectItem1 = _connectItem2 = -1;

            UpdateTerminal(unit1);
            UpdateTerminal(unit2);

            ChangeSelection();
            UpdateInfoBox();
        }

        private void OpenTerminal_Click(object sender, RoutedEventArgs e)
        {
            ListOfTerminals.Find(item => item.UnitInst.Index == _rightClickUnit).OpenTerminal();
        }       

        private void EditBinding_Click(object sender, RoutedEventArgs e)
        {
            var bind = ListOfBinds[_rightClickLine];
            var dialog = new ConnectWindow(bind.Weight, bind.Satellite, bind.Duplex, bind.Disabled,
                _listOfUnits.Find(unit => unit.Index == bind.ListOfBothUnitsIndexes[0]).Disabled ||
                _listOfUnits.Find(unit => unit.Index == bind.ListOfBothUnitsIndexes[1]).Disabled);
            dialog.ShowDialog();

            if (bind.Satellite != dialog.Satellite || bind.Disabled != dialog.Disabled)
            {
                var line = (Line)MyCanvas.FindName("line" + bind.Index);

                if (line != null)
                {
                    line.Stroke = dialog.Disabled ? Brushes.DarkGray : _blueMenuSelectBrush;
                    line.StrokeThickness = dialog.Satellite ? 1 : 2;
                }
            }

            bind.Duplex = dialog.Duplex;
            bind.Weight = dialog.Weight;
            bind.Satellite = dialog.Satellite;
            bind.Disabled = dialog.Disabled;

            UpdateInfoBox();
        }

        private void DisableBinding_Click(object sender, RoutedEventArgs e)
        {
            ListOfBinds[_rightClickLine].Disabled = true;
            _collectionOfLines[_rightClickLine].Stroke = Brushes.DarkGray;
            UpdateInfoBox();

            _connectItem1 = _connectItem2 = -1;
        }

        private void EnableBinding_Click(object sender, RoutedEventArgs e)
        {

            ListOfBinds[_rightClickLine].Disabled = false;
            _collectionOfLines[_rightClickLine].Stroke = _blueMenuSelectBrush;
            UpdateInfoBox();

            _connectItem1 = _connectItem2 = -1;
        }

        private void DeleteBinding(int lineNumber)
        {
            if (lineNumber == -1)
                return;

            DeleteLine(lineNumber);

            var listOfBothUnitsIndexes = ListOfBinds[lineNumber].ListOfBothUnitsIndexes;
            Unit unit1 = GetUnitByIndex(listOfBothUnitsIndexes[0]);
            Unit unit2 = GetUnitByIndex(listOfBothUnitsIndexes[1]);

            unit1.ListBindsIndexes.Remove(ListOfBinds[lineNumber].Index);
            unit2.ListBindsIndexes.Remove(ListOfBinds[lineNumber].Index);

            UpdateTerminal(unit1);
            UpdateTerminal(unit2);

            ListOfBinds.RemoveAt(lineNumber);

            _leftClickLine = _rightClickLine = _enterLine = -1;
        }

        private void DeleteBinding_Click(object sender, RoutedEventArgs e)
        {
            DeleteBinding(_rightClickLine);
            UpdateInfoBox();
        }

        private void EllipseGrid_Click(object sender, MouseButtonEventArgs e)
        {
            _moveItem = FindCircleInside(e.GetPosition(MyCanvas));
            if (_enterUnit != -1)
                SetEllipseSize(_collectionOfUnitGrids[_enterUnit], .8);
        }

        private void StopMoving()
        {
            if (_moveItem != -1)
                SetEllipseSize(_collectionOfUnitGrids[_moveItem], 1);
            _moveItem = -1;
            SetAllUnitsBlack();
        }

        private void MainWindow1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _connectItem1 = _connectItem2 = -1;
            StopMoving();
        }

        // NEEDS TO BE DIVIDED AND EDITED

        private void MainWindow1_MouseMove(object sender, MouseEventArgs e)
        {
            var currentPosition = e.GetPosition(MyCanvas);
            _enterUnit = FindCircleInside(currentPosition);
            _enterLine = FindLineClose(currentPosition);

            // Moving Units

            /* Test */
            labelLeftClick.Content = _leftClickUnit;
            labelMoveItem.Content = _moveItem;
            labelUnitEnter.Content = _enterUnit;


            if (_moveItem != -1 && _listOfUnits.Count > 0)
            {
                var unit = _listOfUnits[_moveItem];
                var mygrd = FindGridByIndex(unit.Index);
                if (mygrd == null)
                    return;

                if (!CheckIfNotCloseToBorder(currentPosition, unit.Index))
                {
                    StopMoving();
                    return;
                }

                var oldPosition = unit.Position;

                if (!CheckIfAlone(currentPosition, unit.Index)) return;
                mygrd.Margin = new Thickness(currentPosition.X - Grgr * .5, currentPosition.Y - Grgr * .5, 0, 0);
                unit.Position = currentPosition;

                foreach (var bind in ListOfBinds)
                {
                    Line line;
                    if (bind.ConnectsUnit(unit.Index) && (line = (Line)MyCanvas.FindName("line" + bind.Index)) != null)
                        bind.TurnLine(line, oldPosition, currentPosition);
                }
            }


            // <-- 
            // T E S T

            labelBinding.Content = _enterLine;
            labelUnit.Content = _enterUnit;

            label.Content = e.GetPosition(MainWindow1);

            labelUnits.Content = _listOfUnits.Count;
            labelBindings.Content = ListOfBinds.Count;
            labelGrids.Content = _collectionOfUnitGrids.Count;
            labelLines.Content = _collectionOfLines.Count;

            // T E S T
            // --!>

            ChangeSelection();
        }

        private void ChangeSelection()
        {
            // Entering Binding
            if (_enterLine != -1 && _enterUnit == -1)
            {
                SetAllUnitsBlack();
                SetAllLinesBlue();
                SetConnectItemPink();
                _collectionOfLines[_enterLine].Stroke = _pinkyBrush;
            }

            // Entering Unit
            else if (_enterUnit != -1)
            {
                _collectionOfUnitGrids[_enterUnit].Children.OfType<Ellipse>().First().Stroke = _pinkyBrush;
                SetAllLinesBlue();
            }

            // Out of Binding and Unit
            else
            {
                // set all ordinary
                SetAllLinesBlue();
                SetAllUnitsBlack();
                SetConnectItemPink();
            }
        }

        private void SetConnectItemPink()
        {
            if (_connectItem1 != -1 && _collectionOfUnitGrids.Count > 0)
                _collectionOfUnitGrids[_connectItem1].Children.OfType<Ellipse>().First().Stroke = _pinkyBrush;
        }

        private void SetAllUnitsBlack()
        {
            var i = 0;
            foreach (var grid in _collectionOfUnitGrids)
                if (i++ != _leftClickUnit)
                    grid.Children.OfType<Ellipse>().First().Stroke = _listOfUnits[i - 1].Disabled
                        ? Brushes.DarkGray
                        : Brushes.Black;
        }

        private void SetAllLinesBlue()
        {
            var i = 0;
            foreach (var line in _collectionOfLines)
                if (i++ != _leftClickLine)
                    line.Stroke = ListOfBinds[i - 1].Disabled
                        ? Brushes.DarkGray
                        : _blueMenuSelectBrush;
        }

        private void SetEllipseSize(Panel grid, double newPart)
        {
            if (_collectionOfUnitGrids.Count <= 0 || _enterUnit == -1) return;
            grid.Children.OfType<Ellipse>().First().Height = newPart * Grgr;
            grid.Children.OfType<Ellipse>().First().Width = newPart * Grgr;
            //grid.Children.OfType<TextBlock>().First().FontSize = MyFontSize * newPart;
        }

        private void button_Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearWindow();
        }

        private void FillInfo(Unit unit)
        {
            var listInfo = new List<Info>()
            {
                new Info() {Category = "Unit:", Value = ""},
                new Info() {Category = "Index", Value = unit.Index.ToString()},
                new Info() {Category = "Status", Value = unit.Disabled ? "Disabled" : "Enabled"},
                new Info() {Category = "Buffer", Value = unit.Buffer.ToString()},
                new Info() {Category = "Connected by:", Value = ""},
            };
            listInfo.AddRange(unit.ListBindsIndexes.Select(index => new Info() { Category = "", Value = index.ToString() }));

            ListViewInfo.ItemsSource = listInfo;
        }

        private void FillInfo(Bind bind)
        {
            var listInfo = new List<Info>
            {
                new Info() {Category = "Binding:", Value = ""},
                new Info() {Category = "Index", Value = ListOfBinds.IndexOf(bind).ToString()},
                new Info() {Category = "Status", Value = bind.Disabled ? "Disabled" : "Enabled"},
                new Info() {Category = "Weight", Value = bind.Weight.ToString()},
                new Info() {Category = "Connection", Value = bind.Satellite ? "Satellite" : "Nonsatellite"},
                new Info() {Category = "Transmition", Value = bind.Duplex ? "Full-duplex" : "Half-duplex"},
                new Info() {Category = "Connects units:", Value = bind.ListOfBothUnitsIndexes[0]
                + " and " + bind.ListOfBothUnitsIndexes[1]}
            };

            ListViewInfo.ItemsSource = listInfo;
        }

        private void FillInfo()
        {

            var unitsCount = _listOfUnits.Count;
            var bindsCount = ListOfBinds.Count;

            var listInfo = new List<Info>
            {
                new Info() {Category = "Network:", Value = ""},
                new Info() {Category = "Number of units", Value = unitsCount.ToString()},
                new Info() {Category = "Number of bindings", Value = bindsCount.ToString()},
                new Info() {Category = "Average degree", Value = (unitsCount == 0)
                    ? "N/A"
                    : ((double)bindsCount * 2/unitsCount).ToString("F")},
            };

            ListViewInfo.ItemsSource = listInfo;
        }

        private void UpdateInfoBox()
        {
            if (_enterUnit != -1)
            {
                _leftClickLine = -1;
                _leftClickUnit = _enterUnit;

                _connectItem1 = _connectItem2 = -1;
                ChangeSelection();

                if (_listOfUnits.Count > _leftClickUnit)
                    FillInfo(_listOfUnits[_leftClickUnit]);
            }
            else if (_enterLine != -1)
            {
                _leftClickUnit = -1;
                _leftClickLine = _enterLine;

                if (ListOfBinds.Count > _leftClickLine)
                    FillInfo(ListOfBinds[_leftClickLine]);
            }
            else
            {
                _leftClickLine = -1;
                _leftClickUnit = -1;

                ChangeSelection();

                FillInfo();
            }
        }

        private void MyGridCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_enterUnit != -1 
                && Keyboard.IsKeyDown(Key.C) 
                && _leftClickUnit != -1 
                && _leftClickUnit != _enterUnit
                && !_listOfUnits[_enterUnit].GetListOfConnectedUnitsIndexes(ListOfBinds)
                    .Contains(_listOfUnits[_leftClickUnit].Index))
            {
                var unit1 = _listOfUnits[_leftClickUnit];
                var unit2 = _listOfUnits[_enterUnit];
                var isGrey = unit1.Disabled || unit2.Disabled;
                CreateBinding(unit1, unit2, 1, false, true, isGrey);

                UpdateTerminal(unit1);
                UpdateTerminal(unit2);
            }
            else if (CheckIfAdd(e.MouseDevice.GetPosition(MyCanvas)) && Keyboard.IsKeyDown(Key.D))
            {
                AddUnit(e.MouseDevice.GetPosition(MyCanvas));
            }

            _currentPosition = e.MouseDevice.GetPosition(MainWindow1);
            UpdateInfoBox();
            //UpdateComboChooseToUnit();
        }

        private void button_Create_Click(object sender, RoutedEventArgs e)
        {
            if ((string)button_Create.Content == "CREATE")
            {
                button_Create.Content = "SAVE";

                if (_randomOpen)
                {
                    AnimateHeight(140);
                    _randomOpen = false;

                    CreateRandomNetwork();
                }
                else
                    RecoverNetworkFromFile(ComboChooseCreate.Text);

                UpdateInfoBox();
            }
            else if ((string)button_Create.Content == "SAVE")
            {
                var uniqueName = FileBackup.FindUniqueName(_listOfFiles.ToArray());
                if (uniqueName == null)
                    return;

                FileBackup.ListOfBinds.Clear();
                FileBackup.ListOfUnits.Clear();
                FileBackup.ListOfBinds.AddRange(ListOfBinds);
                FileBackup.ListOfUnits.AddRange(_listOfUnits);

                FileBackup.PreCoverBindsResize(MyCanvas.ActualWidth,
                    MyCanvas.MinWidth, MyCanvas.ActualHeight, MyCanvas.MinHeight);
                FileBackup.PreCoverUnitsResize(MyCanvas.ActualWidth,
                    MyCanvas.MinWidth, MyCanvas.ActualHeight, MyCanvas.MinHeight);
                FileBackup.WriteAll(_path, uniqueName + ".unt", uniqueName + ".bnd");

                FillComboCreate();
            }
        }


        private void UpdateTerminal(Unit unit)
        {
            UnitTerminal term = ListOfTerminals.Find(t => t.UnitInst == unit);
            if (term != null)
            {
                term.UpdateBuffersNumber(unit);
            }
        }

        private void DeleteTerminal(Unit unit)
        {
            UnitTerminal term = ListOfTerminals.Find(t => t.UnitInst == unit);
            if (term != null)
            {
                term.DeleteUnit(unit);
            }

            ListOfTerminals.Remove(term);
        }

        // C R E A T E   R A N D O M   N E T W O R K


        private void CreateRandomNetwork()
        {
            ClearWindow();

            var width = MyCanvas.Width;
            var height = MyCanvas.Height;
            var rnd = new Random();
            var arrOfSatDots = new Point[3];

            var unitIndexFrom = 0;

            var arrOfDots = GetArrayOfDots(new Point(30, 30), new Point(width + 100, height - 30), rnd.Next(26, 29), rnd);
            arrOfSatDots[0] = arrOfDots[rnd.Next(0, 8)];
            arrOfSatDots[1] = arrOfDots[rnd.Next(8, 17)];
            arrOfSatDots[2] = arrOfDots[rnd.Next(17, arrOfDots.Length-1)];

            CompleteListOfUnitsFromArrayOfDots(arrOfDots, rnd);
            SetSatelliteConns(unitIndexFrom, arrOfDots.Length - 1, 2, rnd);
            ReachRequiredDegree(4, rnd);
        }
        
        private Point[] GetArrayOfDots(Point highLeft, Point lowRight, int numOfDots, Random rnd)
        {
            var arr = new Point[numOfDots];

            var width = lowRight.X - highLeft.X;
            var height = lowRight.Y - highLeft.Y;

            var dotsInWidth = (int)Math.Ceiling(Math.Sqrt(numOfDots * height / width));
            var dotsInHeight = (int)Math.Ceiling(Math.Sqrt(numOfDots * width / height));

            var partInWidth = (int)Math.Ceiling(width / (dotsInWidth + 1));
            var partInHeight = (int)Math.Ceiling(height / (dotsInHeight + 1));

            //var rnd = new Random();
            var i = 0;
            while (i < dotsInWidth)
            {
                var j = 0;

                while (j < dotsInHeight)
                {
                    var newX = highLeft.X + i * partInWidth + rnd.Next(5, partInWidth);
                    var newY = highLeft.Y + j * partInHeight + rnd.Next(5, partInHeight);
                    var badPosition = false;

                    for (var k = 0; k < i * dotsInHeight + j; k++)
                        if (GetDistanceBetweenPoints(new Point(newX, newY), arr[k]) < 22)
                        {
                            badPosition = true;
                            break;
                        }
                    if (badPosition)
                    {
                        continue;
                    }

                    arr[i * dotsInHeight + j] = new Point(newX, newY);
                    j++;

                    if (numOfDots == i * dotsInHeight + j)
                        return arr;
                }
                i++;
            }

            return arr;
        }

        private void CompleteListOfUnitsFromArrayOfDots(Point[] arrOfDots, Random rnd)
        {
            foreach (var dot in arrOfDots)
                _listOfUnits.Add(PutEllipse(dot, GridNameIndex(), checkBoxBufferInterval.IsChecked == true
                    ? rnd.Next(Convert.ToInt16(textBoxBufferRangeFrom.Text), Convert.ToInt16(textBoxBufferRangeTo.Text))
                    : Convert.ToInt16(textBoxBufferRangeFrom.Text)));
        }

        private void SetSatelliteConns(int fromUnit, int toUnit, int numOfS, Random rnd)
        {
            var numOfBinds = 0;
            while (numOfBinds < numOfS)
            {
                var unit1 = _listOfUnits[rnd.Next(fromUnit, toUnit + 1)];
                var unit2 = _listOfUnits[rnd.Next(fromUnit, toUnit + 1)];
                if (unit1 == unit2 || ListOfBinds.Find(bind =>
                    bind.ListOfBothUnitsIndexes.Contains(unit1.Index) &&
                    bind.ListOfBothUnitsIndexes.Contains(unit2.Index)) != null)
                    continue;
                CreateBinding(unit1, unit2,
                    checkBoxWeightInterval.IsChecked == true
                        ? 3 * rnd.Next(Convert.ToInt16(textBoxWeightRangeFrom.Text), Convert.ToInt16(textBoxWeightRangeTo.Text))
                        : 3 * Convert.ToInt16(ConnectionWeight.Text),
                    true, true, false);
                numOfBinds++;
            }
        }

        private void ReachRequiredDegree(double requiredDegree, Random rnd, int from, int to)
        {
            var numOfUnits = _listOfUnits.Count;
            var numOfBinds = ListOfBinds.Count;
            var fromNextCounter = from;
            if (to == 0)
                to = _listOfUnits.Count;
            while (numOfBinds < numOfUnits * requiredDegree / 2)
            {
                var unit1 = _listOfUnits[fromNextCounter];
                var unit2 = _listOfUnits[rnd.Next(from, to)];
                if (unit1 == unit2 || ListOfBinds.Find(bind =>
                    bind.ListOfBothUnitsIndexes.Contains(unit1.Index) &&
                    bind.ListOfBothUnitsIndexes.Contains(unit2.Index)) != null)
                    continue;
                CreateBinding(unit1, unit2,
                    checkBoxWeightInterval.IsChecked == true
                        ? rnd.Next(Convert.ToInt16(textBoxWeightRangeFrom.Text), Convert.ToInt16(textBoxWeightRangeTo.Text))
                        : Convert.ToInt16(ConnectionWeight.Text),
                    false, true, false);
                numOfBinds++;
                if (fromNextCounter == to)
                    fromNextCounter = from;
                else
                    fromNextCounter++;
            }
        }

        private void ReachRequiredDegree(double requiredDegree, Random rnd)
        {
            var numOfUnits = _listOfUnits.Count;
            var numOfBinds = ListOfBinds.Count;
            while (numOfBinds < numOfUnits * requiredDegree / 2)
            {
                var unit1 = _listOfUnits[rnd.Next(0, _listOfUnits.Count)];
                var unit2 = _listOfUnits[rnd.Next(0, _listOfUnits.Count)];
                if (unit1 == unit2 || ListOfBinds.Find(bind =>
                    bind.ListOfBothUnitsIndexes.Contains(unit1.Index) &&
                    bind.ListOfBothUnitsIndexes.Contains(unit2.Index)) != null)
                    continue;
                CreateBinding(unit1, unit2,
                    checkBoxWeightInterval.IsChecked == true
                        ? rnd.Next(Convert.ToInt16(textBoxWeightRangeFrom.Text), Convert.ToInt16(textBoxWeightRangeTo.Text))
                        : Convert.ToInt16(ConnectionWeight.Text),
                    false, true, false);
                numOfBinds++;
            }
        }

        // C R E A T E   N E T W O R K   F R O M   F I L E


        private void RecoverNetworkFromFile(string fileName)
        {
            ClearWindow();

            FileBackup.ReadAll(_path, fileName + ".unt", fileName + ".bnd");
            FileBackup.PreCoverUnitsResize(MyCanvas.MinWidth,
                MyCanvas.ActualWidth, MyCanvas.MinHeight, MyCanvas.ActualHeight);
            FileBackup.PreCoverBindsResize(MyCanvas.MinWidth,
                MyCanvas.ActualWidth, MyCanvas.MinHeight, MyCanvas.ActualHeight);

            _listOfUnits.Clear();
            _listOfUnits.AddRange(FileBackup.ListOfUnits);
            ListOfBinds.Clear();
            ListOfBinds.AddRange(FileBackup.ListOfBinds);

            if (_listOfUnits == null || ListOfBinds == null)
            {
                CreateRandomNetwork();
                return;
            }

            foreach (var unit in _listOfUnits)
                PutEllipse(unit.Position, unit.Index, unit.Buffer);

            foreach (var bind in ListOfBinds)
                PutLine(bind.A, bind.B, bind.Satellite, "line" + bind.Index, bind.Disabled);

            _currentUnitIndex = _listOfUnits.Count == 0 ? -1 : _listOfUnits.Last().Index;
            _currentBindIndex = ListOfBinds.Count == 0 ? -1 : ListOfBinds.Last().Index;
        }

        private void ComboChooseCreate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            button_Create.Content = ComboChooseCreate.SelectedIndex == 0 ? "SAVE" : "CREATE";
            if (ComboChooseCreate.SelectedIndex == 1 && !_randomOpen)
            {
                AnimateHeight(-140);
                _randomOpen = true;
            }
            else if (_randomOpen)
            {
                AnimateHeight(140);
                _randomOpen = false;

            }
        }

        private void AnimateHeight(double delta)
        {
            var da = new DoubleAnimation { From = tabControl.Height };
            da.To = da.From + delta;
            da.Duration = new Duration(TimeSpan.FromSeconds(0.5));
            tabControl.BeginAnimation(HeightProperty, null);
            tabControl.BeginAnimation(HeightProperty, da);
        }

        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            ConnectionWeight.Visibility = Visibility.Hidden;
            textBoxWeightRangeFrom.Visibility = textBoxWeightRangeTo.Visibility = Visibility.Visible;
        }

        private void checkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ConnectionWeight.Visibility = Visibility.Visible;
            textBoxWeightRangeFrom.Visibility = textBoxWeightRangeTo.Visibility = Visibility.Hidden;
        }

        private void checkBoxBufferInterval_Checked(object sender, RoutedEventArgs e)
        {
            textBoxBufferRangeTo.Visibility = Visibility.Visible;
        }

        private void checkBoxBufferInterval_Unchecked(object sender, RoutedEventArgs e)
        {
            textBoxBufferRangeTo.Visibility = Visibility.Hidden;
        }

        private void ResizeControls()
        {
            var tabHeight = tabControl.ActualHeight;
            tabControl.BeginAnimation(HeightProperty, null);
            tabControl.Height = tabHeight + MainWindow1.ActualHeight - _screenHeight;

            MyCanvas.Height += MainWindow1.ActualHeight - _screenHeight;
            MyCanvas.Width += MainWindow1.ActualWidth - _screenWidth;

            var reX = MyCanvas.Width / _canvasWidth;
            var reY = MyCanvas.Height / _canvasHeight;

            foreach (var unit in _listOfUnits)
            {
                var newX = unit.Position.X * reX;
                var newY = unit.Position.Y * reY;
                unit.Position = new Point(newX, newY);

                var grid = (Grid)MyCanvas.FindName("grid" + unit.Index);
                if (grid == null) continue;
                var oldGridY = grid.Margin.Top + .5 * Grgr;
                var oldGridX = grid.Margin.Left + .5 * Grgr;

                grid.Margin = new Thickness(oldGridX * reX - .5 * Grgr, oldGridY * reY - .5 * Grgr, 0, 0);
            }

            foreach (var line in _collectionOfLines)
            {
                var binding = ListOfBinds.Find(bind => bind.Index == Convert.ToInt16(line.Name.Substring(4)));
                binding.A = new Point(line.X1 = binding.A.X * reX, line.Y1 = binding.A.Y * reY);
                binding.B = new Point(line.X2 = binding.B.X * reX, line.Y2 = binding.B.Y * reY);
            }

            _screenHeight = MainWindow1.ActualHeight;
            _screenWidth = MainWindow1.ActualWidth;

            _canvasHeight = MyCanvas.Height;
            _canvasWidth = MyCanvas.Width;

        }

        private void MainWindow1_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ResizeControls();
        }

        private void MainWindow1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                label1.Content = "delete" + _leftClickUnit;
                DeleteUnit(_leftClickUnit);
                DeleteBinding(_leftClickLine);
                UpdateInfoBox();
            }
        }



        //  P O N E S L A S Ь


        /*
        private void CountRouts(int forUnitIndex)
        {
            _counterRecursion = 0;
            _listOfRouteChoices.Clear();

            //foreach (var fromUnit in _listOfUnits.Where(unit => !unit.Disabled))
            //{

            var fromUnit = _listOfUnits.Find(unit => unit.Index == forUnitIndex);

            if (fromUnit.Disabled)
            {
                FillRouteInfo(-1);
                return;
            }

            _listOfRouteChoices.Add(new YouCanGetThere(fromUnit.Index));
            foreach (var toUnit in _listOfUnits.Where(toUnit => toUnit != fromUnit && !toUnit.Disabled))
            {
                _listOfRouteChoices.Last().Directions.Add(new Direction(toUnit.Index));
                WalkThroughAllPaths(new List<int>() { fromUnit.Index }, 0, fromUnit, toUnit.Index);
            }

            //}
        }

        private void WalkThroughAllPaths(List<int> iWasThere, int tempWeight, Unit fromUnit, int toUnit)
        {
            if (iWasThere.Count > 15)
                return;
            foreach (var bindIndex in fromUnit.ListBindsIndexes)
            {
                _counterRecursion++;

                var bind = _listOfBinds.Find(myBind => myBind.Index == bindIndex);
                if (bind.Disabled) continue;

                var unitIndex = bind.GetSecondUnitIndex(fromUnit.Index);

                if (iWasThere.Contains(unitIndex)) continue;

                var newListOfStops = new List<int>(iWasThere) { unitIndex };

                if (unitIndex == toUnit)
                {
                    _listOfRouteChoices.Last().Directions.Last().Routes.Add(
                        new Route(tempWeight + bind.Weight, newListOfStops));
                    continue;
                }

                WalkThroughAllPaths(newListOfStops, tempWeight + bind.Weight,
                    _listOfUnits.Find(unit => unit.Index == unitIndex), toUnit);
            }
        }
        */
        private void button_Click(object sender, RoutedEventArgs e)
        {

        }
        /*
        private void UpdateComboChooseToUnit()
        {
            if (_leftClickUnit == -1 || _listOfUnits.Count <= _leftClickUnit)
            {
                FillRouteInfo(-1);
                return;
            }

            CountRouts(_listOfUnits[_leftClickUnit].Index);

            var listOfToUnits = new List<string>();
            ComboChooseToUnit.ItemsSource = null;
            ComboChooseToUnit.Items.Clear();
            listOfToUnits.Add("All");

            var ListOfPrivateRoutes = _listOfRouteChoices.Find(
                from => @from.FromUnitIndex == _listOfUnits[_leftClickUnit].Index);
            if (ListOfPrivateRoutes != null)
            {
                var ListOfRoutesString = ListOfPrivateRoutes.Directions.Select(direction => direction.ToUnitIndex.ToString());
                if (ListOfRoutesString != null)
                {
                    listOfToUnits.AddRange(ListOfRoutesString);

                    ComboChooseToUnit.ItemsSource = listOfToUnits;
                    ComboChooseToUnit.SelectedIndex = 0;
                }
            }            
            
            //FillRouteInfo(-1);
        }

        private void FillRouteInfo(int toUnit)
        {
            ListViewRoutes.ItemsSource = null;

            if (!_listOfRouteChoices.Any() || _leftClickUnit == -1)
                return;

            var routeInfoLst = new List<RouteInfo>();

            if (toUnit == -1)
                foreach (var direction in _listOfRouteChoices.Find(
                    from => from.FromUnitIndex == _listOfUnits[_leftClickUnit].Index).Directions)
                    routeInfoLst.AddRange(FillRouteInfoLst(direction));
            else
                routeInfoLst.AddRange(FillRouteInfoLst(_listOfRouteChoices.Find(
                    from => from.FromUnitIndex == _listOfUnits[_leftClickUnit].Index).Directions.Find(
                    direction => direction.ToUnitIndex == toUnit)));


            ListViewRoutes.ItemsSource = routeInfoLst;
        }

        private List<RouteInfo> FillRouteInfoLst(Direction direction)
        {
            var index = 0;
            var routeInfoList = new List<RouteInfo>();

            foreach (var route in direction.Routes)
            {
                routeInfoList.Add(new RouteInfo()
                {
                    Index = index.ToString(),
                    To = direction.ToUnitIndex.ToString(),
                    Next = route.ListOfStops[1].ToString(),
                    Weight = route.RouteWeight.ToString(),
                    Stops = (route.ListOfStops.Count - 1).ToString(),
                });

                index++;
            }
            return routeInfoList;
        }
        */
        private void ComboChooseToUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /*
            if (ComboChooseToUnit.ItemsSource == null)
                return;

            switch (ComboChooseToUnit.SelectedIndex)
            {
                case 0:
                    FillRouteInfo(-1);
                    break;
                default:
                    FillRouteInfo(Convert.ToInt16(ComboChooseToUnit.SelectedItem));
                    break;
            }
            */
        }

        private void ButtonStartCasey_Click(object sender, RoutedEventArgs e)
        {
            foreach (var terminal in ListOfTerminals)
            {
                if (terminal != null)
                {
                    terminal.StartWorker();
                }
            }
        }
    }

    public class Info
    {
        public string Category { get; set; }

        public string Value { get; set; }
    }

    public class RouteInfo
    {
        public string Index { get; set; }

        public string To { get; set; }

        public string Next { get; set; }

        public string Weight { get; set; }

        public string Stops { get; set; }
    }

    
}
