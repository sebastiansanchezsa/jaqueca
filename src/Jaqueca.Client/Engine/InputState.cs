using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Jaqueca.Client.Engine;

/// <summary>
/// Teclado, mouse y mando de este cuadro y del anterior. El mouse es para mirar: se usa en modo
/// relativo (el cursor queda atrapado y escondido y sólo cuenta cuánto se movió). Se le pide a SDL
/// (lo que usa MonoGame por debajo); si no se puede, se vuelve a centrar el cursor en cada cuadro.
/// </summary>
public sealed class InputState
{
    private KeyboardState _k, _pk;
    private MouseState _m, _pm;
    private GamePadState _g, _pg;

    /// <summary>Cuánto se movió el mouse en este cuadro (píxeles de pantalla).</summary>
    public Vector2 MouseDelta { get; private set; }
    public int ScrollDelta => _m.ScrollWheelValue - _pm.ScrollWheelValue;
    public bool UsingGamepad { get; private set; }

    private bool _relative, _sdlFailed, _wasActive;

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_SetRelativeMouseMode(int enabled);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint SDL_GetRelativeMouseState(out int x, out int y);

    public void Update(bool active, GameWindow window)
    {
        _pk = _k; _pm = _m; _pg = _g;
        _k = active ? Keyboard.GetState() : default;
        _m = active ? Mouse.GetState() : _pm;
        _g = GamePad.GetState(PlayerIndex.One);

        MouseDelta = Vector2.Zero;
        if (active) Capture(window);
        else if (_relative) Release();
        _wasActive = active;

        if (_g.IsConnected && (_g.ThumbSticks.Left.LengthSquared() > 0.1f || _g.ThumbSticks.Right.LengthSquared() > 0.1f || _g.Buttons != _pg.Buttons))
            UsingGamepad = true;
        if (MouseDelta != Vector2.Zero || _k.GetPressedKeyCount() > 0)
            UsingGamepad = false;
    }

    private void Capture(GameWindow window)
    {
        if (!_sdlFailed)
        {
            try
            {
                if (!_relative) { SDL_SetRelativeMouseMode(1); _relative = true; SDL_GetRelativeMouseState(out _, out _); return; }
                SDL_GetRelativeMouseState(out int dx, out int dy);
                MouseDelta = new Vector2(dx, dy);
                return;
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                _sdlFailed = true;
            }
        }
        // Sin SDL: el cursor vuelve al centro cada cuadro (lo que se movió desde ahí es lo que cuenta).
        var c = new Point(window.ClientBounds.Width / 2, window.ClientBounds.Height / 2);
        if (_wasActive) MouseDelta = new Vector2(_m.X - c.X, _m.Y - c.Y);
        Mouse.SetPosition(c.X, c.Y);
    }

    private void Release()
    {
        try { SDL_SetRelativeMouseMode(0); } catch (Exception) { /* sin SDL no hay nada que soltar */ }
        _relative = false;
    }

    public bool Down(Keys k) => _k.IsKeyDown(k);
    public bool Pressed(Keys k) => _k.IsKeyDown(k) && !_pk.IsKeyDown(k);

    public bool LeftDown => _m.LeftButton == ButtonState.Pressed;
    public bool RightDown => _m.RightButton == ButtonState.Pressed;
    public bool LeftPressed => LeftDown && _pm.LeftButton != ButtonState.Pressed;
    public bool RightPressed => RightDown && _pm.RightButton != ButtonState.Pressed;
    public bool RightReleased => !RightDown && _pm.RightButton == ButtonState.Pressed;

    public GamePadState Pad => _g;
    public bool PadDown(Buttons b) => _g.IsConnected && _g.IsButtonDown(b);
    public bool PadPressed(Buttons b) => _g.IsConnected && _g.IsButtonDown(b) && !_pg.IsButtonDown(b);
}
