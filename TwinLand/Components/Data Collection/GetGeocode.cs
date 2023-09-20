using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;
using TwinLand.Properties;
using TwinLand.Utils;

namespace TwinLand
{
    public class GetGeocode : TwinLandComponent
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public GetGeocode()
            : base("GetGeocode", "GetGeocode",
                "GetGeocode",
                 "Data Collection")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Addresses", "addresses",
                "convert POI or addresses to GetGeocode by ESRI service", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Candidates", "candidates", "List of candidate locations", GH_ParamAccess.tree);
            pManager.AddTextParameter("Latitude", "latitude", "Latitude of candidate locations",
                GH_ParamAccess.tree);
            pManager.AddTextParameter("Longitude", "longitude", "Longitude of candidate locations",
                GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_String> Addresses = new GH_Structure<GH_String>();
            DA.GetDataTree<GH_String>("Addresses", out Addresses);

            GH_Structure<GH_String> addr = new GH_Structure<GH_String>();
            GH_Structure<GH_String> latx = new GH_Structure<GH_String>();
            GH_Structure<GH_String> lony = new GH_Structure<GH_String>();

            for (int i = 0; i < Addresses.Branches.Count; i++)
            {
                IList branch = Addresses.Branches[i];
                GH_Path path = Addresses.Paths[i];
                int count = 0;

                foreach (GH_String addressString in branch)
                {
                    string address = WebUtility.UrlEncode(addressString.Value);
                    
                    string output =
                        GetData(
                            "https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/findAddressCandidates?Address=" +
                            address + "&f=pjson");
                    JObject ja = JObject.Parse(output);

                    GlobalConsole.WriteLine(output);

                    if (ja["candidates"].Count() < 1)
                    {
                        addr.Append(new GH_String("No candidate location found for this address"), path);
                        latx.Append(new GH_String(""), path);
                        lony.Append(new GH_String(""), path);
                    }
                    else
                    {
                        for (int j = 0; j < ja["candidates"].Count(); j++)
                        {
                            if (ja["candidates"][j]["score"].Value<int>() > 99)
                            {
                                addr.Append(new GH_String(ja["candidates"][j]["address"].ToString()),
                                    new GH_Path(path[count], j));
                                addr.Append(new GH_String($"LON: {ja["candidates"][j]["location"]["x"].ToString()}"),
                                    new GH_Path(path[count], j));
                                addr.Append(new GH_String($"LAT: {ja["candidates"][j]["location"]["y"].ToString()}"),
                                    new GH_Path(path[count], j));
                                lony.Append(new GH_String(ja["candidates"][j]["location"]["y"].ToString()),
                                    new GH_Path(path[count], j));
                                latx.Append(new GH_String(ja["candidates"][j]["location"]["x"].ToString()),
                                    new GH_Path(path[count], j));
                            }
                        }
                    }
                }
            }
            
            if (addr == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No candidate location found");
                return;
            }
            else
            {
                DA.SetDataTree(0, addr);
                DA.SetDataTree(1, lony);
                DA.SetDataTree(2, latx);
            }
        }

        public static string GetData(string qst)
        {
            HttpWebRequest req = WebRequest.Create(qst) as HttpWebRequest;
            string result = null;
            try
            {
                using (HttpWebResponse resp = req.GetResponse() as HttpWebResponse)
                {
                    StreamReader reader = new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                    reader.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return result;
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.T_icon;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("C01E7B40-7CBD-4D24-A316-86589B1A53B2");
    }
}