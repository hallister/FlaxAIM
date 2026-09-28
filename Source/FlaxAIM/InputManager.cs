using System.Collections.Generic;
using FlaxEngine;

namespace FlaxAIM;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class InputManager : Script
{
    private readonly List<InputMappingContext> _contextStack = [];

    public void AddInputContext(InputMappingContext context) => AddInputContext([context]);
    public void RemoveInputContext(InputMappingContext context) => RemoveInputContext([context]);
    
    public void AddInputContext(InputMappingContext[] contexts)
    {
        foreach (var context in contexts)
        {
            if (context == null || _contextStack.Contains(context)) return;
            _contextStack.Add(context);  
        }
        
        RebuildVirtualMappings();
    }

    public void RemoveInputContext(InputMappingContext[] contexts)
    {
        foreach (var context in contexts)
        {
            if (!_contextStack.Contains(context)) return;
            _contextStack.Remove(context);
        }
        
        RebuildVirtualMappings();
    }

    /// <summary>
    /// Flattens the active context stack from lowest to highest priority and 
    /// commits them straight down to Flax Engine's live static runtime input tables.
    /// </summary>
    private void RebuildVirtualMappings() => InputMappingCompiler.Compile(_contextStack);
}