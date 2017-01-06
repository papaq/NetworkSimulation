using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworksCeW.Routing
{
    internal class YouCanGetThere
    {
        public int FromUnitIndex ;
        public List<Direction> Directions = new List<Direction>();

        public YouCanGetThere(int unit)
        {
            FromUnitIndex = unit;
        }
    }
}
