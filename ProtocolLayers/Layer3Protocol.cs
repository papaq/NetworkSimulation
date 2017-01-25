using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetworksCeW.ProtocolLayers
{
    /// <summary>
    /// Structure describes other end unit index 
    /// and connection bandwidth
    /// </summary>
    internal class ToUnitConnection
    {
        public int ToUnit { get; set; }
        public int BandWidth { get; set; }
    }

    /// <summary>
    /// Structure used to describe all
    /// datagram header fields
    /// </summary>
    internal class Layer3ProtocolDatagramInstance
    {
        public List<byte> Data { get; set; }
        public int Identification { get; set; }
        public byte Flags { get; set; }
        public int TotalLength { get; set; }
        public int FragmentOffset { get; set; }
        public byte Protocol { get; set; }
        public byte TTL { get; set; }
        public byte Saddr { get; set; }
        public byte Daddr { get; set; }
    }


    internal class Layer3Protocol
    {
        /// <summary>
        /// Structure describes route by destination unit 
        /// and next after current
        /// </summary>
        private class DestinationNext
        {
            public int DestUnitIndex { get; set; }

            // Route with maximum bandwidth
            public int NextUnitIndexW { get; set; }

            public int Width { get; set; }

            // Route with minimum length
            public int NextUnitIndexL { get; set; }

            public int Length { get; set; }

            public DestinationNext()
            {
                Width = Length = 1000;
            }
        }

        /// <summary>
        /// Structure describes unit's own connections
        /// </summary>
        private class UnitConnectionsUnits
        {
            public readonly int UnitIndex;
            public DateTime UpdateTime;
            public List<ToUnitConnection> Connections;

            public UnitConnectionsUnits(int unit)
            {
                UnitIndex = unit;
                Connections = new List<ToUnitConnection>();
            }
        }

        public const byte Version = 0x04;
        public const byte Dscp = 0x0;
        public const byte Tcp = 0x06;
        public const byte Udp = 0x11;
        public const byte Rtp = 0x55;
        public const byte Brdcst = 0xFF;

        public const int Timeout = 40000;

        private readonly int _myUnitIndex;

        private List<DestinationNext> _routes;
        private List<UnitConnectionsUnits> networkTopology;

        private int _newDatagramId = -1;
        //public List<byte> Datagram;

        public Layer3Protocol(int unit)
        {
            _myUnitIndex = unit;
            networkTopology = new List<UnitConnectionsUnits>();
            _routes = new List<DestinationNext>();
        }
        
        public int GetNextId()
        {
            if (_newDatagramId == int.MaxValue)
            {
                _newDatagramId = -1;
            }

            return ++_newDatagramId;
        }

        public int GetLastUsedId()
        {
            return _newDatagramId;
        }


        #region Set and get header fields
        private byte PutHDL(byte len)
        {
            return (byte) (Version + ShiftLeft(len, 4));
        }

        private byte PutDSCPandECN(byte dscp, byte ecn)
        {
            return (byte) (ShiftLeft(ecn, 6) + dscp);
        }

        private List<byte> PutTotalLength(int totLen)
        {
            return new List<byte>()
            {
                GetFirstByte(totLen),
                GetSecondByte(totLen)
            };
        }

        private int GetTotalLength(List<byte> bytes)
        {
            return MakeIntFromBytes(bytes.Skip(2).Take(2).ToList());
        }

        private List<byte> PutIdentification(int identification)
        {
            return new List<byte>()
            {
                GetFirstByte(identification),
                GetSecondByte(identification)
            };
        }

        private int GetIdentification(List<byte> bytes)
        {
            return MakeIntFromBytes(bytes.Skip(4).Take(2).ToList());
        }

        private List<byte> PutFlagsnOffset(byte flags, int offset)
        {
            return new List<byte>()
            {
                (byte) (flags + ShiftLeft(GetFirstByte(offset), 3)),
                GetSecondByte(offset)
            };
        }

        private byte GetFlags(List<byte> bytes)
        {
            return (byte) ShiftRight(bytes[6], 5);
        }

        private int GetOffset(List<byte> bytes)
        {
            return MakeIntFromBytes(bytes.Skip(6).Take(2).ToList()) & 0x1FFF;
        }

        private byte PutTTL(byte ttl)
        {
            return ttl;
        }

        private byte GetTTL(List<byte> bytes)
        {
            return bytes[8];
        }

        private byte PutProtocol(byte protocol)
        {
            return protocol;
        }

        private byte GetProtocol(List<byte> bytes)
        {
            return bytes[9];
        }

        private List<byte> PutCheckSum(int chksm)
        {
            return new List<byte>()
            {
                GetFirstByte(chksm),
                GetSecondByte(chksm)
            };
        }

        private int GetCheckSum(List<byte> bytes)
        {
            return MakeIntFromBytes(bytes.Skip(10).Take(2).ToList());
        }

        private int CountCheckSum(List<byte> header)
        {
            header[10] = header[11] = 0;
            var checkSum = header.Aggregate(0, (current, b) => current + b);

            checkSum = ShiftLeft(GetFourthByte(checkSum), 8) +
                       GetThirdByte(checkSum) + (checkSum & 0xFFF0000);
            return 0xFFFF - checkSum;
        }

        private List<byte> PutSourceAddress(int myAddr)
        {
            return new List<byte>()
            {
                GetFirstByte(myAddr),
                0,
                0,
                0
            };
        }

        private byte GetSourceAddress(List<byte> bytes)
        {
            return bytes[12];
        }

        private List<byte> PutDestinationAddress(int destAddr)
        {
            return new List<byte>()
            {
                GetFirstByte(destAddr),
                0,
                0,
                0
            };
        }

        private byte GetDestinationAddress(List<byte> bytes)
        {
            return bytes[16];
        }
        #endregion


        #region Bytes and bits manipulation

        private byte GetFirstByte(int num)
        {
            return (byte) (num & 0xFF);
        }

        private byte GetSecondByte(int num)
        {
            return (byte) ShiftRight(num & 0xFF00, 8);
        }

        private byte GetThirdByte(int num)
        {
            return (byte) ShiftRight(num & 0xFF0000, 16);
        }

        private byte GetFourthByte(int num)
        {
            return (byte) ShiftRight(num, 24);
        }

        private int MakeIntFromBytes(List<byte> bytes)
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

        private int ShiftLeft(int what, int times)
        {
            return (what << times);
        }

        private int ShiftRight(int what, int times)
        {
            return (what >> times);
        }

        #endregion


        #region Global need funcs

        /// <summary>
        /// Creates header for the datagram
        /// </summary>
        public List<byte> PackData(
            List<byte> data,
            int congestion,
            int id,
            byte flags,
            int offset,
            byte ttl,
            byte protocol,
            int sourceAddr,
            int destAddr)
        {
            var datagram = new List<byte>();
            const byte headerLen = 20;

            datagram.Add(PutHDL(headerLen));
            datagram.Add(PutDSCPandECN(Dscp, (byte) congestion));
            datagram.AddRange(PutTotalLength(data.Count + headerLen));

            datagram.AddRange(PutIdentification(id));
            datagram.AddRange(PutFlagsnOffset(flags, offset));

            datagram.Add(PutTTL(ttl));
            datagram.Add(PutProtocol(protocol));
            datagram.Add(0);
            datagram.Add(0);

            datagram.AddRange(PutSourceAddress(sourceAddr));
            datagram.AddRange(PutDestinationAddress(destAddr));

            // chksum
            var checkSum = PutCheckSum(CountCheckSum(datagram));
            datagram[10] = checkSum[0];
            datagram[11] = checkSum[1];

            // Put data
            datagram.AddRange(data);

            return datagram;
        }

        public Layer3ProtocolDatagramInstance UnpackFrame(List<byte> datagram)
        {
            if (datagram == null)
                return null;

            var datagramCheckSum = GetCheckSum(datagram);
            if (CountCheckSum(datagram.Take(20).ToList()) != datagramCheckSum)
                return null;

            var datagramInst = new Layer3ProtocolDatagramInstance()
            {
                Data = datagram.Skip(20).Take(datagram.Count).ToList(),
                Identification = GetIdentification(datagram),
                Flags = GetFlags(datagram),
                TotalLength = GetTotalLength(datagram),
                FragmentOffset = GetOffset(datagram),
                Protocol = GetProtocol(datagram),
                TTL = (byte)(GetTTL(datagram)-1),
                Saddr = GetSourceAddress(datagram),
                Daddr = GetDestinationAddress(datagram),
            };

            if (datagramInst.TTL == 0)
                return null;

            return datagramInst;
        }

        #endregion


        #region Topology

        /// <summary>
        /// Updates the whole table of units and destinations
        /// </summary>
        public void UpdateNetworkTopology()
        {
            int iter = 0, count = networkTopology.Count;
            while (iter != networkTopology.Count)
            {
                if (Timeout > DateTime.Now.Subtract(networkTopology[iter].UpdateTime).TotalMilliseconds)
                {
                    iter++;
                    continue;
                }

                // Delete unit from all records in list
                var unitIndex = networkTopology[iter].UnitIndex;
                foreach (var unitInfo in networkTopology)
                    unitInfo.Connections.RemoveAll(conn => conn.ToUnit == unitIndex);

                networkTopology.RemoveAt(iter);
            }

            //if (count != networkTopology.Count)
                RebuildRoutes();
        }

        /// <summary>
        /// Updates or writes anew a record about the unit
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="connections"></param>
        public void UpdateUnitInformation(int unit, List<ToUnitConnection> connections)
        {
            var currentUnitInfo = networkTopology.Find(
                unitInfo => unitInfo.UnitIndex == unit
            );

            // Record with this unit exists
            if (currentUnitInfo != null)
            {
                currentUnitInfo.Connections = new List<ToUnitConnection>();
                foreach (var conn in connections)
                {
                    // If there is a unit, to which the connection directed
                    var unitToInfo = networkTopology.Find(
                        unitInfo => unitInfo.UnitIndex == conn.ToUnit
                    );

                    if (unitToInfo == null) continue;

                    currentUnitInfo.Connections.Add(conn);

                    // If the other unit does not have record about the connection
                    if (unitToInfo.Connections.Find(c => c.ToUnit == unit) == null)
                    {
                        unitToInfo.Connections.Add(new ToUnitConnection()
                        {
                            ToUnit = unit,
                            BandWidth = conn.BandWidth
                        });
                    }
                }

                currentUnitInfo.UpdateTime = DateTime.Now;
            }

            // There is no record yet
            else
            {
                var newUnitInfo = new UnitConnectionsUnits(unit);

                // Check all connections for toUnit existance
                var i = 0;
                while (i < connections.Count)
                {
                    var currentToUnitInfo = networkTopology.Find(
                        unitInfo => unitInfo.UnitIndex == connections[i].ToUnit
                    );

                    // Ignore connection, when there is no such toUnit record
                    if (currentToUnitInfo == null)
                    {
                        connections.RemoveAt(i);
                        continue;
                    }

                    // Check, whether there is an appropriate connection record
                    var toMyUnitConn = currentToUnitInfo.Connections.Find(
                        c => c.ToUnit == unit
                    );

                    if (toMyUnitConn == null)
                    {
                        // Add new connection
                        currentToUnitInfo.Connections.Add(new ToUnitConnection()
                        {
                            ToUnit = unit,
                            BandWidth = connections[i].BandWidth
                        });
                    }

                    i++;
                }

                // Create list of connections
                newUnitInfo.Connections = connections;

                // Update time of the record
                newUnitInfo.UpdateTime = DateTime.Now;

                // Add new record to units' list
                networkTopology.Add(newUnitInfo);
            }
        }

        /// <summary>
        /// Converts list of bytes into a list of possible destinations and channel's bandwidth
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public List<ToUnitConnection> GetConnectionsFromBytes(List<byte> list)
        {
            if (list.Count % 8 != 0)
                return null;
            
            var connections = new List<ToUnitConnection>();

            while (list.Count > 0)
            {
                var toUnit = MakeIntFromBytes(list.Take(4).ToList());
                var bandWidth = MakeIntFromBytes(list.Skip(4).Take(4).ToList());
                connections.Add(new ToUnitConnection() {ToUnit = toUnit, BandWidth = bandWidth});

                list.RemoveRange(0, 8);
            }

            return connections;
        }

        /// <summary>
        /// Converts ToUnitConnection instance into a list of 8 bytes
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        private List<byte> GetBytesFromConnection(ToUnitConnection connection)
        {
            var bytes = new List<byte>
            {
                GetFirstByte(connection.ToUnit),
                GetSecondByte(connection.ToUnit),
                GetThirdByte(connection.ToUnit),
                GetFourthByte(connection.ToUnit),
                GetFirstByte(connection.BandWidth),
                GetSecondByte(connection.BandWidth),
                GetThirdByte(connection.BandWidth),
                GetFourthByte(connection.BandWidth)
            };
            
            return bytes;
        }

        /// <summary>
        /// Makes a list of bytes from list of all unit's connections information
        /// </summary>
        /// <param name="connections"></param>
        /// <returns></returns>
        public List<byte> MakeStatusData(List<ToUnitConnection> connections)
        {
            var connectionsInBytes = new List<byte>();

            foreach (var conn in connections)
                connectionsInBytes.AddRange(GetBytesFromConnection(conn));

            return connectionsInBytes;
        }

        #endregion

        #region Routing

        /// <summary>
        /// Finds all possible routes for this unit anew
        /// </summary>
        private void RebuildRoutes()
        {
            // Null route list
            _routes = new List<DestinationNext>();

            // Fill route list with possible destinations
            foreach (var unitsInfo in networkTopology)
            {
                var destination = unitsInfo.UnitIndex;
                if (destination != _myUnitIndex)
                {
                    _routes.Add(new DestinationNext() {DestUnitIndex = destination});
                }
            }

            // Find best routes to each destination
            var passedUnits = new List<int>() {_myUnitIndex};

            var myConnections = networkTopology.Find(u => u.UnitIndex == _myUnitIndex)?.Connections;
            if (myConnections == null)
            {
                return;
            }

            foreach (var conn in myConnections)
            {
                GoThroughAllRoutes(conn.ToUnit, passedUnits, 1, conn.BandWidth);
            }
        }

        /// <summary>
        /// Recursive pass through all graph's units
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="passedUnits"></param>
        /// <param name="routeLen"></param>
        /// <param name="maxWidth"></param>
        private void GoThroughAllRoutes(int unit, List<int> passedUnits, int routeLen, int maxWidth)
        {
            // Add current unit
            passedUnits.Add(unit);

            // Write my route, if best
            var myDestination = _routes.Find(dest => dest.DestUnitIndex == unit);

            // Shorter length
            if (myDestination.Length > routeLen)
            {
                myDestination.NextUnitIndexL = passedUnits[1];
                myDestination.Length = routeLen;
            }

            // Better bandwidth
            if (myDestination.Width > maxWidth)
            {
                myDestination.NextUnitIndexW = passedUnits[1];
                myDestination.Width = maxWidth;
            }

            // Go down through children
            var myConnections = networkTopology.Find(u => u.UnitIndex == _myUnitIndex).Connections;
            foreach (var conn in myConnections)
            {
                var childUnit = conn.ToUnit;

                // If we have checked that unit before, ignore it
                if (passedUnits.Exists(u => u == childUnit)) continue;

                var newWidth = conn.BandWidth;
                GoThroughAllRoutes(childUnit, passedUnits, routeLen + 1, newWidth > maxWidth ? newWidth : maxWidth);
            }

            // Remove current unit
            passedUnits.Remove(unit);
        }

        public List<int> GetDestinations()
        {
            return _routes.Select(route => route.DestUnitIndex).ToList();
        }

        #endregion
    }
}