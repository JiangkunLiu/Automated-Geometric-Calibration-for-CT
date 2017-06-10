using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace AutoGeometricCalibrationCT.Model
{
    class GeometryParameter
    {
        // Parameters are calculated from multiple beads, each var contains mean and std
        public int ID { get; set; }
        public double[] WidthCenter { get; set; }
        public double[] HeightCenter { get; set; }
        // Inplane rotation
        public double[] SkewAngle { get; set; }
        // Rotation around horizantal line
        public double[] TiltAngle { get; set; }
        // Roation around vertical line
        public double[] SlantAngle { get; set; }
        // Source to detector distance
        public double[] SDD { get; set; }

        public GeometryParameter()
        {
            WidthCenter = new double[2];
            SDD = new double[2];
            HeightCenter = new double[2];
            SkewAngle = new double[2];
            TiltAngle = new double[2];
            SlantAngle = new double[2];
        }

        public void CopyValuesTo(GeometryParameter copy)
        {
            foreach (PropertyInfo pi in typeof(GeometryParameter).GetProperties())
            {
                if (!pi.GetGetMethod().IsVirtual)
                {
                    pi.SetValue(copy, pi.GetValue(this));
                }
            }
        }
    }
}
