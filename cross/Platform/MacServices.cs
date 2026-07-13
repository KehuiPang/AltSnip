using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Threading;
using SkiaSharp;

namespace AltSnip.Platform;

/// <summary>macOS 实现：screencapture 截屏 + osascript 写图片剪贴板 + Carbon 全局热键 Alt+A。</summary>
public sealed class MacServices : IPlatformServices
{
    public string Name => "macos";

    public SKBitmap CaptureRegion(PixelRect region)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "altsnip_capture.png");
        // -x 静音, -R x,y,w,h 区域
        Proc.Run("/usr/sbin/screencapture", "-x", "-t", "png",
                 $"-R{region.X},{region.Y},{region.Width},{region.Height}", tmp);
        var bmp = File.Exists(tmp) ? SKBitmap.Decode(tmp) : null;
        return bmp ?? new SKBitmap(Math.Max(1, region.Width), Math.Max(1, region.Height));
    }

    public void CopyImageToClipboard(SKImage image)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "altsnip_copy.png");
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var fs = File.Create(tmp))
            data.SaveTo(fs);
        Proc.Run("/usr/bin/osascript", "-e",
                 $"set the clipboard to (read (POSIX file \"{tmp}\") as «class PNGf»)");
    }

    public IDisposable? RegisterHotkey(Action onAltA)
    {
        try { return MacHotKey.Register(onAltA); }
        catch { return null; } // 注册失败不致命：托盘图标仍可触发
    }

    public Avalonia.PixelPoint? CursorPosition() => null;
}

/// <summary>用 Carbon RegisterEventHotKey 注册全局热键 Alt+A。
/// 纯热键注册无需"辅助功能"授权（不同于 CGEventTap），开箱即用。</summary>
internal static class MacHotKey
{
    const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    // 'keyb'；kEventHotKeyPressed = 5；optionKey = 1<<11；kVK_ANSI_A = 0
    const uint kEventClassKeyboard = 0x6B657962;
    const uint kEventHotKeyPressed = 5;
    const uint optionKey = 0x0800;
    const uint kVK_ANSI_A = 0x00;

    [StructLayout(LayoutKind.Sequential)]
    struct EventTypeSpec { public uint eventClass; public uint eventKind; }

    [StructLayout(LayoutKind.Sequential)]
    struct EventHotKeyID { public uint signature; public uint id; }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int EventHandlerProc(IntPtr callRef, IntPtr evt, IntPtr userData);

    [DllImport(Carbon)] static extern IntPtr GetApplicationEventTarget();
    [DllImport(Carbon)] static extern int InstallEventHandler(
        IntPtr target, EventHandlerProc handler, uint numTypes,
        EventTypeSpec[] typeList, IntPtr userData, out IntPtr outRef);
    [DllImport(Carbon)] static extern int RegisterEventHotKey(
        uint hotKeyCode, uint hotKeyModifiers, EventHotKeyID hotKeyID,
        IntPtr target, uint options, out IntPtr outRef);
    [DllImport(Carbon)] static extern int UnregisterEventHotKey(IntPtr hotKeyRef);
    [DllImport(Carbon)] static extern int RemoveEventHandler(IntPtr handlerRef);

    // 保持委托与回调存活，避免被 GC 回收后 native 侧悬空
    static EventHandlerProc? _proc;
    static Action? _cb;
    static IntPtr _hotKeyRef, _handlerRef;

    static int OnHotKey(IntPtr callRef, IntPtr evt, IntPtr userData)
    {
        var cb = _cb;
        if (cb != null)
            Dispatcher.UIThread.Post(() => { try { cb(); } catch { } });
        return 0; // noErr
    }

    public static IDisposable Register(Action onAltA)
    {
        _cb = onAltA;
        _proc = OnHotKey;
        var target = GetApplicationEventTarget();
        var spec = new EventTypeSpec { eventClass = kEventClassKeyboard, eventKind = kEventHotKeyPressed };
        InstallEventHandler(target, _proc, 1, new[] { spec }, IntPtr.Zero, out _handlerRef);
        var id = new EventHotKeyID { signature = 0x41534E50 /*'ASNP'*/, id = 1 };
        int st = RegisterEventHotKey(kVK_ANSI_A, optionKey, id, target, 0, out _hotKeyRef);
        if (st != 0) throw new InvalidOperationException($"RegisterEventHotKey failed: {st}");
        return new Unregister();
    }

    sealed class Unregister : IDisposable
    {
        public void Dispose()
        {
            if (_hotKeyRef != IntPtr.Zero) { UnregisterEventHotKey(_hotKeyRef); _hotKeyRef = IntPtr.Zero; }
            if (_handlerRef != IntPtr.Zero) { RemoveEventHandler(_handlerRef); _handlerRef = IntPtr.Zero; }
            _cb = null; _proc = null;
        }
    }
}
