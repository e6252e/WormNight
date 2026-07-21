using UnityEngine;

namespace TeamProject01.Gameplay
{
    public enum CombatStatusEffectKind
    {
        None = 0,
        Burn = 1,
        Freeze = 2,
        Holy = 3
    }

    public readonly struct CombatStatusEffectDefinition
    {
        public CombatStatusEffectDefinition(
            string displayName,
            float duration,
            float tickInterval,
            float damagePerSecond,
            float moveSpeedMultiplier,
            float incomingDamageMultiplier,
            DamageType tickDamageType,
            Color floatingColor,
            string vfxEffectName,
            bool isEnemyDebuff)
        {
            DisplayName = displayName;
            Duration = duration;
            TickInterval = tickInterval;
            DamagePerSecond = damagePerSecond;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            IncomingDamageMultiplier = incomingDamageMultiplier;
            TickDamageType = tickDamageType;
            FloatingColor = floatingColor;
            VfxEffectName = vfxEffectName;
            IsEnemyDebuff = isEnemyDebuff;
        }

        public string DisplayName { get; }
        public float Duration { get; }
        public float TickInterval { get; }
        public float DamagePerSecond { get; }
        public float MoveSpeedMultiplier { get; }
        public float IncomingDamageMultiplier { get; }
        public DamageType TickDamageType { get; }
        public Color FloatingColor { get; }
        public string VfxEffectName { get; }
        public bool IsEnemyDebuff { get; }
    }

    public static class CombatStatusEffectCatalog
    {
        public static bool TryGet(CombatStatusEffectKind kind, out CombatStatusEffectDefinition definition)
        {
            switch (kind)
            {
                case CombatStatusEffectKind.Burn:
                    definition = new CombatStatusEffectDefinition(
                        "화상",
                        5f,
                        1f,
                        3f,
                        1f,
                        1f,
                        DamageType.Fire,
                        new Color(1f, 0.34f, 0.12f, 1f),
                        "Fire",
                        true);
                    return true;

                case CombatStatusEffectKind.Freeze:
                    definition = new CombatStatusEffectDefinition(
                        "빙결",
                        5f,
                        1f,
                        1f,
                        0.6f,
                        1f,
                        DamageType.Laser,
                        new Color(0.42f, 0.9f, 1f, 1f),
                        "Ice",
                        true);
                    return true;

                case CombatStatusEffectKind.Holy:
                    definition = new CombatStatusEffectDefinition(
                        "성수",
                        5f,
                        0f,
                        0f,
                        1f,
                        1.3f,
                        DamageType.Direct,
                        new Color(0.86f, 0.96f, 1f, 1f),
                        "Holy",
                        true);
                    return true;

                default:
                    definition = default;
                    return false;
            }
        }

        public static bool IsEnemyDebuff(CombatStatusEffectKind kind)
        {
            return TryGet(kind, out CombatStatusEffectDefinition definition) && definition.IsEnemyDebuff;
        }
    }
}
