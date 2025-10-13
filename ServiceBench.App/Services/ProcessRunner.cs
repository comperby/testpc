using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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

    public async Task<ProcessStartResult> StartAsync(
        string exe,
        string args,
        string? workingDirectory = null,
        bool hidden = true,
        bool captureOutput = false,
        int timeoutStartMs = 2000)
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
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
                : workingDirectory,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var outputBuilder = new StringBuilder();
        if (captureOutput)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (outputBuilder)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (outputBuilder)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                }
            };
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"Не удалось запустить {exe}");
        }

        if (captureOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        await Task.Delay(Math.Max(200, timeoutStartMs));

        var started = !process.HasExited;
        var output = captureOutput ? outputBuilder.ToString() : string.Empty;
        return new ProcessStartResult(process, started, output);
    }

    public async Task TryStopAsync(ProcessStartResult? result)
    {
        if (result?.Process == null)
        {
            return;
        }

        await TryCloseGracefully(result.Process);
    }
}

public sealed class ProcessStartResult
{
    public ProcessStartResult(Process? process, bool started, string output)
    {
        Process = process;
        Started = started;
        Output = output;
    }

    public Process? Process { get; }

    public bool Started { get; }

    public string Output { get; }
}
