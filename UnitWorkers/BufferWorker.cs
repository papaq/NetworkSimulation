using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using NetworksCeW.Structures;
using NetworksCeW.ProtocolLayers;

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
        private class SendFrameStruct
        {
            public readonly List<byte> Frame;

            public DateTime SendTime;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="frame">The frame to be sent</param>
            /// <param name="time">The time, when the frame is to be sent</param>
            public SendFrameStruct(List<byte> frame, DateTime time)
            {
                Frame = frame;
                SendTime = time;
            }
        }

        enum Turn
        {
            tryToStart = -1,
            listen = 0,
            sendInfo = 1,
            sendConfirm = 2,
            receiveConfirm = 3
        }

        #region Queues
        private Queue<List<byte>> toProcessOnLayer2;
        public List<List<byte>> In;

        private Queue<List<byte>> toProcessOnLayer3;

        private List<SendFrameStruct> _sentFrames;
        private List<SendFrameStruct> _toSendFrames;
        #endregion


        #region Existing instances
        public Bind Connection;

        private BufferWorker _endPointWorker;

        private UnitTerminal _myTerminal;
        #endregion


        #region Transmition control
        private Turn _myTurn = Turn.tryToStart;

        private const int DEFAULT_BIT_RATE = 37800;
        private readonly int myBitRate;

        private const int TIMEOUT = 500;
        private const byte WINDOW = 10;
        private byte WindowLeft = 10;

        private readonly int _bufferSize;
        private int _bufferLeft;
        
        private byte nextFrameSend, nextFrameRec = 1;

        private int _channelWidth;
        private bool _chanAsync;

        private DateTime _listenStarted;

        // kostyl
        private bool _markerInUse = false;
        #endregion

        private Layer2Protocol _layer2p;
        private Thread _bufferWorker;
        private Random _rnd;

        private object _lockInQueue, _lockOutQueue, _lockLayer2Queue, _lockLayer3Queue;


        public BufferWorker(int connectionNum, UnitTerminal terminal, int bufferSize)
        {
            Connection = NetworksCeW.Windows.MainWindow.ListOfBinds.Find(bind => bind.Index == connectionNum);
            _channelWidth = (int)(DEFAULT_BIT_RATE / Connection.Weight * (Connection.Satellite ? 0.1 : 1));
            _chanAsync = Connection.Duplex;
            _myTerminal = terminal;
            _bufferSize = _bufferLeft = bufferSize;

            myBitRate = DEFAULT_BIT_RATE / Connection.Weight;

            _layer2p = new Layer2Protocol();

            In = new List<List<byte>>();
            toProcessOnLayer2 = new Queue<List<byte>>();
            toProcessOnLayer3 = new Queue<List<byte>>();

            _sentFrames = new List<SendFrameStruct>();
            _toSendFrames = new List<SendFrameStruct>();

            _lockInQueue = new object();
            _lockOutQueue = new object();
            _lockLayer2Queue = new object();
            _lockLayer3Queue = new object();

            _rnd = new Random();
            _listenStarted = new DateTime();
        }

        public void InitEndPointWorker(BufferWorker worker)
        {
            _endPointWorker = worker;
        }

        public void WorkerStart()
        {
            _bufferWorker = _chanAsync ? new Thread(WorkerDuplex) : new Thread(WorkerHDuplex);
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

        private void WorkerHDuplex()
        {

            // Features:
            // - start connection
            // - send frames, if turn to sendInfo
            // - send marker after definite time space
            // - send confirm frames, if turn to sendConfirm
            // - finish transmition after finite frames number



            // Start connection
            StartCommunication();
            _listenStarted = DateTime.Now;

            // Acquire start acknowledgement or incoming start ask
            while (_myTurn == Turn.tryToStart)
            {

                // Send frame if its transmition time passed
                SendAllWaitingFrames();

                ReactToFrameHD(PullNextIncomingFrame());

                // Resend start ask
                if (_myTurn == Turn.tryToStart &&
                    _rnd.Next(5) == 4 &&
                    DateTime.Now.Subtract(_listenStarted).TotalMilliseconds > TIMEOUT * 3)
                {
                    _sentFrames.Clear();
                    StartCommunication();
                    _listenStarted = DateTime.Now;
                }

                ReactToFrameHD(PullNextIncomingFrame());
            }

            _toSendFrames.Clear();
            _sentFrames.Clear();

            // Exchange frames
            while (true)
            {
                nextFrameRec = 1;
                // S C E N A R I O   1:

                // It is time to receive frames
                if (_myTurn == Turn.listen)
                {
                    WriteLog("\n---------------------\nScenario 1");
                    _markerInUse = false;

                    // Process all incoming frames until 
                    // it is turn to send something
                    while (_myTurn == Turn.listen)
                    {
                        Thread.Sleep(20);
                        ReactToFrameHD(PullNextIncomingFrame());
                    }

                    if (!_markerInUse)
                    {

                        // Add marker pass control frame
                        _toSendFrames.Add(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.marker_pass, 0),
                            DateTime.Now.AddMilliseconds(
                                CountSendTime(Layer2Protocol.HEADER_LENGTH)
                                )));
                    }

                    UpdateWaitTime();

                    // Send all control frames
                    while (_myTurn == Turn.sendConfirm)
                    {
                        WriteLog("   1a");
                        foreach (var item in _toSendFrames)
                        {
                            WriteLog(_layer2p.GetTypeFast(item.Frame).ToString());
                        }



                        // Send marker back, if nothing to send left
                        if (In.Count == 0 && _toSendFrames.Count == 0)
                        {

                            // Wait until ack received, otherwise - resend marker
                            while (_myTurn != Turn.listen)
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    ReactToFrameHD(PullNextIncomingFrame());

                                    if (_myTurn == Turn.listen)
                                        break;

                                    Thread.Sleep(TIMEOUT / 10);
                                    SendAllWaitingFrames();
                                }

                                if (_myTurn == Turn.listen)
                                    break;

                                ReactToFrameHD(PullNextIncomingFrame());

                                // Resend if there was still no response
                                if (_sentFrames.Count > 0)
                                {
                                    _toSendFrames.Insert(0, _sentFrames[_sentFrames.Count - 1]);
                                    _sentFrames.RemoveAt(_sentFrames.Count - 1);
                                }
                            }

                            WriteLog("Marker sent");
                            break;
                        }

                        SendAllWaitingFrames();
                        Thread.Sleep(20);
                    }

                    _sentFrames.Clear();
                }

                nextFrameSend = 1;

                // S C E N A R I O   2

                // It is time to send info frames
                if (_myTurn == Turn.sendInfo)
                {
                    WriteLog("\n---------------------\nScenario 2");

                    var sendingLeftTime = TIMEOUT * 2;
                    var sentFramesCounter = 0;

                    // If there appeared back in list unconfirmed frames
                    // Left time of sending is calculated first
                    if (_toSendFrames.Count != 0)
                    {
                        sentFramesCounter = _toSendFrames.Count;
                        sendingLeftTime -= CountSendTime(_toSendFrames.Sum(frame => frame.Frame.Count));
                    }

                    // Send frames during definite amount of time
                    while (sendingLeftTime > 0)
                    {
                        var datagramToSend = PullNextDatagramToProcess();

                        if (datagramToSend == null)
                        {
                            sendingLeftTime -= 50;
                            Thread.Sleep(50);
                            continue;
                        }
                        WriteLog("   2a");




                        // Count sent frames in order to finish connection
                        // after timeout, if no frame was sent
                        sentFramesCounter++;
                        
                        var frameToSend = _layer2p.PackData(
                                datagramToSend,
                                FrameType.information,
                                nextFrameSend++);

                        sendingLeftTime -= frameToSend.Count;

                        _toSendFrames.Add(new SendFrameStruct(
                            frameToSend,
                            DateTime.Now.AddMilliseconds(CountSendTime(frameToSend.Count))));

                        SendAllWaitingFrames();
                    }

                    // Check if mindestens one frame was sent
                    if (sentFramesCounter != 0)
                    {

                        // Add marker pass control frame
                        _toSendFrames.Add(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.marker_pass, 0),
                            DateTime.Now.AddMilliseconds(
                                CountSendTime(Layer2Protocol.HEADER_LENGTH)
                                )));

                        // Send all processed frames
                        while (_toSendFrames.Count != 0)
                        {
                            WriteLog("   2b");
                            foreach (var item in _toSendFrames)
                            {
                                WriteLog(_layer2p.GetTypeFast(item.Frame).ToString());
                            }



                            SendAllWaitingFrames();
                            Thread.Sleep(50);
                        }
                        
                        // Wait until ack received, otherwise - resend marker
                        while (_myTurn != Turn.receiveConfirm)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                ReactToFrameHD(PullNextIncomingFrame());

                                if (_myTurn == Turn.receiveConfirm)
                                    break;

                                Thread.Sleep(TIMEOUT / 10);
                                SendAllWaitingFrames();
                            }

                            if (_myTurn == Turn.receiveConfirm)
                                break;

                            ReactToFrameHD(PullNextIncomingFrame());

                            // Resend if there was still no response
                            if (_sentFrames.Count > 0)
                            {
                                _toSendFrames.Insert(0, _sentFrames[_sentFrames.Count - 1]);
                                _sentFrames.RemoveAt(_sentFrames.Count - 1);
                            }
                        }

                        WriteLog(_sentFrames.Count.ToString());

                        // Receive all confirmations, until marker is received
                        while (_myTurn == Turn.receiveConfirm)
                        {
                            WriteLog("   2c");




                            Thread.Sleep(20);
                            ReactToFrameHD(PullNextIncomingFrame());

                            WriteLog(_sentFrames.Count.ToString());
                            foreach (var item in _sentFrames)
                            {
                                WriteLog(_layer2p.GetTypeFast(item.Frame).ToString());
                            }
                        }

                        // In case all frames are not successfully received by another buffer
                        // resend not confirmed frames
                        if (_sentFrames.Count != 0)
                        {

                            // Put left sent frames back in _toSendList
                            _toSendFrames.InsertRange(0, _sentFrames);

                            // Update wait time
                            UpdateWaitTime();

                            // Repeat
                            continue;
                        }
                    }
                    
                    // Add finish control frame
                    _toSendFrames.Add(new SendFrameStruct(
                        _layer2p.PackControl(FrameType.finish_init, 0),
                        DateTime.Now.AddMilliseconds(
                            CountSendTime(Layer2Protocol.HEADER_LENGTH)
                            )));
                    
                    // Wait until ack received, otherwise - resend frame
                    while (_myTurn != Turn.listen)
                    {

                        WriteLog("   2d");






                        for (int i = 0; i < 10; i++)
                        {
                            ReactToFrameHD(PullNextIncomingFrame());

                            if (_myTurn == Turn.listen)
                                break;

                            Thread.Sleep(TIMEOUT / 10);
                            SendAllWaitingFrames();
                        }

                        if (_myTurn == Turn.listen)
                            break;

                        ReactToFrameHD(PullNextIncomingFrame());

                        // Resend if there was still no response
                        if (_sentFrames.Count > 0)
                        {
                            _toSendFrames.Insert(0, _sentFrames[_sentFrames.Count - 1]);
                            _sentFrames.RemoveAt(_sentFrames.Count - 1);
                        }
                    }
                }
            }
        }

        private void WorkerDuplex()
        {
            // Features:
            // - Send frames despite receiving others
            // - Resend frames after window is full
            
            while (true)
            {
                SendAllWaitingFrames();
                ReactToFrameD(PullNextIncomingFrame());

                // Send confirm to all previously seen frames 
                if (WindowLeft - In.Count < WINDOW / 2)
                {

                    // React to 4 received frames
                    for (int i = 0; i < 4; i++)
                    {
                        ReactToFrameD(PullNextIncomingFrame());
                    }

                    // Generate confirm
                    _toSendFrames.Add(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.ack, (byte)(nextFrameRec - 1)),
                            DateTime.Now.AddMilliseconds(
                                CountSendTime(Layer2Protocol.HEADER_LENGTH)
                                )));
                    
                    //UpdateWaitTimeNext();
                    WindowLeft = WINDOW;
                }

                // If window is full, resend all frames
                if (_sentFrames.Count == WINDOW)
                {
                    ResendAllSentFrames();
                }

                // If next send number is 31
                // Wait for all frames to be confirmed
                if (nextFrameSend == 31)
                {

                    // Look through a number of incoming frames
                    // and resend already sent frames
                    // until its count is not equal to 0
                    if (_sentFrames.Count != 0)
                    {
                        for (int i = 0; i < WINDOW; i++)
                        {
                            ReactToFrameD(PullNextIncomingFrame());
                        }

                        ResendAllSentFrames();

                        SendAllWaitingFrames();
                        continue;
                    }

                    // Wait for confirming control frame
                    while (_sentFrames.Count != 0)
                    {
                        _sentFrames.Clear();

                        // Send null counter and wait for ack
                        _toSendFrames.Add(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.null_counter, 0),
                            DateTime.Now.AddMilliseconds(
                                CountSendTime(Layer2Protocol.HEADER_LENGTH))));

                        Thread.Sleep(TIMEOUT / 10);

                        var inCount = In.Count;
                        for (int i = 0; i < inCount; i++)
                            ReactToFrameD(PullNextIncomingFrame());
                    }
                }

                // Send next frame, if the window is 
                // not yet full
                if (_sentFrames.Count < WINDOW)
                {




                    WriteLog("bla10");






                    Thread.Sleep(3000);
                    var datagramToSend = PullNextDatagramToProcess();

                    if (datagramToSend == null)
                    {
                        WriteLog("nothing to send");
                        Thread.Sleep(TIMEOUT);
                        continue;
                    }




                    WriteLog("la9");





                    var frameToSend = _layer2p.PackData(
                            datagramToSend,
                            FrameType.information,
                            nextFrameSend++);

                    _toSendFrames.Add(new SendFrameStruct(
                        frameToSend,
                        DateTime.Now.AddMilliseconds(CountSendTime(frameToSend.Count))));
                }
            }
        }

        /// <summary>
        /// Resend all frames, that are still not confirmed
        /// </summary>
        private void ResendAllSentFrames()
        {
            // Resend frames as soon as possible
            int indexAfterAck = 0;
            foreach (var frame in _toSendFrames)
            {
                if (_layer2p.GetTypeFast(frame.Frame) != FrameType.ack)
                    break;
                indexAfterAck++;
            }

            _toSendFrames.InsertRange(indexAfterAck, _sentFrames);
            UpdateWaitTimeNext();
            _sentFrames.Clear();
        }

        /// <summary>
        /// Write log into
        /// unit's terminal
        /// </summary>
        /// <param name="log"></param>
        private void WriteLog(string log)
        {
            _myTerminal.WriteLog(DateTime.Now, 
                "Buffer to " + Connection.GetSecondUnitIndex(_myTerminal.UnitInst.Index).ToString() + 
                ": " + log);
        }

        /// <summary>
        /// Use this by another buffer
        /// </summary>
        /// <param name="frame"></param>
        public void PutFrameToThisBuffer(List<byte> frame)
        {
            lock(_lockInQueue)
            {
                if (_bufferLeft >= frame.Count)
                {
                    _bufferLeft -= frame.Count;
                    In.Add(frame);
                    WriteLog("Frame received");
                }
                else
                {
                    WriteLog("Lost frame");
                }
            }
        }

        /// <summary>
        /// Use this by current buffer
        /// </summary>
        /// <param name="frame"></param>
        private void PutFrameToAnotherBuffer(List<byte> frame)
        {
            if (_endPointWorker != null)
            {
                _endPointWorker.PutFrameToThisBuffer(frame);
            }
        }

        /// <summary>
        /// Get the first frame from In list
        /// </summary>
        /// <returns></returns>
        private List<byte> PullNextIncomingFrame()
        {
            lock (_lockInQueue)
            {
                if (In.Count > 0)
                {
                    var newFrame = In[0];
                    In.RemoveAt(0);
                    _bufferLeft += newFrame.Count;
                    return newFrame;
                }

                return null;
            }
        }

        /// <summary>
        /// Use by upper level:
        /// Put new datagram in the queue for following 
        /// trasmition to another buffer
        /// </summary>
        /// <param name="datagram"></param>
        public void PushNewLayer3Datagram(List<byte> datagram)
        {
            lock (_lockLayer2Queue)
            {
                toProcessOnLayer2.Enqueue(datagram);
            }
        }

        /// <summary>
        /// Use by upper level:
        /// Take from the queue another datagram, 
        /// that was received by this buffer
        /// </summary>
        /// <returns></returns>
        public List<byte> PullNewLayer3Datagram()
        {
            lock (_lockLayer3Queue)
            {
                if (toProcessOnLayer3.Count < 1)
                    return null;

                return toProcessOnLayer3.Dequeue();
            }
        }
        

        /// <summary>
        /// Send start initiation control frame soon
        /// </summary>
        private void StartCommunication()
        {
            _toSendFrames.Add(new SendFrameStruct(
                _layer2p.PackControl(FrameType.start_init, 0),
                DateTime.Now.AddMilliseconds(CountSendTime(Layer2Protocol.HEADER_LENGTH))
                ));
        }
        
        
        /// <summary>
        /// Take another datagram to process and send
        /// </summary>
        /// <returns></returns>
        private List<byte> PullNextDatagramToProcess()
        {
            lock (_lockLayer2Queue)
            {
                if (toProcessOnLayer2.Count < 1)
                    return null;

                return toProcessOnLayer2.Dequeue();
            }
        }

        /// <summary>
        /// Put new datagram, receive from another buffer,
        /// in queue for upper level
        /// </summary>
        /// <param name="datagram"></param>
        private void PushDatagramToProcessOnLayer3(List<byte> datagram)
        {
            lock (_lockLayer3Queue)
            {
                toProcessOnLayer3.Enqueue(datagram);
            }
        }

        
        /// <summary>
        /// Increase wait time for all frames in the list
        /// </summary>
        /// <param name="indexStart"></param>
        /// <param name="plusMSecs"></param>
        private void UpdateWaitTime()
        {
            var waitTime = DateTime.Now;
            foreach (var frameInst in _toSendFrames)
            {
                waitTime.AddMilliseconds(CountSendTime(frameInst.Frame.Count));
                frameInst.SendTime = waitTime;
            }
        }

        /// <summary>
        /// Calculate wait time for all frames in wait list
        /// starting with the second one
        /// </summary>
        private void UpdateWaitTimeNext()
        {
            if (_toSendFrames.Count < 2) return;

            var waitTime = _toSendFrames[0].SendTime;
            foreach (var frameInst in _toSendFrames.Skip(1))
            {
                waitTime.AddMilliseconds(CountSendTime(frameInst.Frame.Count));
                frameInst.SendTime = waitTime;
            }
        }


        /// <summary>
        /// Sends all frames, that were delayed in the list for time space,
        /// during which they had to be inside the channel
        /// </summary>
        private void SendAllWaitingFrames()
        {
            while(_toSendFrames.Count > 0)
            {
                // If the frame had to be sent already, it will be sent right now
                if (DateTime.Now.Subtract(_toSendFrames[0].SendTime).TotalMilliseconds > 0)
                {
                    PutFrameToAnotherBuffer(_toSendFrames[0].Frame);
                    WriteLog("Frame sent");

                    // Ack type control frames don't go to _sentFrames list,
                    // because they don't require confirmation
                    if (_layer2p.GetTypeFast(_toSendFrames[0].Frame) != FrameType.ack)
                    {
                        _sentFrames.Add(new SendFrameStruct(_toSendFrames[0].Frame, DateTime.Now));
                    }
                    _toSendFrames.RemoveAt(0);
                    continue;
                }
                break;
            }
        }

        /// <summary>
        /// Reacts to a pulled frame in half-duplex mode
        /// - deletes sent and confirmed frames
        /// - manages connection
        /// - confirms received frames
        /// </summary>
        /// <param name="frame"></param>
        private void ReactToFrameHD(List<byte> frame)
        {
            var newFrame = _layer2p.UnpackFrame(frame);

            if (newFrame == null)
                return;
            
            switch (newFrame.Type)
            {
                case FrameType.start_init:
                    WriteLog("rInit");



                    if (_myTurn != Turn.tryToStart)
                    {
                        break;
                    }

                    // Case this buffer did not initiate connection,
                    // there are no unconfirmed frames
                    if (_sentFrames.Count == 0)
                    {

                        // Send confirmation (ack)
                        // Without pushing it in wait queue
                        Thread.Sleep(CountSendTime(Layer2Protocol.HEADER_LENGTH));
                        PutFrameToAnotherBuffer(_layer2p.PackControl(FrameType.ack, 0));

                        WriteLog("Sent marker");

                        _myTurn = Turn.listen;
                        nextFrameRec = 1;
                    }
                    else
                    {

                        // Delete both sent frame and ignore received one
                        // wait random time space
                        WriteLog("Failed to configure connection");
                        _sentFrames.Clear();
                        Thread.Sleep(_rnd.Next(TIMEOUT));
                    }

                    break;
                case FrameType.finish_init:
                    WriteLog("rFinish");





                    // If another buffer had the marker
                    // now this one receives it and
                    // all not yet sent ack-frames are deleted
                    if (_myTurn == Turn.listen)
                    {

                        // Send confirmation (ack)
                        // Without pushing it in wait queue
                        Thread.Sleep(CountSendTime(Layer2Protocol.HEADER_LENGTH));
                        PutFrameToAnotherBuffer(_layer2p.PackControl(FrameType.ack, 0));

                        WriteLog("Received marker");

                        _myTurn = Turn.sendInfo;
                        _markerInUse = true;
                        _toSendFrames.RemoveAll(f => _layer2p.GetTypeFast(f.Frame) <= FrameType.ack);
                    }

                    break;
                case FrameType.ack:

                    // The sent frame is confirmed:
                    //* if the frame index != 0, info frames 
                    //* with index till that one are confirmed
                    //* otherwise, the control one
                    if (newFrame.FrameNum != 0)
                    {
                        WriteLog("rAckInfo");





                        _sentFrames.RemoveAll(frameInst =>
                            _layer2p.GetIndexFast(frameInst.Frame) == newFrame.FrameNum);
                    }
                    else
                    {
                        if (_sentFrames.Count == 0)
                            break;
                        
                        // Control frame is confirmed
                        switch (_layer2p.GetTypeFast(_sentFrames[_sentFrames.Count - 1].Frame))
                        {
                            case FrameType.start_init:
                                WriteLog("rAckInit");





                                // Start initiation confirmed
                                _myTurn = Turn.sendInfo;

                                WriteLog("Marker received");
                                nextFrameSend = 1;
                                _sentFrames.Clear();
                                break;
                            case FrameType.finish_init:
                                WriteLog("rFinish");





                                // Finish of transmition confirmed
                                _myTurn = Turn.listen;

                                WriteLog("Marker sent");
                                _sentFrames.Clear();
                                break;
                            case FrameType.marker_pass:
                                WriteLog("rAckMarker");





                                // Now other buffer owns marker to send something
                                _sentFrames.RemoveAt(_sentFrames.Count - 1);
                                _myTurn = _myTurn == Turn.sendConfirm ? Turn.listen : Turn.receiveConfirm;
                                break;
                            case FrameType.null_counter:

                                // Both buffers now nulled next coming frame index
                                nextFrameSend = 1;
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                case FrameType.nack:

                    // Smth gone wrong
                    // ???
                    break;
                case FrameType.marker_pass:
                    WriteLog("rMark");





                    // Asked to send frames back (only in half-duplex)
                    _myTurn = _myTurn == Turn.receiveConfirm ? Turn.sendInfo : Turn.sendConfirm;

                    //_toSendFrames.Clear();

                    Thread.Sleep(CountSendTime(Layer2Protocol.HEADER_LENGTH));
                    PutFrameToAnotherBuffer(_layer2p.PackControl(FrameType.ack, 0));

                    WriteLog("Marker received");
                    UpdateWaitTime();
                    break;
                case FrameType.null_counter:

                    // Next received frame should be nulled
                    PutFrameToAnotherBuffer(_layer2p.PackControl(FrameType.ack, 0));
                    nextFrameRec = 1;

                    break;
                case FrameType.information:
                    WriteLog("rInforma");





                    // If frame number is not equal to the one, which was waited
                    // the previous frame is confirmed
                    if (nextFrameRec != newFrame.FrameNum)
                    {
                        _toSendFrames.Add(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.ack, (byte)(nextFrameRec - 1)),
                            DateTime.Now
                            ));
                        
                        //while (PullNextIncomingFrame() != null) { }
                        break;
                    }
                                        
                    // Push to the upper layer
                    PushDatagramToProcessOnLayer3(newFrame.Data);

                    //WindowLeft--;

                    // Send confirmation, if window is closing
                    //if (WindowLeft < WINDOW / 2)
                    //{
                    _toSendFrames.Add(new SendFrameStruct(
                        _layer2p.PackControl(FrameType.ack, nextFrameRec),
                        DateTime.Now.AddMilliseconds(CountSendTime(Layer2Protocol.HEADER_LENGTH))
                        ));
                        //WindowLeft = WINDOW;
                    //}

                    break;

                case FrameType.info_and_null:

                    // No use maybe
                    // ????
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Reacts to a pulled frame in duplex mode
        /// - deletes sent and confirmed frames
        /// - confirms received frames
        /// </summary>
        /// <param name="frame"></param>
        private void ReactToFrameD(List<byte> frame)
        {
            var newFrame = _layer2p.UnpackFrame(frame);

            if (newFrame == null)
                return;

            switch (newFrame.Type)
            {
                case FrameType.ack:

                    // The sent frame is confirmed:
                    //* if the frame index != 0, info frames 
                    //* with index till that one are confirmed
                    //* otherwise, the control one
                    if (newFrame.FrameNum != 0)
                    {
                        _sentFrames.RemoveAll(frameInst =>
                            _layer2p.GetIndexFast(frameInst.Frame) <= newFrame.FrameNum);
                    }
                    else
                    {
                        if (_sentFrames.Count == 0)
                            break;

                        // Control frame is confirmed
                        if (_layer2p.GetTypeFast(_sentFrames[_sentFrames.Count - 1].Frame) == FrameType.null_counter)

                                // Both buffers now nulled next coming frame index
                                nextFrameSend = 1;
                    }
                    break;

                case FrameType.nack:
                    // No use, maybe
                    break;

                case FrameType.null_counter:
                    if (_toSendFrames.Count > 1)
                        _toSendFrames.Insert(1, new SendFrameStruct(
                            _layer2p.PackControl(FrameType.ack, 0), DateTime.Now));
                    else
                        _toSendFrames.Add(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.ack, 0), DateTime.Now));

                    nextFrameRec = 1;
                    break;

                case FrameType.information:

                    WindowLeft--;

                    // If frame number is not equal to the one, which was waited,
                    // the frame is ignored and the previous received one is confirmed
                    if (nextFrameRec != newFrame.FrameNum)
                    {
                        _toSendFrames.Add(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.ack, (byte)(nextFrameRec - 1)),
                            DateTime.Now
                            ));

                        break;
                    }

                    // Push to the upper layer
                    PushDatagramToProcessOnLayer3(newFrame.Data);
                    nextFrameRec++;
                    break;

                default:
                    // Nothing here
                    break;
            }
        }

        private int CountSendTime(int bytes)
        {
            return myBitRate / 8000 * bytes;
        }
    }
}
