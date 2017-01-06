using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Xml.Serialization;
using System.Windows;
using NetworksCeW.Structures;

namespace NetworksCeW.File
{
    internal static class FileBackup
    {
        //private const string ListOfUnitFile = "..\\Recover\\Units.neo";
        //private const string ListOfBindsFile = "../Recover/Binds.neo";

        private static readonly string[] FileNames =
            {
            "Mangrove",
            "Lagoon",
            "Nomuras",
            "Sea_Nettle",
            "Upside_Down",
            "Comb",
            "Sand",
            "Box",
            "Sea_Wasp",
            "Blue_Blubber",
            "White_Spotted",
            "Turritopsis",
            "Nutricula",
            "Chironex",
            "Pelagia",
            "Moon_Light",
            "Irukandji",
            "Moon",
            "Aurelia",
            "Aurelia",
            "Ball",
            "Cannonball",
            "Man_of_War",
            "War",
            "Blue_Bottle",
            "Lions_Mane",
            "Mane",
            "Sun",
            "Square",
            "Physalia",
            "King",
            "Cassiopeia"
        };

        public static List<Unit> ListOfUnits = new List<Unit>();
        public static List<Bind> ListOfBinds = new List<Bind>();

        public static void ReadAll(string path, string fileUnt, string fileBnd)
        {
            ListOfUnits = (List<Unit>)ReadFromFile<List<Unit>>(path, fileUnt);
            ListOfBinds = (List<Bind>)ReadFromFile<List<Bind>>(path, fileBnd);

        }

        public static void WriteAll(string path, string fileUnt, string fileBnd)
        {
            WriteToFile(ListOfUnits, path, fileUnt);
            WriteToFile(ListOfBinds, path, fileBnd);
        }

        public static string FindUniqueName(string[] names)
        {
            var i = 0;
            var rnd = new Random();
            while (i < FileNames.Length)
            {
                var name = FileNames[rnd.Next(0, FileNames.Length)];
                if (!names.Contains(name))
                    return name;
                i++;
            }
            return null;
        }

        private static void WriteToFile<T>(T list, string path, string file)
        {
            Directory.CreateDirectory(path);

            var serializer = new XmlSerializer(typeof(T));
            FileStream stream;

            try
            {
                stream = System.IO.File.OpenWrite(path + "\\" + file);
            }
            catch
            {
                return;
            }

            serializer.Serialize(stream, list);

            stream.Close();
        }

        private static object ReadFromFile<T>(string path, string file)
        {
            var serializer = new XmlSerializer(typeof(T));
            FileStream stream;

            try
            {
                stream = System.IO.File.OpenRead(path + "\\" + file);
            }
            catch
            {
                return null;
            }
            var retVal = (T)serializer.Deserialize(stream);

            stream.Close();

            return retVal;
        }

        public static void PreCoverUnitsResize(double width,
            double minWidth, double height, double minHeight)
        {
            var reX = minWidth / width;
            var reY = minHeight / height;

            foreach (var unit in ListOfUnits)
            {
                var newX = unit.Position.X * reX;
                var newY = unit.Position.Y * reY;
                unit.Position = new Point(newX, newY);
            }
        }

        public static void PreCoverBindsResize(double width,
            double minWidth, double height, double minHeight)
        {
            var reX = minWidth / width;
            var reY = minHeight / height;

            foreach (var bind in ListOfBinds)
            {
                var newX = bind.A.X * reX;
                var newY = bind.A.Y * reY;
                bind.A = new Point(newX, newY);

                newX = bind.B.X * reX;
                newY = bind.B.Y * reY;
                bind.B = new Point(newX, newY);
            }
        }
    }
}
