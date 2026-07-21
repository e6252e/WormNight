using System;
using System.Collections.Generic;
using System.IO;
using TeamProject01.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TeamProject01.EditorTools
{
    public sealed class MonsterSfxManagerWindow : EditorWindow
    {
        private const float MonsterVolume = 0.8f;
        private const float BossVolume = 1.0f;

        private Vector2 scroll;
        private string lastReport = "Ready.";

        [MenuItem("OZCodingProject/Audio/Monster SFX Manager")]
        public static void Open()
        {
            GetWindow<MonsterSfxManagerWindow>("Monster SFX");
        }

        [MenuItem("OZCodingProject/Audio/Apply Monster SFX")]
        public static void ApplyMonsterSfxMenu()
        {
            ApplyMonsterDefaults();
        }

        public static void ApplyMonsterDefaultsFromCommandLine()
        {
            ApplyMonsterDefaults();
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Monster SFX Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Attaches EnemySfxBridge and GameplaySfxEmitter slots to monster prefabs. Boss slots use volume 1.0, other monster slots use 0.8.",
                MessageType.Info);

            if (GUILayout.Button("Apply Monster SFX", GUILayout.Height(30)))
            {
                lastReport = ApplyMonsterDefaults();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private static string ApplyMonsterDefaults()
        {
            List<string> report = new List<string> { "[Monster SFX]" };
            PrefabSpec[] specs = CreateMonsterSpecs();
            int touched = 0;

            for (int i = 0; i < specs.Length; i++)
            {
                if (ApplyPrefabSpec(specs[i], report))
                {
                    touched++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.Add("Touched prefabs: " + touched);
            string result = string.Join("\n", report);
            Debug.Log(result);
            return result;
        }

        private static PrefabSpec[] CreateMonsterSpecs()
        {
            SlotSpec bossTeleport = Slot("SFX_BossTeleport", GameplaySfxCue.BossTeleport, BossVolume, 0.2f, true, true, "time_warp_reverse_spell_cast_03.wav");
            SlotSpec bossDiamondCharge = Slot("SFX_BossDiamondCharge", GameplaySfxCue.BossDiamondCharge, BossVolume, 0.15f, true, true, "SFX_Vefects_Stylized_AoE_Dark_Area_Cast_01.wav");
            SlotSpec bossDiamondLaunch = Slot("SFX_BossDiamondLaunch", GameplaySfxCue.BossDiamondLaunch, BossVolume, 0.08f, true, true, "SFX_Vefects_Stylized_AoE_Dark_Area_End_01.wav");
            SlotSpec bossDiamondBurstLarge = Slot("SFX_BossDiamondBurstLarge", GameplaySfxCue.BossDiamondBurstLarge, BossVolume, 0.15f, true, true, "energy_blast_large_02.wav");
            SlotSpec bossDiamondBurstSmall = Slot("SFX_BossDiamondBurstSmall", GameplaySfxCue.BossDiamondBurstSmall, BossVolume, 0.02f, true, true, "energy_blast_small_02.wav");
            SlotSpec bossJumpImpact = Slot("SFX_BossJumpImpact", GameplaySfxCue.BossJumpImpact, BossVolume, 0.12f, true, true, "SFX_Vefects_Explosion_Big_01.wav");
            SlotSpec bossDeath = Slot("SFX_BossDeath", GameplaySfxCue.BossDeath, BossVolume, 0.2f, true, true, "troll_monster_death_slow_04.wav");

            SlotSpec skeletonJumpLanding = Slot("SFX_MonsterJumpLanding", GameplaySfxCue.MonsterJumpLanding, MonsterVolume, 0.08f, true, true, "impact_rocks_011.wav");
            SlotSpec skeletonDeath = Slot("SFX_MonsterDeath", GameplaySfxCue.MonsterDeath, MonsterVolume, 0.12f, true, true, "impact_rocks_008.wav");
            SlotSpec suicideExplosion = Slot("SFX_MonsterSuicideExplosion", GameplaySfxCue.MonsterSuicideExplosion, MonsterVolume, 0.08f, true, true, "explosion_small_no_tail_03.wav");
            SlotSpec slowZoneCast = Slot("SFX_MonsterSlowZoneCast", GameplaySfxCue.MonsterSlowZoneCast, MonsterVolume, 0.2f, true, true, "ice_spell_freeze_ground_02.wav");
            SlotSpec slowThrowerRanged = Slot("SFX_MonsterRangedFire", GameplaySfxCue.MonsterRangedFire, MonsterVolume, 0.08f, true, true, "retro_laser_gun_shoot_05.wav");
            SlotSpec segmentCutCast = Slot("SFX_MonsterSegmentCutCast", GameplaySfxCue.MonsterSegmentCutCast, MonsterVolume, 0.2f, true, true, "whoosh_magic_spell_02.wav");
            SlotSpec segmentCutLaunch = Slot("SFX_MonsterSegmentCutLaunch", GameplaySfxCue.MonsterSegmentCutLaunch, MonsterVolume, 0.12f, true, true, "retro_magic_spell_cast_sparkle_03.wav");
            SlotSpec obstacleSummon = Slot("SFX_MonsterObstacleSummon", GameplaySfxCue.MonsterObstacleSummon, MonsterVolume, 0.2f, true, true, "casting_charge_matter_grow_01.wav");
            SlotSpec obstacleMelee = Slot("SFX_MonsterMeleeImpact", GameplaySfxCue.MonsterMeleeImpact, MonsterVolume, 0.08f, true, true, "punch_general_body_impact_03.wav");
            SlotSpec hatchlingMelee = Slot("SFX_MonsterMeleeImpact", GameplaySfxCue.MonsterMeleeImpact, MonsterVolume, 0.08f, true, true, "snake_2_attack_hiss_fast_03.wav");
            SlotSpec buffCasterRanged = Slot("SFX_MonsterRangedFire", GameplaySfxCue.MonsterRangedFire, MonsterVolume, 0.08f, true, true, "retro_laser_gun_shoot_08.wav");
            SlotSpec areaShieldLoop = Slot("SFX_MonsterShieldLoop", GameplaySfxCue.MonsterShieldLoop, MonsterVolume, 0.0f, false, true, "sci-fi_shield_power_on_impact_01.wav");
            SlotSpec areaShieldRanged = Slot("SFX_MonsterRangedFire", GameplaySfxCue.MonsterRangedFire, MonsterVolume, 0.08f, true, true, "retro_laser_gun_shoot_06.wav");
            SlotSpec portalTeleport = Slot("SFX_MonsterPortalTeleport", GameplaySfxCue.MonsterPortalTeleport, MonsterVolume, 0.2f, true, true, "dark_portal_wind_effect_02.wav");
            SlotSpec portalMelee = Slot("SFX_MonsterMeleeImpact", GameplaySfxCue.MonsterMeleeImpact, MonsterVolume, 0.08f, true, true, "retro_impact_hit_10.wav");

            return new[]
            {
                Prefab("Assets/Prefabs/Monster/EnemyPrefab/Enemy_Melee_Normal.prefab", Bridge()),
                Prefab("Assets/Prefabs/Monster/EnemyPrefab/Enemy_Melee_SkeletonDagger.prefab", Bridge()),
                Prefab("Assets/Prefabs/Monster/EnemyPrefab/Enemy_Ranged_Normal.prefab", Bridge()),
                Prefab("Assets/Prefabs/Monster/EnemyPrefab/Enemy_Ranged_SkeletonCrossbow.prefab", Bridge()),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Boss/Boss01.prefab",
                    Bridge(death: GameplaySfxCue.BossDeath),
                    bossTeleport,
                    bossDiamondCharge,
                    bossDiamondLaunch,
                    bossDiamondBurstLarge,
                    bossJumpImpact,
                    bossDeath),

                Prefab(
                    "Assets/Prefabs/Monster/ObstaclePrefab/Boss/BossDiamondProjectile.prefab",
                    BridgeSpec.None,
                    bossDiamondBurstSmall),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_SkeletonGolemJumper.prefab",
                    Bridge(death: GameplaySfxCue.MonsterDeath, jumpLanding: GameplaySfxCue.MonsterJumpLanding),
                    skeletonJumpLanding,
                    skeletonDeath),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_SuicideCharger.prefab",
                    Bridge(suicideExplosion: GameplaySfxCue.MonsterSuicideExplosion),
                    suicideExplosion),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_SlowThrower.prefab",
                    Bridge(slowZoneCast: GameplaySfxCue.MonsterSlowZoneCast, ranged: GameplaySfxCue.MonsterRangedFire),
                    slowZoneCast,
                    slowThrowerRanged),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_SegmentCutCaster.prefab",
                    Bridge(segmentCutCast: GameplaySfxCue.MonsterSegmentCutCast, segmentCutLaunch: GameplaySfxCue.MonsterSegmentCutLaunch),
                    segmentCutCast,
                    segmentCutLaunch),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_ObstacleSingle.prefab",
                    Bridge(obstacleSummon: GameplaySfxCue.MonsterObstacleSummon, melee: GameplaySfxCue.MonsterMeleeImpact),
                    obstacleSummon,
                    obstacleMelee),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_DragonHatchling.prefab",
                    Bridge(melee: GameplaySfxCue.MonsterMeleeImpact),
                    hatchlingMelee),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_BuffCaster.prefab",
                    Bridge(ranged: GameplaySfxCue.MonsterRangedFire),
                    buffCasterRanged),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_AreaShield.prefab",
                    Bridge(ranged: GameplaySfxCue.MonsterRangedFire, shieldLoop: GameplaySfxCue.MonsterShieldLoop),
                    areaShieldLoop,
                    areaShieldRanged),

                Prefab(
                    "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_PortalTotemCaster.prefab",
                    Bridge(melee: GameplaySfxCue.MonsterMeleeImpact, portalTeleport: GameplaySfxCue.MonsterPortalTeleport),
                    portalTeleport,
                    portalMelee)
            };
        }

        private static bool ApplyPrefabSpec(PrefabSpec spec, List<string> report)
        {
            if (string.IsNullOrWhiteSpace(spec.Path))
            {
                return false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Path);
            if (prefab == null)
            {
                report.Add("Missing prefab: " + spec.Path);
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(spec.Path);
            bool changed = false;
            try
            {
                if (RemoveChildIfExists(root.transform, "SFX_MonsterHit"))
                {
                    changed = true;
                }

                if (spec.Bridge.Enabled && ConfigureBridge(root, spec.Bridge))
                {
                    changed = true;
                }

                for (int i = 0; i < spec.Slots.Length; i++)
                {
                    if (EnsureSlot(spec.Path, root.transform, spec.Slots[i], report))
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, spec.Path);
                    report.Add("Updated: " + spec.Path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }

        private static bool ConfigureBridge(GameObject root, BridgeSpec spec)
        {
            EnemySfxBridge bridge = root.GetComponent<EnemySfxBridge>();
            bool changed = false;
            if (bridge == null)
            {
                bridge = root.AddComponent<EnemySfxBridge>();
                changed = true;
            }

            bridge.ConfigureCues(
                spec.Death,
                spec.JumpLanding,
                spec.SuicideExplosion,
                spec.SlowZoneCast,
                spec.Ranged,
                spec.SegmentCutCast,
                spec.SegmentCutLaunch,
                spec.ObstacleSummon,
                spec.Melee,
                spec.ShieldLoop,
                spec.PortalTeleport);
            EditorUtility.SetDirty(bridge);
            return changed || spec.Enabled;
        }

        private static bool RemoveChildIfExists(Transform root, string childName)
        {
            Transform existing = root != null ? root.Find(childName) : null;
            if (existing == null)
            {
                return false;
            }

            DestroyImmediate(existing.gameObject);
            return true;
        }

        private static bool EnsureSlot(string prefabPath, Transform anchor, SlotSpec slot, List<string> report)
        {
            Transform existing = anchor.Find(slot.ChildName);
            GameObject slotObject;
            bool changed = false;
            if (existing == null)
            {
                slotObject = new GameObject(slot.ChildName);
                slotObject.transform.SetParent(anchor, false);
                changed = true;
            }
            else
            {
                slotObject = existing.gameObject;
            }

            return ConfigureSlotObject(prefabPath, slotObject, slot, report) || changed;
        }

        private static bool ConfigureSlotObject(string prefabPath, GameObject slotObject, SlotSpec slot, List<string> report)
        {
            bool changed = false;
            AudioSource source = slotObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = slotObject.AddComponent<AudioSource>();
                changed = true;
            }

            ConfigureSource(source, slot);

            SfxVolumeListener listener = slotObject.GetComponent<SfxVolumeListener>();
            if (listener == null)
            {
                listener = slotObject.AddComponent<SfxVolumeListener>();
                changed = true;
            }

            listener.SetBaseVolume(slot.Volume);

            GameplaySfxEmitter emitter = slotObject.GetComponent<GameplaySfxEmitter>();
            if (emitter == null)
            {
                emitter = slotObject.AddComponent<GameplaySfxEmitter>();
                changed = true;
            }

            AudioClip[] clips = ResolveClips(report, prefabPath + "/" + slot.ChildName, slot.FileNames);
            emitter.Configure(slot.Cue, clips, slot.Volume, slot.Cooldown, slot.Detached);
            ConfigureSource(source, slot);
            EditorUtility.SetDirty(slotObject);
            return changed || clips.Length > 0;
        }

        private static void ConfigureSource(AudioSource source, SlotSpec slot)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = true;
            source.spatialBlend = slot.Spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = slot.Spatial ? 2f : 1f;
            source.maxDistance = slot.Spatial ? 55f : 1f;
            source.volume = Mathf.Clamp01(slot.Volume);
        }

        private static AudioClip[] ResolveClips(List<string> report, string context, string[] fileNames)
        {
            List<AudioClip> clips = new List<AudioClip>();
            if (fileNames == null)
            {
                return clips.ToArray();
            }

            for (int i = 0; i < fileNames.Length; i++)
            {
                string fileName = fileNames[i];
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                AudioClip clip = ResolveClipByFileName(fileName);
                if (clip == null)
                {
                    report.Add("Missing clip [" + context + "]: " + fileName);
                    continue;
                }

                clips.Add(clip);
            }

            return clips.ToArray();
        }

        private static AudioClip ResolveClipByFileName(string fileName)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string[] guids = AssetDatabase.FindAssets(nameWithoutExtension);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private static PrefabSpec Prefab(string path, BridgeSpec bridge, params SlotSpec[] slots)
        {
            return new PrefabSpec(path, bridge, slots);
        }

        private static BridgeSpec Bridge(
            GameplaySfxCue death = GameplaySfxCue.None,
            GameplaySfxCue jumpLanding = GameplaySfxCue.None,
            GameplaySfxCue suicideExplosion = GameplaySfxCue.None,
            GameplaySfxCue slowZoneCast = GameplaySfxCue.None,
            GameplaySfxCue ranged = GameplaySfxCue.None,
            GameplaySfxCue segmentCutCast = GameplaySfxCue.None,
            GameplaySfxCue segmentCutLaunch = GameplaySfxCue.None,
            GameplaySfxCue obstacleSummon = GameplaySfxCue.None,
            GameplaySfxCue melee = GameplaySfxCue.None,
            GameplaySfxCue shieldLoop = GameplaySfxCue.None,
            GameplaySfxCue portalTeleport = GameplaySfxCue.None)
        {
            return new BridgeSpec(
                true,
                death,
                jumpLanding,
                suicideExplosion,
                slowZoneCast,
                ranged,
                segmentCutCast,
                segmentCutLaunch,
                obstacleSummon,
                melee,
                shieldLoop,
                portalTeleport);
        }

        private static SlotSpec Slot(
            string childName,
            GameplaySfxCue cue,
            float volume,
            float cooldown,
            bool detached,
            bool spatial,
            params string[] fileNames)
        {
            return new SlotSpec(childName, cue, volume, cooldown, detached, spatial, fileNames);
        }

        private readonly struct PrefabSpec
        {
            public readonly string Path;
            public readonly BridgeSpec Bridge;
            public readonly SlotSpec[] Slots;

            public PrefabSpec(string path, BridgeSpec bridge, SlotSpec[] slots)
            {
                Path = path;
                Bridge = bridge;
                Slots = slots ?? Array.Empty<SlotSpec>();
            }
        }

        private readonly struct BridgeSpec
        {
            public static readonly BridgeSpec None = new BridgeSpec(false);

            public readonly bool Enabled;
            public readonly GameplaySfxCue Death;
            public readonly GameplaySfxCue JumpLanding;
            public readonly GameplaySfxCue SuicideExplosion;
            public readonly GameplaySfxCue SlowZoneCast;
            public readonly GameplaySfxCue Ranged;
            public readonly GameplaySfxCue SegmentCutCast;
            public readonly GameplaySfxCue SegmentCutLaunch;
            public readonly GameplaySfxCue ObstacleSummon;
            public readonly GameplaySfxCue Melee;
            public readonly GameplaySfxCue ShieldLoop;
            public readonly GameplaySfxCue PortalTeleport;

            public BridgeSpec(
                bool enabled,
                GameplaySfxCue death = GameplaySfxCue.None,
                GameplaySfxCue jumpLanding = GameplaySfxCue.None,
                GameplaySfxCue suicideExplosion = GameplaySfxCue.None,
                GameplaySfxCue slowZoneCast = GameplaySfxCue.None,
                GameplaySfxCue ranged = GameplaySfxCue.None,
                GameplaySfxCue segmentCutCast = GameplaySfxCue.None,
                GameplaySfxCue segmentCutLaunch = GameplaySfxCue.None,
                GameplaySfxCue obstacleSummon = GameplaySfxCue.None,
                GameplaySfxCue melee = GameplaySfxCue.None,
                GameplaySfxCue shieldLoop = GameplaySfxCue.None,
                GameplaySfxCue portalTeleport = GameplaySfxCue.None)
            {
                Enabled = enabled;
                Death = death;
                JumpLanding = jumpLanding;
                SuicideExplosion = suicideExplosion;
                SlowZoneCast = slowZoneCast;
                Ranged = ranged;
                SegmentCutCast = segmentCutCast;
                SegmentCutLaunch = segmentCutLaunch;
                ObstacleSummon = obstacleSummon;
                Melee = melee;
                ShieldLoop = shieldLoop;
                PortalTeleport = portalTeleport;
            }
        }

        private readonly struct SlotSpec
        {
            public readonly string ChildName;
            public readonly GameplaySfxCue Cue;
            public readonly float Volume;
            public readonly float Cooldown;
            public readonly bool Detached;
            public readonly bool Spatial;
            public readonly string[] FileNames;

            public SlotSpec(string childName, GameplaySfxCue cue, float volume, float cooldown, bool detached, bool spatial, string[] fileNames)
            {
                ChildName = childName;
                Cue = cue;
                Volume = volume;
                Cooldown = cooldown;
                Detached = detached;
                Spatial = spatial;
                FileNames = fileNames ?? Array.Empty<string>();
            }
        }
    }
}
