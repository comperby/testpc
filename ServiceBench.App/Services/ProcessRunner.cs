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
        var outputLock = new object();
        if (captureOutput)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (outputLock)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (outputLock)
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
        return new ProcessStartResult(process, started, captureOutput ? outputBuilder : null, outputLock);
    }

    public async Task TryStopAsync(ProcessStartResult? result)
    {
        if (result?.Process == null)
        {
            return;
        }

        await TryCloseGracefully(result.Process);
    }

    public async Task<ProcessExecutionResult> RunForOutputAsync(
        string exe,
        string args,
        string? workingDirectory = null,
        int timeoutMs = 10000)
    {
        var start = await StartAsync(
            exe,
            args,
            workingDirectory,
            hidden: true,
            captureOutput: true,
            timeoutStartMs: Math.Min(timeoutMs, 2000));

        if (start.Process == null)
        {
            return new ProcessExecutionResult(null, start.Started, start.GetOutputSnapshot());
        }

        var process = start.Process;
        var exited = await Task.Run(() => process.WaitForExit(timeoutMs));
        if (!exited)
        {
            await TryCloseGracefully(process);
        }

        await Task.Delay(100);
        var output = start.GetOutputSnapshot();
        process.Dispose();
        return new ProcessExecutionResult(null, exited, output);
    }
}

public sealed class ProcessStartResult
{
    private readonly StringBuilder? _buffer;
    private readonly object? _bufferLock;

    internal ProcessStartResult(Process? process, bool started, StringBuilder? buffer, object? bufferLock)
    {
        Process = process;
        Started = started;
        _buffer = buffer;
        _bufferLock = bufferLock;
    }

    public Process? Process { get; }

    public bool Started { get; }

    public string GetOutputSnapshot()
    {
        if (_buffer == null || _bufferLock == null)
        {
            return string.Empty;
        }

        lock (_bufferLock)
        {
            return _buffer.ToString();
        }
    }
}

public sealed class ProcessExecutionResult
{
    public ProcessExecutionResult(Process? process, bool exited, string output)
    {
        Process = process;
        Exited = exited;
        Output = output;
    }

    public Process? Process { get; }

    public bool Exited { get; }

    public string Output { get; }
}
