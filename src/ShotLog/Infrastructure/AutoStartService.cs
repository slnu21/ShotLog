using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace ShotLog.Infrastructure;

/// <summary>
/// Toggles "start with Windows". Two backends, picked at runtime:
///  • Packaged (MSIX/Store): the <c>windows.startupTask</c> extension via
///    <see cref="Windows.ApplicationModel.StartupTask"/> — the HKCU Run key is virtualized
///    in the container and would never actually launch, so the Store path must use this.
///  • Unpackaged (dev / portable exe): the HKCU Run key (ported from OrbitDock).
/// The <c>TaskId</c> below must match the StartupTask declared in Package.appxmanifest.
/// </summary>
public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ShotLog";
    private const string TaskId = "ShotLogStartup";

    private static bool? _packaged;

    /// <summary>True when running inside an MSIX package (has package identity).</summary>
    private static bool IsPackaged
    {
        get
        {
            if (_packaged is bool cached) return cached;
            try { _ = Windows.ApplicationModel.Package.Current.Id; _packaged = true; }
            catch { _packaged = false; } // InvalidOperationException when unpackaged
            return _packaged.Value;
        }
    }

    public static bool IsEnabled()
    {
        if (IsPackaged) return PackagedIsEnabled();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        catch { return false; }
    }

    public static void Apply(bool enabled)
    {
        if (IsPackaged) { PackagedApply(enabled); return; }
        if (enabled) Enable(); else Disable();
    }

    /// <summary>Packaged only: the OS won't let us toggle (user disabled it in Task Manager, or policy).</summary>
    public static bool IsLockedByUserOrPolicy()
    {
        if (!IsPackaged) return false;
        try
        {
            var state = GetTask().State;
            return state == Windows.ApplicationModel.StartupTaskState.DisabledByUser
                || state == Windows.ApplicationModel.StartupTaskState.DisabledByPolicy;
        }
        catch { return false; }
    }

    // ---- packaged backend: windows.startupTask ----

    private static Windows.ApplicationModel.StartupTask GetTask()
        => Windows.ApplicationModel.StartupTask.GetAsync(TaskId).AsTask().GetAwaiter().GetResult();

    private static bool PackagedIsEnabled()
    {
        try
        {
            var state = GetTask().State;
            return state == Windows.ApplicationModel.StartupTaskState.Enabled
                || state == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
        }
        catch { return false; }
    }

    private static void PackagedApply(bool enabled)
    {
        try
        {
            var task = GetTask();
            if (enabled)
            {
                // DisabledByUser / DisabledByPolicy cannot be overridden programmatically.
                if (task.State == Windows.ApplicationModel.StartupTaskState.Disabled)
                    task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
            }
            else if (task.State == Windows.ApplicationModel.StartupTaskState.Enabled)
            {
                task.Disable();
            }
        }
        catch { /* non-fatal */ }
    }

    // ---- unpackaged backend: HKCU Run ----

    private static void Enable()
    {
        try
        {
            var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return;
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            key?.SetValue(ValueName, $"\"{exe}\"");
        }
        catch { /* non-fatal */ }
    }

    private static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is not null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { /* non-fatal */ }
    }
}
