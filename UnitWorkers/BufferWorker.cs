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
        public Queue<List<byte>> Out;
        public Queue<List<byte>> In;

        public Bind Connection;
        public bool Disabled;

        private Queue<List<byte>> _endPointIn;

        private const int DefaultBitRate = 37800;

        private ProtocolLayers.Layer2Protocol _layer2p;
        private Thread _bufferWorker;
        private int _channelWidth;
        private bool _chanAsync;
        private UnitTerminal _myTerminal;


        public BufferWorker(int connectionNum,  UnitTerminal terminal)
        {
            Connection = NetworksCeW.Windows.MainWindow.ListOfBinds.Find(bind => bind.Index == connectionNum);
            _channelWidth = (int)(DefaultBitRate / Connection.Weight * (Connection.Satellite ? 0.1 : 1));
            _chanAsync = Connection.Duplex;
            _myTerminal = terminal;
            Disabled = Connection.Disabled;

            In = new Queue<List<byte>>();
            Out = new Queue<List<byte>>();
        }

        public void InitEndPointQueue(Queue<List<byte>> inQueue)
        {
            _endPointIn = inQueue;
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

        private void Worker()
        {
            for (int i = 0; i < 10; i++)
            {
                WriteLog("Hi " + i);
                Thread.Sleep(2000);
            }
        }

        private void WriteLog(string log)
        {
            _myTerminal.WriteLog(DateTime.Now, log);
        }
    }
}
