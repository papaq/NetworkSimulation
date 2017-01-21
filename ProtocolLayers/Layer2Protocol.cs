using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworksCeW.ProtocolLayers
{
    class Layer2ProtocolFrameInstance
    {
        public FrameType Type { get; set; }
        public byte FrameNum { get; set; }
        public List<byte> Data { get; set; }
    }

    enum FrameType
    {
        start_init = 0, 
        finish_init = 4,
        ack = 2,
        nack = 3,
        marker_pass = 1,
        null_counter = 5,
        information = 7,
        info_and_null = 6
    }

    class Layer2Protocol
    {
        private const byte FLAG = 0x7E;
        public const byte HEADER_LENGTH = 5;

        public Layer2Protocol() { }

        private byte PutFlagByte()
        {
            return FLAG;
        }

        private byte PutControlByte(FrameType type, byte frameNum)
        {
            byte cByte = (byte)ShiftLeft((int)type, 5);
            
            return (byte)(cByte + frameNum);
        }
        
        private List<byte> PutFCS(List<byte> frame)
        {
            int chksum = 0;
            for (int i = 1; i < frame.Count-3; i++)
            {
                chksum += frame[i];
            }
            chksum = 0xFFFF - (chksum & 0xFFFF);

            return new List<byte>()
            {
                (byte)ShiftRight(chksum, 8),
                (byte)chksum
            };
        }

        private int ShiftLeft(int what, int times)
        {
            return (what << times);
        }

        private int ShiftRight(int what, int times)
        {
            return (what >> times);
        }

        private FrameType GetFrameType(byte controlByte)
        {
            return (FrameType)ShiftRight(controlByte, 5);
        }

        private byte GetFrameNum(byte controlByte)
        {
            return (byte)(controlByte & 31);
        }


        #region Global need funcs

        public List<byte> PackData(List<byte> data, FrameType type, byte frameNum)
        {
            var informationFrame = new List<byte>() { PutFlagByte() };
            informationFrame.Add(PutControlByte(type, frameNum));
            informationFrame.AddRange(data);
            informationFrame.Add(0);
            informationFrame.Add(0);
            informationFrame.Add(PutFlagByte());

            var fcs = PutFCS(informationFrame);
            informationFrame[informationFrame.Count - 3] = fcs[0];
            informationFrame[informationFrame.Count - 2] = fcs[1];

            return informationFrame;
        }

        public List<byte> PackControl(FrameType type, byte frameNum)
        {
            var controlFrame = new List<byte>() { PutFlagByte() };
            controlFrame.Add(PutControlByte(type, frameNum));
            controlFrame.Add(0);
            controlFrame.Add(0);
            controlFrame.Add(PutFlagByte());

            var fcs = PutFCS(controlFrame);
            controlFrame[2] = fcs[0];
            controlFrame[3] = fcs[1];

            return controlFrame;
        }


        public Layer2ProtocolFrameInstance UnpackFrame(List<byte> data)
        {
            if (data == null)
                return null;

            var frameChcksum = data.Skip(data.Count - 3).Take(2).ToList();
            var newChcksum = PutFCS(data);
            if (frameChcksum[0] != newChcksum[0] || frameChcksum[1] != newChcksum[1])
                return null;

            var frameInst = new Layer2ProtocolFrameInstance();
            frameInst.Data = data.Skip(2).Take(data.Count - 5).ToList();
            var ctrlByte = data[1];
            frameInst.Type = GetFrameType(ctrlByte);
            frameInst.FrameNum = GetFrameNum(ctrlByte);

            return frameInst;
        }
        
        public byte GetIndexFast(List<byte> frame)
        {
            return GetFrameNum(frame[1]);
        }

        public FrameType GetTypeFast(List<byte> frame)
        {
            return GetFrameType(frame[1]);
        }

        #endregion
    }
}
