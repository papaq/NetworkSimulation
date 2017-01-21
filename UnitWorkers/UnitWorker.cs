using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetworksCeW.ProtocolLayers;
using NetworksCeW.Structures;

namespace NetworksCeW.UnitWorkers
{
    /// <summary>
    /// Comprises all protocol layers, enables units communication
    /// </summary>
    public class UnitWorker
    {
        public Queue<List<byte>> ListSendThis;
        public List<BufferWorker> ListBufferWorkers;

        private Thread _unitWorker;
        private Unit _unit;

        private UnitTerminal _myTerminal;
        private List<UnitTerminal> _listOfTerminals;

        private byte _congestion = 1;

        // Protocols' instances
        private Layer3Protocol _layer3p;

        public UnitWorker(UnitTerminal terminal, List<UnitTerminal> listTerminals, Unit unit)
        {
            _myTerminal = terminal;
            _listOfTerminals = listTerminals;
            _unit = unit;
            ListBufferWorkers = new List<BufferWorker>();
            _layer3p = new Layer3Protocol(unit.Index);
        }

        public void WorkerStart()
        {
            _unitWorker = new Thread(Worker);
            _unitWorker.Start();
        }

        public void WorkerAbort()
        {
            AbortAllBufferWorkers();
            _unitWorker.Abort();
        }

        private void InitBufferWorkers()
        {
            if (_unit.ListBindsIndexes.Count == 0)
                return;

            for (int i = 0; i < _unit.ListBindsIndexes.Count; i++)
                ListBufferWorkers.Add(new BufferWorker(_unit.ListBindsIndexes[i], _myTerminal, _unit.Buffer));

            Thread.Sleep(2000);

            foreach (var buff in ListBufferWorkers)
            {
                int otherUnit = buff.Connection.GetSecondUnitIndex(_unit.Index);
                buff.InitEndPointWorker(
                    _listOfTerminals.Find(
                        term => term.UnitInst.Index == otherUnit).UnitWorker.ListBufferWorkers.Find(
                        b => b.Connection.Index == buff.Connection.Index));
            }
        }

        private void StartAllBufferWorkers()
        {
            foreach (var buff in ListBufferWorkers)
            {
                buff.WorkerStart();
            }
        }

        private void AbortAllBufferWorkers()
        {
            foreach (var buff in ListBufferWorkers)
            {
                buff.WorkerAbort();
            }
        }

        private void Worker()
        {
            InitBufferWorkers();
            StartAllBufferWorkers();

            //while (true)
            //{
            foreach (var buff in ListBufferWorkers)
                _myTerminal.UpdateBufferState(buff.CountBufferBusy());


            var sendList = new List<byte>() { 1, 2, 3 };
            foreach (var buffer in ListBufferWorkers)
            {
                var dtgr = "";
                sendList.ForEach((byte a) => { dtgr += a.ToString(); });
                WriteLog("Sent: " + dtgr);
                buffer.PushNewLayer3Datagram(sendList);
            }


            while (true)
            { 
                foreach (var buffer in ListBufferWorkers)
                {
                    var recList = buffer.PullNewLayer3Datagram();
                    if (recList != null)
                    {
                        var dtgr = "";
                        recList.ForEach((byte a) => { dtgr += a.ToString(); });
                        WriteLog("Received: " + dtgr);
                    }
                }

                Thread.Sleep(100);

        }


                //Thread.Sleep(1000);
            //}
        }

        /// <summary>
        /// Status must be shared to all unitWorkers each 30 seconds
        /// </summary>
        private void ShareMyStatus()
        {
            var myConnections = new List<ToUnitConnection>();
            foreach (var bind in _unit.ListBindsIndexes)
            {
                var actualBind = Windows.MainWindow.ListOfBinds.Find(b => b.Index == bind);
                myConnections.Add(new ToUnitConnection()
                {
                    ToUnit = actualBind.GetSecondUnitIndex(_unit.Index),
                    BandWidth = actualBind.Weight
                });
            }

            var datagram = _layer3p.PackData(
                _layer3p.MakeStatusData(myConnections),
                _congestion,
                0,
                2,
                0,
                100,
                Layer3Protocol.RTP,
                _unit.Index,
                Layer3Protocol.BRDCST );

            // Put datagram into buffer out list
        }

        private void ReactToFrame(List<byte> datagram)
        {
            var newFrame = _layer3p.UnpackFrame(datagram);

            if (newFrame == null) return;




        }

        /// <summary>
        /// Write log into
        /// unit's terminal
        /// </summary>
        /// <param name="log"></param>
        private void WriteLog(string log)
        {
            _myTerminal.WriteLog(DateTime.Now,
                "Unit: " + log);
        }

    }
}
