using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFModbus.Models
{
    public class Parity
    {
        public string Name { get; set; }
        public string Code { get; set; }

        public Parity(string name, string code) 
        {
            Name = name;
            Code = code;
        }

        public override string ToString()
        {
            return Name;
        }

        public readonly static List<Parity> Types = [
            new Parity("нет"     , "none"),
            new Parity("нечётное", "odd"),
            new Parity("чётное"  , "even"),
            new Parity("марк"    , "mark"),
            new Parity("пропуск" , "space"),
        ];
    }
}
