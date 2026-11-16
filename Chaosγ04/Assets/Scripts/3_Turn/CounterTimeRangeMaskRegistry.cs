using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporarily marks card-range renderers with a dedicated URP rendering layer.
/// GameObject layers are left untouched so collider and raycast behavior do not change.
/// </summary>
public static class CounterTimeRangeMaskRegistry
{
    public const uint RangeRenderingLayerMask = 1u << 7;

    private static readonly Dictionary<Renderer, uint> originalRenderingLayerMasks =
        new Dictionary<Renderer, uint>();

    public static bool HasProtectedRenderers => originalRenderingLayerMasks.Count > 0;

    public static void Protect(IEnumerable<CellManager> cells)
    {
        Clear();
        if (cells == null)
        {
            return;
        }

        foreach (CellManager cell in cells)
        {
            if (cell == null)
            {
                continue;
            }

            foreach (Renderer renderer in cell.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    originalRenderingLayerMasks.ContainsKey(renderer))
                {
                    continue;
                }

                uint originalMask = renderer.renderingLayerMask;
                originalRenderingLayerMasks.Add(renderer, originalMask);
                renderer.renderingLayerMask = originalMask | RangeRenderingLayerMask;
            }
        }
    }

    public static void Clear()
    {
        if (originalRenderingLayerMasks.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Renderer, uint> entry in originalRenderingLayerMasks)
        {
            if (entry.Key != null)
            {
                entry.Key.renderingLayerMask = entry.Value;
            }
        }

        originalRenderingLayerMasks.Clear();
    }
}
