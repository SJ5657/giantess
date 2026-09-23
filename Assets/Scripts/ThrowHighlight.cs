using UnityEngine;
using System.Collections.Generic;

// Temporarily tints whatever the live trajectory preview currently predicts it will hit with an
// orange emissive glow, so aiming makes it obvious which vehicle/person is actually in the
// throw's path -- not just where the arc line is drawn. Only one target is highlighted at a
// time (the first thing TrajectoryPreview's arc simulation would hit), matching the real throw's
// own "stops at the first hit" behavior. Driven entirely by TrajectoryPreview.Show()/Hide().
public static class ThrowHighlight
{
    static Transform current;
    static readonly List<Material> affectedMats = new List<Material>();
    static readonly List<Color> originalColors = new List<Color>();
    static readonly List<bool> originalKeywordStates = new List<bool>();

    static readonly Color highlightColor = new Color(1f, 0.45f, 0.05f) * 3f;

    public static void SetTarget(Transform target)
    {
        if (target == current)
        {
            return;
        }

        Clear();

        if (target == null)
        {
            return;
        }

        current = target;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            Material[] mats = r.materials; // per-renderer instances -- safe to mutate directly
            foreach (Material m in mats)
            {
                if (!m.HasProperty("_EmissionColor"))
                {
                    continue;
                }

                affectedMats.Add(m);
                originalColors.Add(m.GetColor("_EmissionColor"));
                originalKeywordStates.Add(m.IsKeywordEnabled("_EMISSION"));

                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", highlightColor);
            }
        }
    }

    public static void Clear()
    {
        for (int i = 0; i < affectedMats.Count; i++)
        {
            Material m = affectedMats[i];
            if (m == null)
            {
                continue;
            }

            m.SetColor("_EmissionColor", originalColors[i]);
            if (!originalKeywordStates[i])
            {
                m.DisableKeyword("_EMISSION");
            }
        }

        affectedMats.Clear();
        originalColors.Clear();
        originalKeywordStates.Clear();
        current = null;
    }
}
