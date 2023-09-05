using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwinLand
{
    /// <summary>
    /// global variable in grasshopper
    /// </summary>
    public sealed class TwinLandSRS
    {
        private static TwinLandSRS _instance;
        
        private TwinLandSRS(){}

        public static TwinLandSRS Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TwinLandSRS();
                }
                return _instance;
            }
        }

        public string SRS { get; set; } = "WGS84";
    }
}