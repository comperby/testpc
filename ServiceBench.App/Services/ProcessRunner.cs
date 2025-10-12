using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ServiceBench.App.Services;

public class ProcessRunner
{
    public Process Start(string exe, string args, bool hidden = false)
    {
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException($"Не найден исполняемый файл: {exe}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = hidden,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory,
        };

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Не удалось запустить {exe}");
    }

    public async Task TryCloseGracefully(Process process, int waitMs = 5000)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            if (process.CloseMainWindow())
            {
                await Task.Run(() => process.WaitForExit(waitMs));
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }
}
