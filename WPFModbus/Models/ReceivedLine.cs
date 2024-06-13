using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFModbus.Helpers;

namespace WPFModbus.Models
{
    public class ReceivedLine(/*ulong id, DateTime dateTime, byte[] data, Encoding? encoding = null*/)
    {
        /*
        public ulong    Id       { get; set; } = id;
        public DateTime DateTime { get; set; } = dateTime;
        public byte[]   Data     { get; set; } = data;
        public Encoding Encoding { get; set; } = encoding ?? Encoding.ASCII;

        public string   IdString => Id.ToString("D5");
        public string TimeString => DateTime.ToString("HH:mm:ss.fff");
        public string DataString => BitConverter.ToString(Data).Replace('-', ' ');
        public string Text       => SanitizeString.ReplaceControl(Encoding.GetString(Data));
        */

        public string?   Id { get; set; }
        public string? Time { get; set; }
        public string? Data { get; set; }
        public string? Text { get; set; }
    }
}
