using System;
using System.Collections.Generic;
using FlexCLI;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace TwinLand
{
  public class FleX_Scene : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public FleX_Scene()
      : base("FleX_Scene", "FleX_Scene",
        "Set up all active geometries for FleX engine", "Configuration")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddGenericParameter("Particles", "particles", "", GH_ParamAccess.list);
      // TODO. Add more types of 
      pManager.AddGenericParameter("Supplement Constraints", "constraints",
        "This is a optional supplement previous constraints", GH_ParamAccess.list);

      pManager[0].Optional = true;
      pManager[0].DataMapping = GH_DataMapping.Flatten;
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddGenericParameter("Scene", "scene", "Scene object for FleX engine", GH_ParamAccess.item);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      FlexScene scene = new FlexScene();

      List<FlexParticle> particles = new List<FlexParticle>();
      DA.GetDataList("Particles", particles);

      // register input for FLeX engine
      foreach (FlexParticle p in particles)
      {
        scene.RegisterParticles(new float[3]{p.PositionX, p.PositionY, p.PositionZ}, new float[3]{p.VelocityX, p.VelocityY, p.VelocityZ}, new float[1]{p.InverseMass}, p.IsFluid, p.SelfCollision, p.GroupIndex);
      }

      DA.SetData("Scene", scene);
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
      get { return new Guid("91a8f17e-433a-45ec-8d51-5766847df468"); }
    }
  }
}