using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace GD
{
    //NEEDS WORK! DOES NOT WORK BETWEEN SCENES. ASSUMES ALL LOCATIONS ARE IN SAME SCENE. BAD.
    public class LocationTools{
        public static Location GetMidpoint(Location u, Location v)
        {
            float mx = u.X + (v.X - u.X) / 2;
            float my = u.Y + (v.Y - u.Y) / 2;
            float mz = u.Z + (v.Z - u.Z) / 2;
            return new Location(u.Scene, new Vector3(mx, my, mz));
        }

        public static float DistanceBetween(Location u, Location v)
        {
            float dx = u.X - v.X;
            float dy = u.Y - v.Y;
            float dz = u.Z - v.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static float FlatDistanceBetween(Location u, Location v)
        {
            float dx = u.X - v.X;
            float dy = u.Y - v.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static Quaternion AngleBetween(Location origin, Location target)
        {
            Vector3 delta = Vector3.Normalize(Vector3.Subtract(target.Vector, origin.Vector));
            Vector3 euler_angle = new Vector3(0, 0, 0);
            euler_angle.X = (float)Math.Atan2(delta.Z, delta.Y) - (float)(Math.PI / 2);
            euler_angle.Y = (float)Math.Atan2(delta.X, Math.Sqrt((delta.Y * delta.Y) + (delta.Z * delta.Z)));
            return Quaternion.CreateFromEulers(euler_angle);
        }
    }
}
