using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZCodingProject/Audio/Gameplay SFX Catalog", fileName = "GameplaySfxCatalog")]
    public sealed class GameplaySfxCatalog : ScriptableObject
    {
        public const string ResourcePath = "Audio/GameplaySfxCatalog";

        [SerializeField] private GameplaySfxCatalogEntry[] entries = Array.Empty<GameplaySfxCatalogEntry>();

        public GameplaySfxCatalogEntry[] Entries
        {
            get => entries;
            set => entries = value ?? Array.Empty<GameplaySfxCatalogEntry>();
        }

        public bool TryGetEntry(GameplaySfxCue cue, out GameplaySfxCatalogEntry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    GameplaySfxCatalogEntry candidate = entries[i];
                    if (candidate != null && candidate.Cue == cue && candidate.HasClip)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }
    }

    [Serializable]
    public sealed class GameplaySfxCatalogEntry
    {
        public GameplaySfxCue Cue;
        public AudioClip[] Clips = Array.Empty<AudioClip>();
        [Range(0f, 2f)] public float Volume = 1f;
        [Range(0.1f, 3f)] public float MinPitch = 1f;
        [Range(0.1f, 3f)] public float MaxPitch = 1f;
        [Min(0f)] public float Cooldown = 0.03f;
        public bool Spatial = true;
        [Min(0.1f)] public float MinDistance = 2f;
        [Min(1f)] public float MaxDistance = 45f;

        public bool HasClip => Clips != null && Clips.Length > 0;
    }
}
