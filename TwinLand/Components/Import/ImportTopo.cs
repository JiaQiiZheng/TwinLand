using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Collections;
using GH_IO;
using GH_IO.Serialization;
using OSGeo.GDAL;
using OSGeo.OSR;
using OSGeo.OGR;

namespace TwinLand
{
    public class ImportTopo : TwinLandComponent
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ImportTopo()
            : base("ImportTopo", "Nickname",
                "ImportTopo description", "Import")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "boundary", "Boundary for cropping the DEM file",
                GH_ParamAccess.list);
            pManager.AddTextParameter("DEM", "DEM", "The full file path of downloaded DEM file", GH_ParamAccess.item);
            
            pManager[0].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("DEM_Mesh", "DEM_Mesh", "Converted mesh(topography) from input DEM file",
                GH_ParamAccess.tree);
            pManager.AddRectangleParameter("DEM_Extent", "DEM_Extent", "The extent of original DEM file",
                GH_ParamAccess.item);
            pManager.AddTextParameter("DEM_Info", "DEM_Info", "The information of input DEM file", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> boundary = new List<Curve>();
            DA.GetDataList<Curve>("Boundary", boundary);

            string filePath = String.Empty;
            DA.GetData<String>("DEM", ref filePath);

            RESTful.GdalConfiguration.ConfigureGdal();
            OSGeo.GDAL.Gdal.AllRegister();

            Dataset dataSource = Gdal.Open(filePath, Access.GA_ReadOnly);
            OSGeo.GDAL.Driver drv = dataSource.GetDriver();

            if (dataSource == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "DEM file is invalid or null");
                return;
            }

            // output Gdal info about DEM file
            string demInfo = string.Empty;
            List<string> infoOptions = new List<string> { "-stats" };
            demInfo = Gdal.GDALInfo(dataSource, new GDALInfoOptions(infoOptions.ToArray()));

            // Get Spatial Reference System (SRS) from DEM file, if not valid, set to WGS84
            OSGeo.OSR.SpatialReference CRS = new SpatialReference(Osr.SRS_WKT_WGS84_LAT_LONG);
            if (dataSource.GetProjection() == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "DEM coordinate reference system is missing, automatically set to WGS84");
            }
            else
            {
                CRS = new SpatialReference(dataSource.GetProjection());

                if (CRS.Validate() != 0)
                {
                    // Check if SRS needs to be converted from ESRI format to WKT
                    SpatialReference CRS_ESRI = CRS;
                    CRS_ESRI.MorphFromESRI();
                    string proj_ESRI = string.Empty;
                    CRS_ESRI.ExportToWkt(out proj_ESRI, null);

                    // if CRS is not valid, use Ground Control Point from DEM file
                    SpatialReference CRS_GCP = new SpatialReference(dataSource.GetGCPProjection());
                    string proj_GCP = string.Empty;
                    CRS_GCP.ExportToWkt(out proj_GCP, null);

                    if (!string.IsNullOrEmpty(proj_ESRI))
                    {
                        dataSource.SetProjection(proj_ESRI);
                        CRS = CRS_ESRI;
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                            "Spatial Reference System (SRS) morphed from ESRI format.");
                    }

                    else if (!string.IsNullOrEmpty(proj_GCP))
                    {
                        dataSource.SetProjection(proj_GCP);
                        CRS = CRS_GCP;
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                            "Spatial Reference System (SRS) was set from Ground Control Point (GCP) based on DEM file");
                    }

                    else
                    {
                        CRS.SetWellKnownGeogCS("WGS84");
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                            "Spatial Reference System (SRS) is not valid from DEM file, automatically set to WGS84");
                    }
                }

                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        $"DEM SRS - EPSG: {CRS.GetAttrValue("AUTHORITY", 1)}");
                }
            } // end of setting SRS

            OSGeo.OSR.SpatialReference dst = new OSGeo.OSR.SpatialReference("");
            dst.SetWellKnownGeogCS("WGS84");
            OSGeo.OSR.CoordinateTransformation coordTransform = new OSGeo.OSR.CoordinateTransformation(CRS, dst);
            OSGeo.OSR.CoordinateTransformation revTransform = new OSGeo.OSR.CoordinateTransformation(dst, CRS);

            double[] adfGeoTransform = new double[6];
            double[] invTransform = new double[6];
            dataSource.GetGeoTransform(adfGeoTransform);
            Gdal.InvGeoTransform(adfGeoTransform, invTransform);

            int width = dataSource.RasterXSize;
            int height = dataSource.RasterYSize;

            // DEM file bounding box

            // https://gdal.org/tutorials/geotransforms_tut.html
            // X_geo = GT(0) + X_pixel * GT(1) + Y_line * GT(2)
            // Y_geo = GT(3) + X_pixel * GT(4) + Y_line * GT(5)
            
            double oX = adfGeoTransform[0] + adfGeoTransform[1] * 0 + adfGeoTransform[2] * 0;
            double oY = adfGeoTransform[3] + adfGeoTransform[4] * 0 + adfGeoTransform[5] * 0;
            double eX = adfGeoTransform[0] + adfGeoTransform[1] * width + adfGeoTransform[2] * height;
            double eY = adfGeoTransform[3] + adfGeoTransform[4] * width + adfGeoTransform[5] * height;

            // Transform to WGS84
            double[] extMinPT = new double[3] { oX, eY, 0 };
            double[] extMaxPT = new double[3] { eX, oY, 0 };
            coordTransform.TransformPoint(extMinPT);
            coordTransform.TransformPoint(extMaxPT);
            Point3d dsMin = new Point3d(extMinPT[0], extMinPT[1], extMinPT[2]);
            Point3d dsMax = new Point3d(extMaxPT[0], extMaxPT[1], extMaxPT[2]);

            Rectangle3d dsbox = new Rectangle3d(Plane.WorldXY, TwinLand.Convert.WGSToXYZ(dsMin),
                TwinLand.Convert.WGSToXYZ(dsMax));

            double pixelWidth = dsbox.Width / width;
            double pixelHeight = dsbox.Height / height;

            // Declare trees
            GH_Structure<GH_Point> pointcloud = new GH_Structure<GH_Point>();
            GH_Structure<GH_Integer> rCount = new GH_Structure<GH_Integer>();
            GH_Structure<GH_Integer> cCount = new GH_Structure<GH_Integer>();
            GH_Structure<GH_Mesh> tMesh = new GH_Structure<GH_Mesh>();

            for (int i = 0; i < boundary.Count; i++)
            {
                GH_Path path = new GH_Path(i);

                Curve clippingBoundary = boundary[i];

                if (!clip)
                {
                    clippingBoundary = dsbox.ToNurbsCurve();
                }

                string clippedTopoFile = "/vsimem/topoclipped.tif";

                if (!(dsbox.BoundingBox.Contains(clippingBoundary.GetBoundingBox(true).Min) &&
                      (dsbox.BoundingBox.Contains(clippingBoundary.GetBoundingBox(true).Max))) && clip)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "One or more boundaries may be outside the bounds of the topo dataset.");
                }

                ///Offsets to mesh/boundary based on pixel size
                Point3d clipperMinPreAdd = clippingBoundary.GetBoundingBox(true).Corner(true, false, true);
                Point3d clipperMinPostAdd = new Point3d(clipperMinPreAdd.X, clipperMinPreAdd.Y, clipperMinPreAdd.Z);
                Point3d clipperMin = TwinLand.Convert.XYZToWGS(clipperMinPostAdd);

                Point3d clipperMaxPreAdd = clippingBoundary.GetBoundingBox(true).Corner(false, true, true);
                ///add/subtract pixel width if desired to get closer to boundary
                Point3d clipperMaxPostAdd = new Point3d();
                Point3d clipperMax = new Point3d();
                if (clip)
                {
                    clipperMaxPostAdd = new Point3d(clipperMaxPreAdd.X + pixelWidth, clipperMaxPreAdd.Y - pixelHeight,
                        clipperMaxPreAdd.Z);
                    clipperMax = TwinLand.Convert.XYZToWGS(clipperMaxPostAdd);
                }
                else
                {
                    clipperMaxPostAdd = new Point3d(clipperMaxPreAdd.X, clipperMaxPreAdd.Y, clipperMaxPreAdd.Z);
                    clipperMax = TwinLand.Convert.XYZToWGS(clipperMaxPostAdd);
                }


                double lonWest = clipperMin.X;
                double lonEast = clipperMax.X;
                double latNorth = clipperMin.Y;
                double latSouth = clipperMax.Y;

                var translateOptions = new[]
                {
                    "-of", "GTiff",
                    "-a_nodata", "0",
                    "-projwin_srs", "WGS84",
                    "-projwin", $"{lonWest}", $"{latNorth}", $"{lonEast}", $"{latSouth}"
                };

                using (Dataset clippedDataset = Gdal.wrapper_GDALTranslate(clippedTopoFile, dataSource,
                           new GDALTranslateOptions(translateOptions), null, null))
                {
                    Band band = clippedDataset.GetRasterBand(1);
                    width = clippedDataset.RasterXSize;
                    height = clippedDataset.RasterYSize;
                    clippedDataset.GetGeoTransform(adfGeoTransform);
                    Gdal.InvGeoTransform(adfGeoTransform, invTransform);

                    rCount.Append(new GH_Integer(height), path);
                    cCount.Append(new GH_Integer(width), path);
                    Mesh mesh = new Mesh();
                    List<Point3d> verts = new List<Point3d>();

                    double[] bits = new double[width * height];
                    band.ReadRaster(0, 0, width, height, bits, width, height, 0, 0);

                    for (int col = 0; col < width; col++)
                    {
                        for (int row = 0; row < height; row++)
                        {
                            // equivalent to bits[col][row] if bits is 2-dimension array
                            double pixel = bits[col + row * width];
                            if (pixel < -10000)
                            {
                                pixel = 0;
                            }

                            double gcol = adfGeoTransform[0] + adfGeoTransform[1] * col + adfGeoTransform[2] * row;
                            double grow = adfGeoTransform[3] + adfGeoTransform[4] * col + adfGeoTransform[5] * row;

                            ///convert to WGS84
                            double[] wgsPT = new double[3] { gcol, grow, pixel };
                            coordTransform.TransformPoint(wgsPT);
                            Point3d pt = new Point3d(wgsPT[0], wgsPT[1], wgsPT[2]);

                            verts.Add(TwinLand.Convert.WGSToXYZ(pt));
                        }
                    }

                    //Create meshes
                    //non Parallel
                    mesh.Vertices.AddVertices(verts);
                    //Parallel
                    //mesh.Vertices.AddVertices(vertsParallel.Values);

                    for (int u = 1; u < cCount[path][0].Value; u++)
                    {
                        for (int v = 1; v < rCount[path][0].Value; v++)
                        {
                            mesh.Faces.AddFace(v - 1 + (u - 1) * (height), v - 1 + u * (height),
                                v - 1 + u * (height) + 1, v - 1 + (u - 1) * (height) + 1);
                            //(k - 1 + (j - 1) * num2, k - 1 + j * num2, k - 1 + j * num2 + 1, k - 1 + (j - 1) * num2 + 1)
                        }
                    }

                    //mesh.Flip(true, true, true);
                    tMesh.Append(new GH_Mesh(mesh), path);

                    band.Dispose();
                }

                Gdal.Unlink("/vsimem/topoclipped.tif");
            }

            dataSource.Dispose();

            DA.SetDataTree(0, tMesh);
            DA.SetData(1, dsbox);
            DA.SetData(2, demInfo);
        }

        /// <summary>
        /// menu variables
        /// </summary>
        private bool clip = true;

        public bool Clip
        {
            get { return clip; }
            set
            {
                clip = value;
                if (clip)
                {
                    Message = "Clippeed";
                }
                else
                {
                    Message = "Not Clipped";
                }
            }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            ToolStripMenuItem item =
                Menu_AppendItem(menu, "Clip topography by input boundary", Menu_ClipClicked, true, Clip);
            item.ToolTipText = "Check to confirm clipping DEM file by input boundary";
        }
        
        protected void Menu_ClipClicked(object sender, EventArgs e)
        {
            // event handler
            RecordUndoEvent("Clip");
            Clip = !Clip;
            ExpireSolution(true);
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetBoolean("Clip", Clip);
            return base.Write(writer);
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            Clip = reader.GetBoolean("Clip");
            return base.Read(reader);
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
            get { return new Guid("09cb4058-01cc-4fc9-89c6-59b3f547fd9f"); }
        }
    }
}