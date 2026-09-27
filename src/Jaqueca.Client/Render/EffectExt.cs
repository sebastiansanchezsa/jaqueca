using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Render;

/// <summary>El compilador de shaders elimina los uniforms que no se usan: estos setters los toleran.</summary>
public static class EffectExt
{
    public static void Set(this Effect fx, string name, float v) => fx.Parameters[name]?.SetValue(v);
    public static void Set(this Effect fx, string name, Vector2 v) => fx.Parameters[name]?.SetValue(v);
    public static void Set(this Effect fx, string name, Vector3 v) => fx.Parameters[name]?.SetValue(v);
    public static void Set(this Effect fx, string name, Vector4 v) => fx.Parameters[name]?.SetValue(v);
    public static void Set(this Effect fx, string name, Vector4[] v) => fx.Parameters[name]?.SetValue(v);
    public static void Set(this Effect fx, string name, Matrix v) => fx.Parameters[name]?.SetValue(v);
    public static void Set(this Effect fx, string name, Texture v) => fx.Parameters[name]?.SetValue(v);
}
