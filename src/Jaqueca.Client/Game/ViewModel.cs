using Jaqueca.Client.Render;
using Jaqueca.Figures.Content;
using Jaqueca.Figures.Rig;
using Microsoft.Xna.Framework;
using NVec3 = System.Numerics.Vector3;

namespace Jaqueca.Client.Game;

/// <summary>
/// Lo que Ernesto tiene en las manos, delante de la cámara: el arma (y la mano de la corredera), y la
/// pierna cuando patea. Se mueve con el cuerpo: se hamaca al correr, llega tarde a donde mira (la
/// inercia del brazo), salta para atrás con cada tiro, baja y sube al cambiar de arma, y la pierna entra
/// de abajo en la patada. Todo es un par de matrices por cuadro para los "huesos" de las piezas.
/// </summary>
public sealed class ViewModel
{
    private readonly FoeArt _art;
    private readonly Matrix[] _gun = new Matrix[FigureMesh.MaxBones], _leg = new Matrix[FigureMesh.MaxBones];

    /// <summary>El arma que se ve (0 revólver, 1 escopeta) y la que viene (si está cambiando).</summary>
    public int Shown;
    private int _want;
    private float _switch;           // 0 arriba, 1 abajo del todo
    private float _recoil, _recoilV; // retroceso (se amortigua como un resorte)
    private float _pump = 10;        // segundos desde el tiro de escopeta (la corredera va y vuelve)
    private float _kick = 10;        // segundos desde la patada
    private float _bob, _bobAmp;
    private Vector2 _sway, _swayV;
    private float _land;
    /// <summary>Carga del tiro que atraviesa (0..1): el revólver tiembla.</summary>
    public float Charge;
    public const float KickTime = 0.42f, PumpTime = 0.7f;

    public ViewModel(FoeArt art) { _art = art; }

    /// <summary>
    /// Dónde va cada arma respecto de los ojos (adelante, arriba, derecha), cómo se gira (hacia adentro,
    /// hacia arriba, sobre el caño: así se le ve el costado) y a qué escala.
    /// </summary>
    public static Vector3 RevolverAt = new(8.0f, -3.4f, 3.8f), RevolverTurn = new(0.27f, 0.07f, 0.25f);
    public static Vector3 ShotgunAt = new(8.2f, -3.6f, 3.4f), ShotgunTurn = new(0.28f, 0.06f, 0.3f);
    public static float RevolverScale = 1.1f, ShotgunScale = 0.8f;

    /// <summary>Guarda una y saca la otra.</summary>
    public void Switch(int weapon) => _want = weapon;
    public bool Switching => _want != Shown || _switch > 0.01f;
    public void Fire(float strength) { _recoilV += strength; if (Shown == 1) _pump = 0; }
    public void Kick() => _kick = 0;
    public void Land(float impact) => _land = MathF.Min(1, _land + impact / 400);
    /// <summary>¿La pierna está en el punto de la patada que pega?</summary>
    public bool KickActive => _kick > 0.08f && _kick < 0.2f;

    public void Update(float dt, float speed, bool grounded, Vector2 look)
    {
        // El cambio de arma: baja la que está, cambia abajo y sube la otra.
        if (_want != Shown) { _switch += dt * 7; if (_switch >= 1) { _switch = 1; Shown = _want; } }
        else _switch = MathF.Max(0, _switch - dt * 7);
        // El retroceso: un resorte amortiguado.
        _recoilV += (-_recoil * 180 - _recoilV * 22) * dt;
        _recoil += _recoilV * dt;
        _pump += dt;
        _kick += dt;
        _land = MathF.Max(0, _land - dt * 3);
        float moving = grounded ? Math.Clamp(speed / Player.RunSpeed, 0, 1.3f) : 0;
        _bobAmp += (moving - _bobAmp) * MathF.Min(1, dt * 8);
        _bob += dt * speed * 0.085f;
        // La inercia: el arma se queda atrás de lo que mira la cabeza y vuelve.
        var want = -look * 0.9f;
        _swayV += ((want - _sway) * 90 - _swayV * 14) * dt;
        _sway += _swayV * dt;
        _sway = Vector2.Clamp(_sway, new Vector2(-0.6f), new Vector2(0.6f));
    }

    private static Matrix Frame(FpsCamera cam, Vector3 offset, float yaw, float pitch, float roll, float scale = 1)
    {
        // Ejes de la pieza: X adelante, Y arriba, Z derecha (los de la cámara, girados un poco).
        var rot = Matrix.CreateFromYawPitchRoll(0, 0, 0);
        var q = Quaternion.CreateFromAxisAngle(cam.Up, yaw) * Quaternion.CreateFromAxisAngle(cam.Right, pitch) * Quaternion.CreateFromAxisAngle(cam.Forward, roll);
        var f = Vector3.Transform(cam.Forward, q) * scale;
        var u = Vector3.Transform(cam.Up, q) * scale;
        var r = Vector3.Transform(cam.Right, q) * scale;
        var p = cam.Eye + cam.Right * offset.Z + cam.Up * offset.Y + cam.Forward * offset.X;
        return new Matrix(f.X, f.Y, f.Z, 0, u.X, u.Y, u.Z, 0, r.X, r.Y, r.Z, 0, p.X, p.Y, p.Z, 1) * rot;
    }

    /// <summary>Carga lo que se ve en la mano en este cuadro.</summary>
    public void Draw(Renderer r, float time)
    {
        var cam = r.Cam;
        float bx = MathF.Sin(_bob) * 0.22f * _bobAmp, by = -MathF.Abs(MathF.Cos(_bob)) * 0.2f * _bobAmp;
        float down = _switch * _switch * 5f + _land * 0.8f;
        float shake = Charge > 0 ? MathF.Sin(time * 70) * 0.03f * Charge : 0;
        var mesh = Shown == 0 ? _art.Revolver : _art.Shotgun;
        // Dónde queda la mano del arma: abajo a la derecha, un poco adentro.
        var off = Shown == 0 ? RevolverAt : ShotgunAt;
        off += new Vector3(-_recoil * 1.2f, by - down + _sway.Y * 0.4f, bx + _sway.X * 0.5f + shake);
        float pitch = _recoil * 0.9f + _sway.Y * 0.25f - _switch * 0.6f + (Shown == 0 ? RevolverTurn.Y : ShotgunTurn.Y);
        float yaw = (Shown == 0 ? RevolverTurn.X : ShotgunTurn.X) + _sway.X * 0.18f;
        float roll = (Shown == 0 ? RevolverTurn.Z : ShotgunTurn.Z) + bx * 0.1f;
        var hand = Frame(cam, off, yaw, pitch, roll, Shown == 0 ? RevolverScale : ShotgunScale);
        for (int i = 0; i < _gun.Length; i++) _gun[i] = hand;
        if (Shown == 1)
        {
            // La corredera: atrás y adelante después del tiro.
            float t = _pump / PumpTime;
            float back = t < 1 ? MathF.Sin(MathF.Min(1, t * 1.2f) * MathF.PI) : 0;
            var pumpAt = Ernesto.ShotgunPump - new NVec3(back * 1.1f, 0, 0);
            _gun[(int)Bone.HandL] = Matrix.CreateTranslation(pumpAt.X, pumpAt.Y, pumpAt.Z) * hand;
        }
        r.ViewModel.Add(new FigureDraw { Mesh = mesh, Bones = _gun });

        // La patada: la pierna entra de abajo, pega en el medio y vuelve.
        if (_kick < KickTime)
        {
            float t = _kick / KickTime;
            float up = t < 0.3f ? t / 0.3f : 1 - (t - 0.3f) / 0.7f;
            up = MathF.Sin(up * MathF.PI / 2);
            var legOff = new Vector3(2.5f + up * 3.0f, -9.5f + up * 5.3f, 0.6f - up * 0.6f);
            var leg = Frame(cam, legOff, 0.05f, -0.9f + up * 0.75f, 0);
            for (int i = 0; i < _leg.Length; i++) _leg[i] = leg;
            r.ViewModel.Add(new FigureDraw { Mesh = _art.Leg, Bones = _leg });
        }
    }

    /// <summary>Dónde está la punta del caño en el mundo (de ahí sale el fogonazo).</summary>
    public Vector3 Muzzle(FpsCamera cam)
    {
        var m = _gun[(int)Bone.HandR];
        var p = Shown == 0 ? Ernesto.RevolverMuzzle : Ernesto.ShotgunMuzzle;
        return Vector3.Transform(new Vector3(p.X, p.Y, p.Z), m);
    }
}
