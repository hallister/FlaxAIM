using System;
using FlaxAIM.Samples.EngineTests;
using FlaxEditor;
using FlaxEngine;

namespace FlaxAIMExamplesEditor
{
    /// <summary>
    /// Editor support for the FlaxAIM examples project: exits the editor after automated engine test runs.
    /// </summary>
    /// <seealso cref="FlaxEditor.EditorPlugin" />
    public class FlaxAIMExamplesEditor : EditorPlugin
    {
        public FlaxAIMExamplesEditor()
        {
            _description = new PluginDescription
            {
                Name = "FlaxAIM Examples Editor",
                Category = "Other",
                Description = "Editor support for the FlaxAIM examples and in-engine tests.",
                Author = "Justin Hall",
            };
        }

        /// <inheritdoc />
        public override void InitializeEditor()
        {
            base.InitializeEditor();

            // Automated runs (-play <scene> -flaxaim-tests): play mode ends when the run finishes, then the editor
            // exits with the number of failures as its exit code
            var commandLine = Engine.CommandLine ?? string.Empty;
            if (commandLine.Contains(EngineTestRunner.CommandLineSwitch, StringComparison.OrdinalIgnoreCase))
            {
                Editor.PlayModeEnd += OnTestPlayModeEnd;
            }
        }

        /// <inheritdoc />
        public override void DeinitializeEditor()
        {
            Editor.PlayModeEnd -= OnTestPlayModeEnd;
            base.DeinitializeEditor();
        }

        private void OnTestPlayModeEnd()
        {
            Editor.PlayModeEnd -= OnTestPlayModeEnd;

            // No result means the run never finished (e.g. play was stopped by hand).
            // Deferred: while play mode is still ending, an exit request only stops play mode again.
            var exitCode = EngineTestRunner.LastFailureCount ?? 1;
            Scripting.InvokeOnUpdate(() => Engine.RequestExit(exitCode));
        }
    }
}
