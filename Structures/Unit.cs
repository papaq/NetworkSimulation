using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;

namespace NetworksCeW.Structures
{
    /// <summary>
    /// Unit structure, describes all information about it
    /// </summary>
    public class Unit
    {
        public int Index;
        public Point Position;
        public int Buffer;
        public bool Disabled;

        public List<int> ListBindsIndexes = new List<int>();
        //private List<BufferItem> listBuffers = new List<BufferItem>();
        

        public Unit()
        {
            ListBindsIndexes = new List<int>();
        }

        public Unit(int index, double x, double y, int buff, List<int> bindsIndexes, bool disabled)
        {
            Index = index;
            Position = new Point(x, y);
            Buffer = buff;
            ListBindsIndexes = new List<int>();
            foreach (var ind in bindsIndexes)
                ListBindsIndexes.Add(ind);
            Disabled = disabled;
        }

        public void Copy(Unit unit)
        {
            Index = unit.Index;
            Position = new Point(unit.Position.X, unit.Position.Y);
            Buffer = unit.Buffer;
            foreach (var index in unit.ListBindsIndexes)
                ListBindsIndexes.Add(index);
            Disabled = unit.Disabled;
        }

        public void AddBind(Bind newBind)
        {
            ListBindsIndexes.Add(newBind.Index);
        }
        
        public List<int> GetListOfConnectedUnitsIndexes(List<Bind> listOfBinds)
        {
            var listOfCoUnits = new List<int>();
            foreach (var bind in listOfBinds.Where(bind => bind.ListOfBothUnitsIndexes.Contains(Index)))
                listOfCoUnits.Add(bind.GetSecondUnitIndex(Index));
            return listOfCoUnits;
        }

        public string ConnectsIndexes(List<Bind> listOfBinds)
        {
            if (ListBindsIndexes.Count == 0)
            {
                return "No yet";
            }
            var listOfCoUnitsIndexes = GetListOfConnectedUnitsIndexes(listOfBinds);
            var strOfIndexes = "";
            foreach (var index in listOfCoUnitsIndexes)
                strOfIndexes += index + ", ";
            return strOfIndexes.Remove(strOfIndexes.Length - 2);
        }
    }
}
