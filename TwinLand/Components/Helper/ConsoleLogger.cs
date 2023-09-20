using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using TwinLand.Utils;

namespace TwinLand
{
  public class ConsoleLogger : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public ConsoleLogger()
      : base("ConsoleLogger", "ConsoleLogger",
        "ConsoleLogger", "Helper")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddBooleanParameter("Update", "update", "update the log", GH_ParamAccess.item);
      pManager.AddBooleanParameter("Clear", "clear", "clear the log", GH_ParamAccess.item);
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddTextParameter("Logs", "logs", "output logs", GH_ParamAccess.list);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool update = false;
      bool clear = false;

      if (!DA.GetData(0, ref update)) return;
      if (!DA.GetData(1, ref clear)) return;

      if (!update) return;

      if (clear) GlobalConsole.Clear();

      List<string> logs = GlobalConsole.Read();

      DA.SetDataList(0, logs);
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
      get { return new Guid("37cda7b8-727d-47fb-9374-ea62a39302ac"); }
    }
  }
}