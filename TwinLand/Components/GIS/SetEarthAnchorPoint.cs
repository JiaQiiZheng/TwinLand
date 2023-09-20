using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace TwinLand
{
  public class SetEarthAnchorPoint : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public SetEarthAnchorPoint()
      : base("SetEarthAnchorPoint", "setEAP",
        "SetEarthAnchorPoint base on latitude and longitude ", "GIS")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddBooleanParameter("Set", "set", "Set rhino's Earth Anchor Point", GH_ParamAccess.item, false);
      pManager.AddTextParameter("Latitude", "latitude", "Set latitude of the address", GH_ParamAccess.item);
      pManager.AddTextParameter("Longitude", "longitude", "Set longitude of the address", GH_ParamAccess.item);
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddTextParameter("EarthAnchorPoint", "EAP", "The latitude and longitude of the earth anchor point",
        GH_ParamAccess.item);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      string latString = String.Empty;
      string lonString = String.Empty;
      double lat = Double.NaN;
      double lon = Double.NaN;
      bool set = false;
      string res = String.Empty;
      
      // check whether the earth anchor point has been set
      if (!RhinoDoc.ActiveDoc.EarthAnchorPoint.EarthLocationIsSet())
      {
        res = "The earth anchor point has not been set yet";
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, res);
      }
      else
      {
        res =
          $"Latitude:{RhinoDoc.ActiveDoc.EarthAnchorPoint.EarthBasepointLatitude.ToString()} / Longitude:{RhinoDoc.ActiveDoc.EarthAnchorPoint.EarthBasepointLongitude.ToString()}";
      }

      DA.GetData("Set", ref set);
      DA.GetData("Latitude", ref latString);
      DA.GetData("Longitude", ref lonString);

      if (set)
      {
        EarthAnchorPoint ePt = new EarthAnchorPoint();

        lat = TwinLand.Convert.DMStoDDLat(latString);
        lon = TwinLand.Convert.DMStoDDLon(lonString);

        if (Double.IsNaN(lat) && !string.IsNullOrEmpty(latString))
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Latitude value is invalid. Please valid Degree Minute Second format (79°58′36″W | 079:56:55W | 079d 58′ 36″ W | 079 58 36.0 | 079 58 36.4 E)");
          return;
        }

        if (Double.IsNaN(lon) && !string.IsNullOrEmpty(lonString))
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Longitude value is invalid. Please valid Degree Minute Second format (79°58′36″W | 079:56:55W | 079d 58′ 36″ W | 079 58 36.0 | 079 58 36.4 E)");
          return;
        }

        if (!Double.IsNaN(lat) && !Double.IsNaN(lon))
        {
          ePt.EarthBasepointLatitude = lat;
          ePt.EarthBasepointLongitude = lon;
        }

        if (ePt.EarthBasepointLatitude > -90 && ePt.EarthBasepointLatitude < 90 && ePt.EarthBasepointLongitude > -100 &&
            ePt.EarthBasepointLongitude < 100)
        {
          RhinoDoc.ActiveDoc.EarthAnchorPoint = ePt;
        }
      }
      
      DA.SetData("EarthAnchorPoint", res);
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
      get { return new Guid("f586d267-e4d9-464a-b7b1-aee83d17d765"); }
    }
  }
}