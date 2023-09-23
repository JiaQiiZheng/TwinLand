using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Special;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Collections;
using GH_IO;
using GH_IO.Serialization;

using Newtonsoft.Json.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Serialization;
using OSGeo.OGR;

namespace TwinLand
{
  public class RESTGetRaster : TwinLandRasterPreviewComponent
  {
    /// <summary>fin
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments. 
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public RESTGetRaster()
      : base("RESTGetRaster", "RESTGetRaster",
        "Get raster from GIS REST service", "REST")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddCurveParameter("Boundary", "boundary", "Boundary curve(s) for imagery", GH_ParamAccess.list);
      pManager.AddIntegerParameter("Resolution", "resolution", "Maximum resolution for images", GH_ParamAccess.item,1024);
      pManager.AddTextParameter("Target Folder", "folderPath", "Folder to save image files", GH_ParamAccess.item, Path.GetTempPath());
      pManager.AddTextParameter("Prefix", "prefix", "Prefix for image file name", GH_ParamAccess.item, "restRaster");
      pManager.AddTextParameter("REST URL", "URL", "ArcGIS REST Service website to query. Use the component \nmenu item \"Create REST Raster Source List\" for some examples.", GH_ParamAccess.item);
      pManager.AddBooleanParameter("run", "get", "Go ahead and download imagery from the Service", GH_ParamAccess.item, false);

      pManager.AddTextParameter("User Spatial Reference System", "userSRS", "Custom SRS", GH_ParamAccess.item,"WGS84");
      pManager.AddTextParameter("Image Type", "imageType", "Image file type to download from the service.", GH_ParamAccess.item, "jpg");
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddTextParameter("Image", "Image", "File location of downloaded image", GH_ParamAccess.tree);
      pManager.AddCurveParameter("Image Frame", "imageFrame", "Bounding box of image for mapping to geometry", GH_ParamAccess.tree);
      pManager.AddTextParameter("REST Query", "RESTQuery", "Full text of REST query", GH_ParamAccess.tree);
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

            int Res = -1;
            DA.GetData<int>("Resolution", ref Res);

            string folderPath = string.Empty;
            DA.GetData<string>("Target Folder", ref folderPath);
            if (!folderPath.EndsWith(@"\")) { folderPath = folderPath + @"\"; }

            string prefix = string.Empty;
            DA.GetData<string>("Prefix", ref prefix);

            string URL = string.Empty;
            DA.GetData<string>("REST URL", ref URL);
            if (URL.EndsWith(@"/")) { URL = URL + "export?"; }

            bool run = false;
            DA.GetData<bool>("run", ref run);

            string userSRStext = string.Empty;
            DA.GetData<string>("User Spatial Reference System", ref userSRStext);

            string imageType = string.Empty;
            DA.GetData<string>("Image Type", ref imageType);

            ///GDAL setup
            RESTful.GdalConfiguration.ConfigureOgr();

            ///TODO: implement SetCRS here.
            ///Option to set CRS here to user-defined.  Needs a SetCRS global variable.
            //string userSRStext = "EPSG:4326";

            OSGeo.OSR.SpatialReference userSRS = new OSGeo.OSR.SpatialReference("");
            userSRS.SetFromUserInput(userSRStext);
            int userSRSInt = Int16.Parse(userSRS.GetAuthorityCode(null));

            ///Set transform from input spatial reference to Rhino spatial reference
            OSGeo.OSR.SpatialReference rhinoSRS = new OSGeo.OSR.SpatialReference("");
            rhinoSRS.SetWellKnownGeogCS("WGS84");

            ///This transform moves and scales the points required in going from userSRS to XYZ and vice versa
            Transform userSRSToModelTransform = TwinLand.Convert.GetUserSRSToModelTransform(userSRS);
            Transform modelToUserSRSTransform = TwinLand.Convert.GetModelToUserSRSTransform(userSRS);


            GH_Structure<GH_String> mapList = new GH_Structure<GH_String>();
            GH_Structure<GH_String> mapquery = new GH_Structure<GH_String>();
            GH_Structure<GH_Rectangle> imgFrame = new GH_Structure<GH_Rectangle>();

            FileInfo file = new FileInfo(folderPath);
            file.Directory.Create();

            string size = string.Empty;
            if (Res != 0)
            {
                size = "&size=" + Res + "%2C" + Res;
            }

            for (int i = 0; i < boundary.Count; i++)
            {

                GH_Path path = new GH_Path(i);

                ///Get image frame for given boundary
                BoundingBox imageBox = boundary[i].GetBoundingBox(false);
                imageBox.Transform(modelToUserSRSTransform);

                ///Make sure to have a rect for output
                Rectangle3d rect = BBoxToRect(imageBox);

                ///Query the REST service
                string restquery = URL +
                  ///legacy method for creating bounding box string
                  "bbox=" + imageBox.Min.X + "%2C" + imageBox.Min.Y + "%2C" + imageBox.Max.X + "%2C" + imageBox.Max.Y +
                  "&bboxSR=" + userSRSInt +
                  size + //"&layers=&layerdefs=" +
                  "&imageSR=" + userSRSInt + //"&transparent=false&dpi=&time=&layerTimeOptions=" +
                  "&format=" + imageType;// +
                  //"&f=json";
                string restqueryJSON = restquery + "&f=json";
                string restqueryImage = restquery + "&f=image";

                mapquery.Append(new GH_String(restqueryImage), path);

                string result = string.Empty;

                    ///Get extent of image from arcgis rest service as JSON
                    result = TwinLand.Convert.HttpToJson(restqueryJSON);
                    JObject jObj = JsonConvert.DeserializeObject<JObject>(result);
                    if (!jObj.ContainsKey("href"))
                    {
                        restqueryJSON = restqueryJSON.Replace("export?", "exportImage?");
                        restqueryImage = restqueryImage.Replace("export?", "exportImage?");
                        mapquery.RemovePath(path);
                        mapquery.Append(new GH_String(restqueryImage), path);
                        result = TwinLand.Convert.HttpToJson(restqueryJSON);
                        jObj = JsonConvert.DeserializeObject<JObject>(result);
                    }

                if (run)
                {
                    Point3d extMin = new Point3d((double)jObj["extent"]["xmin"], (double)jObj["extent"]["ymin"], 0);
                    Point3d extMax = new Point3d((double)jObj["extent"]["xmax"], (double)jObj["extent"]["ymax"], 0);
                    rect = new Rectangle3d(Plane.WorldXY, extMin, extMax);
                    rect.Transform(userSRSToModelTransform);

                    ///Download image from source
                    ///Catch if JSON query throws an error
                    string imageQueryJSON = jObj["href"].ToString();
                    using (WebClient webC = new WebClient())
                    {
                        try 
                        {
                            if (!String.IsNullOrEmpty(imageQueryJSON))
                            {
                                webC.DownloadFile(imageQueryJSON, folderPath + prefix + "_" + i + "." + imageType);
                                webC.Dispose();
                            }
                            else
                            {
                                webC.DownloadFile(restqueryImage, folderPath + prefix + "_" + i + "." + imageType);
                                webC.Dispose();
                            }

                        }
                        catch (WebException e)
                        {
                            webC.DownloadFile(restqueryImage, folderPath + prefix + "_" + i + "." + imageType);
                            webC.Dispose();
                        }
                    }

                }
                var bitmapPath = folderPath + prefix + "_" + i + "." + imageType;
                mapList.Append(new GH_String(bitmapPath), path);

                imgFrame.Append(new GH_Rectangle(rect), path);
                AddPreviewItem(bitmapPath, rect);
            }

            DA.SetDataTree(0, mapList);
            DA.SetDataTree(1, imgFrame);
            DA.SetDataTree(2, mapquery);
    }
    
    private JObject rasterJson = JObject.Parse(TwinLand.Convert.GetEndpoints());

        /// <summary>
        /// Adds to the context menu an option to create a pre-populated list of common REST Raster sources
        /// </summary>
        /// <param name="menu"></param>
        /// https://discourse.mcneel.com/t/generated-valuelist-not-working/79406/6?u=hypar
        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            var rasterSourcesJson = rasterJson["REST Raster"].Select(x => x["source"]).Distinct();
            List<string> rasterSources = rasterSourcesJson.Values<string>().ToList();
            foreach (var src in rasterSourcesJson)
            {
                ToolStripMenuItem root = GH_DocumentObject.Menu_AppendItem(menu, "Create " + src.ToString() + " Source List", CreateRasterList);
                root.ToolTipText = "Click this to create a pre-populated list of some " + src.ToString() + " sources.";
                base.AppendAdditionalMenuItems(menu);
            }
        }

        /// <summary>
        /// Creates a value list pre-populated with possible accent colors and adds it to the Grasshopper Document, located near the component pivot.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void CreateRasterList(object sender, System.EventArgs e)
        {
            string source = sender.ToString();
            source = source.Replace("Create ", "");
            source = source.Replace(" Source List", "");

            GH_DocumentIO docIO = new GH_DocumentIO();
            docIO.Document = new GH_Document();

            ///Initialize object
            GH_ValueList vl = new GH_ValueList();

            ///Clear default contents
            vl.ListItems.Clear();

            foreach (var service in rasterJson["REST Raster"])
            {
                if (service["source"].ToString() == source)
                {
                    GH_ValueListItem vi = new GH_ValueListItem(service["service"].ToString(), String.Format("\"{0}\"", service["url"].ToString()));
                    vl.ListItems.Add(vi);
                }
            }

            ///Set component nickname
            vl.NickName = source;
            
            ///Get active GH doc
            GH_Document doc = OnPingDocument();
            if (docIO.Document == null) return;
            
            ///Place the object
            docIO.Document.AddObject(vl, false, 1);
            
            ///Get the pivot of the "URL" param
            PointF currPivot = Params.Input[4].Attributes.Pivot;
            
            ///Set the pivot of the new object
            vl.Attributes.Pivot = new PointF(currPivot.X - 400, currPivot.Y - 11);

            docIO.Document.SelectAll();
            docIO.Document.ExpireSolution();
            docIO.Document.MutateAllIds();
            IEnumerable<IGH_DocumentObject> objs = docIO.Document.Objects;
            doc.DeselectAll();
            doc.UndoUtil.RecordAddObjectEvent("Create REST Raster Source List", objs);
            doc.MergeDocument(docIO.Document);
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
      get { return new Guid("16d58c23-7ef0-4ad0-b9bc-8b5c24ebacb7"); }
    }
  }
}