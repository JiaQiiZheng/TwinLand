using System;

namespace TwinLand
{
    public class Helper
    {
        public static bool isWindows
        {
            get
            {
                bool res = !(Environment.OSVersion.Platform == PlatformID.Unix ||
                             Environment.OSVersion.Platform == PlatformID.MacOSX);
                return res;
            }
        }
    }
}