using UnityEngine;

namespace TeamProject01.Gameplay
{
    public enum SegmentExplosionVfxScaleMode
    {
        RadiusDiameter,
        RadiusMultiplier,
        FixedPrefabScale
    }

    [DisallowMultipleComponent]
    public sealed class SegmentExplosionVfxScaleSettings : MonoBehaviour
    {
        public SegmentExplosionVfxScaleMode ScaleMode = SegmentExplosionVfxScaleMode.RadiusDiameter;
        [Min(0f)] public float RadiusScaleMultiplier = 1f;
        [Min(0f)] public float FixedScale = 1f;
        [Min(0f)] public float MinimumScale;
        [Min(0f)] public float MaximumScale;
        public bool ApplyAlphaOverride = true;

        public float ResolveUniformScale(float radius)
        {
            float resolvedScale = ScaleMode switch
            {
                SegmentExplosionVfxScaleMode.RadiusMultiplier => Mathf.Max(0f, radius) * RadiusScaleMultiplier,
                SegmentExplosionVfxScaleMode.FixedPrefabScale => FixedScale,
                _ => Mathf.Max(0f, radius) * 2f,
            };

            if (MinimumScale > 0f)
            {
                resolvedScale = Mathf.Max(MinimumScale, resolvedScale);
            }

            if (MaximumScale > 0f)
            {
                resolvedScale = Mathf.Min(MaximumScale, resolvedScale);
            }

            return Mathf.Max(0.01f, resolvedScale);
        }
    }
}
