using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using OsmSharp;
using OsmSharp.Complete;
using Rhino.Geometry;

namespace TwinLand
{
  public class FilterOpenStreetMap : TwinLandComponent
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public FilterOpenStreetMap()
      : base("FilterOpenStreetMap", "FilterOSM",
        "Filter OpenStreetMap by input 'Key=Value' expression", "Data Engineering")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
      pManager.AddGeometryParameter("FeatureGeometry", "featureGeometry", "Feature geometry from OpenStreetMap file",
        GH_ParamAccess.tree);
      pManager.AddTextParameter("Values", "values", "Data value list of each feature geometry", GH_ParamAccess.tree);
      pManager.AddTextParameter("OSM_Tag_Key = Value", "OSM_Tag_Key = Value",
        "String value used to filter the osm data, format like 'natural=water'", GH_ParamAccess.item);
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      pManager.AddGeometryParameter("Filtered", "filtered", "Filtered feature geometry", GH_ParamAccess.tree);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      string matchExpression = string.Empty;
      DA.GetData("OSM_Tag_Key = Value", ref matchExpression);
      
      GH_Structure<IGH_GeometricGoo> geoFeatures = new GH_Structure<IGH_GeometricGoo>();
      DA.GetDataTree("FeatureGeometry", out geoFeatures);

      GH_Structure<GH_String> values = new GH_Structure<GH_String>(); 
      DA.GetDataTree("Values", out values);

      // split the expression to match key
      string matchKey = matchExpression.Split('=')[1];
      var paths = values.Paths;
      // declare a new data tree to contain features
      GH_Structure<IGH_GeometricGoo> newGeo = new GH_Structure<IGH_GeometricGoo>();
      for (int i = 0; i < values.Branches.Count(); i++)
      {
        foreach (var key in values[i])
        {
          if (key.Value.Equals(matchKey))
          {
            foreach (var geo in geoFeatures[i])
            {
              newGeo.Append(geo, paths[i]);
            }
          }
        }
      }

      DA.SetDataTree(0, newGeo);
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
      get { return new Guid("d1fca95e-05fa-4f75-8200-e71df714487c"); }
    }
  }
}