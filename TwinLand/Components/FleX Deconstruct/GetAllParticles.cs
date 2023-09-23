using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using FlexCLI;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace TwinLand
{
  public class GetAllParticles : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public GetAllParticles()
      : base("GetAllParticles", "GetAllParticles",
        "Get all particles from engine object",  "Deconstruct")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddGenericParameter("FleX Object", "FleX Object", "", GH_ParamAccess.item
      );
      pManager.AddIntegerParameter("Interval", "Interval", "Display particles by indicating interval of solver iteration, large interval speed up the performance but lose smooth appearance", GH_ParamAccess.item, 1);
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddPointParameter("Point", "pt", "", GH_ParamAccess.tree);
      pManager.AddVectorParameter("Vector", "vector", "", GH_ParamAccess.tree);
    }

    /// <summary>
    /// start status
    /// </summary>
    private int interval = 1;
    private int counter = 0;

    /// <summary>
    /// declare new empty tree, output empty when iteration not step onto the interval
    /// </summary>
    private GH_Structure<GH_Point> points = new GH_Structure<GH_Point>();
    private GH_Structure<GH_Vector> vectors = new GH_Structure<GH_Vector>();

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      counter++;
      DA.GetData("Interval", ref interval);
      interval = Math.Max(1, interval);

      if (counter % interval == 0)
      {
        Flex flex = null;
        DA.GetData("FleX Object", ref flex);

        if (flex != null)
        {
          List<FlexParticle> particles = flex.Scene.GetAllParticles();

          points = new GH_Structure<GH_Point>();
          vectors = new GH_Structure<GH_Vector>();

          foreach (FlexParticle fp in particles)
          {
            GH_Path p = new GH_Path(fp.GroupIndex);
            points.Append(new GH_Point(new Point3d(fp.PositionX, fp.PositionY, fp.PositionZ)), p);
            vectors.Append(new GH_Vector(new Vector3d(fp.VelocityX, fp.VelocityY, fp.VelocityZ)), p);
          }
        }
      }

      DA.SetDataTree(0, points);
      DA.SetDataTree(1, vectors);
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
        return Properties.Resources.TL_Engine;
      }
    }

    /// <summary>
    /// Each component must have a unique Guid to identify it. 
    /// It is vital this Guid doesn't change otherwise old ghx files 
    /// that use the old ID will partially fail during loading.
    /// </summary>
    public override Guid ComponentGuid
    {
      get { return new Guid("728ade19-5d60-42fe-87d5-6e53e60ef520"); }
    }
  }
}