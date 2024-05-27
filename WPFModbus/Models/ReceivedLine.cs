using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFModbus.Models
{
    public class ReceivedLine
    {
        public ulong    Id         { get; set; }
        public DateTime DateTime   { get; set; }
        public string   TimeString => DateTime.ToString("HH:mm:ss.fff");
        public byte[]   Data       { get; set; }
        public string   DataString => String.Join(' ', Data);
        public string   Text => Encoding.ASCII.GetString(Data);
        
        public ReceivedLine(ulong id, DateTime dateTime, byte[] data) 
        {
            Id = id;
            DateTime = dateTime;
            Data = data;
        }
    }
}
