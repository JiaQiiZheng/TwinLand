using System.Collections.Generic;

namespace TwinLand.Utils
{
    public static class GlobalConsole
    {
        static List<string> lines = new List<string>();

        public static void WriteLine(string line)
        {
            lines.Add(line);
        }

        public static List<string> Read() => lines;

        public static void Clear()
        {
            lines.Clear();
        }
    }
}