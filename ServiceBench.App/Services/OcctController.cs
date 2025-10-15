using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public sealed class OcctController
{
    private static readonly string[] StartKeywords = { "start", "запуск", "старт" };
    private static readonly string[] StopKeywords = { "stop", "останов", "стоп" };

    private readonly Config _config;
    private readonly ProcessRunner _runner;

    private ProcessStartResult? _startResult;

    public OcctController(Config config, ProcessRunner runner)
    {
        _config = config;
        _runner = runner;
    }

    public async Task RunAsync(
        TestPlan plan,
        Func<string, string, string, Task>? phaseCallback,
        Action<string>? log,
        Func<string, Task>? manualPrompt,
        CancellationToken token)
    {
        var profiles = BuildProfiles(plan);
        if (profiles.Count == 0)
        {
            return;
        }

        if (!File.Exists(_config.OcctPath))
        {
            throw new FileNotFoundException($"Не найден OCCT по пути {_config.OcctPath}. RootBase: {_config.RootBase}");
        }

        _startResult = await _runner.StartAsync(
            _config.OcctPath,
            string.Empty,
            Path.GetDirectoryName(_config.OcctPath),
            hidden: false,
            captureOutput: true,
            timeoutStartMs: 6000);

        var process = _startResult.Process;
        if (process == null || !_startResult.Started)
        {
            var output = _startResult.GetOutputSnapshot();
            throw new InvalidOperationException($"Не удалось запустить OCCT. Путь: {_config.OcctPath}{Environment.NewLine}{output}");
        }

        log?.Invoke($"OCCT PID: {process.Id}");

        var window = await WaitForMainWindowAsync(process, token);
        if (window == null)
        {
            throw new InvalidOperationException("Окно OCCT не найдено для автоматизации");
        }

        for (var i = 0; i < profiles.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var profile = profiles[i];

            if (phaseCallback != null)
            {
                await phaseCallback("OCCT", profile.DisplayName, "RUNNING");
            }

            log?.Invoke($"OCCT: запуск профиля '{profile.DisplayName}' на {profile.DurationMinutes} мин");

            window = await WaitForMainWindowAsync(process, token) ?? window;
            var started = await TryStartProfileAsync(window, profile, token);
            if (!started)
            {
                log?.Invoke($"OCCT: автоматизация не нашла кнопку запуска для '{profile.DisplayName}', требуется ручной старт.");
                if (manualPrompt != null)
                {
                    await manualPrompt($"Не удалось автоматически запустить {profile.DisplayName}. Нажмите Start в OCCT и подтвердите продолжение.");
                }
            }

            await WaitWithCancellation(TimeSpan.FromMinutes(Math.Max(1, profile.DurationMinutes)), token);

            window = await WaitForMainWindowAsync(process, token) ?? window;
            var stopped = await TryStopProfileAsync(window, token);
            if (!stopped)
            {
                log?.Invoke($"OCCT: не удалось автоматически остановить профиль '{profile.DisplayName}'. Проверьте окно приложения.");
            }

            if (phaseCallback != null)
            {
                await phaseCallback("OCCT", profile.DisplayName, "COMPLETED");
            }

            if (i < profiles.Count - 1)
            {
                if (phaseCallback != null)
                {
                    await phaseCallback("OCCT", "Пауза", "WAITING");
                }

                log?.Invoke("OCCT: задержка 10 секунд перед следующим профилем");
                await WaitWithCancellation(TimeSpan.FromSeconds(10), token);
            }
        }
    }

    public async Task StopAsync()
    {
        if (_startResult != null)
        {
            await _runner.TryStopAsync(_startResult);
            _startResult = null;
        }
    }

    private static List<OcctProfile> BuildProfiles(TestPlan plan)
    {
        var profiles = new List<OcctProfile>();
        if (plan.OcctCpuSmall && plan.OcctCpuMinutes > 0)
        {
            profiles.Add(new OcctProfile("OCCT CPU Small", plan.OcctCpuMinutes, new[] { "cpu", "small" }));
        }
        if (plan.OcctGpu3D && plan.OcctGpuMinutes > 0)
        {
            profiles.Add(new OcctProfile("OCCT GPU 3D", plan.OcctGpuMinutes, new[] { "gpu", "3d" }));
        }
        if (plan.OcctVram && plan.OcctVramMinutes > 0)
        {
            profiles.Add(new OcctProfile("OCCT VRAM", plan.OcctVramMinutes, new[] { "vram" }));
        }

        return profiles;
    }

    private static async Task WaitWithCancellation(TimeSpan duration, CancellationToken token)
    {
        var elapsed = TimeSpan.Zero;
        var step = TimeSpan.FromSeconds(1);
        while (elapsed < duration && !token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(step, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            elapsed += step;
        }
    }

    private static async Task<AutomationElement?> WaitForMainWindowAsync(Process process, CancellationToken token)
    {
        for (var i = 0; i < 40; i++)
        {
            token.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                return null;
            }

            var handle = process.MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                try
                {
                    var element = AutomationElement.FromHandle(handle);
                    if (element != null)
                    {
                        return element;
                    }
                }
                catch
                {
                    // ignore and retry
                }
            }

            await Task.Delay(250, token);
        }

        return null;
    }

    private static async Task<bool> TryStartProfileAsync(AutomationElement window, OcctProfile profile, CancellationToken token)
    {
        var selector = FindElement(window, null, profile.Keywords);
        if (selector != null)
        {
            TryActivate(selector);
            await Task.Delay(200, token);
        }

        var startButton = FindElement(window, ControlType.Button, StartKeywords);
        if (startButton == null)
        {
            return false;
        }

        return InvokeElement(startButton);
    }

    private static async Task<bool> TryStopProfileAsync(AutomationElement window, CancellationToken token)
    {
        var stopButton = FindElement(window, ControlType.Button, StopKeywords);
        if (stopButton == null)
        {
            await Task.Delay(200, token);
            stopButton = FindElement(window, ControlType.Button, StopKeywords);
            if (stopButton == null)
            {
                return false;
            }
        }

        return InvokeElement(stopButton);
    }

    private static AutomationElement? FindElement(AutomationElement root, ControlType? type, params string[] keywords)
    {
        if (root == null)
        {
            return null;
        }

        var queue = new Queue<AutomationElement>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            try
            {
                var name = current.Current.Name ?? string.Empty;
                if ((type == null || current.Current.ControlType == type) && MatchesKeywords(name, keywords))
                {
                    return current;
                }

                var walker = TreeWalker.ControlViewWalker;
                var child = walker.GetFirstChild(current);
                while (child != null)
                {
                    queue.Enqueue(child);
                    child = walker.GetNextSibling(child);
                }
            }
            catch
            {
                // ignore element access issues
            }
        }

        return null;
    }

    private static bool MatchesKeywords(string text, IReadOnlyCollection<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return true;
        }

        return keywords.All(keyword => text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void TryActivate(AutomationElement element)
    {
        try
        {
            if (element.GetCurrentPattern(SelectionItemPattern.Pattern) is SelectionItemPattern selection)
            {
                selection.Select();
                return;
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            if (element.GetCurrentPattern(InvokePattern.Pattern) is InvokePattern invoke)
            {
                invoke.Invoke();
            }
        }
        catch
        {
            // ignore
        }
    }

    private static bool InvokeElement(AutomationElement element)
    {
        try
        {
            if (element.GetCurrentPattern(InvokePattern.Pattern) is InvokePattern invoke)
            {
                invoke.Invoke();
                return true;
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            if (element.GetCurrentPattern(SelectionItemPattern.Pattern) is SelectionItemPattern selection)
            {
                selection.Select();
                return true;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private readonly record struct OcctProfile(string DisplayName, int DurationMinutes, string[] Keywords);
}
