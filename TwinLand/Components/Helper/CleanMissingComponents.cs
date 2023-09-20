using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace TwinLand
{
  public class CleanMissingComponents : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public CleanMissingComponents()
      : base("CleanMissingComponents", "CleanMissingComponents",
        "Clean all missing components on canvas", "Helper")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddBooleanParameter("Run", "run", "Run and clean all missing components on canvas",
        GH_ParamAccess.item, false);
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false;
      DA.GetData("Run", ref run);
      
      GH_Document ghDoc = this.OnPingDocument();
      List<IGH_DocumentObject> objs = new List<IGH_DocumentObject>(ghDoc.Objects);
      var trash = ghDoc.Objects.Where(o =>
        o.GetType().ToString() == "Grasshopper.Kernel.Components.GH_PlaceholderComponent").ToList();

      if (run)
      {
        if(trash.Count == 0) return;
        ghDoc.ScheduleSolution(20, d => { ghDoc.RemoveObjects(trash, false);});
      }
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
      get { return new Guid("bb9fcca8-6029-4786-b844-627265616a4b"); }
    }
  }
}