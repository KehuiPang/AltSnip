using System;
using System.Runtime.InteropServices;

namespace WuweiShot.Platform;

/// <summary>长截图滚动支撑（Windows）：Windows 的滚轮只发给"焦点窗口"。长截图开始后，
/// 把选区底下要截的目标窗口设为前台焦点——这样用户的原生滚轮就直接滚它，
/// 走系统正常输入管线，Chrome/Edge/Electron/网页这类 Chromium 窗口也照滚不误
/// （对它们 PostMessage 顶层窗口是无效的，所以必须走"聚焦 + 原生滚轮"）。
/// 我方浮层都是 no-activate，不会把焦点抢回来。非 Windows 为 no-op。</summary>
public static class WinScroll
{
    /// <summary>取屏幕坐标点下的窗口句柄（截图目标 App）。</summary>
    public static IntPtr WindowUnder(int x, int y)
        => OperatingSystem.IsWindows() ? WindowFromPoint(new POINT { X = x, Y = y }) : IntPtr.Zero;

    /// <summary>把目标窗口顶到前台并取得焦点，用 AttachThreadInput 绕过前台切换限制。
    /// WindowFromPoint 命中的常是子控件(如 Chromium 的渲染宿主)，聚焦必须用它的顶层窗口
    /// (GA_ROOT)，否则 SetForegroundWindow 无效、滚轮驱动不了目标。返回最终聚焦到的顶层句柄。</summary>
    public static IntPtr FocusTarget(IntPtr target)
    {
        if (!OperatingSystem.IsWindows() || target == IntPtr.Zero) return IntPtr.Zero;
        IntPtr root = GetAncestor(target, GA_ROOT);
        if (root == IntPtr.Zero) root = target;
        uint myTid = GetCurrentThreadId();

        for (int attempt = 0; attempt < 2; attempt++)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == root) return root;                 // 已在前台
            uint fgTid = GetWindowThreadProcessId(fg, out _);
            uint tgtTid = GetWindowThreadProcessId(root, out _);
            AttachThreadInput(myTid, fgTid, true);
            AttachThreadInput(myTid, tgtTid, true);
            BringWindowToTop(root);
            SetForegroundWindow(root);
            AttachThreadInput(myTid, tgtTid, false);
            AttachThreadInput(myTid, fgTid, false);
            if (GetForegroundWindow() == root) break;
        }
        return root;
    }

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    const uint GA_ROOT = 2;
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
}
