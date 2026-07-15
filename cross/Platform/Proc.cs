using System;
using System.Diagnostics;
using System.IO;

namespace WuweiShot.Platform;

/// <summary>跑外部命令的小工具（macOS/Linux 用系统截图/剪贴板命令）。</summary>
internal static class Proc
{
    public static int Run(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) return -1;
            p.WaitForExit(15000);
            return p.ExitCode;
        }
        catch { return -1; }
    }

    /// <summary>把文件内容作为 stdin 喂给命令（如 wl-copy）。</summary>
    public static int RunWithStdin(string file, string stdinFile, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) return -1;
            using (var input = File.OpenRead(stdinFile))
                input.CopyTo(p.StandardInput.BaseStream);
            p.StandardInput.Close();
            p.WaitForExit(15000);
            return p.ExitCode;
        }
        catch { return -1; }
    }

    public static bool Exists(string tool) => Run("/usr/bin/which", tool) == 0 || Run("which", tool) == 0;
}
