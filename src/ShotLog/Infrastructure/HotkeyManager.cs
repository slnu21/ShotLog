using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;

namespace ShotLog.Infrastructure;

/// <summary>
/// Registers global hotkeys against a message-only window and dispatches WM_HOTKEY to
/// per-id callbacks. Self-contained (no external dependency). Ported from OrbitDock.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId;
    private bool _disposed;

    public HotkeyManager()
    {
        var prms = new HwndSourceParameters("ShotLog.HotkeyWindow")
        {
            ParentWindow = Native.HWND_MESSAGE, // message-only window
        };
        _source = new HwndSource(prms);
        _source.AddHook(WndProc);
    }

    /// <summary>Registers <paramref name="gesture"/> (e.g. "Ctrl+Alt+S"). Returns false on parse or OS failure (e.g. conflict).</summary>
    public bool TryRegister(string gesture, Action callback)
    {
        if (_disposed) return false;
        if (!TryParse(gesture, out uint mods, out uint vk)) return false;

        int id = _nextId + 1;
        if (!Native.RegisterHotKey(_source.Handle, id, mods | Native.MOD_NOREPEAT, vk))
            return false;

        _nextId = id;
        _actions[id] = callback;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>Parses a gesture into Win32 modifier flags + virtual-key code.</summary>
    public static bool TryParse(string gesture, out uint mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(gesture)) return false;

        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= Native.MOD_CONTROL; break;
                case "alt": mods |= Native.MOD_ALT; break;
                case "shift": mods |= Native.MOD_SHIFT; break;
                case "win": case "windows": case "meta": case "super": mods |= Native.MOD_WIN; break;
                default: return false;
            }
        }

        if (!TryParseKey(parts[^1], out Key key)) return false;
        int v = KeyInterop.VirtualKeyFromKey(key);
        if (v == 0) return false;
        vk = (uint)v;
        return true;
    }

    private static bool TryParseKey(string token, out Key key)
    {
        // Bare digit → WPF Key.D0..D9
        if (token.Length == 1 && char.IsDigit(token[0])) token = "D" + token;
        return Enum.TryParse(token, ignoreCase: true, out key) && key != Key.None;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var id in _actions.Keys)
            Native.UnregisterHotKey(_source.Handle, id);
        _actions.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
