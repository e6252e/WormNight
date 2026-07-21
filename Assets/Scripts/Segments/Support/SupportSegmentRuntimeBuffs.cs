using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public static class SupportSegmentRuntimeBuffs
    {
        private sealed class BuffSourceState
        {
            public SupportSegmentAbility Source;
            public SegmentSupportAbilityKind Kind;
            public int SourceSegmentIndex;
            public int FrontSegmentCount;
            public int BackSegmentCount;
            public float FinalDamageMultiplier = 1f;
            public float FinalAttackSpeedMultiplier = 1f;
            public int ActivationOrder;
        }

        private static readonly List<BuffSourceState> Sources = new List<BuffSourceState>(16);
        private static int nextActivationOrder;

        public static void RefreshAllyBuff(SupportSegmentAbility source, SegmentSupportAbilityProfile profile)
        {
            if (source == null || profile == null || source.Segment == null)
            {
                return;
            }

            if (profile.AbilityKind != SegmentSupportAbilityKind.FinalDamageBuff &&
                profile.AbilityKind != SegmentSupportAbilityKind.FinalAttackSpeedBuff)
            {
                ClearSource(source);
                return;
            }

            BuffSourceState state = GetOrCreateSource(source);
            state.Kind = profile.AbilityKind;
            state.SourceSegmentIndex = source.Segment.ChainIndex;
            state.FrontSegmentCount = Mathf.Max(0, profile.FrontSegmentCount);
            state.BackSegmentCount = Mathf.Max(0, profile.BackSegmentCount);
            state.FinalDamageMultiplier = Mathf.Max(1f, profile.FinalDamageMultiplier);
            state.FinalAttackSpeedMultiplier = Mathf.Max(1f, profile.FinalAttackSpeedMultiplier);
        }

        public static void ClearSource(SupportSegmentAbility source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                if (Sources[i].Source == source)
                {
                    Sources.RemoveAt(i);
                }
            }
        }

        public static float GetFinalDamageMultiplier(int segmentIndex)
        {
            CleanupInactiveSources();

            BuffSourceState source = GetWinningSourceForSegment(segmentIndex);
            return source != null && source.Kind == SegmentSupportAbilityKind.FinalDamageBuff
                ? source.FinalDamageMultiplier
                : 1f;
        }

        public static float GetFinalAttackSpeedMultiplier(int segmentIndex)
        {
            CleanupInactiveSources();

            BuffSourceState source = GetWinningSourceForSegment(segmentIndex);
            return source != null && source.Kind == SegmentSupportAbilityKind.FinalAttackSpeedBuff
                ? source.FinalAttackSpeedMultiplier
                : 1f;
        }

        public static bool IsWinningSourceForSegment(SupportSegmentAbility source, int segmentIndex)
        {
            if (source == null)
            {
                return false;
            }

            CleanupInactiveSources();
            BuffSourceState winningSource = GetWinningSourceForSegment(segmentIndex);
            return winningSource != null && winningSource.Source == source;
        }

        private static BuffSourceState GetOrCreateSource(SupportSegmentAbility source)
        {
            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i].Source == source)
                {
                    return Sources[i];
                }
            }

            BuffSourceState state = new BuffSourceState
            {
                Source = source,
                ActivationOrder = ++nextActivationOrder
            };

            Sources.Add(state);
            return state;
        }

        private static BuffSourceState GetWinningSourceForSegment(int segmentIndex)
        {
            BuffSourceState winner = null;
            for (int i = 0; i < Sources.Count; i++)
            {
                BuffSourceState source = Sources[i];
                if (!AffectsSegment(source, segmentIndex))
                {
                    continue;
                }

                if (winner == null || source.ActivationOrder > winner.ActivationOrder)
                {
                    winner = source;
                }
            }

            return winner;
        }

        private static bool AffectsSegment(BuffSourceState source, int segmentIndex)
        {
            int offset = segmentIndex - source.SourceSegmentIndex;
            if (offset == 0)
            {
                return true;
            }

            if (offset > 0)
            {
                return offset <= source.FrontSegmentCount;
            }

            return -offset <= source.BackSegmentCount;
        }

        private static void CleanupInactiveSources()
        {
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                SupportSegmentAbility source = Sources[i].Source;
                if (source == null || !source.IsAbilityActive || source.Segment == null)
                {
                    Sources.RemoveAt(i);
                }
            }
        }
    }
}
