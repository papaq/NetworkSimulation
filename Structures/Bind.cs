using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;

namespace NetworksCeW.Structures
{
    public class Bind
    {
        public Point A;
        public Point B;

        public List<int> ListOfBothUnitsIndexes = new List<int>();
        public int Index;
        public bool Disabled;

        public int Weight { get; set; }

        public bool Satellite { get; set; }

        public bool Duplex { get; set; }


        public Bind()
        {
            
        }

        public Bind(Bind bind)
        {
            A = new Point(bind.A.X, bind.A.Y);
            B = new Point(bind.B.X, bind.B.Y);
            foreach (var index in bind.ListOfBothUnitsIndexes)
                ListOfBothUnitsIndexes.Add(index);
            Index = bind.Index;
            Weight = bind.Weight;
            Satellite = bind.Satellite;
            Duplex = bind.Duplex;
        }

        public bool ConnectsUnit(int unitIndex)
        {
            return ListOfBothUnitsIndexes.Contains(unitIndex);
        }

        public void TurnLine(Line lineConnection, Point oldPoint, Point newPoint)
        {
            if (A == oldPoint)
                A = newPoint;
            else
                B = newPoint;

            if (Math.Abs(lineConnection.X1 - oldPoint.X) < 0.1 && Math.Abs(lineConnection.Y1 - oldPoint.Y) < 0.1)
            {
                lineConnection.X1 = newPoint.X;
                lineConnection.Y1 = newPoint.Y;
            }
            else
            {
                lineConnection.X2 = newPoint.X;
                lineConnection.Y2 = newPoint.Y;
            }
        }
        
        public int GetSecondUnitIndex(int firstUnitIndex)
        {
            return ListOfBothUnitsIndexes[0] == firstUnitIndex ? ListOfBothUnitsIndexes[1] : ListOfBothUnitsIndexes[0];
        }
    }
}
