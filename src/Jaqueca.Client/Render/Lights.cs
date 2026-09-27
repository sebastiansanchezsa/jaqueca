using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Render;

public struct Light
{
    public Vector3 Pos;
    public float Radius;
    /// <summary>Color ya multiplicado por la intensidad.</summary>
    public Vector3 Color;
    /// <summary>0..1 cuánto titila (un tubo que falla, el televisor).</summary>
    public float Flicker;
    public int Seed;
}

/// <summary>
/// Luces puntuales de la escena (el mismo criterio que Inquisition). Cada cuadro elige las que más
/// aportan cerca de la cámara y las carga en el shader (hasta <see cref="Max"/>). Las fijas van en
/// <see cref="Items"/>; las que duran un instante (el fogonazo de un tiro, una chispa) en
/// <see cref="Flashes"/>, que se vacía solo: cada una se apaga en su tiempo.
/// </summary>
public sealed class LightSet
{
    public const int Max = 32;

    public readonly List<Light> Items = new();
    private readonly List<(Light l, float life, float age)> _flashes = new();
    private readonly Vector4[] _posR = new Vector4[Max];
    private readonly Vector4[] _col = new Vector4[Max];
    private (float score, Light l)[] _cands = new (float, Light)[64];
    private int _candN;
    public int Count { get; private set; }

    public static Vector3 Rgb(uint c, float i = 1) => new Vector3((c >> 16) & 255, (c >> 8) & 255, c & 255) / 255f * i;

    /// <summary>Un fogonazo: una luz que dura <paramref name="life"/> segundos y se apaga rápido.</summary>
    public void Flash(Vector3 at, float radius, Vector3 color, float life) => _flashes.Add((new Light { Pos = at, Radius = radius, Color = color }, life, 0));

    public void Update(float dt)
    {
        for (int i = _flashes.Count - 1; i >= 0; i--)
        {
            var f = _flashes[i];
            f.age += dt;
            if (f.age >= f.life) _flashes.RemoveAt(i);
            else _flashes[i] = f;
        }
    }

    public static float FlickerOf(Light l, float time) =>
        l.Flicker > 0 ? 1 - l.Flicker * (0.5f + 0.5f * MathF.Sin(time * 7.3f + l.Seed * 3.1f) * MathF.Sin(time * 2.9f + l.Seed)) : 1;

    private void Add(Light l, Vector3 focus)
    {
        float d = Vector3.Distance(l.Pos, focus);
        if (_candN == _cands.Length) Array.Resize(ref _cands, _cands.Length * 2);
        _cands[_candN++] = ((l.Color.X + l.Color.Y + l.Color.Z) * l.Radius / (60 + d), l);
    }

    public void Upload(Effect fx, Vector3 focus, float time)
    {
        _candN = 0;
        foreach (var l0 in Items)
        {
            var l = l0;
            l.Color *= FlickerOf(l, time);
            if (l.Color.X + l.Color.Y + l.Color.Z > 0.01f) Add(l, focus);
        }
        foreach (var (l0, life, age) in _flashes)
        {
            var l = l0;
            float k = 1 - age / life;
            l.Color *= k * k;
            Add(l, focus);
        }
        Array.Sort(_cands, 0, _candN, ByScore);
        Count = Math.Min(Max, _candN);
        for (int i = 0; i < Max; i++)
        {
            if (i < Count)
            {
                var l = _cands[i].l;
                _posR[i] = new Vector4(l.Pos, l.Radius);
                _col[i] = new Vector4(l.Color, 0);
            }
            else { _posR[i] = Vector4.Zero; _col[i] = Vector4.Zero; }
        }
        fx.Set("LightCount", (float)Count);
        fx.Set("LightPosR", _posR);
        fx.Set("LightCol", _col);
    }

    private static readonly Comparer<(float score, Light l)> ByScore = Comparer<(float score, Light l)>.Create((a, b) => b.score.CompareTo(a.score));
}
