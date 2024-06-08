using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFModbus.Models
{
    public class Parity(string name, string code)
    {
        public string Name { get; set; } = name;
        public string Code { get; set; } = code;

        public override string ToString() => Name;

        public readonly static List<Parity> Types = 
        [
            new Parity("нет"     , "none"),
            new Parity("нечётное", "odd"),
            new Parity("чётное"  , "even"),
            new Parity("марк"    , "mark"),
            new Parity("пропуск" , "space"),
        ];
    }
}
