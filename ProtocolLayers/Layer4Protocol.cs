using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetworksCeW.UnitWorkers;

namespace NetworksCeW.ProtocolLayers
{
    internal class BaseControlBlock
    {
        private readonly UnitTerminal _myTerminal;

        protected byte FromUnitIndex { get; }
        protected byte ToUnitIndex { get; }
        protected int PortHere { get; }
        protected int PortThere { get; }

        
        protected BaseControlBlock(byte unitFrom, byte unitTo, int portHere, int portThere, UnitTerminal terminal)
        {
            FromUnitIndex = unitFrom;
            ToUnitIndex = unitTo;
            PortHere = portHere;
            PortThere = portThere;
            _myTerminal = terminal;
        }


        public bool IsEqual(byte toUnit, int portHere, int portThere)
        {
            return (ToUnitIndex == toUnit && PortHere == portHere && PortThere == portThere);
        }

        protected void Push(List<byte> data)
        {
            if (data.Count > 10)
            {
                WriteLog("\"" + Encoding.UTF8.GetString(data.Take(10).ToArray()) + "...\"");
            }
            else
            {
                WriteLog("\"" + Encoding.UTF8.GetString(data.ToArray()) + "\"");
            }
        }
        private void WriteLog(string log)
        {
            _myTerminal.WriteLog(DateTime.Now,
                "L4: " + log);
        }
    }

    internal class TransmissionControlBlock : BaseControlBlock
    {
        private enum Status
        {
            Listen,
            SynSend,
            SynReceived,
            Established,
            FinWait,
            CloseWait,
            Closing,
        }

        private class Segment
        {
            public int DataOffset { get; }
            public readonly List<byte> Data;

            public Segment(int offset, List<byte> data)
            {
                DataOffset = offset;
                Data = data;
            }
        }

        private class SentSegment
        {
            public DateTime TimeToResend { get; private set; }
            public Segment Segment { get; private set; }

            public SentSegment(int timeout, Segment segment)
            {
                TimeToResend = DateTime.Now.AddMilliseconds(timeout);
                Segment = segment;
            }
        }


        private const int ResendTimeout = 200000;

        private Status _transmissionStatus;
        public bool IsClosed => _transmissionStatus == Status.Closing;

        private int _nextSendSequence, _nextReceivedSequence;
        
        private List<Segment> _segments;
        private readonly List<SentSegment> _sentSegments;
        
        public TransmissionControlBlock(byte unitHere, byte unitThere, int portHere, int portThere, UnitTerminal terminal)
            : base(unitHere, unitThere, portHere, portThere, terminal)
        {
            _transmissionStatus = Status.Listen;
            _sentSegments = new List<SentSegment>();
        }


        public List<byte> ResendIfTimeout()
        {
            if (_sentSegments.Count == 0) return null;
            if (!(DateTime.Now.Subtract(
                _sentSegments[_sentSegments.Count - 1].TimeToResend
                ).TotalMilliseconds > 0))
                return null;

            var toResend = _sentSegments[_sentSegments.Count - 1].Segment;

            _sentSegments.RemoveAt(_sentSegments.Count - 1);
            _sentSegments.Add(new SentSegment(ResendTimeout, toResend));

            return MakePacketWithPseudoHeader(
                toResend.Data.Count > 20 ? toResend.Data.Skip(20).ToList() : null,
                toResend.DataOffset,
                _nextReceivedSequence,
                ack: _transmissionStatus == Status.SynSend, 
                psh: _segments.Count == 0, 
                syn: _transmissionStatus == Status.SynSend 
                    || _transmissionStatus == Status.SynReceived, 
                fin: _transmissionStatus == Status.FinWait
                || _transmissionStatus == Status.CloseWait
            );

            // Resend
        }



        // To make it Sender

        #region Sender part

        public void PushDataToSend(List<byte> data)
        {
            var sequenceNumber = GetRandomSequenceNumber();
            _nextSendSequence = sequenceNumber - 1;

            var listOfSegments = new List<List<byte>>();

            while (data.Count != 0)
            {
                if (data.Count > TCP.MSL)
                {
                    listOfSegments.Add(data.GetRange(0, TCP.MSL));
                    data.RemoveRange(0, TCP.MSL);
                }
                else
                {
                    listOfSegments.Add(data.GetRange(0, data.Count));
                    data.Clear();
                }
            }

            _segments = new List<Segment>();

            listOfSegments.ForEach(s =>
            {
                _segments.Add(new Segment(sequenceNumber, s));
                sequenceNumber += s.Count;
            });
            
            // Return SYN to init channel
            _transmissionStatus = Status.SynSend;

            // Actually, the segment won't be sent by this thread
            // that is why it is marked with 0 timeout to be resent
            // next time unitWorker thread checks for not confirmed segments
            _sentSegments.Add(new SentSegment(
                0, new Segment(_nextSendSequence, TCP.PackData(
                    null,
                    PortHere, PortThere,
                    _nextSendSequence, 0,
                    ack: false, psh: false, syn: true, fin: false
                    )))
                );

            _nextSendSequence ++;
        }

        private static int GetRandomSequenceNumber()
        {
            var rnd = new Random();
            return rnd.Next(0xF, 0x7FFFFFF);
        }

        
        private List<byte> ReceiveFirstPacket(List<byte> packet)
        {
            var unpacked = new TcpInstance(packet);

            if (!unpacked.Syn) return null;

            // Asks for connection, then return SYN ACK

            // Write all initial numbers
            _nextReceivedSequence = unpacked.SequenceNum + 1;
            _nextSendSequence = new Random().Next();

            // Return empty SYN ACK packet with pseudo
            var returnPacket = MakePacketWithPseudoHeader(
                null,
                _nextSendSequence++,
                _nextReceivedSequence,
                ack: true, psh: false, syn: true, fin: false
            );


            // Change status
            _transmissionStatus = Status.SynReceived;

            return returnPacket;
        }

        public List<byte> ReceiveAnotherPacket(List<byte> packet)
        {
            var unpacked = new TcpInstance(packet);

            if (_nextReceivedSequence != unpacked.SequenceNum)
            {
                switch (_transmissionStatus)
                {

                    // Check if SYN, then ReceiveFirstPacket
                    case Status.Listen:
                        if (!unpacked.Syn) break;
                        return ReceiveFirstPacket(packet);

                    // If _nextReceivedSequence + 1 == unpack.SequenceNum
                    // then change status for Established
                    // Don't return but go to next switch to process packet
                    case Status.SynReceived:

                        if (unpacked.SequenceNum == _nextReceivedSequence - 1)
                        {
                            return MakePacketWithPseudoHeader(
                                null,
                                _nextSendSequence - 1,
                                _nextReceivedSequence,
                                ack: true, psh: false, syn: true, fin: false);
                        }

                        if (unpacked.SequenceNum != _nextReceivedSequence + 1)
                            return null;

                        _transmissionStatus = Status.Established;
                        break;

                    // Ack last received packet
                    // To ask client resend next one
                    case Status.Established:

                        return MakePacketWithPseudoHeader(
                            null,
                            _nextSendSequence,
                            _nextReceivedSequence-1,
                            ack: true, psh: false, syn: false, fin: false
                        );

                    case Status.SynSend:

                        _nextReceivedSequence = unpacked.SequenceNum+1;
                        break;

                    case Status.FinWait:

                        // Resend fin or what????
                        //throw new ArgumentOutOfRangeException();
                        //break;
                        return null;

                    default:
                        //return null;
                        throw new ArgumentOutOfRangeException();
                }
            }


            switch (_transmissionStatus)
            {
                case Status.SynSend:

                    if (unpacked.Ack && unpacked.Syn)
                    {

                        // Return Ack
                        // Change status
                        // Start to send info !!!!!!!!!!!!!!!!!!!!!!!!!!!!
                        // Send first segment with ACK 

                        _transmissionStatus = Status.Established;

                        _sentSegments.Clear();

                        var segmentToSend = _segments[0];
                        _segments.RemoveAt(0);
                        _sentSegments.Add(new SentSegment(ResendTimeout, segmentToSend));
                        var thisIndex = _nextSendSequence;
                        _nextSendSequence += segmentToSend.Data.Count;

                        return MakePacketWithPseudoHeader(
                            segmentToSend.Data,
                            thisIndex, 
                            _nextReceivedSequence, 
                            ack: true, psh: _segments.Count == 0, syn: false, fin: false);
                    }

                    throw new Exception("BAD!!!");

                case Status.SynReceived:

                    if (unpacked.Ack)
                    {

                        // ACK for SYN received:
                        // - change status
                        // - Inc nextReceiveSequence
                        // - return null
                        _transmissionStatus = Status.Established;

                        if (unpacked.Data != null)
                        {

                            // Process data
                            // Check push
                            // Increase nextReceive
                            // Return ACK
                            _segments = new List<Segment> {new Segment(unpacked.SequenceNum, unpacked.Data)};
                            _nextReceivedSequence += unpacked.Data.Count;

                            if (unpacked.Psh)
                            {
                                Push(unpacked.Data);
                            }

                            return MakePacketWithPseudoHeader(
                                null,
                                _nextSendSequence++,
                                _nextReceivedSequence,
                                ack: true, psh: false, syn: false, fin: false
                            );
                        }
                        
                        //_nextReceivedSequence++;
                        return null;
                    }

                    throw new Exception("BAD!!!");

                case Status.Established:

                    // If info - ack
                    // If ack - next info 
                    //    or fin
                    if (unpacked.Ack)
                    {
                        
                        List<byte> returnPacket;
                        
                        // Next info or fin
                        _nextReceivedSequence = unpacked.SequenceNum + 1;

                        if (_segments.Count == 0
                            && unpacked.AcknowledgementNum == _nextSendSequence)
                        {

                            _sentSegments.Clear();

                            // Send FIN
                            returnPacket = MakePacketWithPseudoHeader(
                                null,
                                _nextSendSequence++,
                                _nextReceivedSequence,
                                ack: false, psh: false, syn: false, fin: true
                            );

                            _sentSegments.Add(
                                new SentSegment(ResendTimeout, new Segment(
                                    _nextSendSequence - 1, returnPacket)
                                )
                            );

                            // Change status
                            _transmissionStatus = Status.FinWait;

                            return returnPacket;
                        }

                        if (unpacked.AcknowledgementNum != _nextSendSequence)
                        {
                            // Pull last sent packet and insert in the beginning of _segments
                            _segments.Insert(0, _sentSegments[_sentSegments.Count - 1].Segment);
                            _sentSegments.RemoveAt(_sentSegments.Count - 1);
                        }
                        
                        // Send one of previous
                        while (_segments[0].DataOffset != unpacked.AcknowledgementNum)
                        {
                            if (_sentSegments.Count == 0)
                                throw new Exception("Gone wrong");

                            // Pull last sent packet and insert in the beginning of _segments
                            _segments.Insert(0, _sentSegments[_sentSegments.Count - 1].Segment);
                            _sentSegments.RemoveAt(_sentSegments.Count - 1);
                        }
                        
                        // Remove sent segment
                        _sentSegments.Clear();

                        var nextSend = _nextSendSequence;
                        _nextSendSequence += _segments[0].Data.Count;

                        // Send next
                        // Push to application, if last segment
                        returnPacket = MakePacketWithPseudoHeader(
                            _segments[0].Data,
                            nextSend,
                            _nextReceivedSequence++,
                            ack: false, psh: _segments.Count == 1, syn: false, fin: false
                        );

                        _segments.RemoveAt(0);

                        _sentSegments.Add(
                            new SentSegment(ResendTimeout, new Segment(
                                nextSend, returnPacket)
                            )
                        );

                        return returnPacket;

                    }

                    if (unpacked.Fin)
                    {

                        // Send FIN ACK
                        // Change status
                        _transmissionStatus = Status.CloseWait;
                        _nextReceivedSequence = unpacked.SequenceNum + 1;
                        return MakePacketWithPseudoHeader(
                            null,
                            _nextSendSequence++,
                            _nextReceivedSequence,
                            ack: true, psh: false, syn: false, fin: true
                        );
                    }

                    if (unpacked.Data.Count != 0)
                    {

                        // Send ack
                        // Check push !!!!!!!!!!!
                        _segments.Add(new Segment(unpacked.SequenceNum, unpacked.Data));
                        _nextReceivedSequence += unpacked.Data.Count;

                        if (unpacked.Psh)
                        {
                            Push(unpacked.Data);
                        }

                        return MakePacketWithPseudoHeader(
                            null,
                            _nextSendSequence++,
                            _nextReceivedSequence,
                            ack: true, psh: false, syn: false, fin: false
                        );
                    }

                    throw new Exception("aaaaaaaaaaaaaaaaaa");


                case Status.FinWait:

                    if (unpacked.Ack && unpacked.Fin)
                    {
                        _sentSegments.Clear();

                        // Return Ack
                        // Change status
                        _transmissionStatus = Status.Closing;
                        _nextReceivedSequence = unpacked.SequenceNum + 1;
                        return MakePacketWithPseudoHeader(
                            null,
                            _nextSendSequence,
                            _nextReceivedSequence,
                            ack: true, psh: false, syn: false, fin: false
                        );
                    }

                    throw new Exception("BAD!!!");

                case Status.CloseWait:

                    if (unpacked.Ack)
                    {

                        // ACK for FIN received:
                        // - change status
                        // - return null
                        _transmissionStatus = Status.Closing;
                        return null;
                    }

                    throw new Exception("BAD!!!");

                default:
                    throw new Exception("What is it!!!?????");
            }

        }

        private List<byte> MakePacketWithPseudoHeader(
            List<byte> data,
            int sequenceNumber,
            int acknowledgementNumber,
            bool ack, bool psh, bool syn, bool fin
        )
        {
            var pseudoHeader = Layer4Protocol.PackPseudoHeader(FromUnitIndex, ToUnitIndex, TCP.TcpCode);
            pseudoHeader.AddRange(
                TCP.PackData(data, PortHere, PortThere, sequenceNumber, acknowledgementNumber, ack, psh, syn, fin));
            return pseudoHeader;
        }

        #endregion
    }

    internal class TcpInstance
    {
        public int SequenceNum { get; }
        public int AcknowledgementNum { get; }
        public int Window { get; }
        public int CheckSum { get; }

        public bool Ack { get; }
        public bool Psh { get; }
        public bool Syn { get; }
        public bool Fin { get; }

        public List<byte> Data { get; }

        public TcpInstance(List<byte> packet)
        {
            SequenceNum = TCP.GetSequenceNumber(packet);
            AcknowledgementNum = TCP.GetAcknowledge(packet);
            CheckSum = TCP.GetCheckSum(packet);
            Window = TCP.GetWindow(packet);

            Ack = TCP.GetACK(packet);
            Psh = TCP.GetPSH(packet);
            Syn = TCP.GetSYN(packet);
            Fin = TCP.GetFIN(packet);

            Data = packet.Skip(20).ToList();
        }
    }

    internal class HostControlBlock : BaseControlBlock
    {
        private enum Status
        {
            Opened,
            Sender,
            Receiver,
            Closed
        }
        
        private UnitTerminal _myTerminal;

        private readonly List<List<byte>> _segments;

        private Status _currentStatus;

        public bool ToBeDeleted => _currentStatus == Status.Closed;
        public bool isReceiver => _currentStatus == Status.Receiver;
        public bool isSender => _currentStatus == Status.Sender;

        public HostControlBlock(byte unitHere, byte unitThere, int portHere, int portThere, UnitTerminal terminal)
            : base(unitHere, unitThere, portHere, portThere, terminal)
        {
            _myTerminal = terminal;
            _segments = new List<List<byte>>();
            _currentStatus = Status.Opened;
        }

        // To make it Sender

        #region Sender part

        public void PushDataToSend(List<byte> data)
        {
            _currentStatus = Status.Sender;

            if (data == null)
            {
                _currentStatus = Status.Closed;
                return;
            }

            while (data.Count != 0)
            {
                if (data.Count > UDP.MSL)
                {
                    _segments.Add(data.GetRange(0, UDP.MSL));
                    data.RemoveRange(0, UDP.MSL);
                }
                else
                {
                    _segments.Add(data.GetRange(0, data.Count));
                    data.Clear();
                }
            }
        }

        public List<byte> GetNextPacket()
        {
            if (_segments.Count == 1)
            {
                _currentStatus = Status.Closed;
            }

            var packetData = _segments[0];
            _segments.RemoveAt(0);

            return MakePacketWithPseudoHeader(packetData);
        }

        private List<byte> MakePacketWithPseudoHeader(List<byte> data)
        {
            var pseudoHeader = Layer4Protocol.PackPseudoHeader(FromUnitIndex, ToUnitIndex, UDP.UdpCode);
            pseudoHeader.AddRange(
                UDP.PackData(data, PortHere, PortThere));
            return pseudoHeader;
        }

        #endregion


        #region Receiver part

        public void ReactToPacket(List<byte> packet)
        {
            var udpInst = new UdpInstance(packet);
            if (udpInst.Length == 0)
            {
                return;
            }

            Push(udpInst.Data);
        }

        #endregion
    }

    internal class UdpInstance
    {
        public int Length { get; }
        public int CheckSum { get; }

        public List<byte> Data { get; }

        public UdpInstance(List<byte> packet)
        {
            CheckSum = UDP.GetChecksum(packet);
            packet[6] = 0;
            packet[7] = 0;

            Length = UDP.CountChecksum(packet) == CheckSum
                ? UDP.GetLength(packet)
                : 0;
            
            Data = packet.Skip(8).ToList();
        }
    }

    internal class TCP : ProtocolInitialManipulations
    {
        public enum TcpFrameType
        {
            SYN = 0,
            SYN_ACK = 1,
            ACK = 2,
            FIN = 3,
            FIN_ACK = 4,
        }

        public const int MSL = 1480;
        public const byte SizeOfHeader = 5;
        public const byte TcpCode = 6;


        public TCP()
        {
        }


        #region Get header fields

        public static int GetPortSource(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Take(2).ToList());
        }

        public static int GetPortDestination(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Skip(2).Take(2).ToList());
        }

        public static int GetSequenceNumber(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Skip(4).Take(4).ToList());
        }

        public static int GetAcknowledge(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Skip(8).Take(4).ToList());
        }

        public static int GetCheckSum(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Skip(16).Take(2).ToList());
        }

        public static int GetWindow(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Skip(18).Take(2).ToList());
        }

        public static bool GetACK(List<byte> packet)
        {
            return (ShiftRight(packet[13], 4) & 1) == 1;
        }

        public static bool GetPSH(List<byte> packet)
        {
            return (ShiftRight(packet[13], 3) & 1) == 1;
        }

        public static bool GetSYN(List<byte> packet)
        {
            return (ShiftRight(packet[13], 1) & 1) == 1;
        }

        public static bool GetFIN(List<byte> packet)
        {
            return (packet[13] & 1) == 1;
        }

        #endregion


        #region Put header fields

        private static List<byte> PutPort(int port)
        {
            return Make2BytesFromInt(port);
        }

        private static List<byte> PutSequenceNumber(int seqNum)
        {
            return Make4BytesFromInt(seqNum);
        }

        private static List<byte> PutAcknowledgementNumber(int ackn)
        {
            return Make4BytesFromInt(ackn);
        }

        private static byte PutDataOffset()
        {
            return (byte) ShiftLeft(SizeOfHeader, 4);
        }

        private static byte PutFlags(bool ack, bool psh, bool syn, bool fin)
        {
            return (byte) (
                ShiftLeft(ack ? 1 : 0, 4)
                + ShiftLeft(psh ? 1 : 0, 3)
                + ShiftLeft(syn ? 1 : 0, 1)
                + (fin ? 1 : 0)
            );
        }

        private static List<byte> PutWindow(int sizeOfFrame)
        {
            return Make2BytesFromInt(sizeOfFrame);
        }

        private static List<byte> PutUrgentPointer()
        {
            return new List<byte> {0, 0};
        }

        private static List<byte> PutChecksum(int checksum)
        {
            return Make2BytesFromInt(checksum);
        }

        private static int CountChecksum(List<byte> packet)
        {
            var checkSum = packet.Aggregate(0, (current, b) => current + b);

            checkSum = ShiftLeft(GetFourthByte(checkSum), 8) +
                       GetThirdByte(checkSum) + (checkSum & 0xFFF0000);
            return 0xFFFF - checkSum;
        }

        #endregion

        #region Global needs

        public static List<byte> PackData(
            List<byte> data,
            int portS, int portD,
            int sequenceNumber,
            int acknowledgementNumber,
            bool ack, bool psh, bool syn, bool fin
        )
        {

            // Pack according to fields
            var header = new List<byte>();
            header.AddRange(PutPort(portS));
            header.AddRange(PutPort(portD));

            header.AddRange(PutSequenceNumber(sequenceNumber));
            header.AddRange(PutAcknowledgementNumber(acknowledgementNumber));
            header.Add(PutDataOffset());
            header.Add(PutFlags(ack, psh, syn, fin));
            header.AddRange(PutWindow(data?.Count ?? 0));
            header.AddRange(PutUrgentPointer());

            if (data != null)
            {
                header.AddRange(data);
            }

            header.InsertRange(16, PutChecksum(CountChecksum(header)));
            
            return header;
        }

        #endregion
    }

    internal class UDP : ProtocolInitialManipulations
    {
        public const byte UdpCode = 17;

        public const int MSL = 1492;

        #region Put header fields

        private static List<byte> PutPort(int port)
        {
            return Make2BytesFromInt(port);
        }

        private static List<byte> PutLength(int length)
        {
            return Make2BytesFromInt(length);
        }

        private static List<byte> PutChecksum(int checksum)
        {
            return Make2BytesFromInt(checksum);
        }

        public static int CountChecksum(List<byte> packet)
        {
            var checkSum = packet.Aggregate(0, (current, b) => current + b);

            checkSum = ShiftLeft(GetFourthByte(checkSum), 8) +
                       GetThirdByte(checkSum) + (checkSum & 0xFFF0000);
            return 0xFFFF - checkSum;
        }

        #endregion


        #region Get header fields

        public static int GetPortSource(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Take(2).ToList());
        }

        public static int GetPortDestination(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Skip(2).Take(2).ToList());
        }

        public static int GetLength(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Skip(4).Take(2).ToList());
        }

        public static int GetChecksum(List<byte> packet)
        {
            return MakeIntFromBytes(packet.Skip(6).Take(2).ToList());
        }

        #endregion


        #region Global needs

        public static List<byte> PackData(
            List<byte> data,
            int portS, int portD
        )
        {

            // Pack according to fields
            var header = new List<byte>();
            header.AddRange(PutPort(portS));
            header.AddRange(PutPort(portD));
            
            header.AddRange(PutLength((data?.Count ?? 0) + 8));

            if (data != null)
            {
                header.AddRange(data);
            }

            header.InsertRange(6, PutChecksum(CountChecksum(header)));

            return header;
        }

        #endregion

    }

    internal class Layer4Protocol
    {
        private readonly byte _myUnitIndex;
        private readonly List<TransmissionControlBlock> _allActiveTransmissions;
        private readonly List<HostControlBlock> _allActiveHosts;
        private readonly UnitTerminal _terminal;

        public Layer4Protocol(byte unitId, UnitTerminal terminal)
        {
            _allActiveTransmissions = new List<TransmissionControlBlock>();
            _allActiveHosts = new List<HostControlBlock>();
            _myUnitIndex = unitId;
            _terminal = terminal;
        }

        private void PushDataToSendWithTCP(List<byte> data, byte toUnit)
        {
            int portHere = 0, portThere = 0;

            while (_allActiveTransmissions.FindIndex(
                        block => block.IsEqual(toUnit, portHere, portThere)) > -1)
            {
                portHere++;
                portThere++;
            }

            var tcb = new TransmissionControlBlock(
                _myUnitIndex, toUnit,
                portHere, portThere,
                _terminal
                );

            tcb.PushDataToSend(data);

            _allActiveTransmissions.Add(tcb);
        }

        private void PushDataToSendWithUDP(List<byte> data, byte toUnit)
        {
            var rnd = new Random();
            var portHere = rnd.Next(1, 0xFFF);
            var portThere = rnd.Next(1, 0xFFA);

            var hcb = new HostControlBlock(
                _myUnitIndex, toUnit,
                portHere, portThere,
                _terminal
                );

            hcb.PushDataToSend(data);

            _allActiveHosts.Add(hcb);
        }

        public void PushMessageToSend(byte protocol, List<byte> data, byte toUnit)
        {
            switch (protocol)
            {
                case 6:

                    // It is TCP
                    PushDataToSendWithTCP(data, toUnit);
                    break;
                case 17:

                    // It is UDP
                    PushDataToSendWithUDP(data, toUnit);
                    break;

                default:
                    throw new Exception("Unknown protocol!");
            }
        }

        public List<byte> ProcessNewSegment(List<byte> pseudoPacket)
        {

            byte addrOther = GetPseudoSource(pseudoPacket);
            List<byte> packet;
            int portHere, portOther;


            switch (GetProtocolCode(pseudoPacket))
            {
                case 6:

                    // It is TCP
                    packet = pseudoPacket.Skip(12).ToList();
                    portOther = TCP.GetPortSource(packet);
                    portHere = TCP.GetPortDestination(packet);

                    var tcbIndex = _allActiveTransmissions.FindIndex(
                        block => block.IsEqual(addrOther, portHere, portOther));
                    if (tcbIndex < 0)
                    {
                        // New connection
                        var newTcb = new TransmissionControlBlock(_myUnitIndex, addrOther, portHere, portOther, _terminal);

                        // Push incoming packet without pseudo header
                        var returnPacket = newTcb.ReceiveAnotherPacket(packet);

                        _allActiveTransmissions.Add(newTcb);
                        return returnPacket;

                    }
                    
                    // Connection exists
                    return _allActiveTransmissions[tcbIndex].ReceiveAnotherPacket(packet);

                case 17:

                    // It is UDP
                    packet = pseudoPacket.Skip(12).ToList();
                    portOther = UDP.GetPortSource(packet);
                    portHere = UDP.GetPortDestination(packet);

                    var hcbIndex = _allActiveHosts.FindIndex(
                        host => host.IsEqual(addrOther, portHere, portOther));
                    if (hcbIndex < 0)
                    {
                        // New connection
                        var newHcb = new HostControlBlock(_myUnitIndex, addrOther, portHere, portOther, _terminal);

                        // Push incoming packet without pseudo header
                        newHcb.ReactToPacket(packet);

                        _allActiveHosts.Add(newHcb);
                        return null;
                    }

                    // Connection exists
                    _allActiveHosts[hcbIndex].ReactToPacket(packet);
                    return null;

                default:
                    throw new Exception("Unknown protocol!");
            }
        }

        public List<byte> ResendPacketAfterTimeout()
        {
            return _allActiveTransmissions.Select(
                tcb => tcb.ResendIfTimeout()
                ).FirstOrDefault(packet => packet != null);
        }

        public List<byte> GetNewUdpPacketToSend()
        {

            return (from hcb in _allActiveHosts where hcb.isSender select hcb.GetNextPacket()).FirstOrDefault();
        }

        public void RemoveClosedConnections()
        {
            _allActiveTransmissions.RemoveAll(tcb => tcb.IsClosed);
            _allActiveHosts.RemoveAll(hcb => hcb.ToBeDeleted);
        }

        #region Manage pseudo header

        public static List<byte> PackPseudoHeader(byte source, byte destination, byte protocolCode)
        {
            return new List<byte>()
            {
                source, 0, 0, 0,
                destination, 0, 0, 0,
                0, protocolCode, 0, 20,
            };
        }

        public static byte GetPseudoSource(List<byte> pseudoHeader)
        {
            return pseudoHeader[0];
        }

        public static byte GetPseudoDestination(List<byte> pseudoHeader)
        {
            return pseudoHeader[4];
        }
        
        public static byte GetProtocolCode(List<byte> pseudoHeader)
        {
            return pseudoHeader[9];
        }

        #endregion

    }
}
