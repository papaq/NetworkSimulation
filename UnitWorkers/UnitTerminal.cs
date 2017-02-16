using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetworksCeW.Structures;
using NetworksCeW.Windows;

namespace NetworksCeW.UnitWorkers
{
    /// <summary>
    /// Combines Unit protocol layers and unit terminal
    /// </summary>
    public class UnitTerminal
    {
        public Unit UnitInst;
        public UnitWorker UnitWorker;

        private TerminalWindow _terminal;
        private readonly MainWindow _mainWindow;

        public UnitTerminal(MainWindow window, Unit unit, List<UnitTerminal> listTerminals)
        {
            _mainWindow = window;
            UnitInst = unit;
            _terminal = new TerminalWindow(UnitInst, UnitWorker);
            UnitWorker = new UnitWorker(this, listTerminals, UnitInst);
        }

        public void OpenTerminal()
        {
            if (_terminal != null && _terminal.IsVisible == true)
                return;

            _terminal = new TerminalWindow(UnitInst, UnitWorker);
            _terminal.Show();
        }

        public void UpdateBuffersNumber(Unit unit)
        {
            _terminal.UpdateUnit(unit);
            _terminal.Update();
        }

        public void UpdateBufferSent(int toUnit, int sentFrames, int sentBytes, int resentBytes)
        {
            _terminal.UpdateItem(new BufferBusy()
            {
                ToUnit = toUnit,
                Frames = sentFrames,
                BytesSent = sentBytes,
                BytesResent = resentBytes
            });
        }

        public void UpdateDestinations(List<byte> dests)
        {
            _terminal.UpdateDestinatioinVariants(dests);
        }

        public void DeleteUnit(Unit unit)
        {
            UnitWorker.WorkerAbort();
            UnitWorker = null;
            _terminal.Close();
        }

        public void StartWorker()
        {
            UnitWorker.WorkerStart();
        }

        public void AbortAll()
        {
            UnitWorker.WorkerAbort();
            UnitWorker = null;
            _terminal.Close();
        }

        public void PutAnimatedMessage(int fromUnit, int toUnit)
        {
            _mainWindow.InsertAnimatedFrame(fromUnit, toUnit);
        }

        public void WriteLog(DateTime time, string log)
        {
            _terminal.WriteLog(time, log);

            _terminal.Update();
        }
    }
}
