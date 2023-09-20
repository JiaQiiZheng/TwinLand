using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using OsmSharp;
using OsmSharp.Streams;
using OsmSharp.Tags;
using Rhino;
using Rhino.Geometry;

namespace TwinLand
{
  public class ImportOpenStreetMap : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public ImportOpenStreetMap()
      : base("ImportOpenStreetMap", "ImportOSM",
        "Import OSM data from downloaded osm file", "Import")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddCurveParameter("Boundary", "boundary", "Optional boundary used to crop the osm data",
        GH_ParamAccess.item);
      pManager.AddTextParameter("FilePath", "filePath", "The path of downloaded osm file", GH_ParamAccess.item);
      pManager.AddTextParameter("OSMTag_Key", "OSMTag_Key",
        "List of field name used to filter the osm data, format like natural, buildings, highways",
        GH_ParamAccess.list);
      pManager.AddTextParameter("OSMTag_Key = Value", "OSMTag_Key = Value",
        "List of field, value used to filter the osm data, format like 'natural=water'", GH_ParamAccess.list);

      pManager[0].Optional = true;
      pManager[2].Optional = true;
      pManager[3].Optional = true;
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddCurveParameter("Extends", "extends", "Output data boundary", GH_ParamAccess.tree);
      pManager.AddTextParameter("Fields", "fields", "List of field names read from osm file", GH_ParamAccess.tree);
      pManager.AddTextParameter("Values", "values", "List of values read from osm file", GH_ParamAccess.tree);
      pManager.AddGeometryParameter("FeatureGeometry", "featureGeometry",
        "Feature geometry including points, polylines, surfaces read from osm file", GH_ParamAccess.tree);
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

      string filePath = string.Empty;
      DA.GetData("FilePath", ref filePath);

      List<string> OSMTag_Key = new List<string>();
      DA.GetDataList("OSMTag_Key", OSMTag_Key);

      List<string> key_value = new List<string>();
      DA.GetDataList("OSMTag_Key = Value", key_value);

      // TODO. figure out when to implement transform method between unit
      Transform ToMetric =
        new Transform(Rhino.RhinoMath.UnitScale(RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Meters));
      Transform formMetric =
        new Transform(Rhino.RhinoMath.UnitScale(Rhino.UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem));

      // declare trees
      Rectangle3d cropRec = new Rectangle3d();
      GH_Structure<GH_String> fieldNames = new GH_Structure<GH_String>();
      GH_Structure<GH_String> fieldValues = new GH_Structure<GH_String>();
      GH_Structure<IGH_GeometricGoo> geometryGoo = new GH_Structure<IGH_GeometricGoo>();

      // get boundary corners if boundary was input
      Point3d max = new Point3d();
      Point3d min = new Point3d();

      // TODO: figure out the min and max corner definition
      if (boundary != null)
      {
        Point3d maxM = boundary.GetBoundingBox(true).Corner(false, false, true);
        max = TwinLand.Convert.XYZToWGS(maxM);
        Point3d minM = boundary.GetBoundingBox(true).Corner(true, true, true);
        min = TwinLand.Convert.XYZToWGS(minM);
      }

      // generate the crop boundary from default boundary attributes in downloaded osm file
      System.Xml.Linq.XDocument xdoc = System.Xml.Linq.XDocument.Load(filePath);
      if (xdoc.Root.Element("bounds") != null)
      {
        double minlat = System.Convert.ToDouble(xdoc.Root.Element("bounds").Attribute("minlat").Value);
        double minlon = System.Convert.ToDouble(xdoc.Root.Element("bounds").Attribute("minlon").Value);
        double maxlat = System.Convert.ToDouble(xdoc.Root.Element("bounds").Attribute("maxlat").Value);
        double maxlon = System.Convert.ToDouble(xdoc.Root.Element("bounds").Attribute("maxlon").Value);

        Point3d boundsMin = TwinLand.Convert.WGSToXYZ(new Point3d(minlon, minlat, 0));
        Point3d boundsMax = TwinLand.Convert.WGSToXYZ(new Point3d(maxlon, maxlat, 0));

        cropRec = new Rectangle3d(Plane.WorldXY, boundsMin, boundsMax);
      }
      else
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
          "Extends of this OSM file could not be found, please remove optional boundary input or download the osm file again");
      }

      // read osm file and data operation
      using (var fileStreamSource = File.OpenRead(filePath))
      {
        // create a source
        OsmSharp.Streams.XmlOsmStreamSource source = new OsmSharp.Streams.XmlOsmStreamSource(fileStreamSource);

        // filter by bounding box
        OsmSharp.Streams.OsmStreamSource sourceClipped = source;
        if (clipped)
        {
          sourceClipped = source.FilterBox((float)min.X, (float)max.Y, (float)max.X, (float)min.Y, true);
        }

        // create a dictionary of elements
        OsmSharp.Db.Impl.MemorySnapshotDb sourceMem = new OsmSharp.Db.Impl.MemorySnapshotDb(sourceClipped);

        // filter the source
        var filtered = from osmGeos in sourceClipped where osmGeos.Tags != null select osmGeos;

        if (OSMTag_Key.Any())
        {
          filtered = from osmGeos in filtered where osmGeos.Tags.ContainsAnyKey(OSMTag_Key) select osmGeos;
        }

        if (key_value.Any())
        {
          List<Tag> tags = new List<Tag>();
          foreach (string value in key_value)
          {
            string[] kv = value.Split('=');
            Tag tag = new Tag(kv[0], kv[1]);
            tags.Add(tag);
          }

          filtered = from osmGeos in filtered where osmGeos.Tags.Intersect(tags).Any() select osmGeos;
        }

        source.Dispose();

        // loop over all objects and count them
        int nodes = 0, ways = 0, relations = 0;

        foreach (OsmSharp.OsmGeo osmGeo in filtered)
        {
          // append nodes
          if (osmGeo.Type == OsmGeoType.Node)
          {
            OsmSharp.Node n = (OsmSharp.Node)osmGeo;
            GH_Path nodesPath = new GH_Path(0, nodes);
            
            // collect field and value for each node
            fieldNames.AppendRange(GetKeys(osmGeo), nodesPath);
            fieldValues.AppendRange(GetValues(osmGeo), nodesPath);
            
            // get feature geometry (point) for each node
            Point3d nPoint = TwinLand.Convert.WGSToXYZ(new Point3d((double)n.Longitude, (double)n.Latitude, 0));
            geometryGoo.Append(new GH_Point(nPoint), nodesPath);
            
            // increment nodes
            nodes++;
          }
          
          // append ways
          else if (osmGeo.Type == OsmGeoType.Way)
          {
            OsmSharp.Way w = (OsmSharp.Way)osmGeo;
            GH_Path waysPath = new GH_Path(1, ways);
            
            // collect field and value for each way
            fieldNames.AppendRange(GetKeys(osmGeo), waysPath);
            fieldValues.AppendRange(GetValues(osmGeo), waysPath);
            
            // get feature geometry (polyline) for each way
            List<Point3d> wayNodes = new List<Point3d>();
            foreach (long j in w.Nodes)
            {
              OsmSharp.Node n = (OsmSharp.Node)sourceMem.Get(OsmGeoType.Node, j);
              wayNodes.Add(TwinLand.Convert.WGSToXYZ(new Point3d((double)n.Longitude, (double)n.Latitude, 0)));
            }

            PolylineCurve plc = new PolylineCurve(wayNodes);
            if (plc.IsClosed)
            {
              Brep[] breps = Brep.CreatePlanarBreps(plc, DocumentTolerance());
              geometryGoo.Append(new GH_Brep(breps[0]), waysPath);
            }
            else
            {
              geometryGoo.Append(new GH_Curve(plc), waysPath);
            }
            
            // increment ways
            ways++;
          }
          
          // append relation
          else if (osmGeo.Type == OsmGeoType.Relation)
          {
            OsmSharp.Relation r = (OsmSharp.Relation)osmGeo;
            GH_Path relationsPath = new GH_Path(2, relations);
            
            // collect field and value for each relation
            fieldNames.AppendRange(GetKeys(osmGeo), relationsPath);
            fieldValues.AppendRange(GetValues(osmGeo), relationsPath);

            List<Curve> plines = new List<Curve>();
            bool allClosed = true;
            
            // looping through members
            for (int mem = 0; mem < r.Members.Length; mem++)
            {
              GH_Path memberPath = new GH_Path(2, relations, mem);

              OsmSharp.RelationMember rMem = r.Members[mem];
              OsmSharp.OsmGeo rMemGeo = sourceMem.Get(rMem.Type, rMem.Id);

              if (rMemGeo != null)
              {
                // get geometry for node
                if (rMemGeo.Type == OsmGeoType.Node)
                {
                  long memNodeId = rMem.Id;
                  OsmSharp.Node memN = (OsmSharp.Node)sourceMem.Get(rMem.Type, rMem.Id);
                  Point3d memPoint =
                    TwinLand.Convert.WGSToXYZ(new Point3d((double)memN.Longitude, (double)memN.Latitude, 0));
                  geometryGoo.Append(new GH_Point(memPoint), memberPath);
                }
                
                // get geometry for way
                else if (rMemGeo.Type == OsmGeoType.Way)
                {
                  long memWayId = rMem.Id;
                  OsmSharp.Way memW = (OsmSharp.Way)rMemGeo;
                  
                  // get polyline geometry for way
                  List<Point3d> memNodes = new List<Point3d>();
                  foreach (long memNodeId in memW.Nodes)
                  {
                    OsmSharp.Node memNode = (OsmSharp.Node)sourceMem.Get(OsmGeoType.Node, memNodeId);
                    memNodes.Add(TwinLand.Convert.WGSToXYZ(new Point3d((double)memNode.Longitude,
                      (double)memNode.Latitude, 0)));
                  }

                  PolylineCurve plc = new PolylineCurve(memNodes);
                  geometryGoo.Append(new GH_Curve(plc.ToNurbsCurve()), memberPath);

                  // orientate all curves to CounterClockwise
                  CurveOrientation orient = plc.ClosedCurveOrientation(Plane.WorldXY);
                  if (orient != CurveOrientation.CounterClockwise) plc.Reverse();

                  if (!plc.IsClosed) allClosed = false;
                  plines.Add(plc.ToNurbsCurve());
                }
                
                // get nested relations
                else if (rMemGeo.Type == OsmGeoType.Relation)
                {
                  // TODO.figure out whether this is needed
                }
              }
            }
            
            // end members loop
            if (plines.Count > 0 && allClosed)
            {
              // create base surface
              Brep[] breps = Brep.CreatePlanarBreps(plines, DocumentTolerance());
              geometryGoo.RemovePath(relationsPath);

              foreach (Brep b in breps)
              {
                geometryGoo.Append(new GH_Brep(b), relationsPath);
                
                // TODO.add building massing construction function if needed
              }
            }
            
            // increment relations
            relations++;
          }// end relation loop
        }// end filtered loop
      }// end osm source loop

      if (cropRec.IsValid)
      {
        DA.SetData(0, cropRec);
      }

      DA.SetDataTree(1, fieldNames);
      DA.SetDataTree(2, fieldValues);
      DA.SetDataTree(3, geometryGoo);
    }

    private bool clipped = true;

    public bool Clipped
    {
      get { return clipped; }
      set
      {
        clipped = value;
        if (clipped)
        {
          Message = "Clipped";
        }
        else
        {
          Message = "Not Clipped";
        }
      }
    }
    
    private static List<GH_String> GetKeys(OsmGeo osmGeo)
    {
      List<GH_String> keys = new List<GH_String>();
      keys.Add(new GH_String("osm id"));
      if (osmGeo.Tags != null)
      {
        foreach (var t in osmGeo.Tags)
        {
          keys.Add(new GH_String(t.Key));
        }
      }
      else
      {
        keys.Add(null);
      }
      return keys;
    }

    private static List<GH_String> GetValues(OsmGeo osmGeo)
    {
      List<GH_String> values = new List<GH_String>();

      values.Add(new GH_String(osmGeo.Id.ToString()));

      if (osmGeo.Tags != null)
      {
        foreach (var t in osmGeo.Tags)
        {
          values.Add(new GH_String(t.Value));
        }
      }
      else
      {
        values.Add(null);
      }
      return values;
    }

    public override bool Write(GH_IO.Serialization.GH_IWriter writer)
    {
      // first add field
      writer.SetBoolean("Clipped", clipped);
      // then call the base class implementation
      return base.Write(writer);
    }

    public override bool Read(GH_IO.Serialization.GH_IReader reader)
    {
      // first read field
      Clipped = reader.GetBoolean("Clipped");
      // then call the base class implementation
      return base.Read(reader);
    }

    protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
    {
      // Append the item to the menu, making sure it's always enabled and check if Absolute is True.
      ToolStripMenuItem item = Menu_AppendItem(menu, "Clipped", Menu_ClippedClicked, true, Clipped);
      item.ToolTipText = "Confirm to crop by input boundary";
    }

    private void Menu_ClippedClicked(object sender, EventArgs e)
    {
      RecordUndoEvent("Absolute");
      Clipped = !Clipped;
      ExpireSolution(true);
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
      get { return new Guid("e25b3f79-066a-4506-9108-39be97d6a90b"); }
    }
  }
}