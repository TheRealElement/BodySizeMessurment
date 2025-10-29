using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BodySize.Client.Rendering
{
    public static class MannequinMorpher
    {
        // Baseline circumferences (approx) of mannequin
        public static double BaseChest = 100;
        public static double BaseWaist = 82;
        public static double BaseHips  = 96;

        // Fractions of height where bands sit
        public static double YChest = 0.72;
        public static double YWaist = 0.60;
        public static double YHips  = 0.50;

        public static MeshGeometry3D MorphToMeasurements(
            MeshGeometry3D baseMesh, double heightCm, double chestC, double waistC, double hipC)
        {
            var clone = new MeshGeometry3D
            {
                TriangleIndices = new Int32Collection(baseMesh.TriangleIndices),
                Positions = new Point3DCollection(baseMesh.Positions)
            };

            double chestR = chestC / (2 * Math.PI);
            double waistR = waistC / (2 * Math.PI);
            double hipR   = hipC   / (2 * Math.PI);

            double baseChestR = BaseChest / (2 * Math.PI);
            double baseWaistR = BaseWaist / (2 * Math.PI);
            double baseHipsR  = BaseHips  / (2 * Math.PI);

            double sChest = chestR / baseChestR;
            double sWaist = waistR / baseWaistR;
            double sHips  = hipR   / baseHipsR;

            var b = baseMesh.Bounds;
            double y0 = b.Y;
            double yChest = y0 + heightCm * YChest * 0.01; // model units in meters if using cm? We'll keep 1 unit = 1 cm
            double yWaist = y0 + heightCm * YWaist * 0.01;
            double yHips  = y0 + heightCm * YHips  * 0.01;

            // Since our OBJ is generated in cm units, don't scale by 0.01:
            yChest = b.Y + b.SizeY * YChest;
            yWaist = b.Y + b.SizeY * YWaist;
            yHips  = b.Y + b.SizeY * YHips;

            double sigma = heightCm * 0.04;

            for (int i = 0; i < clone.Positions.Count; i++)
            {
                var p = clone.Positions[i];
                double r = Math.Sqrt(p.X * p.X + p.Z * p.Z);
                if (r < 1e-6) continue;

                double wc = Gauss(p.Y, yChest, sigma);
                double ww = Gauss(p.Y, yWaist, sigma);
                double wh = Gauss(p.Y, yHips,  sigma);
                double denom = wc + ww + wh + 1.0;
                double scale = (wc * sChest + ww * sWaist + wh * sHips + 1.0) / denom;

                double k = scale;
                clone.Positions[i] = new Point3D(p.X * k, p.Y, p.Z * k);
            }

            return clone;
        }

        private static double Gauss(double x, double mu, double sigma)
        {
            double d = (x - mu) / sigma;
            return Math.Exp(-0.5 * d * d);
        }
    }
}