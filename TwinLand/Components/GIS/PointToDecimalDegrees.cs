using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace TwinLand
{
  public class PointToDecimalDegrees : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public PointToDecimalDegrees()
      : base("PointToDecimalDegrees", "XYZ_DD",
        "Convert point3d in model space to decimal degrees based on Spatial Reference System in model space", "GIS")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddPointParameter("Point", "point", "Point defined in model space", GH_ParamAccess.item);
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddNumberParameter("Latitude", "latitude", "Latitude value from Decimal Degrees of the input point",
        GH_ParamAccess.item);
      pManager.AddNumberParameter("Longitude", "longitude", "Longitude value from Decimal Degrees of the input point",
        GH_ParamAccess.item);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      // TODO. whether it is neccessary to add transform output here

      Point3d p = new Point3d();
      if (!DA.GetData("Point", ref p))
      {
        return;
      }

      DA.SetData("Latitude", TwinLand.Convert.XYZToWGS(p).Y);
      DA.SetData("Longitude", TwinLand.Convert.XYZToWGS(p).X);
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
      get { return new Guid("64f7a851-a9c9-4540-85d4-def0f1572d0c"); }
    }
  }
}