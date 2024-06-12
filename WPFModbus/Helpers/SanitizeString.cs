using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFModbus.Helpers
{
    public class SanitizeString
    {
        public static string ReplaceControl(string input, string replacment = " . ")
        {
            StringBuilder sanitized = new(input.Length);
            foreach (var c in input)
                sanitized.Append(
                    char.IsControl(c)
                    ? replacment
                    : c
                );
            return sanitized.ToString();
        }
        public static string ReplaceControl(byte[] data, string replacment = " . ")
        {
            StringBuilder sanitized = new(data.Length);
            foreach (var c in data)
                sanitized.Append(
                    char.IsControl((char)c) || c > 127
                    ? replacment
                    : (char)c
                );
            return sanitized.ToString();
        }
    }
}
