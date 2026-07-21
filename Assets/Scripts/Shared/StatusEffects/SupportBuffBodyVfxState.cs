using UnityEngine;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SupportBuffBodyVfxState : MonoBehaviour
    {
        private ConvoySegmentRuntime targetSegment;
        private SupportSegmentAbility source;
        private StatusBodyVfxController bodyVfx;

        public static void Show(
            ConvoySegmentRuntime targetSegment,
            SupportSegmentAbility source,
            GameObject vfxPrefab)
        {
            if (targetSegment == null || source == null || vfxPrefab == null)
            {
                return;
            }

            SupportBuffBodyVfxState state = targetSegment.GetComponent<SupportBuffBodyVfxState>();
            if (state == null)
            {
                state = targetSegment.gameObject.AddComponent<SupportBuffBodyVfxState>();
            }

            state.targetSegment = targetSegment;
            state.source = source;
            if (state.bodyVfx == null)
            {
                state.bodyVfx = targetSegment.GetComponent<StatusBodyVfxController>();
                if (state.bodyVfx == null)
                {
                    state.bodyVfx = targetSegment.gameObject.AddComponent<StatusBodyVfxController>();
                }
            }

            Transform root = FindChildRecursive(targetSegment.transform, "VFX_BuffBodyRoot");
            state.bodyVfx.Show(vfxPrefab, "Support Buff", root);
        }

        public static void ClearIfSource(ConvoySegmentRuntime targetSegment, SupportSegmentAbility source)
        {
            if (targetSegment == null || source == null)
            {
                return;
            }

            SupportBuffBodyVfxState state = targetSegment.GetComponent<SupportBuffBodyVfxState>();
            if (state != null && state.source == source)
            {
                state.Clear();
            }
        }

        private void Update()
        {
            if (targetSegment == null || source == null || !source.IsAbilityActive)
            {
                Clear();
                return;
            }

            if (!SupportSegmentRuntimeBuffs.IsWinningSourceForSegment(source, targetSegment.ChainIndex))
            {
                Clear();
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Clear()
        {
            if (bodyVfx != null)
            {
                bodyVfx.Clear();
            }

            source = null;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
