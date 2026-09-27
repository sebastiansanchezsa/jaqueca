using System.Numerics;

namespace Jaqueca.Mathx;

/// <summary>Helpers matemáticos compartidos por la simulación y el cliente.</summary>
public static class M
{
    public const float Pi = MathF.PI;
    public const float Tau = MathF.PI * 2f;

    public static Vector2 Dir(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));
    public static float Angle(this Vector2 v) => MathF.Atan2(v.Y, v.X);
    public static Vector2 Perp(this Vector2 v) => new(-v.Y, v.X);

    public static Vector2 Rotate(this Vector2 v, float a)
    {
        float c = MathF.Cos(a), s = MathF.Sin(a);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    public static Vector2 SafeNormalize(this Vector2 v)
    {
        float l = v.Length();
        return l > 1e-5f ? v / l : Vector2.Zero;
    }

    public static Vector2 ClampLength(this Vector2 v, float max)
    {
        float l = v.Length();
        return l > max ? v * (max / l) : v;
    }

    public static float WrapAngle(float a)
    {
        a = (a + Pi) % Tau;
        if (a < 0) a += Tau;
        return a - Pi;
    }

    /// <summary>Diferencia con signo más corta para ir de <paramref name="from"/> a <paramref name="to"/>.</summary>
    public static float AngleDiff(float from, float to) => WrapAngle(to - from);

    public static float RotateTowards(float cur, float target, float maxDelta)
    {
        float d = AngleDiff(cur, target);
        if (MathF.Abs(d) <= maxDelta) return WrapAngle(target);
        return WrapAngle(cur + MathF.Sign(d) * maxDelta);
    }

    public static float LerpAngle(float a, float b, float t) => WrapAngle(a + AngleDiff(a, b) * t);

    public static float Approach(float v, float target, float delta)
        => v < target ? MathF.Min(v + delta, target) : MathF.Max(v - delta, target);

    public static Vector2 Approach(Vector2 v, Vector2 target, float delta)
    {
        var d = target - v;
        float l = d.Length();
        return l <= delta || l < 1e-6f ? target : v + d / l * delta;
    }

    public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float InvLerp(float a, float b, float v) => Clamp01((v - a) / (b - a));
    public static float Smooth(float t) { t = Clamp01(t); return t * t * (3 - 2 * t); }
    public static float EaseOutCubic(float t) { t = 1 - Clamp01(t); return 1 - t * t * t; }
    public static float EaseInOutQuad(float t) { t = Clamp01(t); return t < 0.5f ? 2 * t * t : 1 - MathF.Pow(-2 * t + 2, 2) / 2; }

    /// <summary>Suavizado exponencial independiente del framerate.</summary>
    public static float Damp(float a, float b, float sharpness, float dt) => M.Lerp(a, b, 1 - MathF.Exp(-sharpness * dt));
    public static Vector2 Damp(Vector2 a, Vector2 b, float sharpness, float dt) => Vector2.Lerp(a, b, 1 - MathF.Exp(-sharpness * dt));

    public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b, out float t, out Vector2 closest)
    {
        var ab = b - a;
        float len2 = ab.LengthSquared();
        t = len2 < 1e-6f ? 0 : Clamp01(Vector2.Dot(p - a, ab) / len2);
        closest = a + ab * t;
        return Vector2.Distance(p, closest);
    }

    /// <summary>Primer punto (t en [0,1]) donde el segmento a→b entra en el círculo.</summary>
    public static bool SegmentCircle(Vector2 a, Vector2 b, Vector2 c, float r, out float t)
    {
        t = 0;
        var d = b - a;
        var f = a - c;
        float A = Vector2.Dot(d, d);
        if (A < 1e-8f) return f.LengthSquared() <= r * r;
        float B = 2 * Vector2.Dot(f, d);
        float C = Vector2.Dot(f, f) - r * r;
        if (C <= 0) { t = 0; return true; } // arranca adentro
        float disc = B * B - 4 * A * C;
        if (disc < 0) return false;
        disc = MathF.Sqrt(disc);
        float t1 = (-B - disc) / (2 * A);
        if (t1 >= 0 && t1 <= 1) { t = t1; return true; }
        return false;
    }

    public static bool SegmentSegment(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out float t, out float u)
    {
        t = u = 0;
        var r = p2 - p;
        var s = q2 - q;
        float denom = Cross(r, s);
        if (MathF.Abs(denom) < 1e-8f) return false;
        var qp = q - p;
        t = Cross(qp, s) / denom;
        u = Cross(qp, r) / denom;
        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    public static Vector2 Reflect(Vector2 v, Vector2 n) => v - 2 * Vector2.Dot(v, n) * n;
}
