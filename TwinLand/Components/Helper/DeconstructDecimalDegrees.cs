using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using TwinLand.Properties;

namespace TwinLand
{
  public class DeconstructDecimalDegrees : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public DeconstructDecimalDegrees()
      : base("DeconstructDecimalDegrees", "DeconstructDecimalDegrees",
        "Deconstruct address decimal degrees to Latitude and Longitude", "Helper")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddTextParameter("DecimalDegrees", "DD", "The address decimal degrees",
        GH_ParamAccess.item);
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddTextParameter("Latitude", "latitude", "Latitude from decimal degrees", GH_ParamAccess.item);
      pManager.AddTextParameter("Longitude", "longitude", "Longitude from decimal degrees", GH_ParamAccess.item);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      string origin = "";
      if(!DA.GetData("Location", ref origin)) return;

      string[] data = origin.Split(',');
      DA.SetData("Latitude", data[0]);
      DA.SetData("Longitude", data[1]);
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
      get { return new Guid("c4a3a3d6-af69-4c46-8763-aede9f775f5f"); }
    }
  }
}