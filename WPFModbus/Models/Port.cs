using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFModbus.Models
{
    public class Port
    {
        public string Name        { get; set; }
        public bool   IsAvaliable { get; set; } = false;

        public Port(string name, bool isAvaliable)
        {
            Name = name;
            IsAvaliable = isAvaliable;
        }

        public override string ToString() => Name;
    }
}
