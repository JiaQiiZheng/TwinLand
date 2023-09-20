using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows.Forms;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;

namespace TwinLand
{
  public class GetOpenStreetMap : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public GetOpenStreetMap()
      : base("GetOpenStreetMap", "GetOSM",
        "Get OSM file from OpenStreetMap Overpass API ", "Data Collection")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddCurveParameter("Boundary", "boundary", "The framework of download area for OpenStreetMap API",
        GH_ParamAccess.item);
      pManager.AddTextParameter("TargetFolder", "targetFolder", "The folder path of the downloaded .osm file located",
        GH_ParamAccess.item, Path.GetTempPath());
      pManager.AddTextParameter("FileName", "fileName", "The file name of the downloaded .osm file", GH_ParamAccess.item, OSMSource);
      pManager.AddTextParameter("OSMTag_Key", "OSMTag_Key", "Optional indicate specific layer in OpenStreetMap dataset",
        GH_ParamAccess.item);
      pManager.AddTextParameter("OverpassQueryLanguage", "overpassQL",
        "Code applying onto OpenStreetMap data fetching process", GH_ParamAccess.item);
      pManager.AddBooleanParameter("Run", "run", "Start to download", GH_ParamAccess.item, false);

      pManager[0].Optional = true;
      pManager[2].Optional = true;
      pManager[3].Optional = true;
      pManager[4].Optional = true;

      Message = OSMSource;
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddTextParameter("OpenStreetMapFile", "osmFile", "Downloaded OpenStreetMapFile", GH_ParamAccess.item);
      pManager.AddTextParameter("OpenStreetMapQuery", "osmQuery", "Query came back from OSM server",
        GH_ParamAccess.item);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      Curve boundary = null;
      DA.GetData("Boundary", ref boundary);
      
      string targetFolder = string.Empty;
      DA.GetData("TargetFolder", ref targetFolder);
      if (Helper.isWindows && !targetFolder.EndsWith(@"\"))
      {
        targetFolder += @"\";
      }
      else if (!Helper.isWindows && !targetFolder.EndsWith(@"/"))
      {
        targetFolder += @"/";
      }
      
      string fileName = string.Empty;
      DA.GetData("FileName", ref fileName);
      if (string.IsNullOrEmpty(fileName))
      {
        fileName = osmSource;
      }
      
      string OSMTag_Key = string.Empty;
      DA.GetData("OSMTag_Key", ref OSMTag_Key);
      if (!string.IsNullOrEmpty(OSMTag_Key))
      {
        OSMTag_Key = System.Net.WebUtility.UrlEncode($"[{OSMTag_Key}]");
      }
      
      string overpassQL = string.Empty;
      DA.GetData("OverpassQueryLanguage", ref overpassQL);
      
      bool run = false;
      if(!DA.GetData("Run", ref run)) return;
      
      int timeout = 60;
      
      string URL = osmURL;
      
      GH_Structure<GH_String> osmList = new GH_Structure<GH_String>();
      GH_Structure<GH_String> osmQuery = new GH_Structure<GH_String>();
      
      string oq = string.Empty;
      
      string left = string.Empty;
      string bottom = string.Empty;
      string right = string.Empty;
      string top = string.Empty;
      
      /// get query with bounding box if exist
      if (boundary != null)
      {
        // check if the bounding box is valid
        BoundingBox boundingBox = boundary.GetBoundingBox(true);
        if (!boundingBox.IsValid)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Boundary is not valid");
          return;
        }
      
        // get OSM frome from given Boundary
        Point3d min = TwinLand.Convert.XYZToWGS(boundingBox.Min);
        Point3d max = TwinLand.Convert.XYZToWGS(boundingBox.Max);
      
        left = min.X.ToString();
        bottom = min.Y.ToString();
        right = max.X.ToString();
        top = max.Y.ToString();
      
        if (!String.IsNullOrEmpty(overpassQL))
        {
          string bbox = $"({bottom},{left},{top},{right})";
          overpassQL = overpassQL.Replace("{bbox}", bbox);
          oq = $"{osmURL.Split('=')[0]}={overpassQL}";
          osmQuery.Append(new GH_String(oq));
          DA.SetDataTree(1, osmQuery);
        }
        else
        {
          oq = Convert.GetOSMURL(timeout, OSMTag_Key, left, bottom, right, top, osmURL);
          osmQuery.Append(new GH_String(oq));
          DA.SetDataTree(1, osmQuery);
        }
      }
      
      /// get query with Overpass QL
      else if (!string.IsNullOrEmpty(overpassQL) && boundary == null)
      {
        oq = $"{osmURL.Split('=')[0]}={overpassQL}";
        osmQuery.Append(new GH_String(oq));
        DA.SetDataTree(1, osmQuery);
      }
      
      /// start to download
      if (run)
      {
        WebClient wc = new WebClient();
        wc.DownloadFile(oq, targetFolder + fileName + ".osm");
        wc.Dispose();
      }
      
      osmList.Append(new GH_String(targetFolder + fileName + ".osm"));
      
      /// output downloaded file path
      DA.SetDataTree(0, osmList);
    }

    /// <summary>
    /// add function of selecting service from component menu 
    /// </summary>
    /// <param name="serviceString"></param>
    /// <returns></returns>
    private bool IsServiceSelected(string serviceString)
    {
      return serviceString.Equals(osmSource);
    }
    
    protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
    {
      if (osmSourceList == "")
      {
        osmSourceList = TwinLand.Convert.GetEndpoints();
      }
    
      JObject osmJson = JObject.Parse(osmSourceList);
      ToolStripMenuItem root = new ToolStripMenuItem("Pick OSM vector service");
    
      foreach (var service in osmJson["OSM Vector"])
      {
        string sName = service["service"].ToString();
    
        ToolStripMenuItem serviceName = new ToolStripMenuItem(sName);
        serviceName.Tag = sName;
        serviceName.Checked = IsServiceSelected(sName);
        serviceName.ToolTipText = service["description"].ToString();
        serviceName.Click += ServiceItemOnClick;
    
        root.DropDownItems.Add(serviceName);
      }
    
      menu.Items.Add(root);
      
      base.AppendAdditionalComponentMenuItems(menu);
    }
    
    private void ServiceItemOnClick(object sender, EventArgs e)
    {
      ToolStripMenuItem item = sender as ToolStripMenuItem;
      if (item == null) return;
      string code = (string)item.Tag;
      if (IsServiceSelected(code)) return;
    
      RecordUndoEvent("OSMSource");
      RecordUndoEvent("OSMURL");
    
      osmSource = code;
      osmURL = JObject.Parse(osmSourceList)["OSM Vector"].SelectToken("[?(@.service == '" + osmSource + "')].url")
        .ToString();
      Message = osmSource;
      
      ExpireSolution(true);
    }

    /// <summary>
    /// Dynamic variables
    /// </summary>
    private string osmSourceList = TwinLand.Convert.GetEndpoints();
    private string osmSource = JObject.Parse(TwinLand.Convert.GetEndpoints())["OSM Vector"][0]["service"].ToString();
    private string osmURL = JObject.Parse(TwinLand.Convert.GetEndpoints())["OSM Vector"][0]["url"].ToString();

    public string SlippySourceList
    {
      get { return osmSourceList; }
      set
      {
        osmSourceList = value;
      }
    }
    
    public string OSMSource
    {
      get { return osmSource; }
      set
      {
        osmSource = value;
        Message = osmSource;
      }
    }
    
    public string OSMURL
    {
      get { return osmURL; }
      set
      {
        osmURL = value;
      }
    }
    
    public override bool Write(GH_IO.Serialization.GH_IWriter writer)
    {
      writer.SetString("OSMService", OSMSource);
      return base.Write(writer);
    }
    
    public override bool Read(GH_IO.Serialization.GH_IReader reader)
    {
      OSMSource = reader.GetString("OSMService");
      return base.Read(reader);
    }

    /// <summary>
    /// Provides an Icon for every component that will be visible in the User Interface.
    /// Icons need to be 24x24 pixels.
    /// </summary>
    protected override System.Drawing.Bitmap Icon
    {
      get
      { 
        // You can add image files to your project resources and access them like this:
        //return Resources.IconForThisComponent;
        return Properties.Resources.T_icon;
      }
    }

    /// <summary>
    /// Each component must have a unique Guid to identify it. 
    /// It is vital this Guid doesn't change otherwise old ghx files 
    /// that use the old ID will partially fail during loading.
    /// </summary>
    public override Guid ComponentGuid
    {
      get { return new Guid("8d30ffd1-ab9a-4441-881b-4f55703132d8"); }
    }
  }
}