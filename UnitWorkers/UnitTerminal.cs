using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworksCeW
{
    /// <summary>
    /// Combines Unit protocol layers and unit terminal
    /// </summary>
    public class UnitTerminal
    {
        public Unit Unit;
        public UnitWorker UnitWorker;

        private TerminalWindow _terminal;

        public UnitTerminal(Unit unit, List<UnitTerminal> listTerminals)
        {
            Unit = unit;
            _terminal = new TerminalWindow(Unit);
            UnitWorker = new UnitWorker(this, listTerminals, Unit);
        }

        public void OpenTerminal()
        {
            if (_terminal != null && _terminal.IsVisible == true)
            {
                return;
            }

            _terminal = new TerminalWindow(Unit);
            _terminal.Show();
        }

        public void UpdateBuffersNumber(Unit unit)
        {
            _terminal.UpdateUnit(unit);
            _terminal.Update();
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
