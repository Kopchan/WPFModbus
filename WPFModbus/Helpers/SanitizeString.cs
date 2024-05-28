using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFModbus.Helpers
{
    public class SanitizeString
    {
        public static string Sanitize(string input)
        {
            StringBuilder sanitized = new (input.Length);
            foreach (var c in input)
                sanitized.Append(
                    char.IsControl(c) || c > 127
                    ? " . " 
                    : c
                );
            return sanitized.ToString();
        }
        public static string Sanitize(byte[] data)
        {
            StringBuilder sanitized = new(data.Length);
            foreach (var c in data)
                sanitized.Append(
                    char.IsControl((char)c) || c > 127
                    ? " . " 
                    : (char)c
                );
            return sanitized.ToString();
        }
    }
}
