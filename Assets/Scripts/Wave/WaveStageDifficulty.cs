using UnityEngine;

namespace TeamProject01.Gameplay
{
    public readonly struct WaveStageDifficulty
    {
        public readonly int Stage;
        public readonly float HealthMultiplier;
        public readonly float MoveSpeedMultiplier;
        public readonly float NexusDamageMultiplier;

        public WaveStageDifficulty(int stage, float healthMultiplier, float moveSpeedMultiplier, float nexusDamageMultiplier)
        {
            Stage = Mathf.Max(1, stage);
            HealthMultiplier = SanitizeMultiplier(healthMultiplier);
            MoveSpeedMultiplier = SanitizeMultiplier(moveSpeedMultiplier);
            NexusDamageMultiplier = SanitizeMultiplier(nexusDamageMultiplier);
        }

        private static float SanitizeMultiplier(float value)
        {
            return Mathf.Max(0.01f, value);
        }
    }
}
