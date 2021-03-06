﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworksCeW.ProtocolLayers
{
    /// <summary>
    /// Stracture used to describe all neccessary
    /// frame header fields
    /// </summary>
    internal class Layer2ProtocolFrameInstance
    {
        public FrameType Type { get; set; }
        public byte FrameNum { get; set; }
        public List<byte> Data { get; set; }
    }

    /// <summary>
    /// Type used to describe the type of frame
    /// </summary>
    internal enum FrameType
    {
        StartInit = 0, 
        FinishInit = 4,
        Ack = 2,
        Nack = 3,
        MarkerPass = 1,
        NullCounter = 5,
        Information = 7,
        InfoAndNull = 6
    }

    internal class Layer2Protocol : ProtocolInitialManipulations
    {
        private const byte FLAG = 0x7E;
        public const byte HEADER_LENGTH = 5;

        public Layer2Protocol() { }

        #region Set and get header fields
        
        private static byte PutFlagByte()
        {
            return FLAG;
        }

        private static byte PutControlByte(FrameType type, byte frameNum)
        {
            byte cByte = (byte)ShiftLeft((int)type, 5);
            
            return (byte)(cByte + frameNum);
        }
        
        private static List<byte> PutFCS(List<byte> frame)
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
        
        private static FrameType GetFrameType(byte controlByte)
        {
            return (FrameType)ShiftRight(controlByte, 5);
        }

        public FrameType GetTypeFast(List<byte> frame)
        {
            return GetFrameType(frame[1]);
        }

        private static byte GetFrameIndex(byte controlByte)
        {
            return (byte)(controlByte & 31);
        }
        
        public byte GetIndexFast(List<byte> frame)
        {
            return GetFrameIndex(frame[1]);
        }

        #endregion


        #region Global need funcs

        public List<byte> PackData(List<byte> data, FrameType type, byte frameNum)
        {
            var informationFrame = new List<byte>
            {
                PutFlagByte(),
                PutControlByte(type, frameNum),
            };

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
            var controlFrame = new List<byte>
            {
                PutFlagByte(),
                PutControlByte(type, frameNum),
                0,
                0,
                PutFlagByte()
            };

            var fcs = PutFCS(controlFrame);
            controlFrame[2] = fcs[0];
            controlFrame[3] = fcs[1];

            return controlFrame;
        }


        public Layer2ProtocolFrameInstance UnpackFrame(List<byte> frame)
        {
            if (frame == null)
                return null;

            var frameChcksum = frame.Skip(frame.Count - 3).Take(2).ToList();
            var newChcksum = PutFCS(frame);
            if (frameChcksum[0] != newChcksum[0] || frameChcksum[1] != newChcksum[1])
                return null;

            var frameInst = new Layer2ProtocolFrameInstance
            {
                Data = frame.Skip(2).Take(frame.Count - 5).ToList()
            };

            var ctrlByte = frame[1];
            frameInst.Type = GetFrameType(ctrlByte);
            frameInst.FrameNum = GetFrameIndex(ctrlByte);

            return frameInst;
        }
        
        #endregion
    }
}
