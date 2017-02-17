
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetworksCeW.ProtocolLayers;
using NetworksCeW.Windows;

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

        private enum Turn
        {
            TryToStart = -1,
            Listen = 0,
            SendInfo = 1,
            SendConfirm = 2,
            ReceiveConfirm = 3
        }

        #region Queues

        private Queue<List<byte>> toProcessOnLayer2;
        public List<List<byte>> In;

        private Queue<List<byte>> toProcessOnLayer3;

        private List<SendFrameStruct> _sentFrames;
        private List<SendFrameStruct> _toSendFrames;

        #endregion

        #region Existing instances

        public readonly int EndUnitIndex;
        private readonly int _thisUnitIndex;

        private BufferWorker _endPointWorker;

        private readonly UnitTerminal _myTerminal;

        #endregion

        #region Transmition control

        private Turn _myTurn = Turn.TryToStart;

        private const int DEFAULT_BIT_RATE = 37800;
        private readonly int _myBitRate;
        private readonly int _myBitRateIndex;

        private const int TIMEOUT = 500;
        private const byte WINDOW = 10;
        private byte _windowLeft = 10;
        private int _spoilMaximum;

        private readonly int _bufferSize;
        private int _bufferLeft;

        private byte _nextFrameSend = 1, _nextFrameRec = 1;

        private readonly bool _chanAsync;

        private DateTime _tryAgain;

        public int NumberOfSentFrames { get; private set; }
        public int NumberOfBytesSent { get; private set; }
        public int NumberOfBytesResent { get; private set; }

        // kostyl
        private bool _markerInUse;

        #endregion

        private readonly Layer2Protocol _layer2p;
        private Thread _bufferWorker;
        private readonly Random _rnd;

        private readonly object _lockInQueue, _lockLayer3InQueue, _lockLayer3OutQueue;


        public BufferWorker(int connectionNum, UnitTerminal terminal, int bufferSize, int spoilMaximum = 100)
        {
            var connection = Windows.MainWindow.ListOfBinds.Find(bind => bind.Index == connectionNum);
            _chanAsync = connection.Duplex;
            _myTerminal = terminal;
            _bufferSize = _bufferLeft = bufferSize;
            EndUnitIndex = connection.GetSecondUnitIndex(terminal.UnitInst.Index);
            _thisUnitIndex = _myTerminal.UnitInst.Index;

            _myBitRateIndex = connection.Weight;
            _myBitRate = DEFAULT_BIT_RATE / _myBitRateIndex;
            _spoilMaximum = spoilMaximum;
            
            _layer2p = new Layer2Protocol();

            In = new List<List<byte>>();
            toProcessOnLayer2 = new Queue<List<byte>>();
            toProcessOnLayer3 = new Queue<List<byte>>();

            _sentFrames = new List<SendFrameStruct>();
            _toSendFrames = new List<SendFrameStruct>();

            _lockInQueue = new object();
            _lockLayer3InQueue = new object();
            _lockLayer3OutQueue = new object();
            
            _rnd = new Random(terminal.UnitInst.Index);

            _tryAgain = new DateTime();
        }

        public void InitEndPointWorker(BufferWorker worker)
        {
            _endPointWorker = worker;
        }

        public void WorkerStart()
        {
            _bufferWorker = _chanAsync ? new Thread(WorkerDuplex) : new Thread(WorkerHDuplex);
            _bufferWorker.Start();
            _bufferWorker.IsBackground = true;
        }

        public void WorkerAbort()
        {
            _bufferWorker.Abort();
        }
        
        /// <summary>
        /// Write log into
        /// unit's terminal
        /// </summary>
        /// <param name="log"></param>
        private void WriteLog(string log)
        {
            _myTerminal.WriteLog(DateTime.Now,
                "Buffer to " + EndUnitIndex + ": " + log);
        }


        #region Layer 2-2 communication

        /// <summary>
        /// Use this by another buffer
        /// </summary>
        /// <param name="frame"></param>
        public void PutFrameToThisBuffer(List<byte> frame)
        {
            lock (_lockInQueue)
            {
                if (_bufferLeft >= frame.Count)
                {
                    _bufferLeft -= frame.Count;
                    In.Add(frame);

                    //WriteLog("Frame received " + _layer2p.GetIndexFast(frame));
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
            if (_endPointWorker == null)
            {
                WorkerAbort();
                return;
            }

            NumberOfSentFrames++;
            NumberOfBytesSent += frame.Count;
            _myTerminal.PutAnimatedMessage(_thisUnitIndex, EndUnitIndex);

            // Spoil frame (or not)
            if (frame.Count > 7)
            {
                // SpoilFrame(frame);
            }

            _endPointWorker?.PutFrameToThisBuffer(frame);
        }

        private int SpoilByteIndex(int length)
        {
            if (_rnd.Next(_spoilMaximum) <= _myBitRateIndex)
            {
                return _rnd.Next(1, length - 2);
            }
            return -1;
        }

        private void SpoilFrame(List<byte> frame)
        {
            // Try to spoil message
            var spoilIndex = SpoilByteIndex(frame.Count);
            if (spoilIndex != -1)
            {
                frame[spoilIndex] = (byte)((frame[spoilIndex] + 3) % 255);
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
                if (In.Count <= 0) return null;

                var newFrame = In[0];
                In.RemoveAt(0);
                _bufferLeft += newFrame.Count;
                return newFrame;
            }
        }

        #endregion

        #region Layer 2-3 communication

        /// <summary>
        /// Use by upper level:
        /// Put new datagram in the queue for following 
        /// trasmition to another buffer
        /// </summary>
        /// <param name="datagram"></param>
        public void PushDatagramToProcessOnLayer2(List<byte> datagram)
        {
            //WriteLog("to Process " + datagram.Count);
            lock (_lockLayer3InQueue)
            {
                toProcessOnLayer2.Enqueue(datagram);
            }
        }
        
        /// <summary>
        /// Take another datagram to process and send
        /// </summary>
        /// <returns></returns>
        private List<byte> PullDatagramToProcessOnLayer2()
        {
            lock (_lockLayer3InQueue)
            {
                return toProcessOnLayer2.Count < 1 ? null : toProcessOnLayer2.Dequeue();
            }
        }

        /// <summary>
        /// Put new datagram, receive from another buffer,
        /// in queue for upper level
        /// </summary>
        /// <param name="datagram"></param>
        private void PushDatagramToProcessOnLayer3(List<byte> datagram)
        {
            //WriteLog("Return " + datagram.Count);
            lock (_lockLayer3OutQueue)
            {
                toProcessOnLayer3.Enqueue(datagram);
            }
        }

        /// <summary>
        /// Use by upper level:
        /// Take from the queue another datagram, 
        /// that was received by this buffer
        /// </summary>
        /// <returns></returns>
        public List<byte> PullDatagramToProcessOnLayer3()
        {
            lock (_lockLayer3OutQueue)
            {
                return toProcessOnLayer3.Count < 1 ? null : toProcessOnLayer3.Dequeue();
            }
        }

        #endregion

        
        private void WorkerHDuplex()
        {
            // Features:
            // - start connection
            // - send frames, if turn to sendInfo
            // - send marker after definite time space
            // - send confirm frames, if turn to sendConfirm
            // - finish transmition after finite frames number

            
            _tryAgain = DateTime.Now.AddMilliseconds(TIMEOUT * _rnd.Next(6));

            // Acquire start acknowledgement or incoming start ask
            while (_myTurn == Turn.TryToStart)
            {

                // Send frame if its transmition time passed
                SendAllWaitingFrames();

                ReactToFrameHD(PullNextIncomingFrame());

                if (_myTurn != Turn.TryToStart 
                    || !(DateTime.Now.Subtract(_tryAgain).TotalMilliseconds > 0)) continue;

                // Resend start ask
                _sentFrames.Clear();
                StartCommunication();
                _tryAgain = DateTime.Now.AddMilliseconds(_rnd.Next(TIMEOUT * 5));
            }

            _toSendFrames.Clear();
            _sentFrames.Clear();


            _nextFrameRec = 1;

            // Exchange frames
            while (true)
            {

                // S C E N A R I O   1:


                // It is time to receive frames
                if (_myTurn == Turn.Listen)
                {
                    // WriteLog("---Scenario 1");
                    _markerInUse = false;

                    // Process all incoming frames until 
                    // it is turn to send something
                    while (_myTurn == Turn.Listen)
                    {
                        Thread.Sleep(20);
                        ReactToFrameHD(PullNextIncomingFrame());
                    }

                    if (!_markerInUse)
                    {
                        // Add marker pass control frame
                        _toSendFrames.Add(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.MarkerPass, 0),
                            DateTime.Now.AddMilliseconds(
                                CountSendTime(Layer2Protocol.HEADER_LENGTH)
                            )));
                    }

                    UpdateWaitTime();

                    var lastAckFrame = _toSendFrames.FindLast(fr => _layer2p.GetTypeFast(fr.Frame) == FrameType.Ack);
                    if (lastAckFrame != null)
                    {
                        var index = _layer2p.GetIndexFast(lastAckFrame.Frame);
                        _toSendFrames.RemoveAll(
                            fr =>_layer2p.GetIndexFast(fr.Frame) < index
                            && _layer2p.GetIndexFast(fr.Frame) > 0
                            );
                    }
                    
                    // Send all control frames
                    while (_myTurn == Turn.SendConfirm)
                    {

                        // Send marker back, if nothing to send left
                        // if (In.Count == 0 && _toSendFrames.Count == 0)
                        if (_toSendFrames.Count == 0)
                        {
                            
                            // Wait until ack received, otherwise - resend marker
                            while (_myTurn != Turn.Listen)
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    ReactToFrameHD(PullNextIncomingFrame());

                                    if (_myTurn == Turn.Listen)
                                        break;

                                    Thread.Sleep(TIMEOUT / 10);
                                    SendAllWaitingFrames();
                                }

                                if (_myTurn == Turn.Listen)
                                    break;

                                ReactToFrameHD(PullNextIncomingFrame());

                                if (_sentFrames.Count <= 0) continue;

                                // Count frames to resend
                                IncreaseResentBytes(_sentFrames.Sum(fr => fr.Frame.Count));

                                // Resend if there was still no response
                                _toSendFrames.Insert(0, _sentFrames[_sentFrames.Count - 1]);
                                _sentFrames.RemoveAt(_sentFrames.Count - 1);
                            }
                            
                            break;
                        }

                        SendAllWaitingFrames();
                        Thread.Sleep(20);
                    }

                    _sentFrames.Clear();
                }

                _nextFrameSend = 1;


                // S C E N A R I O   2


                // It is time to send info frames
                if (_myTurn == Turn.SendInfo)
                {
                    // WriteLog("---Scenario 2");

                    var sendingLeftTime = TIMEOUT * 2;
                    var sentFramesCounter = 0;
                    _sentFrames.Clear();

                    // If there appeared back in list unconfirmed frames
                    // Left time of sending is calculated first
                    if (_toSendFrames.Count != 0)
                    {
                        sentFramesCounter = _toSendFrames.Count;
                        sendingLeftTime -= CountSendTime(_toSendFrames.Sum(frame => frame.Frame.Count));

                        // Set number of the frame to be sent next
                        _nextFrameSend = (byte)(_layer2p.GetIndexFast(
                            _toSendFrames[_toSendFrames.Count - 1].Frame) + 1);
                    }

                    // Send frames during definite amount of time
                    while (sendingLeftTime > 0 && _nextFrameSend < 31)
                    {
                        var datagramToSend = PullDatagramToProcessOnLayer2();

                        if (datagramToSend == null)
                        {
                            sendingLeftTime -= 50;
                            Thread.Sleep(50);
                            continue;
                        }

                        // Count sent frames in order to finish connection
                        // after timeout, if no frame was sent
                        sentFramesCounter++;

                        var frameToSend = _layer2p.PackData(
                            datagramToSend,
                            FrameType.Information,
                            _nextFrameSend++);

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
                            _layer2p.PackControl(FrameType.MarkerPass, 0),
                            DateTime.Now.AddMilliseconds(
                                CountSendTime(Layer2Protocol.HEADER_LENGTH)
                            )));
                        
                        // Send all processed frames
                        while (_toSendFrames.Count != 0)
                        {
                            SendAllWaitingFrames();
                            Thread.Sleep(50);
                        }

                        // Wait until ack received, otherwise - resend marker
                        while (_myTurn != Turn.ReceiveConfirm)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                ReactToFrameHD(PullNextIncomingFrame());

                                if (_myTurn == Turn.ReceiveConfirm)
                                    break;

                                Thread.Sleep(TIMEOUT / 10);
                                SendAllWaitingFrames();
                            }

                            if (_myTurn == Turn.ReceiveConfirm)
                                break;

                            ReactToFrameHD(PullNextIncomingFrame());
                            
                            if (_sentFrames.Count <= 0) continue;

                            // Resend if there was still no response
                            _toSendFrames.Insert(0, _sentFrames[_sentFrames.Count - 1]);
                            _sentFrames.RemoveAt(_sentFrames.Count - 1);
                        }
                        
                        // Receive all confirmations, until marker is received
                        while (_myTurn == Turn.ReceiveConfirm)
                        {
                            Thread.Sleep(20);
                            ReactToFrameHD(PullNextIncomingFrame());
                        }
                        
                        // In case all frames are not successfully received by another buffer
                        // resend not confirmed frames
                        if (_sentFrames.Count != 0)
                        {

                            WriteLog("not confirmed:" + _sentFrames.Count);


                            // Count frames to resend
                            IncreaseResentBytes(_sentFrames.Sum(fr => fr.Frame.Count));

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
                        _layer2p.PackControl(FrameType.FinishInit, 0),
                        DateTime.Now.AddMilliseconds(
                            CountSendTime(Layer2Protocol.HEADER_LENGTH)
                        )));

                    // Wait until ack received, otherwise - resend frame
                    while (_myTurn != Turn.Listen)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            ReactToFrameHD(PullNextIncomingFrame());

                            if (_myTurn == Turn.Listen)
                                break;

                            Thread.Sleep(TIMEOUT / 10);
                            SendAllWaitingFrames();
                        }

                        if (_myTurn == Turn.Listen)
                            break;

                        ReactToFrameHD(PullNextIncomingFrame());

                        // Resend if there was still no response
                        if (_sentFrames.Count <= 0) continue;

                        _toSendFrames.Insert(0, _sentFrames[_sentFrames.Count - 1]);
                        _sentFrames.RemoveAt(_sentFrames.Count - 1);
                    }
                    
                    _nextFrameRec = 1;
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
                if (_windowLeft - In.Count < WINDOW / 2)
                {
                    // React to 4 received frames
                    for (var i = 0; i < 4; i++)
                    {
                        ReactToFrameD(PullNextIncomingFrame());
                    }

                    // Generate confirm
                    _toSendFrames.Add(new SendFrameStruct(
                        _layer2p.PackControl(FrameType.Ack, (byte)(_nextFrameRec - 1)),
                        DateTime.Now.AddMilliseconds(
                            CountSendTime(Layer2Protocol.HEADER_LENGTH)
                        )));
                    
                    _windowLeft = WINDOW;
                }

                // If window is full, resend all frames
                if (_sentFrames.Count >= WINDOW)
                {

                    WriteLog("__we resend:__ " + _sentFrames.Count);
                    
                    ResendAllSentFrames();
                }
                
                // Send next frame, if the window is 
                // not yet full
                if (_sentFrames.Count >= WINDOW) continue;

                //Thread.Sleep(150);
                
                var datagramToSend = PullDatagramToProcessOnLayer2();

                if (datagramToSend == null)
                {
                    Thread.Sleep(TIMEOUT);
                    continue;
                }
                
                // If next frame num = 30, send info and null frame
                // Else send plain info frame
                var frameToSend = _layer2p.PackData(datagramToSend, 
                    _nextFrameSend == 30 ? FrameType.InfoAndNull : FrameType.Information, 
                    _nextFrameSend);

                _nextFrameSend = _nextFrameSend == 30 ? (byte)1 : ++_nextFrameSend;

                _toSendFrames.Add(new SendFrameStruct(
                    frameToSend,
                    DateTime.Now.AddMilliseconds(CountSendTime(frameToSend.Count))));
            }
        }


        private void IncreaseResentBytes(int count)
        {
            NumberOfBytesResent += count;
        }


        /// <summary>
        /// Resend all frames, that are still not confirmed
        /// </summary>
        private void ResendAllSentFrames()
        {

            // Count frames to resend
            IncreaseResentBytes(_sentFrames.Sum(fr => fr.Frame.Count));

            // Resend frames as soon as possible
            int indexAfterAck = 0;
            foreach (var frame in _toSendFrames)
            {
                if (_layer2p.GetTypeFast(frame.Frame) != FrameType.Ack)
                    break;
                indexAfterAck++;
            }

            WriteLog("insert in " + indexAfterAck);

            _toSendFrames.InsertRange(indexAfterAck, _sentFrames);

            if (indexAfterAck == 0)
                UpdateWaitTime();
            else
                UpdateWaitTimeNext();

            _sentFrames.Clear();
        }

        /// <summary>
        /// Send start initiation control frame soon
        /// </summary>
        private void StartCommunication()
        {
            _toSendFrames.Add(new SendFrameStruct(
                _layer2p.PackControl(FrameType.StartInit, 0),
                DateTime.Now.AddMilliseconds(CountSendTime(Layer2Protocol.HEADER_LENGTH))
            ));
        }

        /// <summary>
        /// Increase wait time for all frames in the list
        /// </summary>
        private void UpdateWaitTime()
        {
            var waitTime = DateTime.Now;
            foreach (var frameInst in _toSendFrames)
            {
                waitTime = waitTime.AddMilliseconds(CountSendTime(frameInst.Frame.Count));
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
                waitTime = waitTime.AddMilliseconds(CountSendTime(frameInst.Frame.Count));
                frameInst.SendTime = waitTime;
            }
        }
        
        /// <summary>
        /// Sends all frames, that were delayed in the list for time space,
        /// during which they had to be inside the channel
        /// </summary>
        private void SendAllWaitingFrames()
        {
            while (_toSendFrames.Count > 0)
            {
                // If the frame had to be sent already, it will be sent right now
                if (DateTime.Now.Subtract(_toSendFrames[0].SendTime).TotalMilliseconds > 0)
                {
                    PutFrameToAnotherBuffer(_toSendFrames[0].Frame);
                    
                    // Ack type control frames don't go to _sentFrames list,
                    // because they don't require confirmation
                    if (_layer2p.GetTypeFast(_toSendFrames[0].Frame) != FrameType.Ack)
                    {
                        //WriteLog("sent frame " + _layer2p.GetIndexFast(_toSendFrames[0].Frame));
                        _sentFrames.Add(new SendFrameStruct(_toSendFrames[0].Frame, DateTime.Now));
                    }

                    _toSendFrames.RemoveAt(0);
                    continue;
                }
                break;
            }
        }

        private void PutFrameInBeginningToSend(SendFrameStruct frameStruct)
        {
            _toSendFrames.Insert(_toSendFrames.Count > 1 ? 1 : 0, frameStruct);
            UpdateWaitTimeNext();
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
                case FrameType.StartInit:

                    if (_myTurn != Turn.TryToStart)
                    {
                        break;
                    }

                    // Case this buffer did not initiate connection,
                    // there are no unconfirmed frames
                    if (_sentFrames.Count == 0)
                    {

                        WriteLog("Connected");


                        // Send confirmation (ack)
                        // Without pushing it in wait queue
                        Thread.Sleep(CountSendTime(Layer2Protocol.HEADER_LENGTH));
                        PutFrameToAnotherBuffer(_layer2p.PackControl(FrameType.Ack, 0));
                        
                        _myTurn = Turn.Listen;
                        _nextFrameRec = 1;
                    }
                    else
                    {
                        // Delete both sent frame and ignore received one
                        _sentFrames.Clear();

                        lock (_lockInQueue)
                        {
                            In.Clear();
                        }

                        WriteLog("Failed to configure connection");
                    }

                    break;
                case FrameType.FinishInit:

                    // If another buffer had the marker
                    // now this one receives it and
                    // all not yet sent ack-frames are deleted
                    if (_myTurn == Turn.Listen)
                    {
                        // Send confirmation (ack)
                        // Without pushing it in wait queue
                        Thread.Sleep(CountSendTime(Layer2Protocol.HEADER_LENGTH));
                        PutFrameToAnotherBuffer(_layer2p.PackControl(FrameType.Ack, 0));
                        
                        _myTurn = Turn.SendInfo;
                        _markerInUse = true;
                        _toSendFrames.RemoveAll(f => _layer2p.GetTypeFast(f.Frame) <= FrameType.Ack);
                    }

                    break;
                case FrameType.Ack:

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
                        switch (_layer2p.GetTypeFast(_sentFrames[_sentFrames.Count - 1].Frame))
                        {
                            case FrameType.StartInit:

                                // Start initiation confirmed
                                _myTurn = Turn.SendInfo;
                                
                                _nextFrameSend = 1;
                                _sentFrames.Clear();
                                break;
                            case FrameType.FinishInit:

                                // Finish of transmition confirmed
                                _myTurn = Turn.Listen;
                                
                                _sentFrames.Clear();
                                break;
                            case FrameType.MarkerPass:

                                // Now other buffer owns marker to send something
                                _sentFrames.RemoveAt(_sentFrames.Count - 1);
                                _myTurn = _myTurn == Turn.SendConfirm ? Turn.Listen : Turn.ReceiveConfirm;
                                break;
                            case FrameType.NullCounter:

                                // Both buffers now nulled next coming frame index
                                _nextFrameSend = 1;
                                break;
                        }
                    }
                    break;
                case FrameType.Nack:

                    // Smth gone wrong
                    // ???
                    break;
                case FrameType.MarkerPass:

                    // Asked to send frames back (only in half-duplex)
                    _myTurn = _myTurn == Turn.ReceiveConfirm ? Turn.SendInfo : Turn.SendConfirm;
                    
                    Thread.Sleep(CountSendTime(Layer2Protocol.HEADER_LENGTH));
                    PutFrameToAnotherBuffer(_layer2p.PackControl(FrameType.Ack, 0));
                    
                    UpdateWaitTime();
                    break;
                case FrameType.NullCounter:

                    // Next received frame should be nulled
                    PutFrameToAnotherBuffer(_layer2p.PackControl(FrameType.Ack, 0));
                    _nextFrameRec = 1;

                    break;
                case FrameType.Information:

                    // If frame number is not equal to the one, which was waited
                    // the previous frame is confirmed
                    if (_nextFrameRec != newFrame.FrameNum)
                    {
                        WriteLog("panic");
                        
                        _toSendFrames.Add(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.Ack, (byte) (_nextFrameRec - 1)),
                            DateTime.Now
                        ));
                        
                        break;
                    }
                    
                    // Push to the upper layer
                    PushDatagramToProcessOnLayer3(newFrame.Data);
                    
                    // Send confirmation
                    _toSendFrames.Add(new SendFrameStruct(
                        _layer2p.PackControl(FrameType.Ack, _nextFrameRec),
                        DateTime.Now.AddMilliseconds(CountSendTime(Layer2Protocol.HEADER_LENGTH))
                    ));

                    // Increase number of next frame to be recieved
                    _nextFrameRec++;

                    break;

                case FrameType.InfoAndNull:

                    // No use maybe
                    // ????
                    break;
                default:
                    throw new Exception("BLAAAA WTFFFF");
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
                case FrameType.Ack:

                    // The sent frame is confirmed:
                    //* if the frame index != 0, info frames 
                    //* with index till that one are confirmed
                    //* otherwise, the control one
                    
                    if (_sentFrames.Count == 0)
                        break;

                    // Information frame ack
                    if (newFrame.FrameNum == 0)
                        break;

                    var idx = _sentFrames.FindIndex(fr => _layer2p.GetIndexFast(fr.Frame)
                              == newFrame.FrameNum);
                    if (idx > -1)
                    {
                        _sentFrames.RemoveAll(
                            fr =>
                                _layer2p.GetIndexFast(fr.Frame) <= newFrame.FrameNum
                                || _layer2p.GetIndexFast(fr.Frame) > 30 - WINDOW + newFrame.FrameNum);
                    }

                    break;

                case FrameType.Nack:
                    // No use, maybe
                    break;

                case FrameType.InfoAndNull:

                    // If number of frames are not equal,
                    // send ack to last confirmed frame and
                    // ignore all next received ones

                    // Send ack as soon as possible
                    PutFrameInBeginningToSend(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.Ack,
                            (byte)(_nextFrameRec != newFrame.FrameNum 
                                ? (_nextFrameRec == 1 ? 30 : _nextFrameRec - 1) 
                                : _nextFrameRec)),
                            DateTime.Now.AddMilliseconds(CountSendTime(Layer2Protocol.HEADER_LENGTH))
                            ));

                    if (_nextFrameRec != newFrame.FrameNum)
                    {
                        while (In.Count != 0)
                            PullNextIncomingFrame();

                        break;
                    }

                    // Push to the upper layer
                    PushDatagramToProcessOnLayer3(newFrame.Data);

                    _windowLeft--;
                    _nextFrameRec = 1;
                    break;

                case FrameType.Information:

                    _windowLeft--;

                    // If frame number is not equal to the one, which was waited,
                    // the frame is ignored and the previous received one is confirmed
                    if (_nextFrameRec != newFrame.FrameNum)
                    {
                        PutFrameInBeginningToSend(new SendFrameStruct(
                            _layer2p.PackControl(FrameType.Ack, 
                            (byte)(_nextFrameRec == 1 ? 30 : _nextFrameRec - 1)),
                            DateTime.Now.AddMilliseconds(CountSendTime(Layer2Protocol.HEADER_LENGTH))
                            ));
                        
                        while (In.Count != 0)
                            PullNextIncomingFrame();

                        break;
                    }
                    
                    // Push to the upper layer
                    PushDatagramToProcessOnLayer3(newFrame.Data);

                    _nextFrameRec++;

                    break;

                default:
                    throw new Exception("BAAAAAAAD!!!!!");
            }
        }

        private int CountSendTime(int bytes)
        {
            return _myBitRate / 8000 * bytes;
        }
    }
}