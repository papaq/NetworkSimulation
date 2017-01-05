﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace NetworksCeW
{
    /// <summary>
    /// Comprises all protocol layers, enables units communication
    /// </summary>
    public class UnitWorker
    {
        public Queue<List<byte>> ListSendThis;
        public List<BufferWorker> ListBufferWorkers;

        private Thread _unitWorker;
        private Unit _unit;

        private UnitTerminal _myTerminal;
        private List<UnitTerminal> _listOfTerminals;

        public UnitWorker(UnitTerminal terminal, List<UnitTerminal> listTerminals, Unit unit)
        {
            _myTerminal = terminal;
            _listOfTerminals = listTerminals;
            _unit = unit;
            ListBufferWorkers = new List<BufferWorker>();
        }

        public void WorkerStart()
        {
            _unitWorker = new Thread(Worker);
            _unitWorker.Start();
        }

        public void WorkerAbort()
        {
            AbortAllBufferWorkers();
            _unitWorker.Abort();
        }

        private void InitBufferWorkers()
        {
            if (_unit.ListBindsIndexes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _unit.ListBindsIndexes.Count; i++)
            {
                ListBufferWorkers.Add(new BufferWorker(_unit.ListBindsIndexes[i], _myTerminal));
            }
            Thread.Sleep(10000);
            foreach (var buff in ListBufferWorkers)
            {
                buff.InitEndPointQueue(_listOfTerminals.Find(
                    term => term.Unit.ListBindsIndexes.Contains(
                        buff.Connection.Index
                        )
                    ).UnitWorker.ListBufferWorkers.Find(
                    buffWorker => buffWorker.Connection.Index == buff.Connection.Index).In
                    );
            }
        }

        private void StartAllBufferWorkers()
        {
            foreach (var buff in ListBufferWorkers)
            {
                buff.WorkerStart();
            }
        }

        private void AbortAllBufferWorkers()
        {
            foreach (var buff in ListBufferWorkers)
            {
                buff.WorkerAbort();
            }
        }

        private void Worker()
        {
            InitBufferWorkers();
            StartAllBufferWorkers();
        }
    }
}