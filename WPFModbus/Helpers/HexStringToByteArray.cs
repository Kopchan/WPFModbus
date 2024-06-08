using System.Text.RegularExpressions;

namespace WPFModbus.Helpers
{
    public class HexStringToByteArray
    {
        public static byte[] Convert(string hex)
        {
            hex = Regex.Replace(
                hex.Replace("0x", ""),
                "[^0-9A-Fa-f]",
                ""
            );
            if (hex.Length % 2 != 0) hex = hex.Insert(0, "0");
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => System.Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}
