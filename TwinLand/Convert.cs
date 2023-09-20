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
using System.Net;

using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using OSGeo.OGR;
using OSGeo.GDAL;
using OSGeo.OSR;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TwinLand.Properties;

namespace TwinLand
{
    class Convert
    {
        private static RhinoDoc ActiveDoc => RhinoDoc.ActiveDoc;
        private static EarthAnchorPoint EarthAnchorPoint => ActiveDoc.EarthAnchorPoint;

        public static Point3d XYZToWGS(Point3d xyz)
        {
            var point = new Point3d(xyz);
            point.Transform(XYZToWGSTransform());
            return point;
        }
        
        public static Point3d ToWGS(Point3d xyz)
        {
            EarthAnchorPoint eap = new EarthAnchorPoint();
            eap = Rhino.RhinoDoc.ActiveDoc.EarthAnchorPoint;
            Rhino.UnitSystem us = new Rhino.UnitSystem();
            Transform xf = eap.GetModelToEarthTransform(us);

            xyz = xyz * Rhino.RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Meters);
            Point3d ptON = new Point3d(xyz.X, xyz.Y, xyz.Z);
            ptON = xf * ptON;

            ///TODO: Make translation of ptON here using SetCRS global variable (WGS84 -> CRS)

            return ptON;
        }

        public static Transform XYZToWGSTransform()
        {
            return EarthAnchorPoint.GetModelToEarthTransform(ActiveDoc.ModelUnitSystem);
        }

        public static Point3d WGSToXYZ(Point3d wgs)
        {
            var transformedPoint = new Point3d(wgs);
            transformedPoint.Transform(WGSToXYZTransform());
            return transformedPoint;
        }

        public static Transform WGSToXYZTransform()
        {
            var XYZToWGS = XYZToWGSTransform();
            if (XYZToWGS.TryGetInverse(out Transform transform))
            {
                return transform;
            }

            return Transform.Unset;
        }
        
        public static Transform GetUserSRSToModelTransform(OSGeo.OSR.SpatialReference userSRS)
        {
            ///TODO: Check what units the userSRS is in and coordinate with the scaling function.  Currently only accounts for a userSRS in meters.
            ///TODO: translate or scale GCS (decimal degrees) to something like a Projectected Coordinate System.  Need to go dd to xy

            ///transform rhino EAP from rhinoSRS to userSRS
            double eapLat = EarthAnchorPoint.EarthBasepointLatitude;
            double eapLon = EarthAnchorPoint.EarthBasepointLongitude;
            double eapElev = EarthAnchorPoint.EarthBasepointElevation;
            Plane eapPlane = EarthAnchorPoint.GetEarthAnchorPlane(out Vector3d eapNorth);

            OSGeo.OSR.SpatialReference rhinoSRS = new OSGeo.OSR.SpatialReference("");
            rhinoSRS.SetWellKnownGeogCS("WGS84");

            OSGeo.OSR.CoordinateTransformation coordTransform = new OSGeo.OSR.CoordinateTransformation(rhinoSRS, userSRS);
            //OSGeo.OGR.Geometry userAnchorPointDD = Heron.Convert.Point3dToOgrPoint(new Point3d(eapLon, eapLat, eapElev));
            OSGeo.OGR.Geometry userAnchorPointDD = new OSGeo.OGR.Geometry(wkbGeometryType.wkbPoint);
            userAnchorPointDD.AddPoint(eapLon, eapLat, eapElev);
            Transform t = new Transform(1.0);

            userAnchorPointDD.Transform(coordTransform);

            Point3d userAnchorPointPT = TwinLand.Convert.OgrPointToPoint3d(userAnchorPointDD, t);

            ///setup userAnchorPoint plane for move and rotation
            double eapLatNorth = EarthAnchorPoint.EarthBasepointLatitude + 0.5;
            double eapLonEast = EarthAnchorPoint.EarthBasepointLongitude + 0.5;

            //OSGeo.OGR.Geometry userAnchorPointDDNorth = Heron.Convert.Point3dToOgrPoint(new Point3d(eapLon, eapLatNorth, eapElev));
            OSGeo.OGR.Geometry userAnchorPointDDNorth = new OSGeo.OGR.Geometry(wkbGeometryType.wkbPoint);
            userAnchorPointDDNorth.AddPoint(eapLon, eapLatNorth, eapElev);

            //OSGeo.OGR.Geometry userAnchorPointDDEast = Heron.Convert.Point3dToOgrPoint(new Point3d(eapLonEast, eapLat, eapElev));
            OSGeo.OGR.Geometry userAnchorPointDDEast = new OSGeo.OGR.Geometry(wkbGeometryType.wkbPoint);
            userAnchorPointDDEast.AddPoint(eapLonEast, eapLat, eapElev);

            userAnchorPointDDNorth.Transform(coordTransform);
            userAnchorPointDDEast.Transform(coordTransform);
            Point3d userAnchorPointPTNorth = TwinLand.Convert.OgrPointToPoint3d(userAnchorPointDDNorth, t);
            Point3d userAnchorPointPTEast = TwinLand.Convert.OgrPointToPoint3d(userAnchorPointDDEast, t);
            Vector3d userAnchorNorthVec = userAnchorPointPTNorth - userAnchorPointPT;
            Vector3d userAnchorEastVec = userAnchorPointPTEast - userAnchorPointPT;

            Plane userEapPlane = new Plane(userAnchorPointPT, userAnchorEastVec, userAnchorNorthVec);

            ///shift (move and rotate) from userSRS EAP to 0,0 based on SRS north direction
            Transform scale = Transform.Scale(new Point3d(0.0, 0.0, 0.0), (1 / Rhino.RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Meters)));

            if (userSRS.GetLinearUnitsName().ToUpper().Contains("FEET") || userSRS.GetLinearUnitsName().ToUpper().Contains("FOOT"))
            {
                scale = Transform.Scale(new Point3d(0.0, 0.0, 0.0), (1 / Rhino.RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Feet)));
            }

            ///if SRS is geographic (ie WGS84) use Rhino's internal projection
            ///this is still buggy as it doesn't work with other geographic systems like NAD27
            if ((userSRS.IsProjected() == 0) && (userSRS.IsLocal() == 0))
            {
                userEapPlane.Transform(WGSToXYZTransform());
                scale = WGSToXYZTransform();
            }

            Transform shift = Transform.ChangeBasis(eapPlane, userEapPlane);

            Transform shiftScale = Transform.Multiply(scale, shift);

            return shiftScale;
        }

        public static Transform GetModelToUserSRSTransform(OSGeo.OSR.SpatialReference userSRS)
        {
            var xyzToUserSRS = GetUserSRSToModelTransform(userSRS);
            if (xyzToUserSRS.TryGetInverse(out Transform transform))
            {
                return transform;
            }
            return Transform.Unset;
        }

        public static Transform GetUserSRSToTwinLandSRSTransform(OSGeo.OSR.SpatialReference userSRS)
        {
            ///transform rhino EAP from rhinoSRS to userSRS
            Plane eapPlane = EarthAnchorPoint.GetEarthAnchorPlane(out Vector3d eapNorth);

            OSGeo.OSR.SpatialReference rhinoSRS = new OSGeo.OSR.SpatialReference("");
            rhinoSRS.SetWellKnownGeogCS("WGS84");

            OSGeo.OSR.SpatialReference twinLandSRS = new OSGeo.OSR.SpatialReference("");
            twinLandSRS.SetFromUserInput(TwinLandSRS.Instance.SRS);
            var unitsToMeters = userSRS.GetLinearUnits();

            OSGeo.OSR.CoordinateTransformation coordTransform = new OSGeo.OSR.CoordinateTransformation(rhinoSRS, twinLandSRS);
            OSGeo.OGR.Geometry userAnchorPointDD = new OSGeo.OGR.Geometry(wkbGeometryType.wkbPoint);
            userAnchorPointDD.AddPoint(EarthAnchorPoint.EarthBasepointLongitude, EarthAnchorPoint.EarthBasepointLatitude, EarthAnchorPoint.EarthBasepointElevation);
            Transform t = new Transform(1.0);

            userAnchorPointDD.Transform(coordTransform);

            OSGeo.OSR.CoordinateTransformation coordTransformHeronSRStoUserSRS = new OSGeo.OSR.CoordinateTransformation(twinLandSRS, userSRS);
            userAnchorPointDD.Transform(coordTransformHeronSRStoUserSRS);


            Point3d userAnchorPointPT = TwinLand.Convert.OgrPointToPoint3d(userAnchorPointDD, t);
            ///Set TwinLand EAP plane to have same north as Rhino EAP in case the user had switched it from World Y
            Plane userEapPlane = new Plane(userAnchorPointPT, eapPlane.XAxis, eapPlane.YAxis);

            ///shift (move and rotate) from userSRS EAP to 0,0 based on SRS north direction
            Transform scale = Transform.Scale(new Point3d(0.0, 0.0, 0.0), (1 / (Rhino.RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Meters) / unitsToMeters)));


            ///if SRS is geographic (ie WGS84) use Rhino's internal projection
            ///this is still buggy as it doesn't work with other geographic systems like NAD27
            if ((userSRS.IsProjected() == 0) && (userSRS.IsLocal() == 0))
            {
                userEapPlane.Transform(WGSToXYZTransform());
                scale = WGSToXYZTransform();
            }
            
            Transform shift = Transform.ChangeBasis(eapPlane, userEapPlane);

            Transform shiftScale = Transform.Multiply(scale, shift);

            return shiftScale;
        }

        public static Transform GetTwinLandSRSToUserSRSTransform(OSGeo.OSR.SpatialReference userSRS)
        {
            var xyzToUserSRS = GetUserSRSToTwinLandSRSTransform(userSRS);
            if (xyzToUserSRS.TryGetInverse(out Transform transform))
            {
                return transform;
            }

            return Transform.Unset;
        }

        public static Point3d OSRTransformPoint3dToPoint3d(Point3d pt,
            OSGeo.OSR.CoordinateTransformation coordinateTransformation)
        {
            double[] ptArray = new double[3] { pt.X, pt.Y, pt.Z };
            coordinateTransformation.TransformPoint(ptArray);
            return new Point3d(ptArray[0], ptArray[1], ptArray[2]);
        }

        public static Point3d OgrPointToPoint3d(OSGeo.OGR.Geometry ogrPoint, Transform transform)
        {
            Point3d pt3d = new Point3d(ogrPoint.GetX(0), ogrPoint.GetY(0), ogrPoint.GetZ(0));
            pt3d.Transform(transform);
            
            return pt3d;
        }
        
        /// <summary>
        /// transform by math
        /// </summary>
        /// <param name="lon"></param>
        /// <param name="spatRef"></param>
        /// <returns></returns>
        public static double ConvertLon(double lon, int spatRef)
        {
            double clon = lon;
            if (spatRef == 3857)
            {
                double y = Math.Log(Math.Tan((90 + lon) * Math.PI / 360)) / (Math.PI / 180);
                y = y * 20037508.34 / 180;
                clon = y;
            }
            return clon;
        }

        public static double ConvertLat(double lat, int spatRef)
        {
            double clat = lat;
            if (spatRef == 3857)
            {
                double x = lat * 20037508.34 / 180;
                clat = x;
            }
            return clat;
        }
        
        //////////////////////////////////////////////////////
        ///Convert Degree Minute Second (DMS) format to Decimal Degree (DD)
        ///Relies on regular expressions
        ///Escape double quotes by adding another double quote
        ///https://stackoverflow.com/questions/2148587/finding-quoted-strings-with-escaped-quotes-in-c-sharp-using-a-regular-expression
        ///Regex pattern from here
        ///https://www.regexlib.com/(A(01GR39szfGm1gLcdC4FCoPEJDFXu6LgRzwFx-1isNi69fZ3psu9BCC6xQRLCI08-mp4YPR2aya9kigMOk--fo1CP9WhvURrgSaKBoq5nBO0FZx4UYdIDOmf6CVWTvL5-0CMYvve2eO0C7V8jmxo4zhmwBNlD3k4IEHtggeP5JWiqxCVLAgvXn0aTRUgVXKmZ0))/Search.aspx?k=longitude&c=-1&m=-1&ps=20&AspxAutoDetectCookieSupport=1
        ///https://stackoverflow.com/questions/4123455/how-do-i-match-an-entire-string-with-a-regex
        ///https://stackoverflow.com/questions/5970961/regular-expression-javascript-convert-degrees-minutes-seconds-to-decimal-degree/5971628
        ///https://stackoverflow.com/questions/48534863/splitting-a-string-containing-a-longitude-or-latitude-expression-in-perl
        ///Latitude and Longitude in Degrees Minutes Seconds (DMS) zero padded, separated by spaces or : or (d, m, s) or (°, ', ") 
        ///or run together and followed by cardinal direction initial (N,S,E,W) Longitude Degree range: -180 to 180 Latitude Degree range: -90 to 90 
        ///Minute range: 0 to 60 Second range: 0.00 to 60.00 Note: Only seconds can have decimals places. A decimal point with no trailing digits is invalid.
        ///Examples of valid formats
        ///40:26:46N,079:56:55W | 40°26′47″N 079°58′36″W | 40d 26m 47s N 079d 58′ 36″ W | 90 00 00.0, 180 00 00.0 | 89 59 50.4141 S c | 00 00 00.0, 000 00 00.0

        public static double DMStoDDLon(string dms)
        {
            ///Regex pattern for verifying DMS Longitude is with -180 to 180
            var lonDMSPattern = @"[ ,]*(-?(180[ :°d]*00[ :\'\'m]*00(\.0+)?|(1[0-7][0-9]|0[0-9][0-9]|[0-9][0-9])[ :°d]*[0-5][0-9][ :\'\'m]*[0-5][0-9](\.\d+)?)[ :\?\""s]*(E|e|W|w)?)";
            
            ///Regex pattern DD Longitude is with -180 to 180
            var lonDDPattern = @"^(\+|-)?(?:180(?:(?:\.0{1,20})?)|(?:[0-9]|[1-9][0-9]|1[0-7][0-9])(?:(?:\.[0-9]{1,20})?))$";
            
            ///Get rid of any white spaces
            dms = dms.Trim();

            double coordinate = Double.NaN;

            ///Test if the Lon is DD format and return as double if valid
            if (Regex.Match(dms, lonDDPattern).Success)
            {
                return Double.Parse(dms);
            }

            ///Else test if the Lon is DMS format and convert to double DD and return if valid
            else if (Regex.Match(dms, lonDMSPattern).Success && Regex.Match(dms, lonDMSPattern).Value.Length == dms.Length)
            {
                bool sw = dms.ToLower().EndsWith("w");
                int f = sw ? -1 : 1;
                var bits = Regex.Matches(dms, @"[\d.]+", RegexOptions.IgnoreCase);
                coordinate = 0;
                double result;
                for (int i = 0; i < bits.Count; i++)
                {
                    if (Double.TryParse(bits[i].ToString(), out result))
                    {
                        coordinate += result / f;
                        f *= 60;
                    }
                }

                return coordinate;
            }

            ///If DD or DMS format is invalid, return NaN
            else
            {
                return coordinate;
            }
        }

        public static double DMStoDDLat(string dms)
        {
            ///Regex pattern for verifying DMS Latitude is with -90 to 90
            var latDMSPattern = @"(-?(90[ :°d]*00[ :\'\'m]*00(\.0+)?|[0-8][0-9][ :°d]*[0-5][0-9][ :\'\'m]*[0-5][0-9](\.\d+)?)[ :\?\""s]*(N|n|S|s)?)";
            
            ///Regex pattern DD Latitude is with -90 to 90
            var latDDPattern = @"^(\+|-)?(?:90(?:(?:\.0{1,20})?)|(?:[0-9]|[1-8][0-9])(?:(?:\.[0-9]{1,20})?))$";

            ///Get rid of any white spaces
            dms = dms.Trim();

            double coordinate = Double.NaN;

            ///Test if the Lat is DD format and return as double if valid
            if (Regex.Match(dms, latDDPattern).Success)
            {
                return Double.Parse(dms);
            }
            
            ///Else test if the Lat is DMS format and convert to double DD and return if valid
            else if (Regex.Match(dms, latDMSPattern).Success && Regex.Match(dms, latDMSPattern).Value.Length == dms.Length)
            {
                bool sw = dms.ToLower().EndsWith("s");
                int f = sw ? -1 : 1;
                var bits = Regex.Matches(dms, @"[\d.]+", RegexOptions.IgnoreCase);
                coordinate = 0;
                double result;
                for (int i = 0; i < bits.Count; i++)
                {
                    if (Double.TryParse(bits[i].ToString(), out result))
                    {
                        coordinate += result / f;
                        f *= 60;
                    }
                }
                return coordinate;
            }

            ///If DD or DMS format is invalid, return NaN
            else
            {
                return coordinate;
            }
        }

        public static string HttpToJson(string URL)
        {
            System.Net.ServicePointManager.Expect100Continue = true;
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            
            // get json from rest service
            System.Net.HttpWebRequest req = System.Net.WebRequest.Create(URL) as System.Net.HttpWebRequest;
            string result = null;

            using (System.Net.HttpWebResponse resp = req.GetResponse() as System.Net.HttpWebResponse)
            {
                StreamReader reader = new StreamReader(resp.GetResponseStream());
                result = reader.ReadToEnd();
                reader.Close();
            }

            return result;
        }

        public static string DownloadHttpImage(string URL, string fileName)
        {
            System.Net.ServicePointManager.Expect100Continue = true;
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            System.Net.HttpWebRequest request = null;
            System.Net.HttpWebResponse response = null;
            request = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(URL);
            try
            {
                response = (System.Net.HttpWebResponse)request.GetResponse();

                using (var file = File.OpenWrite(fileName))
                using (Stream stream = response.GetResponseStream())
                {
                    if (stream == null) return "No response from the server";
                    else stream.CopyTo(file);
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }

            return "";
        }

        ///Get list of mapping service Endpoints
        public static string GetEndpoints()
        {
            string jsonString = string.Empty;

            // get from github
            using (System.Net.WebClient wc = new System.Net.WebClient())
            {
                string URI = "https://raw.githubusercontent.com/JiaQiiZheng/TwinLand/main/TwinLand/Resources/TwinLandServiceEndpoints.json";
                jsonString = wc.DownloadString(URI);
            }
            
            // get from local resources
            if (string.IsNullOrEmpty(jsonString))
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fileName = "TwinLand.Resources.TwinLandServiceEndpoints.json";
                
                using(Stream stream = assembly.GetManifestResourceStream(fileName))
                using (StreamReader reader = new StreamReader(stream))
                {
                    jsonString = reader.ReadToEnd();
                }

                Console.WriteLine(jsonString);
            }
            
            return jsonString;
        }
        
        public static List<IGH_GeometricGoo> OgrGeomToGHGoo(OSGeo.OGR.Geometry geom, Transform transform)
        {
            List<IGH_GeometricGoo> gGoo = new List<IGH_GeometricGoo>();

            switch (geom.GetGeometryType())
            {
                case wkbGeometryType.wkbGeometryCollection:
                case wkbGeometryType.wkbGeometryCollection25D:
                case wkbGeometryType.wkbGeometryCollectionM:
                case wkbGeometryType.wkbGeometryCollectionZM:
                    OSGeo.OGR.Geometry sub_geom;
                    for (int gi = 0; gi < geom.GetGeometryCount(); gi++)
                    {
                        sub_geom = geom.GetGeometryRef(gi);
                        gGoo.AddRange(GetGoo(sub_geom, transform));
                        sub_geom.Dispose();
                    }
                    break;

                default:
                    gGoo = GetGoo(geom, transform);
                    break;
            }

            return gGoo;
        }
        
        public static List<IGH_GeometricGoo> GetGoo(OSGeo.OGR.Geometry geom, Transform transform)
        {
            List<IGH_GeometricGoo> gGoo = new List<IGH_GeometricGoo>();

            OSGeo.OGR.Geometry sub_geom;

            //find appropriate geometry type in feature and convert to Rhino geometry
            switch (geom.GetGeometryType())
            {
                case wkbGeometryType.wkbPoint25D:
                case wkbGeometryType.wkbPointM:
                case wkbGeometryType.wkbPointZM:
                case wkbGeometryType.wkbPoint:
                    gGoo.Add(new GH_Point(TwinLand.Convert.OgrPointToPoint3d(geom, transform)));
                    break;

                case wkbGeometryType.wkbMultiPoint25D:
                case wkbGeometryType.wkbMultiPointZM:
                case wkbGeometryType.wkbMultiPointM:
                case wkbGeometryType.wkbMultiPoint:
                    List<GH_Point> gH_Points = new List<GH_Point>();
                    foreach (Point3d p in TwinLand.Convert.OgrMultiPointToPoint3d(geom, transform)) gH_Points.Add(new GH_Point(p));
                    gGoo.AddRange(gH_Points);
                    break;

                case wkbGeometryType.wkbLinearRing:
                    gGoo.Add(new GH_Curve(TwinLand.Convert.OgrLinestringToCurve(geom, transform)));
                    break;

                case wkbGeometryType.wkbLineString25D:
                case wkbGeometryType.wkbLineStringM:
                case wkbGeometryType.wkbLineStringZM:
                case wkbGeometryType.wkbLineString:
                    gGoo.Add(new GH_Curve(TwinLand.Convert.OgrLinestringToCurve(geom, transform)));
                    break;

                case wkbGeometryType.wkbMultiLineString25D:
                case wkbGeometryType.wkbMultiLineStringZM:
                case wkbGeometryType.wkbMultiLineStringM:
                case wkbGeometryType.wkbMultiLineString:
                    List<GH_Curve> gH_Curves = new List<GH_Curve>();
                    foreach (Curve crv in TwinLand.Convert.OgrMultiLinestringToCurves(geom, transform)) gH_Curves.Add(new GH_Curve(crv));
                    gGoo.AddRange(gH_Curves);
                    break;

                case wkbGeometryType.wkbPolygonZM:
                case wkbGeometryType.wkbPolygonM:
                case wkbGeometryType.wkbPolygon25D:
                case wkbGeometryType.wkbPolygon:
                    gGoo.Add(new GH_Mesh(TwinLand.Convert.OgrPolygonToMesh(geom, transform)));
                    break;

                case wkbGeometryType.wkbMultiPolygonZM:
                case wkbGeometryType.wkbMultiPolygon25D:
                case wkbGeometryType.wkbMultiPolygonM:
                case wkbGeometryType.wkbMultiPolygon:
                case wkbGeometryType.wkbSurface:
                case wkbGeometryType.wkbSurfaceZ:
                case wkbGeometryType.wkbSurfaceZM:
                case wkbGeometryType.wkbSurfaceM:
                case wkbGeometryType.wkbPolyhedralSurface:
                case wkbGeometryType.wkbPolyhedralSurfaceM:
                case wkbGeometryType.wkbPolyhedralSurfaceZ:
                case wkbGeometryType.wkbPolyhedralSurfaceZM:
                case wkbGeometryType.wkbTINZ:
                case wkbGeometryType.wkbTINM:
                case wkbGeometryType.wkbTINZM:
                case wkbGeometryType.wkbTIN:
                    Mesh[] mDis = TwinLand.Convert.OgrMultiPolyToMesh(geom, transform).SplitDisjointPieces();
                    foreach (var mPiece in mDis)
                    {
                        gGoo.Add(new GH_Mesh(mPiece));
                    }
                    break;

                default:

                    ///If Feature is of an unrecognized geometry type
                    ///Loop through geometry points

                    for (int gpc = 0; gpc < geom.GetPointCount(); gpc++)
                    {
                        double[] ogrPt = new double[3];
                        geom.GetPoint(gpc, ogrPt);
                        Point3d pt3D = new Point3d(ogrPt[0], ogrPt[1], ogrPt[2]);
                        pt3D.Transform(transform);
                        gGoo.Add(new GH_Point(pt3D));
                    }


                    for (int gi = 0; gi < geom.GetGeometryCount(); gi++)
                    {
                        sub_geom = geom.GetGeometryRef(gi);
                        List<Point3d> geom_list = new List<Point3d>();

                        for (int ptnum = 0; ptnum < sub_geom.GetPointCount(); ptnum++)
                        {
                            double[] pT = new double[3];
                            pT[0] = sub_geom.GetX(ptnum);
                            pT[1] = sub_geom.GetY(ptnum);
                            pT[2] = sub_geom.GetZ(ptnum);

                            Point3d pt3D = new Point3d();
                            pt3D.X = pT[0];
                            pt3D.Y = pT[1];
                            pt3D.Z = pT[2];

                            pt3D.Transform(transform);
                            gGoo.Add(new GH_Point(pt3D));
                        }
                        sub_geom.Dispose();
                    }
                    break;

            }

            return gGoo;
        }
        
        public static List<Point3d> OgrMultiPointToPoint3d(OSGeo.OGR.Geometry ogrMultiPoint, Transform transform)
        {
            List<Point3d> ptList = new List<Point3d>();
            OSGeo.OGR.Geometry sub_geom;
            for (int i = 0; i < ogrMultiPoint.GetGeometryCount(); i++)
            {
                sub_geom = ogrMultiPoint.GetGeometryRef(i);
                for (int ptnum = 0; ptnum < sub_geom.GetPointCount(); ptnum++)
                {
                    ptList.Add(TwinLand.Convert.OgrPointToPoint3d(sub_geom, transform));
                }
            }
            return ptList;

        }
        
        public static Curve OgrLinestringToCurve(OSGeo.OGR.Geometry linestring, Transform transform)
        {
            List<Point3d> ptList = new List<Point3d>();
            for (int i = 0; i < linestring.GetPointCount(); i++)
            {
                Point3d pt = new Point3d(linestring.GetX(i), linestring.GetY(i), linestring.GetZ(i));
                pt.Transform(transform);
                ptList.Add(pt);
            }
            Polyline pL = new Polyline(ptList);

            return pL.ToNurbsCurve();
        }

        public static Curve OgrRingToCurve(OSGeo.OGR.Geometry ring, Transform transform)
        {
            double tol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            List<Point3d> ptList = new List<Point3d>();
            for (int i = 0; i < ring.GetPointCount(); i++)
            {
                Point3d pt = new Point3d(ring.GetX(i), ring.GetY(i), ring.GetZ(i));
                pt.Transform(transform);
                ptList.Add(pt);
            }
            //ptList.Add(ptList[0]);
            Polyline pL = new Polyline(ptList);
            Curve crv = pL.ToNurbsCurve();
            //crv.MakeClosed(tol);
            return crv;
        }

        public static List<Curve> OgrMultiLinestringToCurves(OSGeo.OGR.Geometry multilinestring, Transform transform)
        {
            List<Curve> cList = new List<Curve>();
            OSGeo.OGR.Geometry sub_geom;

            for (int i = 0; i < multilinestring.GetGeometryCount(); i++)
            {
                sub_geom = multilinestring.GetGeometryRef(i);
                cList.Add(TwinLand.Convert.OgrLinestringToCurve(sub_geom, transform));
                sub_geom.Dispose();
            }
            return cList;
        }

        public static Mesh OgrPolygonToMesh(OSGeo.OGR.Geometry polygon, Transform transform)
        {
            double tol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            List<Curve> pList = new List<Curve>();
            OSGeo.OGR.Geometry sub_geom;

            for (int i = 0; i < polygon.GetGeometryCount(); i++)
            {
                sub_geom = polygon.GetGeometryRef(i);
                Curve crv = TwinLand.Convert.OgrRingToCurve(sub_geom, transform);
                //possible cause of viewport issue, try not forcing a close.  Other possibility would be trying to convert to (back to) polyline
                //crv.MakeClosed(tol);

                if (!crv.IsClosed && sub_geom.GetPointCount() > 2)
                {
                    Curve closingLine = new Line(crv.PointAtEnd, crv.PointAtStart).ToNurbsCurve();
                    Curve[] result = Curve.JoinCurves(new Curve[] { crv, closingLine });
                    crv = result[0];
                }

                pList.Add(crv);
                sub_geom.Dispose();
            }

            //need to catch if not closed polylines
            Mesh mPatch = new Mesh();

            if (pList[0] != null && pList[0].IsClosed)
            {
                Polyline pL = null;
                pList[0].TryGetPolyline(out pL);
                pList.RemoveAt(0);
                mPatch = Rhino.Geometry.Mesh.CreatePatch(pL, tol, null, pList, null, null, true, 1);

                ///Adds ngon capability
                ///https://discourse.mcneel.com/t/create-ngon-mesh-rhinocommon-c/51796/12
                mPatch.Ngons.AddPlanarNgons(tol);
                mPatch.FaceNormals.ComputeFaceNormals();
                mPatch.Normals.ComputeNormals();
                mPatch.Compact();
                mPatch.UnifyNormals();
            }

            return mPatch;
        }
        
        public static Mesh OgrMultiPolyToMesh(OSGeo.OGR.Geometry multipoly, Transform transform)
        {
            double tol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            OSGeo.OGR.Geometry sub_geom;
            List<Mesh> mList = new List<Mesh>();

            for (int i = 0; i < multipoly.GetGeometryCount(); i++)
            {
                sub_geom = multipoly.GetGeometryRef(i);
                Mesh mP = TwinLand.Convert.OgrPolygonToMesh(sub_geom, transform);
                mP.UnifyNormals();
                mList.Add(mP);
                sub_geom.Dispose();
            }
            Mesh m = new Mesh();
            m.Append(mList);

            //m.Ngons.AddPlanarNgons(tol);
            m.FaceNormals.ComputeFaceNormals();
            m.Normals.ComputeNormals();
            //m.RebuildNormals();
            m.Compact();
            m.UnifyNormals();


            if (m.DisjointMeshCount > 0)
            {
                Mesh[] mDis = m.SplitDisjointPieces();
                Mesh mm = new Mesh();
                foreach (Mesh mPiece in mDis)
                {
                    if (mPiece.SolidOrientation() < 0) mPiece.Flip(false, false, true);
                    mm.Append(mPiece);
                }
                //mm.Ngons.AddPlanarNgons(tol);
                mm.FaceNormals.ComputeFaceNormals();
                mm.Normals.ComputeNormals();
                //mm.RebuildNormals();
                mm.Compact();
                mm.UnifyNormals();

                return mm;

            }
            else
            {
                return m;
            }

        }
        
        public static string GetOSMURL(int timeout, string searchTerm, string left, string bottom, string right, string top, string url)
        {
            string search = "(node" + searchTerm + "; way" + searchTerm + "; relation" + searchTerm + ";);(._;>;);";
            string u = url.Replace("{timeout}", timeout.ToString());
            if (searchTerm.Length > 0)
            {
                u = u.Replace("{searchTerm}", search);
            }
            else { u = u.Replace("{searchTerm}", "(node;way;relation;);(._;>;);"); }
            u = u.Replace("{left}", left);
            u = u.Replace("{bottom}", bottom);
            u = u.Replace("{right}", right);
            u = u.Replace("{top}", top);
            return u;
        }
        
        //get the range of tiles that intersect with the bounding box of the polygon
        public static (Interval XRange, Interval YRange) GetTileRange(BoundingBox bnds, int zoom)
        {
            Point3d bndsMin = Convert.XYZToWGS(bnds.Min);
            Point3d bndsMax = Convert.XYZToWGS(bnds.Max);
            double xm = bndsMin.X;
            double xmx = bndsMax.X;
            double ym = bndsMin.Y;
            double ymx = bndsMax.Y;
            List<int> starting = Convert.DegToNum(ymx, xm, zoom);
            List<int> ending = Convert.DegToNum(ym, xmx, zoom);
            var x_range = new Interval(starting[0], ending[0]);
            var y_range = new Interval(starting[1], ending[1]);
            return (x_range, y_range);
        }
        
        //download all tiles within bbox
        public static List<int> DegToNum(double lat_deg, double lon_deg, int zoom)
        {
            double lat_rad = Rhino.RhinoMath.ToRadians(lat_deg);
            int n = (1 << zoom);
            int xtile = (int)Math.Floor((lon_deg + 180.0) / 360 * n);
            int ytile = (int)Math.Floor((1 - Math.Log(Math.Tan(lat_rad) + (1 / Math.Cos(lat_rad))) / Math.PI) / 2.0 * n);
            return new List<int> { xtile, ytile };
        }
        
        //get the tile as a polyline object
        public static Polyline GetTileAsPolygon(int z, int y, int x)
        {
            List<double> nw = Convert.NumToDeg(x, y, z);
            List<double> se = Convert.NumToDeg(x + 1, y + 1, z);
            double xm = nw[1];
            double xmx = se[1];
            double ym = se[0];
            double ymx = nw[0];
            Polyline tile_bound = new Polyline();
            tile_bound.Add(Convert.WGSToXYZ(new Point3d(xm, ym, 0)));
            tile_bound.Add(Convert.WGSToXYZ(new Point3d(xmx, ym, 0)));
            tile_bound.Add(Convert.WGSToXYZ(new Point3d(xmx, ymx, 0)));
            tile_bound.Add(Convert.WGSToXYZ(new Point3d(xm, ymx, 0)));
            tile_bound.Add(Convert.WGSToXYZ(new Point3d(xm, ym, 0)));
            return tile_bound;
        }
        
        public static List<double> NumToDeg(int xtile, int ytile, int zoom)
        {
            double n = Math.Pow(2, zoom);
            double lon_deg = xtile / n * 360 - 180;
            double lat_rad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * ytile / n)));
            double lat_deg = Rhino.RhinoMath.ToDegrees(lat_rad);
            return new List<double> { lat_deg, lon_deg };
        }
        
        ///Check if cached images exist in cache folder
        public static bool CheckCacheImagesExist(List<string> filePaths)
        {
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                    return false;
            }
            return true;
        }
        
        //convert the generic URL to get the specific URL of the tile
        public static string GetZoomURL(int x, int y, int z, string url)
        {
            string u = url.Replace("{x}", x.ToString());
            u = u.Replace("{y}", y.ToString());
            u = u.Replace("{z}", z.ToString());
            return u;
        }
    }
    
    public static class BitmapExtension
    {
        public static void AddCommentsToJPG(this Bitmap bitmap, string comment)
        {
            //add tile range meta data to image comments
            //doesn't work for png, need to find a common ID between jpg and png
            //https://stackoverflow.com/questions/18820525/how-to-get-and-set-propertyitems-for-an-image/25162782#25162782
            var newItem = (System.Drawing.Imaging.PropertyItem)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(System.Drawing.Imaging.PropertyItem));
            newItem.Id = 40092;
            newItem.Type = 1;
            newItem.Value = Encoding.Unicode.GetBytes(comment);
            newItem.Len = newItem.Value.Length;
            bitmap.SetPropertyItem(newItem);
        }

        public static string GetCommentsFromJPG(this Bitmap bitmap)
        {
            //doesn't work for png
            System.Drawing.Imaging.PropertyItem prop = bitmap.GetPropertyItem(40092);
            string comment = Encoding.Unicode.GetString(prop.Value);
            return comment;
        }

        public static void AddCommentsToPNG(this Bitmap bitmap, string comment)
        {
            //add tile range meta data to image comments
            //ID:40094 doesn't seem to work for png and 40092 only works for JPG
            //https://stackoverflow.com/questions/18820525/how-to-get-and-set-propertyitems-for-an-image/25162782#25162782
            var newItem = (System.Drawing.Imaging.PropertyItem)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(System.Drawing.Imaging.PropertyItem));
            newItem.Id = 40094;
            newItem.Type = 1;
            newItem.Value = Encoding.Unicode.GetBytes(comment);
            newItem.Len = newItem.Value.Length;
            bitmap.SetPropertyItem(newItem);
        }

        public static string GetCommentsFromPNG(this Bitmap bitmap)
        {
            //doesn't work for png
            System.Drawing.Imaging.PropertyItem prop = bitmap.GetPropertyItem(40094);
            string comment = Encoding.Unicode.GetString(prop.Value);
            return comment;
        }
    }
}
