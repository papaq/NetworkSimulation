using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworksCeW.Routing
{
    internal class Direction
    {
        public int ToUnitIndex;

        public List<Route> Routes = new List<Route>();

        public Direction(int idx)
        {
            ToUnitIndex = idx;
        }
    }
}
