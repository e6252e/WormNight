using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZ/Segments/Starter Catalog", fileName = "StarterCatalog")]
    public sealed class StarterCatalogAsset : ScriptableObject
    {
        public StarterSegmentEntry[] Starters = Array.Empty<StarterSegmentEntry>();

        public bool TryGetStarterPrefab(string wormId, out GameObject prefab)
        {
            prefab = null;
            if (Starters == null)
            {
                return false;
            }

            string normalizedWormId = MetaWormIds.Normalize(wormId);
            for (int i = 0; i < Starters.Length; i++)
            {
                StarterSegmentEntry entry = Starters[i];
                if (!string.Equals(entry.NormalizedWormId, normalizedWormId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return entry.TryGetPrefab(out prefab);
            }

            return false;
        }

        public bool TryGetStarterDefinition(string wormId, out SegmentDefinition definition)
        {
            definition = null;
            if (Starters == null)
            {
                return false;
            }

            string normalizedWormId = MetaWormIds.Normalize(wormId);
            for (int i = 0; i < Starters.Length; i++)
            {
                StarterSegmentEntry entry = Starters[i];
                if (!string.Equals(entry.NormalizedWormId, normalizedWormId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                definition = entry.StarterDefinition;
                return definition != null;
            }

            return false;
        }

        public bool TryGetStarterLevelPrefab(string wormId, string sharedSegmentId, int level, out GameObject prefab)
        {
            prefab = null;
            if (!TryGetStarterDefinition(wormId, out SegmentDefinition definition) || definition == null)
            {
                return false;
            }

            string normalizedSharedId = string.IsNullOrWhiteSpace(sharedSegmentId) ? string.Empty : sharedSegmentId.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSharedId)
                && !string.Equals(definition.UpgradeId, normalizedSharedId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return definition.TryGetSegmentPrefab(level, out prefab) && prefab != null;
        }
    }

    [Serializable]
    public struct StarterSegmentEntry
    {
        public string WormId;
        public SegmentDefinition StarterDefinition;
        public GameObject StarterPrefab;
        [TextArea(1, 3)] public string Memo;

        public string NormalizedWormId => MetaWormIds.Normalize(WormId);

        public bool TryGetPrefab(out GameObject prefab)
        {
            prefab = StarterPrefab;
            if (prefab != null)
            {
                return true;
            }

            return StarterDefinition != null && StarterDefinition.TryGetSegmentPrefab(1, out prefab);
        }
    }
}
