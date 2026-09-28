using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FlaxEngine;
using FlaxEngine.GUI;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace FlaxAIM.Samples.EngineTests;

/// <summary>
/// Marks a method on an <see cref="EngineTestFixture"/> as an in-engine test. Return <c>void</c> for a test that
/// finishes in one call, or <see cref="IEnumerator"/> for one that runs over several frames
/// (<c>yield return null</c> waits a frame, <c>yield return 0.5f</c> waits half a second, and yielding another
/// <see cref="IEnumerator"/> runs it to the end first).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EngineTestAttribute : Attribute
{
    /// <summary>
    /// Seconds this test may take, overriding <see cref="EngineTestRunner.TestTimeout"/> when set.
    /// </summary>
    public float Timeout { get; set; }
}

public enum EngineTestOutcome
{
    Passed,
    Failed,
    Skipped,
}

public readonly record struct EngineTestResult(string Name, EngineTestOutcome Outcome, string Message, double Milliseconds);

/// <summary>
/// Runs the <see cref="EngineTestAttribute"/> tests in play mode, for what unit tests can't reach because it needs
/// engine objects (scripts, assets, Flax's virtual input tables). Add it to an actor in an otherwise empty scene and press Play;
/// results go to the log and on screen.
/// <para>
/// Automated runs: pass <c>-flaxaim-tests</c> on the command line (editor with <c>-play &lt;scene id&gt;</c>, or a
/// cooked build) to run and exit with the number of failures as the exit code.
/// </para>
/// </summary>
public class EngineTestRunner : Script
{
    public const string CommandLineSwitch = "-flaxaim-tests";

    [Tooltip("Run the tests when play starts. Otherwise press RunKey.")]
    public bool RunOnStart = true;

    [Tooltip("Key that runs the tests again.")]
    public KeyboardKeys RunKey = KeyboardKeys.F6;

    [Tooltip("Only run tests whose name (Fixture.Method) contains this text. Empty runs everything.")]
    public string Filter;

    [Tooltip("Seconds a multi-frame test may take before it fails.")]
    public float TestTimeout = 10f;

    [Tooltip("Show results on screen.")]
    public bool ShowResults = true;

    [Tooltip("Exit when the tests finish (cooked builds only), with the number of failures as the exit code.")]
    public bool QuitWhenDone;

    /// <summary>
    /// Results of the last run, in order.
    /// </summary>
    public IReadOnlyList<EngineTestResult> Results => _results;

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Failures in the last completed run, or null while no run has completed.
    /// </summary>
    public static int? LastFailureCount { get; private set; }

    /// <summary>
    /// Raised when a run finishes.
    /// </summary>
    public event Action<EngineTestRunner> Finished;

    private readonly List<EngineTestResult> _results = [];
    private readonly Queue<(Type Fixture, MethodInfo Method)> _pending = new();

    // The multi-frame test in progress
    private EngineTestFixture _fixture;
    private MethodInfo _method;
    // Nested coroutines: a test can yield another IEnumerator, which runs to completion before the test resumes
    private readonly Stack<IEnumerator> _steps = new();
    private Stopwatch _stopwatch;
    private float _elapsed;
    private float _timeout;
    private float _waitRemaining;

    private Action<LogType, string> _previousLogOutput;
    private bool _fromCommandLine;

    private UICanvas _canvas;
    private Label _label;

    public override void OnStart()
    {
        _fromCommandLine = Engine.CommandLine?.Contains(CommandLineSwitch, StringComparison.OrdinalIgnoreCase) == true;
        if (RunOnStart || _fromCommandLine) Run();
    }

    public override void OnDisable()
    {
        if (IsRunning) Abort("play stopped");
        DestroyOverlay();
    }

    /// <summary>
    /// Starts a run of every test matching <see cref="Filter"/>.
    /// </summary>
    public void Run()
    {
        if (IsRunning) return;

        _results.Clear();
        _pending.Clear();
        LastFailureCount = null;
        foreach (var test in Discover())
        {
            if (string.IsNullOrEmpty(Filter) || NameOf(test.Fixture, test.Method).Contains(Filter, StringComparison.OrdinalIgnoreCase))
            {
                _pending.Enqueue(test);
            }
        }

        IsRunning = true;
        Debug.Log($"[EngineTests] Running {_pending.Count} tests");
        UpdateOverlay();
    }

    public override void OnUpdate()
    {
        if (!IsRunning)
        {
            if (Input.GetKeyDown(RunKey)) Run();
            return;
        }

        if (_steps.Count > 0 && !Step(Time.UnscaledDeltaTime)) return;

        // Synchronous tests finish in one call, so run them back to back until a multi-frame test starts
        while (_steps.Count == 0 && _pending.Count > 0)
        {
            var (fixture, method) = _pending.Dequeue();
            Begin(fixture, method);
        }

        if (_steps.Count == 0 && _pending.Count == 0) Complete();
    }

    private void Begin(Type fixtureType, MethodInfo method)
    {
        _stopwatch = Stopwatch.StartNew();
        _method = method;
        _elapsed = 0f;
        var timeout = method.GetCustomAttribute<EngineTestAttribute>()?.Timeout ?? 0f;
        _timeout = timeout > 0f ? timeout : TestTimeout;
        _waitRemaining = 0f;

        try
        {
            _fixture = (EngineTestFixture)Activator.CreateInstance(fixtureType);
            _fixture.Runner = this;
            CaptureLogs(_fixture);

            var result = method.Invoke(_fixture, null);
            if (result is IEnumerator steps)
            {
                _steps.Push(steps);
                return;
            }

            Finish(null);
        }
        catch (Exception e)
        {
            Finish(e);
        }
    }

    /// <summary>
    /// Advances the current multi-frame test. Returns true once it has finished.
    /// </summary>
    private bool Step(float deltaTime)
    {
        _elapsed += deltaTime;
        if (_elapsed > _timeout)
        {
            Finish(new EngineTestFailure($"Timed out after {_timeout:0.#} s"));
            return true;
        }

        if (_waitRemaining > 0f)
        {
            _waitRemaining -= deltaTime;
            return false;
        }

        try
        {
            while (true)
            {
                var steps = _steps.Peek();
                if (!steps.MoveNext())
                {
                    _steps.Pop();
                    if (_steps.Count > 0) continue;

                    Finish(null);
                    return true;
                }

                switch (steps.Current)
                {
                    case IEnumerator nested:
                        _steps.Push(nested);
                        continue;
                    case float seconds:
                        _waitRemaining = seconds;
                        return false;
                    case double seconds:
                        _waitRemaining = (float)seconds;
                        return false;
                    default:
                        return false;
                }
            }
        }
        catch (Exception e)
        {
            Finish(e);
            return true;
        }
    }

    private void Finish(Exception error)
    {
        var fixture = _fixture;
        var name = NameOf(_method.DeclaringType, _method);

        // Tests invoked through reflection report their own exception inside TargetInvocationException
        while (error is TargetInvocationException { InnerException: not null } wrapper) error = wrapper.InnerException;

        try
        {
            fixture?.Cleanup();
        }
        catch (Exception e)
        {
            error ??= new EngineTestFailure($"Cleanup failed: {e.Message}");
        }

        RestoreLogs();

        var milliseconds = _stopwatch?.Elapsed.TotalMilliseconds ?? 0;
        var result = error switch
        {
            null => new EngineTestResult(name, EngineTestOutcome.Passed, null, milliseconds),
            EngineTestSkipped skipped => new EngineTestResult(name, EngineTestOutcome.Skipped, skipped.Message, milliseconds),
            EngineTestFailure failure => new EngineTestResult(name, EngineTestOutcome.Failed, WithLogs(failure.Message, fixture), milliseconds),
            _ => new EngineTestResult(name, EngineTestOutcome.Failed, WithLogs($"{error.GetType().Name}: {error.Message}\n{error.StackTrace}", fixture), milliseconds),
        };
        _results.Add(result);

        switch (result.Outcome)
        {
            case EngineTestOutcome.Passed: Debug.Log($"[EngineTests] PASS {name} ({milliseconds:0} ms)"); break;
            case EngineTestOutcome.Skipped: Debug.Log($"[EngineTests] SKIP {name}: {result.Message}"); break;
            // Warnings rather than errors: errors pause play mode in the editor
            default: Debug.LogWarning($"[EngineTests] FAIL {name}: {result.Message}"); break;
        }

        _fixture = null;
        _method = null;
        _steps.Clear();
        UpdateOverlay();
    }

    private void Abort(string reason)
    {
        if (_steps.Count > 0) Finish(new EngineTestFailure($"Aborted: {reason}"));
        _pending.Clear();
        IsRunning = false;
    }

    private void Complete()
    {
        IsRunning = false;

        var failed = _results.Count(r => r.Outcome == EngineTestOutcome.Failed);
        var skipped = _results.Count(r => r.Outcome == EngineTestOutcome.Skipped);
        var passed = _results.Count - failed - skipped;
        var summary = $"[EngineTests] {passed} passed, {failed} failed, {skipped} skipped";
        if (failed > 0) Debug.LogWarning(summary);
        else Debug.Log(summary);
        LastFailureCount = failed;

        UpdateOverlay();
        Finished?.Invoke(this);

        // In the editor this only ends play mode; the editor plugin then exits the editor for command-line runs
        if (_fromCommandLine || (QuitWhenDone && !Engine.IsEditor)) Engine.RequestExit(failed);
    }

    // ---- Discovery -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Every test in the loaded assemblies, ordered by fixture and then by declaration order.
    /// </summary>
    public static List<(Type Fixture, MethodInfo Method)> Discover()
    {
        var tests = new List<(Type, MethodInfo)>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }

            foreach (var type in types.Where(t => !t.IsAbstract && typeof(EngineTestFixture).IsAssignableFrom(t)).OrderBy(t => t.Name))
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(m => m.GetCustomAttribute<EngineTestAttribute>() != null && m.GetParameters().Length == 0)
                    .OrderBy(m => m.MetadataToken);
                foreach (var method in methods) tests.Add((type, method));
            }
        }
        return tests;
    }

    private static string NameOf(Type fixture, MethodInfo method) => $"{fixture.Name}.{method.Name}";

    // ---- Logs ------------------------------------------------------------------------------------------------------

    // FlaxAIM's own log output is captured per test, so tests can check warnings and failures can show it
    private void CaptureLogs(EngineTestFixture fixture)
    {
        _previousLogOutput = InputLog.Output;
        InputLog.Output = (type, message) => fixture.Logs.Add((type, message));
    }

    private void RestoreLogs()
    {
        InputLog.Output = _previousLogOutput;
        _previousLogOutput = null;
    }

    private static string WithLogs(string message, EngineTestFixture fixture)
    {
        if (fixture == null || fixture.Logs.Count == 0) return message;

        var text = new StringBuilder(message);
        text.Append("\n  Last FlaxAIM log lines:");
        foreach (var (type, line) in fixture.Logs.Skip(Math.Max(0, fixture.Logs.Count - 5)))
        {
            text.Append($"\n    {type}: {line}");
        }
        return text.ToString();
    }

    // ---- Overlay ---------------------------------------------------------------------------------------------------

    private void UpdateOverlay()
    {
        if (!ShowResults) return;

        if (_canvas == null)
        {
            _canvas = new UICanvas
            {
                Name = "EngineTestCanvas",
                RenderMode = CanvasRenderMode.ScreenSpace,
                Parent = Actor,
            };
            _label = new Label
            {
                TextColor = Color.White,
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Near,
                Wrapping = TextWrapping.WrapWords,
            };
            new UIControl
            {
                Name = "EngineTestText",
                Parent = _canvas,
                Control = _label,
            };
            _label.SetAnchorPreset(AnchorPresets.StretchAll, false, false);
            _label.Offsets = new Margin(10, 10, 10, 10);
        }

        var failed = _results.Count(r => r.Outcome == EngineTestOutcome.Failed);
        var skipped = _results.Count(r => r.Outcome == EngineTestOutcome.Skipped);
        var text = new StringBuilder();
        text.AppendLine(IsRunning
            ? $"FlaxAIM engine tests: running ({_results.Count} done, {_pending.Count} left)"
            : $"FlaxAIM engine tests: {_results.Count - failed - skipped} passed, {failed} failed, {skipped} skipped   ({RunKey} runs again)");

        // Failures first, then skips; passes only as a count
        foreach (var result in _results.Where(r => r.Outcome == EngineTestOutcome.Failed))
        {
            text.AppendLine($"FAIL {result.Name}: {result.Message.Split('\n')[0]}");
        }
        foreach (var result in _results.Where(r => r.Outcome == EngineTestOutcome.Skipped))
        {
            text.AppendLine($"SKIP {result.Name}: {result.Message}");
        }

        _label.Text = text.ToString();
        _label.TextColor = failed > 0 ? Color.OrangeRed : IsRunning ? Color.White : Color.LightGreen;
    }

    private void DestroyOverlay()
    {
        if (_canvas == null) return;

        Destroy(_canvas);
        _canvas = null;
        _label = null;
    }
}
