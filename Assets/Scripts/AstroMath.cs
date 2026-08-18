using System;

public static class AstroMath
{
    public const double AU_TO_KM = 149597870.7;

    public const double EARTH_MASS_KG = 5.972e24;
    public const double SOLAR_MASS_KG = 1.989e30;

    public const double GRAVITATIONAL_CONSTANT = 6.67430e-20;

    public static double CalculateLuminosity(double solarMasses)
    {
        return Math.Pow(solarMasses, 3.5);
    }

    public static (double innerAU, double outerAU) CalculateHabitableZone(double luminosity)
    {
        double inner = Math.Sqrt(luminosity / 1.1);
        double outer = Math.Sqrt(luminosity / 0.53);
        return (inner, outer);
    }

    public static double CalculateFrostLine(double luminosity)
    {
        return 2.7 * Math.Sqrt(luminosity);
    }
}