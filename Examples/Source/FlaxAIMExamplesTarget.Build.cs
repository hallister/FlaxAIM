using Flax.Build;

public class FlaxAIMExamplesTarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for game
        Modules.Add("FlaxAIMExamples");
    }
}
