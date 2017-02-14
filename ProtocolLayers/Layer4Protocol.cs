using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using NetworksCeW.UnitWorkers;
using NetworksCeW.Windows;

namespace NetworksCeW.ProtocolLayers
{
    internal class BaseTCB
    {
        protected byte FromUnitIndex { get; }
        protected byte ToUnitIndex { get; }
        protected int PortHere { get; }
        protected int PortThere { get; }

        
        protected BaseTCB(byte unitFrom, byte unitTo, int portHere, int portThere)
        {
            FromUnitIndex = unitFrom;
            ToUnitIndex = unitTo;
            PortHere = portHere;
            PortThere = portThere;
        }


        public bool IsEqual(byte toUnit, int portHere, int portThere)
        {
            return (ToUnitIndex == toUnit && PortHere == portHere && PortThere == portThere);
        }
    }

    internal class TransmissionControlBlock : BaseTCB
    {
        public enum Status
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
        
        public Status TransmissionStatus { get; private set; }
        public bool? Sender;
        //private TCP _tcp;

        private int _nextSendSequence, _nextReceivedSequence;


        public bool MustBeDeleted { get; }

        public int NextToBeTransmited
        {
            get
            {
                var part = _segments[_segments.Count - 1];
                return part.DataOffset + part.Data.Count;
            }
        }

        private List<Segment> _segments;
        private readonly List<SentSegment> _sentSegments;

        private readonly UnitTerminal _myTerminal;

        public TransmissionControlBlock(byte unitHere, byte unitThere, int portHere, int portThere, UnitTerminal terminal)
            : base(unitHere, unitThere, portHere, portThere)
        {
            _myTerminal = terminal;
            TransmissionStatus = Status.Listen;
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
                ack: TransmissionStatus == Status.SynSend, 
                psh: _segments.Count == 0, 
                syn: TransmissionStatus == Status.SynSend 
                    || TransmissionStatus == Status.SynReceived, 
                fin: TransmissionStatus == Status.FinWait
                || TransmissionStatus == Status.CloseWait
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

            Sender = true; /////////////////////////////////////////////////////////////////////////////////

            // Return SYN to init channel
            TransmissionStatus = Status.SynSend;

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
            /////////////////////////////////////////_segments = new List<Segment> {new Segment(unpacked.SequenceNum, null)};
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
            TransmissionStatus = Status.SynReceived;

            return returnPacket;
        }

        public List<byte> ReceiveAnotherPacket(List<byte> packet)
        {
            var unpacked = new TcpInstance(packet);

            if (_nextReceivedSequence != unpacked.SequenceNum)
            {
                switch (TransmissionStatus)
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

                        TransmissionStatus = Status.Established;
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


            switch (TransmissionStatus)
            {
                case Status.SynSend:

                    if (unpacked.Ack && unpacked.Syn)
                    {

                        // Return Ack
                        // Change status
                        // Start to send info !!!!!!!!!!!!!!!!!!!!!!!!!!!!
                        // Send first segment with ACK 

                        TransmissionStatus = Status.Established;

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
                        TransmissionStatus = Status.Established;

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
                                Push();
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
                            TransmissionStatus = Status.FinWait;

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
                        TransmissionStatus = Status.CloseWait;
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
                            Push();
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
                        TransmissionStatus = Status.Closing;
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
                        TransmissionStatus = Status.Closing;
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

        private void Push()
        {
            WriteLog("Pushed");
        }

        #endregion

        private void WriteLog(string log)
        {
            _myTerminal.WriteLog(DateTime.Now,
                "L4: " + log);
        }
    }

    internal class TcpInstance
    {
        /*
        public byte AddrSource { get; }
        public byte AddrDest { get; }
        

        public int PortSource { get; }
        public int PortDest { get; }
        */

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
            /*
            AddrSource = TCP.GetPseudoSource(Pseudopacket);
            AddrDest = TCP.GetPseudoDestination(Pseudopacket);

            var packet = Pseudopacket.Skip(12).ToList();
            

            PortSource = TCP.GetPortSource(packet);
            PortDest = TCP.GetPseudoDestination(packet);
            */

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

    internal class UDP
    {
        public const byte UdpCode = 17;
    }

    internal class Layer4Protocol
    {
        private readonly byte _myUnitIndex;
        private List<TransmissionControlBlock> _allActiveTransmissions;
        private readonly UnitTerminal _terminal;

        public Layer4Protocol(byte unitId, UnitTerminal terminal)
        {
            _allActiveTransmissions = new List<TransmissionControlBlock>();
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

                    break;

                default:
                    throw new Exception("Unknown protocol!");
            }
        }

        public List<byte> ProcessNewSegment(List<byte> pseudoPacket)
        {
            switch (GetProtocolCode(pseudoPacket))
            {
                case 6:

                    // It is TCP
                    var addrOther = GetPseudoSource(pseudoPacket);
                    var packet = pseudoPacket.Skip(12).ToList();
                    var portOther = TCP.GetPortSource(packet);
                    var portHere = TCP.GetPortDestination(packet);

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
