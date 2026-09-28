using System;
using System.Runtime.CompilerServices;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// Resolves the stable identity of an <see cref="InputAction"/>: the ID of the asset it was loaded from.
/// The serialized <see cref="InputAction.ID"/> is copied when an asset is duplicated, so it's only used for
/// actions that don't come from an asset.
/// </summary>
internal static class ActionIdentity
{
    private static readonly ConditionalWeakTable<InputAction, StrongBox<Guid>> Cache = new();
    private static readonly string InputActionTypeName = typeof(InputAction).FullName;

    /// <summary>
    /// Records the asset an action instance came from. Called by the compiler, which has the asset reference.
    /// </summary>
    public static Guid Register(JsonAssetReference<InputAction> reference, InputAction action)
    {
        var assetId = reference.Asset?.ID ?? Guid.Empty;
        if (assetId == Guid.Empty) return Of(action);

        Cache.AddOrUpdate(action, new StrongBox<Guid>(assetId));
        return assetId;
    }

    public static Guid Of(InputAction action)
    {
        if (Cache.TryGetValue(action, out var cached)) return cached.Value;

        var id = FindAssetId(action) ?? action.ID;
        Cache.AddOrUpdate(action, new StrongBox<Guid>(id));
        return id;
    }

    private static Guid? FindAssetId(InputAction action)
    {
        foreach (var asset in Content.GetAssets(typeof(JsonAsset)))
        {
            if (asset is JsonAsset jsonAsset
                && jsonAsset.DataTypeName == InputActionTypeName
                && ReferenceEquals(jsonAsset.Instance, action))
            {
                return jsonAsset.ID;
            }
        }
        return null;
    }
}
