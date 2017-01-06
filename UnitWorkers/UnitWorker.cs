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
            {
                return;
            }

            for (int i = 0; i < _unit.ListBindsIndexes.Count; i++)
            {
                ListBufferWorkers.Add(new BufferWorker(_unit.ListBindsIndexes[i], _myTerminal));
            }
            Thread.Sleep(10000);
            foreach (var buff in ListBufferWorkers)
            {
                buff.InitEndPointQueue(
                    _listOfTerminals.Find(
                        term => term.UnitInst.ListBindsIndexes.Contains(buff.Connection.Index))
                        .UnitWorker.ListBufferWorkers.Find(
                        buffWorker => buffWorker.Connection.Index == buff.Connection.Index).In);
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

        
        
    }
}
