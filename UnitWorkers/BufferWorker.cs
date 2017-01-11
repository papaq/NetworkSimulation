using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using NetworksCeW.Structures;

namespace NetworksCeW.UnitWorkers
{
    /// <summary>
    /// Buffer class, sends and receives frames, uses Data link layer protocol
    /// </summary>
    public class BufferWorker
    {
        /// <summary>
        /// This structure contains a frame and 
        /// time, till which it should be confirmed
        /// or until which it should not be sent
        /// </summary>
        private class SendFrame
        {
            public readonly List<byte> Frame;

            public readonly DateTime TimeToCheck;

            public SendFrame(List<byte> frame, DateTime time)
            {
                Frame = frame;
                TimeToCheck = time;
            }
        }
        

        public Queue<List<byte>> Out;
        public Queue<List<byte>> In;

        private List<SendFrame> _sentFrames;
        private List<SendFrame> _toSendFrames;

        public Bind Connection;
        private bool _myTurn;

        private BufferWorker _endPointWorker;

        private const int DEFAULT_BIT_RATE = 37800;
        private readonly int myBitRate;
        private const int Timeout = 500;

        private readonly int _bufferSize;
        private int _bufferLeft;

        private int nextFrameSend = 0;
        private int nextFrameRec = 0;

        private ProtocolLayers.Layer2Protocol _layer2p;
        private Thread _bufferWorker;
        private int _channelWidth;
        private bool _chanAsync;
        private UnitTerminal _myTerminal;
        private Random _rnd;

        private object _blockerInQueue, _blockerOutQueue;


        public BufferWorker(int connectionNum, UnitTerminal terminal, int bufferSize)
        {
            Connection = NetworksCeW.Windows.MainWindow.ListOfBinds.Find(bind => bind.Index == connectionNum);
            _channelWidth = (int)(DEFAULT_BIT_RATE / Connection.Weight * (Connection.Satellite ? 0.1 : 1));
            _chanAsync = Connection.Duplex;
            _myTerminal = terminal;
            _bufferSize = _bufferLeft = bufferSize;

            myBitRate = DEFAULT_BIT_RATE / Connection.Weight;

            _layer2p = new ProtocolLayers.Layer2Protocol();

            In = new Queue<List<byte>>();
            Out = new Queue<List<byte>>();

            _sentFrames = new List<SendFrame>();
            _toSendFrames = new List<SendFrame>();

            _blockerInQueue = new object();
            _blockerOutQueue = new object();

            _rnd = new Random();
        }

        public void InitEndPointWorker(BufferWorker worker)
        {
            _endPointWorker = worker;
        }

        public void WorkerStart()
        {
            _bufferWorker = new Thread(Worker);
            _bufferWorker.Start();
        }

        public void WorkerAbort()
        {
            _bufferWorker.Abort();
        }

        public byte CountBufferBusy()
        {
            return (byte)((_bufferSize - _bufferLeft) / _bufferSize * 100);
        }

        private void Worker()
        {
            Thread.Sleep(_rnd.Next(0, 300));
            MakeSelfFirst();
            while (true)
            {
                var newFrame = _layer2p.Unpack(GetNewFrame());

                if (newFrame == null)
                {
                    Thread.Sleep(Timeout / 10);
                    continue;
                }

                if (newFrame.Control &&
                    newFrame.Start &&
                    newFrame.Ack)
                {
                    WriteLog("Buffer " + Connection.Index + ": Marker received!");
                    continue;
                }

                if (newFrame.Start &&
                    newFrame.Init)
                {
                    WriteLog("Buffer " + Connection.Index + ": Marker asked!");
                    continue;
                }
            }
        }

        private void WriteLog(string log)
        {
            _myTerminal.WriteLog(DateTime.Now, log);
        }

        /// <summary>
        /// Use this by another buffer
        /// </summary>
        /// <param name="frame"></param>
        public void AddNewFrame(List<byte> frame)
        {
            lock(_blockerInQueue)
            {
                if (_bufferLeft >= frame.Count)
                {
                    _bufferLeft -= frame.Count;
                    In.Enqueue(frame);
                    WriteLog("Buffer " + Connection.Index + ": frame received!");
                }
                else
                {
                    WriteLog("Buffer " + Connection.Index + ": lost frame!");
                }
            }
        }

        private List<byte> GetNewFrame()
        {
            lock (_blockerInQueue)
            {
                if (In.Count > 0)
                {
                    var newFrame = In.Dequeue();
                    _bufferLeft += newFrame.Count;
                    return newFrame;
                }

                return null;
            }
        }

        private void MakeSelfFirst()
        {
            while (true)
            {
                _sentFrames.Add(new SendFrame(_layer2p.AskConnection(true), DateTime.Now));
                Thread.Sleep(CountSendTime(_sentFrames.Count));
                if (In.Count != 0)
                {
                    _sentFrames.RemoveAt(_sentFrames.Count - 1);
                    return;
                }

                if (_endPointWorker != null)
                {
                    _endPointWorker.AddNewFrame(_layer2p.AskConnection(true));
                }
            }            
        }

        private void MakeOtherFirst()
        {
            var newFrame = _layer2p.Unpack(GetNewFrame());

            if (newFrame == null)
            {
                Thread.Sleep(Timeout / 10);
                return;
            }

            if (newFrame.Control &&
                newFrame.Start &&
                newFrame.Ack)
            {
                WriteLog("Buffer " + Connection.Index + ": Marker received!");
                continue;
            }

            if (newFrame.Start &&
                newFrame.Init)
            {
                WriteLog("Buffer " + Connection.Index + ": Marker asked!");
                continue;
            }
        }

        private int CountSendTime(int bytes)
        {
            return myBitRate / 8000 * bytes;
        }
    }
}
