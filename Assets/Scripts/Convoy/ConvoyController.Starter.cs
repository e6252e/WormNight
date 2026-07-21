using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class ConvoyController
    {
        [Header("Starter Segment")]
        public bool EnableStarterSegment; // selected worm starter segment
        public StarterCatalogAsset StarterCatalog; // worm id -> starter Lv prefabs
        [Min(0.1f)] public float StarterSegmentDistanceBehindHead = 1.7f; // fallback spacing
        [Min(0.1f)] public float StarterSegmentVisualClearanceDistance = 2.7f; // large body clearance

        private Transform starterSegment; // current attached starter
        private string activeStarterWormId; // current starter worm id

        private bool HasActiveStarterSegment => starterSegment != null
            && segments.Count > 0
            && segments[0] == starterSegment; // starter is always chain index 0

        public bool ApplySelectedWormStarterSegment(string wormId)
        {
            return EnsureStarterSegment(wormId, true);
        }

        private void EnsureStarterSegmentFromCurrentLoadout()
        {
            if (!EnableStarterSegment)
            {
                ClearStarterTracking();
                return;
            }

            EnsureStarterSegment(RunLoadoutContext.CurrentStartBonus.SelectedWormId, false);
        }

        private bool EnsureStarterSegment(string wormId, bool snapToPath)
        {
            if (!EnableStarterSegment)
            {
                return false;
            }

            string normalizedWormId = NormalizeStarterWormId(wormId);
            ApplySelectedWormVisual(normalizedWormId);

            if (HasActiveStarterSegment
                && string.Equals(activeStarterWormId, normalizedWormId, StringComparison.OrdinalIgnoreCase))
            {
                if (snapToPath)
                {
                    SnapSegmentsToPath();
                }

                return true;
            }

            if (!TryResolveStarterPrefabFromCatalog(normalizedWormId, out GameObject prefab))
            {
                return false;
            }

            RemoveStarterSegmentIfPresent();
            Transform segment = CreateSegment(0, prefab);
            if (segment == null)
            {
                ClearStarterTracking();
                return false;
            }

            segment.name = "ConvoyStarterSegment";
            segments.Insert(0, segment);
            segmentGroundChecks.Insert(0, GetSegmentGroundCheck(segment));
            segmentRuntimes.Insert(0, GetSegmentRuntime(segment, 0, true));

            starterSegment = segment;
            activeStarterWormId = normalizedWormId;
            RegisterActiveStarterDefinition(normalizedWormId);
            SyncSegmentRuntimes(true);

            if (snapToPath)
            {
                SnapSegmentsToPath();
            }

            NotifySegmentCountChanged();
            return true;
        }

        private bool TryResolveStarterPrefabFromCatalog(string wormId, out GameObject prefab)
        {
            prefab = null;
            if (StarterCatalog == null)
            {
                Debug.LogWarning($"{nameof(ConvoyController)} has no StarterCatalog. Starter segment was not created.", this);
                return false;
            }

            string normalizedWormId = NormalizeStarterWormId(wormId);
            if (StarterCatalog.TryGetStarterPrefab(normalizedWormId, out prefab) && prefab != null)
            {
                return true;
            }

            Debug.LogWarning($"StarterCatalog has no starter prefab for '{normalizedWormId}'.", this);
            return false;
        }

        private bool TryResolveActiveStarterLevelPrefab(string sharedSegmentId, int level, out GameObject prefab)
        {
            prefab = null;
            if (StarterCatalog == null || string.IsNullOrWhiteSpace(activeStarterWormId))
            {
                return false;
            }

            return StarterCatalog.TryGetStarterLevelPrefab(activeStarterWormId, sharedSegmentId, level, out prefab)
                && prefab != null;
        }

        private void RegisterActiveStarterDefinition(string wormId)
        {
            if (StarterCatalog != null
                && StarterCatalog.TryGetStarterDefinition(wormId, out SegmentDefinition definition)
                && definition != null)
            {
                RegisterSegmentDefinition(definition);
            }
        }

        private void RemoveStarterSegmentIfPresent()
        {
            int index = starterSegment != null ? segments.IndexOf(starterSegment) : -1;
            if (index < 0)
            {
                ClearStarterTracking();
                return;
            }

            Transform segment = starterSegment;
            segments.RemoveAt(index);
            RemoveSegmentGroundCheck(index);
            RemoveSegmentRuntime(index);

            if (segment != null)
            {
                DestroyUnityObject(segment.gameObject);
            }

            ClearStarterTracking();
            SyncSegmentRuntimes(true);
        }

        private void ClearStarterTracking()
        {
            starterSegment = null;
            activeStarterWormId = string.Empty;
        }

        private int GetRegularSegmentCount()
        {
            return Mathf.Max(0, segments.Count - (HasActiveStarterSegment ? 1 : 0));
        }

        private int GetFirstDetachableSegmentIndex()
        {
            return HasActiveStarterSegment ? 1 : 0;
        }

        private float GetEffectiveStarterSegmentDistance()
        {
            return Mathf.Max(0.1f, StarterSegmentDistanceBehindHead, StarterSegmentVisualClearanceDistance);
        }

        private static string NormalizeStarterWormId(string wormId)
        {
            return MetaWormIds.Normalize(wormId);
        }
    }
}
