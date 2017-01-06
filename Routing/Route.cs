using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworksCeW.Routing
{
    internal class Route
    {
        public int RouteWeight { get; set; }

        public List<int> ListOfStops = new List<int>();

        public Route(int weight, List<int> lst)
        {
            RouteWeight = weight;
            ListOfStops.AddRange(lst);
        }
    }
}
