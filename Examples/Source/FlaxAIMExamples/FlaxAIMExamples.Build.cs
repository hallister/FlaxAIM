using Flax.Build;
using Flax.Build.NativeCpp;

/// <summary>
/// FlaxAIM examples and in-engine tests. Games using FlaxAIM don't need this module.
/// </summary>
public class FlaxAIMExamples : GameModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        options.PublicDependencies.Add("FlaxAIM");

        BuildNativeCode = false;
    }
}
