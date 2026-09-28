using System;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// Where FlaxAIM's log messages go.
/// </summary>
public static class InputLog
{
    /// <summary>
    /// Receives every message. Defaults to Flax's debug log when null; set it to capture messages
    /// (e.g. in unit tests, which run without the engine).
    /// </summary>
    public static Action<LogType, string> Output;

    /// <summary>
    /// Formats tags in messages. Tag names come from the engine, so tests replace this.
    /// </summary>
    internal static Func<Tag, string> TagName = tag => tag.ToString();

    internal static void Info(string message) => Write(LogType.Info, message);
    internal static void Warning(string message) => Write(LogType.Warning, message);
    internal static void Error(string message) => Write(LogType.Error, message);

    private static void Write(LogType type, string message)
    {
        message = "[FlaxAIM] " + message;

        if (Output != null)
        {
            Output(type, message);
            return;
        }

        switch (type)
        {
            case LogType.Warning: Debug.LogWarning(message); break;
            case LogType.Error:
            case LogType.Fatal: Debug.LogError(message); break;
            default: Debug.Log(message); break;
        }
    }
}
