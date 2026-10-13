using Avalonia.Input;

namespace AVAArkanoid.Engine;

/// <summary>
/// Keyboard, mouse and gamepad boiled down to the controls: left and right, fire, and where the mouse
/// is. The mouse steers the paddle once it moves, until a key or the stick is used again.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly ControllerManager _pad = new(0);
    private double? _mouseX;
    private bool _mouseFire;

    public void OnKeyDown(Key key)
    {
        _held.Remove(key);
        _held.Add(key);
        if (key is Key.Left or Key.Right or Key.A or Key.D) _mouseX = null;
    }

    public void OnKeyUp(Key key) => _held.Remove(key);
    public bool Has(Key key) => _held.Contains(key);

    /// <summary>The mouse is over the playfield at this x (in game pixels).</summary>
    public void OnMouse(double x) => _mouseX = x;
    public void OnMouseButton(bool down) => _mouseFire = down;

    public void Update() => _pad.Update();

    public Controls Controls
    {
        get
        {
            double dx = 0;
            for (int i = _held.Count - 1; i >= 0 && dx == 0; i--)
                dx = _held[i] is Key.Left or Key.A or Key.NumPad4 ? -1 : _held[i] is Key.Right or Key.D or Key.NumPad6 ? 1 : 0;
            if (dx == 0) dx = _pad.DPadLeft ? -1 : _pad.DPadRight ? 1 : Math.Abs(_pad.LeftStickX) > 0.2f ? _pad.LeftStickX : 0;
            if (dx != 0) _mouseX = null;
            bool fire = Has(Key.Space) || Has(Key.Z) || Has(Key.Up) || Has(Key.W) || Has(Key.LeftCtrl) || Has(Key.RightCtrl) || _mouseFire || _pad.ButtonA || _pad.ButtonB;
            return new Controls(dx, fire, _mouseX);
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.Space) || Has(Key.D1) || _pad.ButtonStart || _pad.ButtonA;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
