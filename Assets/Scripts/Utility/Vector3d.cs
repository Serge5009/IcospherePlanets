using System;

[Serializable]
public struct Vector3d
{
    public double x, y, z;

    public Vector3d(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3d operator *(Vector3d a, double d) => new Vector3d(a.x * d, a.y * d, a.z * d);
    public static Vector3d operator /(Vector3d a, double d) => new Vector3d(a.x / d, a.y / d, a.z / d);

    public double magnitude => Math.Sqrt(x * x + y * y + z * z);
    public Vector3d normalized => magnitude > 0 ? this / magnitude : new Vector3d(0, 0, 0);

    public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3((float)x, (float)y, (float)z);
}