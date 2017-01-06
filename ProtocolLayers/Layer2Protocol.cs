using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworksCeW.ProtocolLayers
{
    class Layer2Protocol
    {
        private Queue<List<byte>> packetParts;

        public List<byte> Packet;
        //public List<List<byte>> Frames;



        private const int MTU = 296;
        private const byte SNRM = 1;
        private const byte SARM = 24;
        private const byte RD = 3;
        private const byte DISC = 2;

        public Layer2Protocol()
        {
            
        }

        public List<byte> RequestConnection(bool async)
        {
            if (async)
            {
                return EncodeUFrame(SARM);
            }

            return EncodeUFrame(SNRM);
        }

        public void LoadPacket(List<byte> packet)
        {
            Packet = packet;
            DividePacket();
        }

        private void PutFlag(List<byte> frame)
        {
            frame.Add(0x7E);
        }
        
        private void PutControl(List<byte> frame, byte control)
        {
            frame.Add(control);
        }

        private void PutData(List<byte> frame, List<byte> data)
        {
            frame.AddRange(data);
        }

        private void ShiftLeft(ref byte what, int times)
        {
            what = (byte)(what << times);
        }

        private List<byte> EncodeIFrame(List<byte> data, byte numSend, byte numRec)
        {
            List<byte> frame = new List<byte>();
            PutFlag(frame);
            PutControl(frame, EncodeIControlByte(numSend, numRec));
            PutData(frame, packetParts.Dequeue());
            PutFlag(frame);

            return frame;
        }

        private void DecodeIFrame(List<byte> data)
        {

        }

        private List<byte> EncodeSFrame(byte numRec, byte type)
        {
            List<byte> frame = new List<byte>();
            PutFlag(frame);
            PutControl(frame, EncodeSControlByte(numRec, type));
            PutFlag(frame);

            return frame;
        }

        private void DecodeSFrame(List<byte> data)
        {

        }

        private List<byte> EncodeUFrame(byte type)
        {
            List<byte> frame = new List<byte>();
            PutFlag(frame);
            PutControl(frame, EncodeUControlByte(type));
            PutFlag(frame);

            return frame;
        }

        private void DecodeUFrame(List<byte> data)
        {

        }

        private void DividePacket()
        {
            for (int i = 0; i < Packet.Count; i+=MTU)
            {
                packetParts.Enqueue(Packet.GetRange(i, i <= Packet.Count - MTU ? MTU : Packet.Count - i + 1));
            }
        }

        private byte EncodeIControlByte(byte numSend, byte numRec)
        {
            byte b = numRec;
            ShiftLeft(ref b, 4);
            b += numSend;
            ShiftLeft(ref b, 4);
            return b;
        }

        private byte DecodeIControlByte()
        {

            return 1;
        }

        private byte EncodeSControlByte(byte numRec, byte type)
        {
            byte b = numRec;
            ShiftLeft(ref b, 3);
            b += type;
            ShiftLeft(ref b, 2);
            b += 1;
            return b;
        }

        private byte DecodeSControlByte()
        {

            return 1;
        }

        private byte EncodeUControlByte(byte type)
        {
            byte lowerType = (byte)(type & 0x3);
            byte b = (byte)(type & 0x1C);
            ShiftLeft(ref b, 1);
            b += lowerType;
            ShiftLeft(ref b, 2);
            b += 3;
            return b;
        }

        private byte DecodeUControlByte()
        {

            return 1;
        }
    }
}
