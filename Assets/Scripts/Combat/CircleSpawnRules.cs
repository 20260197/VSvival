using System;

namespace CookieBreaker.Combat
{
    public static class CircleSpawnRules
    {
        // sqrt(u) gives a uniform area density, rather than clustering near the center.
        public static bool Sample(double angleUnit, double radiusUnit, double circleRadius, double cookieRadius, out double x, out double y)
        {
            x = y = 0;
            if (double.IsNaN(angleUnit) || double.IsNaN(radiusUnit) || double.IsNaN(circleRadius) || double.IsNaN(cookieRadius) ||
                double.IsInfinity(circleRadius) || angleUnit < 0 || angleUnit > 1 || radiusUnit < 0 || radiusUnit > 1 ||
                cookieRadius < 0 || circleRadius <= cookieRadius) return false;
            double radius = Math.Sqrt(radiusUnit) * (circleRadius - cookieRadius);
            double angle = angleUnit * Math.PI * 2;
            x = Math.Cos(angle) * radius; y = Math.Sin(angle) * radius;
            return true;
        }
        public static bool Contains(double x, double y, double radius, double cookieRadius)
        {
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(radius) || double.IsNaN(cookieRadius) ||
                double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(radius) || double.IsInfinity(cookieRadius) ||
                radius < 0 || cookieRadius < 0) return false;
            double allowed = radius - cookieRadius;
            return allowed >= 0 && x * x + y * y <= allowed * allowed + 1e-10;
        }
    }
}
