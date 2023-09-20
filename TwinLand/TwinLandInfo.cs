using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace TwinLand
{
  public class TwinLandInfo : GH_AssemblyInfo
  {
    public override string Name => "TwinLand";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon => null;

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "";

    public override Guid Id => new Guid("B5F86E36-51B9-4E62-9686-7DB4A35CA77A");

    //Return a string identifying you or your company.
    public override string AuthorName => "Jiaqi Zheng";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "jqzzz";
  }
}