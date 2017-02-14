using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworksCeW.ProtocolLayers
{
    internal abstract class ProtocolInitialManipulations
    {

        #region Bytes and bits manipulation

        protected static byte GetFirstByte(int num)
        {
            return (byte)(num & 0xFF);
        }

        protected static byte GetSecondByte(int num)
        {
            return (byte)ShiftRight(num & 0xFF00, 8);
        }

        protected static byte GetThirdByte(int num)
        {
            return (byte)ShiftRight(num & 0xFF0000, 16);
        }

        protected static byte GetFourthByte(int num)
        {
            return (byte)ShiftRight(num, 24);
        }

        protected static int MakeIntFromBytes(IReadOnlyList<byte> bytes)
        {
            if (bytes == null || bytes.Count > 4 || bytes.Count == 0)
                return -1;
            byte b0 = 0, b1 = 0, b2 = 0, b3 = 0;

            if (bytes.Count == 4)
                b3 = bytes[3];

            if (bytes.Count > 2)
                b2 = bytes[2];

            if (bytes.Count > 1)
                b1 = bytes[1];

            if (bytes.Count > 0)
                b0 = bytes[0];

            return b0 +
                   ShiftLeft(b1, 8) +
                   ShiftLeft(b2, 16) +
                   ShiftLeft(b3, 24);
        }

        protected static List<byte> Make4BytesFromInt(int number)
        {
            return new List<byte>
            {
                GetFirstByte(number),
                GetSecondByte(number),
                GetThirdByte(number),
                GetFourthByte(number),
            };
        }

        protected static List<byte> Make2BytesFromInt(int _2Bytes)
        {
            return new List<byte>
            {
                GetFirstByte(_2Bytes),
                GetSecondByte(_2Bytes),
            };
        }

        protected static int ShiftLeft(int what, int times)
        {
            return (what << times);
        }

        protected static int ShiftRight(int what, int times)
        {
            return (what >> times);
        }

        #endregion


    }
}
