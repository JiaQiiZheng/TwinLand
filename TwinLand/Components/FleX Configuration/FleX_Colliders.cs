using System;
using System.Collections.Generic;
using FlexCLI;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace TwinLand
{
    public class FleX_Colliders : TwinLandComponent
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public FleX_Colliders()
            : base("FleX_Colliders", "FleX_Colliders",
                "Setup collision geometry for FleX simulation environment", "Configuration")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPlaneParameter("Planes", "planes", "Planes as collision geometries", GH_ParamAccess.list);
            pManager.AddSurfaceParameter("Spheres", "spheres", "Spheres as collision geometries", GH_ParamAccess.list);
            pManager.AddBoxParameter("Boxes", "boxes", "Boxes as collision geometries", GH_ParamAccess.list);
            pManager.AddMeshParameter("Meshes", "meshes", "Meshes as collision geometries", GH_ParamAccess.list);

            for (int i = 0; i < pManager.ParamCount; i++)
            {
                pManager[i].Optional = true;
                pManager[i].DataMapping = GH_DataMapping.Flatten;
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("FleX Colliders", "colliders", "", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Plane> planes = new List<Plane>();
            List<Surface> spheres = new List<Surface>();
            List<Box> boxes = new List<Box>();
            List<Mesh> meshes = new List<Mesh>();

            FlexCollisionGeometry cg = new FlexCollisionGeometry();

            DA.GetDataList("Planes", planes);
            DA.GetDataList("Spheres", spheres);
            DA.GetDataList("Boxes", boxes);
            DA.GetDataList("Meshes", meshes);

            foreach (Plane p in planes)
            {
                double[] pe = p.GetPlaneEquation();
                // register planes
                cg.AddPlane((float)pe[0], (float)pe[1], (float)pe[2], (float)pe[3]);
            }

            foreach (Surface s in spheres)
            {
                Sphere sph;
                if (!s.TryGetSphere(out sph))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid sphere found.");
                }
                else
                {
                    // register spheres
                    cg.AddSphere(new float[]
                        { (float)sph.Center.X, (float)sph.Center.Y, (float)sph.Center.Z }, (float)sph.Radius);
                }
            }

            foreach (Box b in boxes)
            {
                if (!b.IsValid) AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid box found.");
                else
                {
                    Plane p = Plane.WorldXY;
                    p.Origin = b.Center;
                    Quaternion q = Quaternion.Rotation(Plane.WorldXY, b.Plane);

                    // register boxes
                    cg.AddBox(
                        new float[] { (float)(b.X.Length / 2), (float)(b.Y.Length / 2), (float)(b.Z.Length / 2) },
                        new float[] { (float)b.Center.X, (float)b.Center.Y, (float)b.Center.Z },
                        new float[] { (float)q.Vector.X, (float)q.Vector.Y, (float)q.Vector.Z, (float)q.Scalar });
                }
            }

            foreach (Mesh m in meshes)
            {
                if(!m.IsValid) AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid mesh found.");
                else
                {
                    // FleX needs face pointing inward
                    m.Flip(false, false,true);

                    float[] vertices = new float[m.Vertices.Count * 3];
                    int[] faces = new int[m.Faces.Count * 3];

                    for (int i = 0; i < m.Vertices.Count; i++)
                    {
                        vertices[3 * i] = m.Vertices[i].X;
                        vertices[3 * i + 1] = m.Vertices[i].Y;
                        vertices[3 * i + 2] = m.Vertices[i].Z;
                    }

                    for (int i = 0; i < m.Vertices.Count; i++)
                    {
                        faces[3 * i] = m.Faces[i].A;
                        faces[3 * i + 1] = m.Faces[i].B;
                        faces[3 * i + 2] = m.Faces[i].C;
                    }
                    
                    // register meshes
                    cg.AddMesh(vertices, faces);

                    if (!m.IsClosed)
                    {
                        Mesh mm = m.DuplicateMesh();
                        mm.Flip(true,true,true);
                        
                        vertices = new float[mm.Vertices.Count * 3];
                        faces = new int[mm.Faces.Count * 3];

                        for (int i = 0; i < mm.Vertices.Count; i++)
                        {
                            vertices[3 * i] = mm.Vertices[i].X;
                            vertices[3 * i + 1] = mm.Vertices[i].Y;
                            vertices[3 * i + 2] = mm.Vertices[i].Z;
                        }

                        for (int i = 0; i < mm.Faces.Count; i++)
                        {
                            faces[3 * i] = mm.Faces[i].A;
                            faces[3 * i + 1] = mm.Faces[i].B;
                            faces[3 * i + 2] = mm.Faces[i].C;
                        }
                        
                        // register meshes
                        cg.AddMesh(vertices, faces);
                    }
                }
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
            get { return new Guid("18133741-76ca-4e9e-a234-a24fcdc0c79a"); }
        }
    }
}