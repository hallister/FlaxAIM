using System;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// The sample game plugin.
/// </summary>
/// <seealso cref="FlaxEngine.GamePlugin" />
public class MyPlugin : GamePlugin
{
    /// <inheritdoc />
    public MyPlugin()
    {
        _description = new PluginDescription
        {
            Name = "Flax Adaptive Input",
            Category = "Other",
            Author = "Justin Hall",
            AuthorUrl = null,
            HomepageUrl = null,
            RepositoryUrl = "https://github.com/hallister/FlaxAIM",
            Description = "An input system modeled after the Unreal Enhanced Input system.",
            Version = new Version(1, 0),
            IsAlpha = true,
            IsBeta = false,
        };
    }
}