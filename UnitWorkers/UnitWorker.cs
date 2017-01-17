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
        private Layer3Protocol _layer3Protocol;

        public UnitWorker(UnitTerminal terminal, List<UnitTerminal> listTerminals, Unit unit)
        {
            _myTerminal = terminal;
            _listOfTerminals = listTerminals;
            _unit = unit;
            ListBufferWorkers = new List<BufferWorker>();
            _layer3Protocol = new Layer3Protocol(unit.Index);
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
                    WriteLog("Sent: " + sendList.ToString());
                    buffer.PushNewLayer3Datagram(sendList);
                }

                foreach (var buffer in ListBufferWorkers)
                {
                    var recList = buffer.PullNewLayer3Datagram();
                    if (recList != null)
                    {
                        WriteLog("Received: " + recList.ToString());
                    }
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

            var datagram = _layer3Protocol.PackData(
                _layer3Protocol.MakeStatusData(myConnections),
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
