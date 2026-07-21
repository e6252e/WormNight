using System;
using System.Collections.Generic;
using System.IO;
using TeamProject01.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TeamProject01.EditorTools
{
    public sealed class GameplaySfxManagerWindow : EditorWindow
    {
        private const string CatalogFolder = "Assets/Resources/Audio";
        private const string CatalogPath = CatalogFolder + "/GameplaySfxCatalog.asset";

        private Vector2 scroll;
        private string lastReport = "Ready.";

        [MenuItem("OZCodingProject/Audio/Gameplay SFX Manager")]
        public static void Open()
        {
            GetWindow<GameplaySfxManagerWindow>("Gameplay SFX");
        }

        [MenuItem("OZCodingProject/Audio/Apply Default Gameplay SFX")]
        public static void ApplyDefaultsMenu()
        {
            ApplyDefaults();
        }

        public static void ApplyDefaultsFromCommandLine()
        {
            ApplyDefaults();
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Gameplay SFX Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates/refreshes the Gameplay SFX catalog and attaches SFX emitter slots to the gameplay prefabs. Audio clips are resolved by exact file name under Assets.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Catalog", GUILayout.Height(30)))
            {
                lastReport = CreateOrRefreshCatalog();
            }

            if (GUILayout.Button("Apply Default Prefab Slots", GUILayout.Height(30)))
            {
                lastReport = ApplyDefaultPrefabSlots();
            }

            if (GUILayout.Button("Apply All Defaults", GUILayout.Height(30)))
            {
                lastReport = ApplyDefaults();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Setup", GUILayout.Height(24)))
            {
                lastReport = ValidateSetup();
            }

            if (GUILayout.Button("Ping Catalog", GUILayout.Height(24)))
            {
                UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<GameplaySfxCatalog>(CatalogPath);
                if (catalog != null)
                {
                    EditorGUIUtility.PingObject(catalog);
                    Selection.activeObject = catalog;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private static string ApplyDefaults()
        {
            List<string> report = new List<string>();
            report.Add(CreateOrRefreshCatalog());
            report.Add(ApplyDefaultPrefabSlots());
            report.Add(ValidateSetup());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string result = string.Join("\n\n", report);
            Debug.Log(result);
            return result;
        }

        private static string CreateOrRefreshCatalog()
        {
            EnsureFolder(CatalogFolder);

            GameplaySfxCatalog catalog = AssetDatabase.LoadAssetAtPath<GameplaySfxCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = CreateInstance<GameplaySfxCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            List<string> report = new List<string> { "[Catalog]" };
            GameplaySfxCatalogEntry[] entries = CreateDefaultCatalogEntries(report);
            catalog.Entries = entries;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            report.Add("Catalog refreshed: " + CatalogPath);
            return string.Join("\n", report);
        }

        private static string ApplyDefaultPrefabSlots()
        {
            List<string> report = new List<string> { "[Prefab Slots]" };
            PrefabSpec[] specs = CreateDefaultPrefabSpecs();
            int touched = 0;

            for (int i = 0; i < specs.Length; i++)
            {
                PrefabSpec spec = specs[i];
                if (ApplyPrefabSpec(spec, report))
                {
                    touched++;
                }
            }

            ApplyAllExistingNamedSlots(
                "Assets/Segments/SG50_WarDrum/Prefabs/SG50_WarDrum_Lv1.prefab",
                Slot("SFX_Activation", GameplaySfxCue.Activation, 1f, 0.3f, false, true, "magic_light_bubble_01.wav", "metal_drum_impact_thud_03.wav"),
                report,
                ref touched);

            ApplyChestGuidSpecs(report, ref touched);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.Add("Touched prefabs: " + touched);
            return string.Join("\n", report);
        }

        private static string ValidateSetup()
        {
            List<string> report = new List<string> { "[Validate]" };
            GameplaySfxCatalog catalog = AssetDatabase.LoadAssetAtPath<GameplaySfxCatalog>(CatalogPath);
            report.Add(catalog != null ? "Catalog OK: " + CatalogPath : "Missing catalog: " + CatalogPath);

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Segments", "Assets/Prefabs", "Assets/Resources" });
            int emitterCount = 0;
            int emptyEmitterCount = 0;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                GameplaySfxEmitter[] emitters = prefab.GetComponentsInChildren<GameplaySfxEmitter>(true);
                emitterCount += emitters.Length;
                for (int j = 0; j < emitters.Length; j++)
                {
                    if (emitters[j].Clips == null || emitters[j].Clips.Length == 0)
                    {
                        emptyEmitterCount++;
                        report.Add("Empty emitter: " + path + " / " + emitters[j].name + " / " + emitters[j].Cue);
                    }
                }
            }

            report.Add("Emitter count: " + emitterCount);
            report.Add("Empty emitter count: " + emptyEmitterCount);
            return string.Join("\n", report);
        }

        private static GameplaySfxCatalogEntry[] CreateDefaultCatalogEntries(List<string> report)
        {
            return new[]
            {
                CatalogEntry(GameplaySfxCue.Pickup, true, 1f, 0.03f, report, "Bling01.wav", "BurstingBubbles20.wav"),
                CatalogEntry(GameplaySfxCue.GoodPickup, true, 1f, 0.03f, report, "Bling05.wav"),
                CatalogEntry(GameplaySfxCue.Open, true, 1f, 0.08f, report, "GetaPoint38.wav"),
                CatalogEntry(GameplaySfxCue.ShieldBreak, true, 1f, 0.1f, report, "BreakingGlass24.wav"),
                CatalogEntry(GameplaySfxCue.ResultClear, false, 1f, 0f, report, "Fanfares08.wav"),
                CatalogEntry(GameplaySfxCue.ResultGameOver, false, 1f, 0f, report, "Farting01.wav"),
                CatalogEntry(GameplaySfxCue.MeteorCast, true, 1f, 0.5f, report, "rock_avalanche_landslide_debris_02.wav"),
                CatalogEntry(GameplaySfxCue.HudSkill2, true, 1f, 0.2f, report, "wind_blizzard_storm_spell_blast_02.wav"),
                CatalogEntry(GameplaySfxCue.ManaOrbPickup, true, 1f, 0.03f, report, "chimes_magic_bell_ding_1.wav"),
                CatalogEntry(GameplaySfxCue.NexusHeal, true, 1f, 0.1f, report, "potion_heal_flask_spell_02.wav"),
                CatalogEntry(GameplaySfxCue.ShieldRegenStart, true, 1f, 0.2f, report, "shield_up_002.wav"),
                CatalogEntry(GameplaySfxCue.ShieldHit, true, 1f, 0.08f, report, "shield_hit_003.wav"),
                CatalogEntry(GameplaySfxCue.HudSkill3LoopA, true, 0.9f, 0f, report, "time_warp_healing_spell_loop1.wav"),
                CatalogEntry(GameplaySfxCue.HudSkill3LoopB, true, 0.8f, 0f, report, "clock_chime_ticking_loop.wav")
            };
        }

        private static GameplaySfxCatalogEntry CatalogEntry(
            GameplaySfxCue cue,
            bool spatial,
            float volume,
            float cooldown,
            List<string> report,
            params string[] fileNames)
        {
            AudioClip[] clips = ResolveClips(report, cue.ToString(), fileNames);
            return new GameplaySfxCatalogEntry
            {
                Cue = cue,
                Clips = clips,
                Volume = volume,
                MinPitch = 1f,
                MaxPitch = 1f,
                Cooldown = cooldown,
                Spatial = spatial,
                MinDistance = spatial ? 2f : 1f,
                MaxDistance = spatial ? 55f : 1f
            };
        }

        private static PrefabSpec[] CreateDefaultPrefabSpecs()
        {
            SlotSpec cannonFire = Slot("SFX_Fire", GameplaySfxCue.Fire, 0.9f, 0.03f, false, true, "weapon_cannon_shot_01.wav");
            SlotSpec missileFire = Slot("SFX_Fire", GameplaySfxCue.Fire, 0.91f, 0.08f, false, true, "weapon_missile_003.wav");
            SlotSpec missileExplosion = Slot("SFX_Explosion", GameplaySfxCue.Explosion, 1f, 0.05f, true, true, "explosion_med_long_tail_02.wav");
            SlotSpec flamethrowerStart = Slot("SFX_FireStart", GameplaySfxCue.FireStart, 1f, 1f, false, true, "gas_large_flame_ignite_01.wav");
            SlotSpec flamethrowerLoop = Slot("SFX_FireLoop", GameplaySfxCue.FireLoop, 1f, 0f, false, true, "weapon_flame_005.wav");
            SlotSpec ballistaFire = Slot("SFX_Fire", GameplaySfxCue.Fire, 1f, 0.03f, false, true, "bow_crossbow_arrow_shoot_type1_01.wav");
            SlotSpec ballistaHit = Slot("SFX_Hit", GameplaySfxCue.Hit, 0.8f, 0.04f, true, true, "bullet_impact_body_flesh_04.wav");
            SlotSpec sawFire = Slot("SFX_Fire", GameplaySfxCue.Fire, 1f, 0.06f, false, true, "metal_door_hatch_close_slam_04.wav");
            SlotSpec sawHit = Slot("SFX_Hit", GameplaySfxCue.Hit, 0.7f, 0.08f, true, true, "sword_impact_body.wav");
            SlotSpec trebuchetFireStart = Slot("SFX_FireStart", GameplaySfxCue.FireStart, 1f, 0.2f, false, true, "door_A_creak_05.wav");
            SlotSpec trebuchetExplosion = Slot("SFX_Explosion", GameplaySfxCue.Explosion, 1f, 0.08f, true, true, "rock_smashable_hit_impact_large_03.wav");
            SlotSpec trebuchetRollHit = Slot("SFX_RollHit", GameplaySfxCue.RollHit, 1f, 0.12f, true, true, "punch_general_body_impact_02.wav");
            SlotSpec lightningFire = Slot("SFX_Fire", GameplaySfxCue.Fire, 1f, 0.08f, false, true, "electric_lightning_blast_03.wav");
            SlotSpec fireballFire = Slot("SFX_Fire", GameplaySfxCue.Fire, 1f, 0.05f, false, true, "fireball_blast_projectile_spell_01.wav");
            SlotSpec fireballExplosion = Slot("SFX_Explosion", GameplaySfxCue.Explosion, 1f, 0.05f, true, true, "explosion_large_03.wav");
            SlotSpec iceFire = Slot("SFX_Fire", GameplaySfxCue.Fire, 1f, 0.08f, false, true, "ice_spell_forming_shards_03.wav");
            SlotSpec iceExplosion = Slot("SFX_Explosion", GameplaySfxCue.Explosion, 1f, 0.05f, true, true, "ice_spell_impact_hit_shard_02.wav");
            SlotSpec warBannerActivation = Slot("SFX_Activation", GameplaySfxCue.Activation, 1f, 0.3f, false, true, "magic_shinny_high_tone_01.wav");
            SlotSpec warDrumActivation = Slot("SFX_Activation", GameplaySfxCue.Activation, 1f, 0.3f, false, true, "magic_light_bubble_01.wav", "metal_drum_impact_thud_03.wav");
            SlotSpec frostBellActivation = Slot("SFX_Activation", GameplaySfxCue.Activation, 1f, 0.3f, false, true, "ice_spell_forming_shards_04.wav");
            SlotSpec wormholeExplosion = Slot("SFX_Explosion", GameplaySfxCue.Explosion, 1f, 0.05f, true, true, "light_in_dark_spell_03.wav");
            SlotSpec pickup = Slot("SFX_Pickup", GameplaySfxCue.Pickup, 1f, 0.03f, false, true, "Bling01.wav", "BurstingBubbles20.wav");
            SlotSpec goodPickup = Slot("SFX_GoodPickup", GameplaySfxCue.GoodPickup, 1f, 0.03f, false, true, "Bling05.wav");
            SlotSpec manaOrbPickup = Slot("SFX_ManaOrbPickup", GameplaySfxCue.ManaOrbPickup, 1f, 0.03f, false, true, "chimes_magic_bell_ding_1.wav");
            SlotSpec shieldBreak = Slot("SFX_ShieldBreak", GameplaySfxCue.ShieldBreak, 1f, 0.1f, false, true, "BreakingGlass24.wav");
            SlotSpec shieldHit = Slot("SFX_ShieldHit", GameplaySfxCue.ShieldHit, 1f, 0.08f, false, true, "shield_hit_003.wav");
            SlotSpec shieldRegenStart = Slot("SFX_ShieldRegenStart", GameplaySfxCue.ShieldRegenStart, 1f, 0.2f, false, true, "shield_up_002.wav");
            SlotSpec nexusHeal = Slot("SFX_NexusHeal", GameplaySfxCue.NexusHeal, 1f, 0.1f, false, true, "potion_heal_flask_spell_02.wav");
            SlotSpec meteorExplosion = Slot("SFX_Explosion", GameplaySfxCue.Explosion, 1f, 0.05f, true, true, "rock_earthquake_impact_02.wav");
            SlotSpec resultClear = Slot("SFX_ResultClear", GameplaySfxCue.ResultClear, 1f, 0f, true, false, "Fanfares08.wav");
            SlotSpec resultGameOver = Slot("SFX_ResultGameOver", GameplaySfxCue.ResultGameOver, 1f, 0f, true, false, "Farting01.wav");

            return new[]
            {
                Prefab("Assets/Segments/SG01_Cannon/Prefabs/SG01_Cannon_Lv1.prefab", Names("Muzzle"), cannonFire),
                Prefab("Assets/Segments/SG01_Cannon/Prefabs/SG01_Cannon_Lv2.prefab", Names("Muzzle"), cannonFire),
                Prefab("Assets/Segments/SG01_Cannon/Prefabs/SG01_Cannon_Lv3.prefab", Names("Muzzle_01", "Muzzle_02", "Muzzle_03", "Muzzle_04", "Muzzle_05", "Muzzle_06"), cannonFire),
                Prefab("Assets/Segments/Starter/SG00_StarterCannon/Prefabs/SG00_StarterCannon_Lv1.prefab", Names("Muzzle"), cannonFire),
                Prefab("Assets/Segments/Starter/SG00_StarterCannon/Prefabs/SG00_StarterCannon_Lv2.prefab", Names("Muzzle"), cannonFire),
                Prefab("Assets/Segments/Starter/SG00_StarterCannon/Prefabs/SG00_StarterCannon_Lv3.prefab", Names("Muzzle_01", "Muzzle_02", "Muzzle_03", "Muzzle_04", "Muzzle_05", "Muzzle_06"), cannonFire),
                Prefab("Assets/Segments/Starter/SG00_StarterCannon/Prefabs/SG00_StarterCannon.prefab", Names("Muzzle"), cannonFire),
                Prefab("Assets/Resources/StarterSegments/SG00_StarterCannon.prefab", Names("Muzzle"), cannonFire),

                Prefab("Assets/Segments/SG02_Missile/Prefabs/SG02_Missile_Lv1.prefab", Names("Muzzle"), missileFire),
                Prefab("Assets/Segments/SG02_Missile/Prefabs/SG02_Missile_Lv2.prefab", Names("Muzzle"), missileFire),
                Prefab("Assets/Segments/SG02_Missile/Prefabs/SG02_Missile_Lv3.prefab", Names("Muzzle"), missileFire),
                Prefab("Assets/Segments/Starter/SG00_StarterAttack/Prefabs/SG00_StarterAttack_Lv1.prefab", Names("Muzzle"), missileFire),
                Prefab("Assets/Segments/Starter/SG00_StarterAttack/Prefabs/SG00_StarterAttack_Lv2.prefab", Names("Muzzle"), missileFire),
                Prefab("Assets/Segments/Starter/SG00_StarterAttack/Prefabs/SG00_StarterAttack_Lv3.prefab", Names("Muzzle"), missileFire),
                Prefab("Assets/Segments/Starter/SG00_StarterAttack/Prefabs/SG00_StarterAttack.prefab", Names("Muzzle"), missileFire),
                Prefab("Assets/Resources/StarterSegments/SG00_StarterAttack.prefab", Names("Muzzle"), missileFire),
                Prefab("Assets/Resources/StarterSegments/SG02_Missile_Lv1.prefab", Names("Muzzle"), missileFire),
                Prefab("Assets/Segments/SG02_Missile/Prefabs/SG02_MissileProjectile_BlueRocket.prefab", Names(string.Empty), missileExplosion),

                Prefab("Assets/Segments/SG03_Trebuchet/Prefabs/SG03_Trebuchet_Lv1.prefab", Names("Muzzle"), trebuchetFireStart),
                Prefab("Assets/Segments/SG03_Trebuchet/Prefabs/SG03_Trebuchet_Lv2.prefab", Names("Muzzle"), trebuchetFireStart),
                Prefab("Assets/Segments/SG03_Trebuchet/Prefabs/SG03_Trebuchet_Lv3.prefab", Names("Muzzle"), trebuchetFireStart),
                Prefab("Assets/Segments/SG03_Trebuchet/Prefabs/SG03_TrebuchetStoneProjectile.prefab", Names(string.Empty), trebuchetExplosion, trebuchetRollHit),

                Prefab("Assets/Segments/SG04_SawLauncher/Prefabs/SG04_SawLauncher_Lv1.prefab", Names("Muzzle"), sawFire),
                Prefab("Assets/Segments/SG04_SawLauncher/Prefabs/SG04_SawLauncher_Lv2.prefab", Names("Muzzle"), sawFire),
                Prefab("Assets/Segments/SG04_SawLauncher/Prefabs/SG04_SawLauncher_Lv3.prefab", Names("Muzzle"), sawFire),
                Prefab("Assets/Segments/Starter/SG00_StarterMobility/Prefabs/SG00_StarterMobility_Lv1.prefab", Names("Muzzle"), sawFire),
                Prefab("Assets/Segments/Starter/SG00_StarterMobility/Prefabs/SG00_StarterMobility_Lv2.prefab", Names("Muzzle"), sawFire),
                Prefab("Assets/Segments/Starter/SG00_StarterMobility/Prefabs/SG00_StarterMobility_Lv3.prefab", Names("Muzzle"), sawFire),
                Prefab("Assets/Segments/Starter/SG00_StarterMobility/Prefabs/SG00_StarterMobility.prefab", Names("Muzzle"), sawFire),
                Prefab("Assets/Resources/StarterSegments/SG00_StarterMobility.prefab", Names("Muzzle"), sawFire),
                Prefab("Assets/Resources/StarterSegments/SG04_SawLauncher_Lv1.prefab", Names("Muzzle"), sawFire),
                Prefab("Assets/Segments/SG04_SawLauncher/Prefabs/SG04_SawBladeProjectile.prefab", Names(string.Empty), sawHit),

                Prefab("Assets/Segments/SG05_Flamethrower/Prefabs/SG05_Flamethrower_Lv1.prefab", Names("Muzzle"), flamethrowerStart, flamethrowerLoop),
                Prefab("Assets/Segments/SG05_Flamethrower/Prefabs/SG05_Flamethrower_Lv2.prefab", Names("Muzzle"), flamethrowerStart, flamethrowerLoop),
                Prefab("Assets/Segments/SG05_Flamethrower/Prefabs/SG05_Flamethrower_Lv3.prefab", Names("Muzzle"), flamethrowerStart, flamethrowerLoop),

                Prefab("Assets/Segments/SG06_Ballista/Prefabs/SG06_Ballista_Lv1.prefab", Names("Muzzle"), ballistaFire),
                Prefab("Assets/Segments/SG06_Ballista/Prefabs/SG06_Ballista_Lv2.prefab", Names("Muzzle"), ballistaFire),
                Prefab("Assets/Segments/SG06_Ballista/Prefabs/SG06_Ballista_Lv3.prefab", Names("Muzzle"), ballistaFire),
                Prefab("Assets/Segments/SG06_Ballista/Prefabs/SG06_BallistaArrowProjectile.prefab", Names(string.Empty), ballistaHit),

                Prefab("Assets/Segments/SG20_LightningObelisk/Prefabs/SG20_LightningObelisk_Lv1.prefab", Names("Muzzle"), lightningFire),
                Prefab("Assets/Segments/SG20_LightningObelisk/Prefabs/SG20_LightningObelisk_Lv2.prefab", Names("Muzzle"), lightningFire),
                Prefab("Assets/Segments/SG20_LightningObelisk/Prefabs/SG20_LightningObelisk_Lv3.prefab", Names("Muzzle"), lightningFire),
                Prefab("Assets/Segments/Starter/SG00_StarterMagic/Prefabs/SG00_StarterMagic_Lv1.prefab", Names("Muzzle"), lightningFire),
                Prefab("Assets/Segments/Starter/SG00_StarterMagic/Prefabs/SG00_StarterMagic_Lv2.prefab", Names("Muzzle"), lightningFire),
                Prefab("Assets/Segments/Starter/SG00_StarterMagic/Prefabs/SG00_StarterMagic_Lv3.prefab", Names("Muzzle"), lightningFire),
                Prefab("Assets/Segments/Starter/SG00_StarterMagic/Prefabs/SG00_StarterMagic.prefab", Names("Muzzle"), lightningFire),
                Prefab("Assets/Resources/StarterSegments/SG00_StarterMagic.prefab", Names("Muzzle"), lightningFire),
                Prefab("Assets/Resources/StarterSegments/SG20_LightningObelisk_Lv1.prefab", Names("Muzzle"), lightningFire),

                Prefab("Assets/Segments/SG21_FireballTower/Prefabs/SG21_FireballTower_Lv1.prefab", Names("Muzzle"), fireballFire),
                Prefab("Assets/Segments/SG21_FireballTower/Prefabs/SG21_FireballTower_Lv2.prefab", Names("Muzzle"), fireballFire),
                Prefab("Assets/Segments/SG21_FireballTower/Prefabs/SG21_FireballTower_Lv3.prefab", Names("Muzzle"), fireballFire),
                Prefab("Assets/Segments/Starter/SG00_StarterSupport/Prefabs/SG00_StarterSupport_Lv1.prefab", Names("Muzzle"), fireballFire),
                Prefab("Assets/Segments/Starter/SG00_StarterSupport/Prefabs/SG00_StarterSupport_Lv2.prefab", Names("Muzzle"), fireballFire),
                Prefab("Assets/Segments/Starter/SG00_StarterSupport/Prefabs/SG00_StarterSupport_Lv3.prefab", Names("Muzzle"), fireballFire),
                Prefab("Assets/Segments/Starter/SG00_StarterSupport/Prefabs/SG00_StarterSupport.prefab", Names("Muzzle"), fireballFire),
                Prefab("Assets/Resources/StarterSegments/SG00_StarterSupport.prefab", Names("Muzzle"), fireballFire),
                Prefab("Assets/Resources/StarterSegments/SG21_FireballTower_Lv1.prefab", Names("Muzzle"), fireballFire),
                Prefab("Assets/Segments/SG21_FireballTower/Prefabs/SG21_FireballProjectile.prefab", Names(string.Empty), fireballExplosion),

                Prefab("Assets/Segments/SG22_IceCrystalOrb/Prefabs/SG22_IceCrystalOrb_Lv1.prefab", Names("Muzzle"), iceFire),
                Prefab("Assets/Segments/SG22_IceCrystalOrb/Prefabs/SG22_IceCrystalOrb_Lv2.prefab", Names("Muzzle"), iceFire),
                Prefab("Assets/Segments/SG22_IceCrystalOrb/Prefabs/SG22_IceCrystalOrb_Lv3.prefab", Names("Muzzle"), iceFire),
                Prefab("Assets/Segments/SG22_IceCrystalOrb/Prefabs/SG22_IceCrystalOrbProjectile.prefab", Names(string.Empty), iceExplosion),

                Prefab("Assets/Segments/SG50_WarDrum/Prefabs/SG50_WarDrum_Lv1.prefab", Names("VFX_ActiveRoot", string.Empty), warDrumActivation),
                Prefab("Assets/Segments/SG52_FrostBell/Prefabs/SG52_FrostBell_Lv1.prefab", Names("VFX_ActiveRoot"), frostBellActivation),
                Prefab("Assets/Segments/SG53_WarBanner/Prefabs/SG53_WarBanner_Lv1.prefab", Names("VFX_ActiveRoot"), warBannerActivation),
                Prefab("Assets/Segments/SG56_WormholePortal/Prefabs/SG56_WormholeProjectile_BlueComet01.prefab", Names(string.Empty), wormholeExplosion),

                Prefab("Assets/Prefabs/Player/PlayerWorm.prefab", Names(string.Empty), pickup, goodPickup),
                Prefab("Assets/Prefabs/Player/PlayerWorm_Attack.prefab", Names(string.Empty), pickup, goodPickup),
                Prefab("Assets/Prefabs/Player/PlayerWorm_Magic.prefab", Names(string.Empty), pickup, goodPickup),
                Prefab("Assets/Prefabs/Player/PlayerWorm_Mobility.prefab", Names(string.Empty), pickup, goodPickup),
                Prefab("Assets/Prefabs/Player/PlayerWorm_Support.prefab", Names(string.Empty), pickup, goodPickup),
                Prefab("Assets/Resources/RewardPickups/PF_SpecialWaveManaOrbPickup.prefab", Names(string.Empty), manaOrbPickup),

                Prefab("Assets/Prefabs/Nexus/PF_Nexus_VisualStack.prefab", Names(string.Empty), shieldBreak, shieldHit, shieldRegenStart, nexusHeal),
                Prefab("Assets/Prefabs/ActionHud/PF_GoldActionMeteorImpact.prefab", Names(string.Empty), meteorExplosion),
                Prefab("Assets/Prefabs/UI/RunResult/PF_RunResultOverlay.prefab", Names(string.Empty), resultClear, resultGameOver)
            };
        }

        private static void ApplyChestGuidSpecs(List<string> report, ref int touched)
        {
            SlotSpec chestOpen = Slot("SFX_Open", GameplaySfxCue.Open, 1f, 0.08f, false, true, "GetaPoint38.wav");
            string[] guids =
            {
                "511198219d384134c8311337315ae660",
                "21e1a44ae31baec42bce7d17f5ff3d6f",
                "69beb7d8398acdb44b9866760561cce2"
            };

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    report.Add("Missing chest prefab GUID: " + guids[i]);
                    continue;
                }

                if (ApplyPrefabSpec(Prefab(path, Names(string.Empty), chestOpen), report))
                {
                    touched++;
                }
            }
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
                List<Transform> anchors = ResolveAnchors(root.transform, spec.AnchorNames);
                if (anchors.Count == 0)
                {
                    report.Add("Missing anchor in " + spec.Path + ": " + string.Join(", ", spec.AnchorNames));
                    return false;
                }

                for (int i = 0; i < anchors.Count; i++)
                {
                    Transform anchor = anchors[i];
                    for (int j = 0; j < spec.Slots.Length; j++)
                    {
                        if (EnsureSlot(spec.Path, anchor, spec.Slots[j], report))
                        {
                            changed = true;
                        }
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

        private static void ApplyAllExistingNamedSlots(string prefabPath, SlotSpec slot, List<string> report, ref int touched)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.Add("Missing prefab: " + prefabPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = false;
            try
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == null || !string.Equals(candidate.name, slot.ChildName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (ConfigureSlotObject(prefabPath, candidate.gameObject, slot, report))
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    report.Add("Updated existing named slots: " + prefabPath + " / " + slot.ChildName);
                    touched++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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

        private static List<Transform> ResolveAnchors(Transform root, string[] anchorNames)
        {
            List<Transform> anchors = new List<Transform>();
            if (root == null)
            {
                return anchors;
            }

            if (anchorNames == null || anchorNames.Length == 0)
            {
                anchors.Add(root);
                return anchors;
            }

            for (int i = 0; i < anchorNames.Length; i++)
            {
                string anchorName = anchorNames[i];
                if (string.IsNullOrEmpty(anchorName))
                {
                    if (!anchors.Contains(root))
                    {
                        anchors.Add(root);
                    }
                    continue;
                }

                AddChildrenByName(root, anchorName, anchors);
            }

            return anchors;
        }

        private static void AddChildrenByName(Transform root, string name, List<Transform> results)
        {
            if (root.name == name && !results.Contains(root))
            {
                results.Add(root);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                AddChildrenByName(root.GetChild(i), name, results);
            }
        }

        private static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static PrefabSpec Prefab(string path, string[] anchorNames, params SlotSpec[] slots)
        {
            return new PrefabSpec(path, anchorNames, slots);
        }

        private static string[] Names(params string[] names)
        {
            return names;
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
            public readonly string[] AnchorNames;
            public readonly SlotSpec[] Slots;

            public PrefabSpec(string path, string[] anchorNames, SlotSpec[] slots)
            {
                Path = path;
                AnchorNames = anchorNames ?? Array.Empty<string>();
                Slots = slots ?? Array.Empty<SlotSpec>();
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
