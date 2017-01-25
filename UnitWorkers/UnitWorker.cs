using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Annotations;
using NetworksCeW.ProtocolLayers;
using NetworksCeW.Structures;

namespace NetworksCeW.UnitWorkers
{
    /// <summary>
    /// Comprises all protocol layers, enables units communication
    /// </summary>
    public class UnitWorker
    {
        internal class EarlierProcessedDatagram
        {
            public readonly DateTime Time;
            private readonly int _hashCode;

            public EarlierProcessedDatagram(int id, byte s, byte d)
            {
                _hashCode = GetHashCode(id, s, d);
                Time = new DateTime();
                Time = DateTime.Now;
            }

            private static int GetHashCode(int id, byte s, byte d)
            {
                return ((id << 16) + (s << 8) + d);
            }

            public bool EqualHashCode(int id, byte s, byte d)
            {
                return GetHashCode(id, s, d) == _hashCode;
            }
        }

        private Dictionary<int, Dictionary<int, List<Layer3ProtocolDatagramInstance>>> _receivedPartsOfDatagrams;
        private List<EarlierProcessedDatagram> _listOfRecAndSentDatagrams;

        public Queue<List<byte>> ListSendThis; /////////////////////////////
        public List<BufferWorker> ListBufferWorkers;

        private Thread _unitWorker;
        private readonly Unit _unit;

        private readonly UnitTerminal _myTerminal;
        private readonly List<UnitTerminal> _listOfTerminals;

        private byte _congestion = 1;    //////////////////////////////

        // Protocols' instances
        private readonly Layer3Protocol _layer3p;

        public UnitWorker(UnitTerminal terminal, List<UnitTerminal> listTerminals, Unit unit)
        {
            _myTerminal = terminal;
            _listOfTerminals = listTerminals;
            _unit = unit;
            ListBufferWorkers = new List<BufferWorker>();
            _layer3p = new Layer3Protocol(unit.Index);
            _receivedPartsOfDatagrams = new Dictionary<int, Dictionary<int, List<Layer3ProtocolDatagramInstance>>>();
            _listOfRecAndSentDatagrams = new List<EarlierProcessedDatagram>();
        }

        public void WorkerStart()
        {
            _unitWorker = new Thread(Worker);
            _unitWorker.Start();
            _unitWorker.IsBackground = true;
        }

        public void WorkerAbort()
        {
            AbortAllBufferWorkers();
            _unitWorker.Abort();
            ListBufferWorkers.Clear();
        }

        private void InitBufferWorkers()
        {
            if (_unit.ListBindsIndexes.Count == 0)
                return;

            foreach (var bind in _unit.ListBindsIndexes)
            {
                ListBufferWorkers.Add(new BufferWorker(bind, _myTerminal, _unit.Buffer));
            }


            Thread.Sleep(2000);

            foreach (var buff in ListBufferWorkers)
            {
                int otherUnit = buff.EndUnitIndex;
                buff.InitEndPointWorker(
                    _listOfTerminals.Find(
                        term => term.UnitInst.Index == otherUnit).UnitWorker.ListBufferWorkers.Find(
                        b => b.EndUnitIndex == _unit.Index));
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

            var lastUpdated = new DateTime();
            lastUpdated = DateTime.Now;
            var secondsPassed = 30;


            while (true)
            {

                // Update buffer busy every second
                // Remove all outdated frames
                if (DateTime.Now.Subtract(lastUpdated).TotalSeconds > 1)
                {
                    foreach (var buff in ListBufferWorkers)
                        _myTerminal.UpdateBufferState(buff.CountBufferBusy());

                    RemoveOldDatagrams();

                    secondsPassed++;
                    lastUpdated = DateTime.Now;
                }

                // Share status every 30 seconds
                //if (secondsPassed > 29)
                if (true)
                {
                    // Send own status
                    var statusDatagram = MyStatusToDatagram();

                    foreach (var buffer in ListBufferWorkers)
                    {
                        buffer.PushDatagramToProcessOnLayer2(statusDatagram);
                    }

                    // Push own status to topology
                    _layer3p.UpdateUnitInformation(
                    _unit.Index,
                    MakeListOfToUnitConnections()
                    );

                    _layer3p.UpdateNetworkTopology();
                    _myTerminal.UpdateDestinations(_layer3p.GetDestinations());

                    secondsPassed = 0;
                }

                // Process to push upper or resend one new datagram from each buffer
                foreach (var buffer in ListBufferWorkers)
                {
                    ReactToFrame(buffer.PullDatagramToProcessOnLayer3());
                }






                // Send datagrams received from terminal or else






                Thread.Sleep(200);
            }
        }

        /// <summary>
        /// Status must be shared to all unitWorkers each 30 seconds
        /// </summary>
        private List<byte> MyStatusToDatagram()
        {
            var newId = _layer3p.GetNextId();
            AddDatagramToSeen(newId, (byte) _unit.Index, Layer3Protocol.Brdcst);

            return _layer3p.PackData(
                _layer3p.MakeStatusData(MakeListOfToUnitConnections()),
                _congestion,
                newId,
                2,
                0,
                100,
                Layer3Protocol.Rtp,
                _unit.Index,
                Layer3Protocol.Brdcst );
        }

        /// <summary>
        /// Put own connections' status into list of ToUnitConnections
        /// </summary>
        /// <returns></returns>
        private List<ToUnitConnection> MakeListOfToUnitConnections()
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

            return myConnections;
        }

        /// <summary>
        /// Make something with new datagram
        /// </summary>
        /// <param name="datagram"></param>
        private void ReactToFrame(List<byte> datagram)
        {
            //WriteLog("React to frame");



            var newFrame = _layer3p.UnpackFrame(datagram);

            if (newFrame == null) return;
            if (DatagramWasReceivedBefore(newFrame)) return;

            // Add frame to seen before
            AddDatagramToSeen(newFrame.Identification, newFrame.Saddr, newFrame.Daddr);

            switch (newFrame.Protocol)
            {
                case Layer3Protocol.Rtp:
                    WriteLog("frame RTP");
                    
                    _layer3p.UpdateUnitInformation(
                        newFrame.Saddr, 
                        _layer3p.GetConnectionsFromBytes(newFrame.Data)
                        );

                    var datagramToSend = _layer3p.PackData(
                                newFrame.Data,
                                _congestion,
                                newFrame.Identification,
                                newFrame.Flags,
                                newFrame.FragmentOffset,
                                newFrame.TTL,
                                newFrame.Protocol,
                                newFrame.Saddr,
                                newFrame.Daddr
                                );

                    foreach (var buffer in ListBufferWorkers)
                    {
                        buffer.PushDatagramToProcessOnLayer2(datagramToSend);

                    }

                    break;
                case Layer3Protocol.Tcp:

                    break;
                case Layer3Protocol.Udp:

                    break;
                default:
                    throw new Exception("BLAAAA WTFFFF");
            }
        }

        private void RemoveOldDatagrams()
        {
            _listOfRecAndSentDatagrams.RemoveAll(
                dtgr => DateTime.Now.Subtract(dtgr.Time).TotalSeconds > 20);
        }

        private void AddDatagramToSeen(int id, byte source, byte dest)
        {
            _listOfRecAndSentDatagrams.Add(new EarlierProcessedDatagram(id, source, dest));
        }

        private bool DatagramWasReceivedBefore(Layer3ProtocolDatagramInstance inst)
        {
            foreach (var datagram in _listOfRecAndSentDatagrams)
            {
                if (datagram.EqualHashCode(inst.Identification, inst.Saddr, inst.Daddr)) return true;
            }
            return false;
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
