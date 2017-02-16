using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly List<EarlierProcessedDatagram> _listOfRecAndSentDatagrams;

        public Queue<List<byte>> ListSendThis; /////////////////////////////
        public List<BufferWorker> ListBufferWorkers;

        private Thread _unitWorker;
        private readonly Unit _unit;

        private readonly UnitTerminal _myTerminal;
        private readonly List<UnitTerminal> _listOfTerminals;
        
        private byte _congestion = 1; 

        // Protocols' instances
        private readonly Layer3Protocol _layer3P;
        private readonly Layer4Protocol _layer4P;

        public UnitWorker(UnitTerminal terminal, List<UnitTerminal> listTerminals, Unit unit)
        {
            _myTerminal = terminal;
            _listOfTerminals = listTerminals;
            _unit = unit;
            ListBufferWorkers = new List<BufferWorker>();
            _layer3P = new Layer3Protocol((byte)unit.Index);
            _layer4P = new Layer4Protocol((byte)unit.Index, terminal);
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
            _unitWorker?.Abort();
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

        public void SendMessage(byte protocol, List<byte> data, byte toUnit)
        {
            if (_unitWorker.IsAlive)
            {
                _layer4P.PushMessageToSend(protocol, data, toUnit);
            }
        }

        private void Worker()
        {
            InitBufferWorkers();
            StartAllBufferWorkers();

            var lastUpdated = DateTime.Now;
            var secondsPassed = 30;


            while (true)
            {

                // Update buffer busy every second
                // Remove all outdated frames
                if (DateTime.Now.Subtract(lastUpdated).TotalSeconds > 1)
                {
                    foreach (var buff in ListBufferWorkers)
                        _myTerminal.UpdateBufferState(buff.EndUnitIndex, buff.CountBufferBusy());

                    RemoveOldDatagrams();

                    secondsPassed++;
                    lastUpdated = DateTime.Now;
                }

                // Share status every 30 seconds
                if (secondsPassed > 29)
                {

                    // Make own status
                    var statusDatagram = MyStatusToDatagram();

                    // Send status to each connected unit
                    foreach (var buffer in ListBufferWorkers)
                    {
                        buffer.PushDatagramToProcessOnLayer2(statusDatagram);
                    }

                    // Push own status to topology
                    _layer3P.UpdateUnitInformation(
                        (byte)_unit.Index,
                        MakeListOfToUnitConnections()
                    );

                    _layer3P.UpdateNetworkTopology();
                    _myTerminal.UpdateDestinations(_layer3P.GetDestinations());

                    secondsPassed = 0;

                    // Remove all closed channels
                    _layer4P.RemoveClosedConnections();


                }

                // Process to push upper or resend one new datagram from each buffer
                foreach (var buffer in ListBufferWorkers)
                {
                    ReactToFrame(buffer.PullDatagramToProcessOnLayer3());
                }


                // Resend packet after timeout
                var resendDatagram = _layer4P.ResendPacketAfterTimeout();
                if (resendDatagram != null)
                {
                    var buffer = GetBufferWorker(Layer4Protocol.GetPseudoDestination(resendDatagram));
                    buffer.PushDatagramToProcessOnLayer2(
                            _layer3P.PackData(
                                resendDatagram.Skip(12).ToList(),
                                _congestion,
                                _layer3P.GetNextId(),
                                2,
                                0,
                                100,
                                Layer4Protocol.GetProtocolCode(resendDatagram),
                                _unit.Index,
                                Layer4Protocol.GetPseudoDestination(resendDatagram)
                            ));

                    WriteLog("Resent packet");
                }

                // Send next UDP datagram
                var nextUdp = _layer4P.GetNewUdpPacketToSend();
                if (nextUdp != null)
                {
                    var buffer = GetBufferWorker(Layer4Protocol.GetPseudoDestination(nextUdp));
                    buffer.PushDatagramToProcessOnLayer2(
                            _layer3P.PackData(
                                nextUdp.Skip(12).ToList(),
                                _congestion,
                                _layer3P.GetNextId(),
                                2,
                                0,
                                100,
                                Layer4Protocol.GetProtocolCode(nextUdp),
                                _unit.Index,
                                Layer4Protocol.GetPseudoDestination(nextUdp)
                            ));

                    WriteLog("UDP sent");
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
            var newId = _layer3P.GetNextId();
            AddDatagramToSeen(newId, (byte) _unit.Index, Layer3Protocol.Brdcst);

            return _layer3P.PackData(
                _layer3P.MakeStatusData(MakeListOfToUnitConnections()),
                _congestion,
                newId,
                2,
                0,
                100,
                Layer3Protocol.Rtp,
                _unit.Index,
                Layer3Protocol.Brdcst );
        }

        private BufferWorker GetBufferWorker(byte toUnit)
        {
            var nextUnit = _layer3P.GetNextRoutingTo(toUnit);
            return ListBufferWorkers.Find(buffer => buffer.EndUnitIndex == nextUnit);
        }

        /// <summary>
        /// Put own connections' status into list of ToUnitConnections
        /// </summary>
        /// <returns></returns>
        private List<ToUnitConnection> MakeListOfToUnitConnections()
        {
            return (from bind in _unit.ListBindsIndexes
                select Windows.MainWindow.ListOfBinds.Find(b => b.Index == bind)
                into actualBind
                where !actualBind.Disabled
                select new ToUnitConnection()
                {
                    ToUnit = actualBind.GetSecondUnitIndex(_unit.Index), BandWidth = actualBind.Weight
                }).ToList();
        }

        /// <summary>
        /// Make something with new datagram
        /// </summary>
        /// <param name="datagram"></param>
        private void ReactToFrame(List<byte> datagram)
        {
            //WriteLog("React to frame");



            var receivedFrame = _layer3P.UnpackFrame(datagram);

            if (receivedFrame == null) return;
            if (DatagramWasReceivedBefore(receivedFrame)) return;

            // Add frame to seen before
            AddDatagramToSeen(receivedFrame.Identification, receivedFrame.Saddr, receivedFrame.Daddr);

            switch (receivedFrame.Protocol)
            {
                case Layer3Protocol.Rtp:
                    WriteLog("Received RTP");
                    
                    _layer3P.UpdateUnitInformation(
                        receivedFrame.Saddr, 
                        _layer3P.GetConnectionsFromBytes(receivedFrame.Data)
                        );

                    var datagramToSend = _layer3P.PackData(
                                receivedFrame.Data,
                                _congestion,
                                receivedFrame.Identification,
                                receivedFrame.Flags,
                                receivedFrame.FragmentOffset,
                                receivedFrame.Ttl,
                                receivedFrame.Protocol,
                                receivedFrame.Saddr,
                                receivedFrame.Daddr
                                );

                    foreach (var buffer in ListBufferWorkers)
                    {
                        buffer.PushDatagramToProcessOnLayer2(datagramToSend);
                    }

                    break;
                case Layer3Protocol.Tcp:
                case Layer3Protocol.Udp:

                    WriteLog("Received " + (receivedFrame.Protocol == 6 ? "TCP" : "UDP"));

                    // Send further
                    if (receivedFrame.Daddr != _unit.Index)
                    {
                        GetBufferWorker(_layer3P.GetNextRoutingTo(receivedFrame.Daddr))
                            .PushDatagramToProcessOnLayer2(
                                _layer3P.PackData(
                                    receivedFrame.Data,
                                    _congestion,
                                    receivedFrame.Identification,
                                    receivedFrame.Flags,
                                    receivedFrame.FragmentOffset,
                                    receivedFrame.Ttl,
                                    receivedFrame.Protocol,
                                    receivedFrame.Saddr,
                                    receivedFrame.Daddr
                                    )
                             );

                        WriteLog("Sent further");

                        break;
                    }


                    // Process here
                    var pseudoPacket = Layer4Protocol.PackPseudoHeader(
                        receivedFrame.Saddr, receivedFrame.Daddr, receivedFrame.Protocol);

                    pseudoPacket.AddRange(receivedFrame.Data);

                    var answerDatagram = _layer4P.ProcessNewSegment(
                        pseudoPacket
                        );

                    // if answer is not null then send
                    if (answerDatagram == null)
                        break;

                    var destination = Layer4Protocol.GetPseudoDestination(answerDatagram);

                    GetBufferWorker(_layer3P.GetNextRoutingTo(destination))
                        .PushDatagramToProcessOnLayer2(
                            _layer3P.PackData(
                                answerDatagram.Skip(12).ToList(),
                                _congestion,
                                _layer3P.GetNextId(),
                                2,
                                0,
                                100,
                                Layer4Protocol.GetProtocolCode(answerDatagram),
                                _unit.Index,
                                destination
                            ));

                    WriteLog("Sent answer");
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
            return _listOfRecAndSentDatagrams.Any(
                datagram => datagram.EqualHashCode(inst.Identification, inst.Saddr, inst.Daddr));
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
