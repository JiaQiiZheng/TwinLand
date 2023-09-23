﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;

namespace TwinLand
{
    internal struct TwinLandRasterPreviewItem
    {
        public DisplayMaterial mat;
        public Mesh mesh;
    }

    public abstract class TwinLandRasterPreviewComponent : TwinLandComponent
    {
        private List<TwinLandRasterPreviewItem> _previewItems;
        private BoundingBox _boundingBox;
        public TwinLandRasterPreviewComponent(string name, string nickName, string description, string subCategory) : base(name, nickName, description, subCategory)
        {
            _previewItems = new List<TwinLandRasterPreviewItem>();
        }

        protected override void BeforeSolveInstance()
        {
            _previewItems.Clear();
            _boundingBox = BoundingBox.Empty;
        }

        internal static Rectangle3d BBoxToRect(BoundingBox imageBox)
        {
            var xInterval = new Interval(imageBox.Min.X, imageBox.Max.X);
            var yInterval = new Interval(imageBox.Min.Y, imageBox.Max.Y);
            var rect = new Rectangle3d(Plane.WorldXY, xInterval, yInterval);
            return rect;
        }

        public override bool IsPreviewCapable => true;

        internal void AddPreviewItem(string bitmap, Rectangle3d bounds)
        {
            AddPreviewItem(bitmap, bounds.ToNurbsCurve(), bounds);
        }

        internal void AddPreviewItem(string bitmap, Curve c, Rectangle3d bounds)
        {
            var mesh = Mesh.CreateFromPlanarBoundary(c, MeshingParameters.FastRenderMesh, 0.1);
            TextureMapping tm = TextureMapping.CreatePlaneMapping(bounds.Plane, bounds.X, bounds.Y, new Interval(-1, 1));
            mesh.SetTextureCoordinates(tm, Transform.Identity, true);
            var mat = new DisplayMaterial(System.Drawing.Color.White);
            mat.SetBitmapTexture(bitmap, true);
            _previewItems.Add(new TwinLandRasterPreviewItem()
            {
                mesh = mesh,
                mat = mat
            });
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            foreach (var item in _previewItems)
            {
                args.Display.DrawMeshShaded(item.mesh, item.mat);
            }
            base.DrawViewportMeshes(args);
        }

    }
}
