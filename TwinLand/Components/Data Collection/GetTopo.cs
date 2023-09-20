using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows.Forms;

using Grasshopper;
using GH_IO;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.Geometry;

namespace TwinLand
{
  public class GetTopo : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public GetTopo()
      : base("GetTopo", "GetTopo",
        "Get Topography based on DEM data from services ", "Data Collection")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddCurveParameter("Boundary", "boundary", "The download area of DEM file", GH_ParamAccess.list);
      pManager.AddTextParameter("TargetFolder", "targetFolder",
        "The target folder used to place the downloaded DEM file", GH_ParamAccess.item, Path.GetTempPath());
      pManager.AddTextParameter("FileName", "fileName", "The file name of downloaded DEM file", GH_ParamAccess.item);
      pManager.AddBooleanParameter("Run", "run", "Start to download the DEM file from the server", GH_ParamAccess.item, false);

      pManager[2].Optional = true;

      Message = source_DEM;
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddTextParameter("DEM_FilePath", "DEM", "The file path of downloaded DEM file", GH_ParamAccess.tree);
      pManager.AddTextParameter("TopoQuery", "TopoQuery", "The response query from DEM file download process",
        GH_ParamAccess.tree);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      List<Curve> boundary = new List<Curve>();
      DA.GetDataList<Curve>("Boundary", boundary);

      string folderPath = string.Empty;
      DA.GetData("TargetFolder", ref folderPath);
      if (Helper.isWindows && !folderPath.EndsWith(@"\"))
      {
        folderPath += @"\";
      }
      else if (!Helper.isWindows && !folderPath.EndsWith(@"/"))
      {
        folderPath += @"/";
      }

      string fileName = string.Empty;
      DA.GetData("FileName", ref fileName);
      if (string.IsNullOrEmpty(fileName))
      {
        fileName = source_DEM;
      }

      bool run = false;
      DA.GetData("Run", ref run);

      GH_Structure<GH_String> demList = new GH_Structure<GH_String>();
      GH_Structure<GH_String> demQuery = new GH_Structure<GH_String>();

      for (int i = 0; i < boundary.Count; i++)
      {
        GH_Path path = new GH_Path(i);

        // get DEM file based on input valid boundary
        if (!boundary[i].GetBoundingBox(true).IsValid)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid boundary exist.");
          return;
        }

        double distance = 200 * Rhino.RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);
        Curve offsetBoundary = boundary[i].Offset(Rhino.Geometry.Plane.WorldXY, distance, 1, CurveOffsetCornerStyle.Sharp)[0];

        Point3d min = TwinLand.Convert.XYZToWGS(offsetBoundary.GetBoundingBox(true).Min);
        Point3d max = TwinLand.Convert.XYZToWGS(offsetBoundary.GetBoundingBox(true).Max);

        double left = min.X;
        double bottom = min.Y;
        double right = max.X;
        double top = max.Y;

        string topoQuery = String.Format(dem_url, left, bottom, right, top);

        // prepare fileFullPath for download or load purpose
        string fileFullPath = $"{folderPath}{fileName}_{i}.tif";

        if (run)
        {
          WebClient wb = new WebClient();
          wb.DownloadFile(topoQuery, fileFullPath);
          wb.Dispose();
        }
        
        demList.Append(new GH_String(fileFullPath), path);
        demQuery.Append(new GH_String(topoQuery), path);
      }

      DA.SetDataTree(0, demList);
      DA.SetDataTree(1, demQuery);
    }

    /// <summary>
    /// additional menu items
    /// </summary>
    /// <param name="serviceString"></param>
    /// <returns></returns>
    private bool IsServiceSelected(string serviceString)
    {
      return serviceString.Equals(source_DEM);
    }

    protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
    {
      if (string.IsNullOrEmpty(sourceList_DEM))
      {
        sourceList_DEM = TwinLand.Convert.GetEndpoints();
      }

      JObject jsonSourceObject = JObject.Parse(sourceList_DEM);
      foreach (var service in jsonSourceObject["REST Topo"])
      {
        string sName = service["service"].ToString();

        ToolStripMenuItem serviceItem = new ToolStripMenuItem(sName);
        serviceItem.Tag = sName;
        serviceItem.Checked = IsServiceSelected(sName);
        serviceItem.ToolTipText = service["description"].ToString();
        serviceItem.Click += ServiceItemOnClicks;

        menu.Items.Add(serviceItem);
      }
      
      base.AppendAdditionalComponentMenuItems(menu);
    }

    private void ServiceItemOnClicks(object sender, EventArgs e)
    {
      ToolStripMenuItem item = sender as ToolStripMenuItem;
      if (item == null) return;

      string code = (string)item.Tag;
      if(IsServiceSelected(code)){return;}

      RecordUndoEvent("source_DEM");

      source_DEM = code;
      //TODO. study more about JSONPath expression.
      dem_url = JObject.Parse(SourceList_DEM)["REST Topo"].SelectToken("[?(@.service == '" + Source_DEM + "')].url").ToString();
      Message = source_DEM;
      
      ExpireSolution(true);
    }


    /// <summary>
    /// Dynamic Variables
    /// </summary>
    private string sourceList_DEM = TwinLand.Convert.GetEndpoints();
    private string source_DEM = JObject.Parse(TwinLand.Convert.GetEndpoints())["REST Topo"][0]["service"].ToString();
    private string dem_url = JObject.Parse(TwinLand.Convert.GetEndpoints())["REST Topo"][0]["url"].ToString();

    public string SourceList_DEM
    {
      get { return sourceList_DEM; }
      set { sourceList_DEM = value; }
    }
    
    public string Source_DEM
    {
      get { return source_DEM; }
      set
      {
        source_DEM = value;
        Message = source_DEM;
      }
    }

    public string DEM_URL
    {
      get { return dem_url; }
      set { dem_url = value; }
    }

    public override bool Write(GH_IO.Serialization.GH_IWriter writer)
    {
      writer.SetString("Source_DEM", Source_DEM);
      return base.Write(writer);
    }

    public override bool Read(GH_IO.Serialization.GH_IReader reader)
    {
      Source_DEM = reader.GetString("Source_DEM");
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
      get { return new Guid("4147d23d-db2f-4692-bce2-190aea4d3f59"); }
    }
  }
}