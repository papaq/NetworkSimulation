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

        public UnitTerminal(Unit unit, List<UnitTerminal> listTerminals)
        {
            UnitInst = unit;
            _terminal = new TerminalWindow(UnitInst);
            UnitWorker = new UnitWorker(this, listTerminals, UnitInst);
        }

        public void OpenTerminal()
        {
            if (_terminal != null && _terminal.IsVisible == true)
                return;

            _terminal = new TerminalWindow(UnitInst);
            _terminal.Show();
        }

        public void UpdateBuffersNumber(Unit unit)
        {
            _terminal.UpdateUnit(unit);
            _terminal.Update();
        }

        public void UpdateBufferState(byte busy)
        {
            _terminal.UpdateItem(new BufferBusy()
            {
                ToUnit = UnitInst.Index,
                Utilization = busy.ToString() + "%"
            });
        }

        public void DeleteUnit(Unit unit)
        {

        }

        public void StartWorker()
        {
            UnitWorker.WorkerStart();
        }

        public void WriteLog(DateTime time, string log)
        {
            _terminal.WriteLog(time, log);

            _terminal.Update();
        }
    }
}
