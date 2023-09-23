using System;
using System.Collections.Generic;
using FlexCLI;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace TwinLand
{
  public class FleX_Parameters : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public FleX_Parameters()
      : base("FleX_Parameters", "FleX_Parameters",
        "Environmental variables setup for FleX engine", "Configuration")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      #region MyRegion

      // Particle Properties
      // Gravity
      pManager.AddVectorParameter("Gravity Acceleration", "gravity acceleration", "Default value set to the gravity acceleration value on earth", GH_ParamAccess.item,
        new Vector3d(0.0, 0.0, -9.807));
      // Radius
      pManager.AddNumberParameter("Maximum Radius", "max radius", "Set maximum interactive radius radius for particles", GH_ParamAccess.item, 0.15);
      
      // Collision Parameters
      // Solid Rest Distance
      pManager.AddNumberParameter("Solid Rest Distance", "solid rest distance",
        "The positive distance non-fluid particles attempt to maintain from each other", GH_ParamAccess.item, 0.15);
      // Fluid Rest Distance
      pManager.AddNumberParameter("Fluid Rest Distance", "fluid rest distance",
        "The positive distance fluid particles attempt to maintain from each other", GH_ParamAccess.item, 0.1);
      // Collision Distance
      pManager.AddNumberParameter("Collision Distance", "collision distance",
        "The positive distance particles attempt to maintain from shape", GH_ParamAccess.item, 0.875);
      // Particle Collision Margin
      pManager.AddNumberParameter("Particle Collision Margin", "particle collision margin",
        "Increase the particle radius to avoid missing collision while moving too fast in a single step",
        GH_ParamAccess.item, 0.5);
      // Shape Collision Margin
      pManager.AddNumberParameter("Shape Collision Margin", "shape collision margin",
        "Increase the particle radius while colliding with kinematic shapes", GH_ParamAccess.item, 0.5);
      // Max Speed
      pManager.AddNumberParameter("Maximum Speed", "max speed",
        "Particles' velocity in each iteration will be limited by this value", GH_ParamAccess.item, float.MaxValue);
      // Max Acceleration
      pManager.AddNumberParameter("Maximum Acceleration", "max acceleration",
        "Particles' acceleration will be limited by this value", GH_ParamAccess.item, 100.0);
      
      // Friction Parameters
      // Dynamic Friction
      pManager.AddNumberParameter("Coefficient of Dynamic Friction", "coefficient of dynamic friction",
        "Coefficient of friction used when colliding objects have dynamic movement", GH_ParamAccess.item, 0.0);
      // Static Friction
      pManager.AddNumberParameter("Coefficient of Static Friction", "coefficient of static friction",
        "Coefficient of friction used when colliding objects are relatively static", GH_ParamAccess.item, 0.0);
      // Particle Friction
      pManager.AddNumberParameter("Coefficient of Particle Friction", "coefficient of particle friction",
        "Coefficient of friction used in particles' collision", GH_ParamAccess.item, 0.0);
      // Restitution
      pManager.AddNumberParameter("Restitution", "restitution",
        "Coefficient of restitution used when colliding with shapes. Particle collision are always inelastic",
        GH_ParamAccess.item, 0.0);
      // Adhesion
      pManager.AddNumberParameter("Adhesion", "adhesion", "Adhesion of the fluid and colliding solid",
        GH_ParamAccess.item, 0.0);
      // Stop Threshold
      pManager.AddNumberParameter("Particle Stop Threshold", "particle stop threshold",
        "Indicate a value to stop iteration for a particle while its velocity smaller than the value",
        GH_ParamAccess.item, 0.0);
      // Shock Propagation
      pManager.AddNumberParameter("Shock Propagation", "ShockPropagation", "Artificially decrease the mass of particles based on height from a fixed reference point, this makes stacks and piles converge faster.", GH_ParamAccess.item, 0.0);
      // Dissipation
      pManager.AddNumberParameter("Dissipation", "dissipation",
        "Indicate a factor to damp the velocity of a particle based on how many particles it collide with",
        GH_ParamAccess.item, 0.0);
      // Damping
      pManager.AddNumberParameter("Damping", "damping",
        "The viscous drag force which is opposite to the particle velocity", GH_ParamAccess.item, 0.0);
      
      // Fluid Parameters
      // Fluid
      pManager.AddBooleanParameter("Is Fluid", "is fluid",
        "Set to true will take particles in group index {0} as fluid and implement fluid algorithm",
        GH_ParamAccess.item, true);
      // Viscosity
      pManager.AddNumberParameter("Viscosity", "viscosity", "Smooth particles velocities using the XSPH viscosity",
        GH_ParamAccess.item, 0.0);
      // Cohesion
      pManager.AddNumberParameter("Cohesion", "cohesion",
        "The coefficient controls how strongly particles attempt to hold each other", GH_ParamAccess.item, 0.025);
      // Surface Tension
      pManager.AddNumberParameter("Surface Tension", "surface tension",
        "The coefficient controls how strongly particles attempt to minimize surface area", GH_ParamAccess.item, 0.0);
      // Solid Pressure
      pManager.AddNumberParameter("Solid Pressure", "solid pressure",
        "The coefficient controls the pressure solid giving to particles", GH_ParamAccess.item, 1.0);
      // Free Surface Drag
      pManager.AddNumberParameter("Free Surface Drag", "free surface drags",
        "Drag force applied to boundary fluid particles", GH_ParamAccess.item, 0.0);
      // Buoyancy
      pManager.AddNumberParameter("Buoyancy", "buoyancy", "A scale factor for particle gravity under fluid status",
        GH_ParamAccess.item, 1.0);
      
      // Solid Parameters
      // Solid Stop Threshold
      pManager.AddNumberParameter("Solid Stop Threshold", "solid stop threshold",
        "Indicate a threshold value for a solid shape. Once its moving magnitude smaller that the value than stop iteration",
        GH_ParamAccess.item, 0.0);
      // Solid Creep
      pManager.AddNumberParameter("Solid Creep", "solid creep",
        "A coefficient controls the rate of a static solid had passed the stop threshold", GH_ParamAccess.item, 0.0);

      // Cloth
      // Wind
      pManager.AddVectorParameter("Wind", "wind",
        "Constant acceleration applied to particles of cloth and inflatables.", GH_ParamAccess.item,
        new Vector3d(0.0, 0.0, 0.0));
      // Drag
      pManager.AddNumberParameter("Drag", "drag", "Drag force applied to particles of cloth", GH_ParamAccess.item, 0.0);
      // Lift
      pManager.AddNumberParameter("Lift", "lift", "Lift force applied to particles of cloth and inflatables",
        GH_ParamAccess.item, 0.0);
      // Relaxation Mode
      pManager.AddBooleanParameter("Relaxation Mode", "relaxation mode",
        "Set to true to apply relaxation mode, will have slower convergence but more reliable", GH_ParamAccess.item,
        true);
      // Relaxation Factor
      pManager.AddNumberParameter("Relaxation Factor", "relaxation factor",
        "Control the convergence rate of parallel solver", GH_ParamAccess.item, 1.0);

      // loop through all params and set all of them optional
      for (int i = 0; i < pManager.ParamCount; i++)
      {
        pManager[i].Optional = true;
      }

      #endregion
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddGenericParameter("FleX Parameters", "FleX Params", "Converted Parameters setting object for FleX engine",
        GH_ParamAccess.item);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    FlexParams paramObj = new FlexParams();
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      paramObj = new FlexParams();
      
      // initialize coefficients
      // particle properties
      Vector3d ga = new Vector3d(0.0, 0.0, -9.807);
      double mr = 0.15;
      
      DA.GetData("Gravity Acceleration", ref ga);
      DA.GetData("Maximum Radius", ref mr);
      
      // collision
      double srd = 0.075;
      double frd = 0.075;
      double cd = 0.0;
      double pcm = 0.0;
      double scm = 0.0;
      double mxs = 0.0;
      double mxa = 0.0;

      DA.GetData("Solid Rest Distance", ref srd);
      DA.GetData("Fluid Rest Distance", ref frd);
      DA.GetData("Collision Distance", ref cd);
      DA.GetData("Particle Collision Margin", ref pcm);
      DA.GetData("Shape Collision Margin", ref scm);
      DA.GetData("Maximum Speed", ref mxs);
      DA.GetData("Maximum Acceleration", ref mxa);
      
      // friction
      double df = 0.0;
      double sf = 0.0;
      double pf = 0.0;
      double res = 0.0;
      double adh = 0.0;
      double pst = 0.0;
      double shp = 0.0;
      double dis = 0.0;
      double dam = 0.0;
      
      DA.GetData("Coefficient of Dynamic Friction", ref df);
      DA.GetData("Coefficient of Static Friction", ref sf);
      DA.GetData("Coefficient of Particle Friction", ref pf);
      DA.GetData("Restitution", ref res);
      DA.GetData("Adhesion", ref adh);
      DA.GetData("Particle Stop Threshold", ref pst);
      DA.GetData("Shock Propagation", ref shp);
      DA.GetData("Dissipation", ref dis);
      DA.GetData("Damping", ref dam);
      
      // fluid
      bool flu = true;
      double vis = 0.0;
      double coh = 0.0;
      double st = 0.0;
      double sp = 0.0;
      double fsd = 0.0;
      double buo = 0.0;

      DA.GetData("Is Fluid", ref flu);
      DA.GetData("Viscosity", ref vis);
      DA.GetData("Cohesion", ref coh);
      DA.GetData("Surface Tension", ref st);
      DA.GetData("Solid Pressure", ref sp);
      DA.GetData("Free Surface Drag", ref fsd);
      DA.GetData("Buoyancy", ref buo);
      
      // solid
      double sst = 0.0;
      double sc = 0.0;

      DA.GetData("Solid Stop Threshold", ref sst);
      DA.GetData("Solid Creep", ref sc);
      
      // cloth
      Vector3d wind = new Vector3d(0.0, 0.0, 0.0);
      double drag = 0.0;
      double lift = 0.0;
      bool rm = true;
      double rf = 1.0;

      DA.GetData("Wind", ref wind);
      DA.GetData("Drag", ref drag);
      DA.GetData("Lift", ref lift);
      DA.GetData("Relaxation Mode", ref rm);
      DA.GetData("Relaxation Factor", ref rf);
      
      // exception warning
      if (srd > mr)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Solid rest distance need not be larger than maximum radius.");
      }

      // particles properties
      paramObj.GravityX = (float)ga.X;
      paramObj.GravityY = (float)ga.Y;
      paramObj.GravityZ = (float)ga.Z;
      paramObj.Radius = (float)mr;
      
      // collision
      paramObj.SolidRestDistance = (float)srd;
      paramObj.FluidRestDistance = (float)frd;
      paramObj.CollisionDistance = (float)cd;
      paramObj.ParticleCollisionMargin = (float)pcm;
      paramObj.ShapeCollisionMargin = (float)scm;
      paramObj.MaxSpeed = (float)mxs;
      paramObj.MaxAcceleration = (float)mxa;
      
      // friction
      paramObj.DynamicFriction = (float)df;
      paramObj.StaticFriction = (float)sf;
      paramObj.ParticleFriction = (float)pf;
      paramObj.Restitution = (float)res;
      paramObj.Adhesion = (float)adh;
      paramObj.SleepThreshold = (float)pst;
      paramObj.ShockPropagation = (float)shp;
      paramObj.Dissipation = (float)dis;
      paramObj.Damping = (float)dam;
      
      // fluid
      paramObj.Fluid = flu;
      paramObj.Viscosity = (float)vis;
      paramObj.Cohesion = (float)coh;
      paramObj.SurfaceTension = (float)st;
      paramObj.SolidPressure = (float)sp;
      paramObj.FreeSurfaceDrag = (float)fsd;
      paramObj.Buoyancy = (float)buo;
      
      // solid
      paramObj.PlasticThreshold = (float)sst;
      paramObj.PlasticCreep = (float)sc;
      
      // cloth
      paramObj.WindX = (float)wind.X;
      paramObj.WindY = (float)wind.Y;
      paramObj.WindZ = (float)wind.Z;
      paramObj.Drag = (float)drag;
      paramObj.Lift = (float)lift;
      paramObj.RelaxationMode = rm? 1 : 0;
      paramObj.RelaxationFactor = (float)rf;
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
      get { return new Guid("bf130865-94d0-4671-ba5d-fc4875ebc5bb"); }
    }
  }
}