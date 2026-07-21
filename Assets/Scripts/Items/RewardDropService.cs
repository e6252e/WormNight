using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class RewardDropService : MonoBehaviour // 몬스터 보상 월드 드랍 입구
    {
        private const string ExperiencePickupResourcePath = "RewardPickups/PF_RewardPickup_Exp";
        private const string GoldPickupResourcePath = "RewardPickups/PF_RewardPickup_Gold";
        private const string DiamondPickupResourcePath = "RewardPickups/PF_RewardPickup_Diamond";
        private const string SegmentChoiceTicketPickupResourcePath = "RewardPickups/PF_RewardPickup_SegmentChoiceTicket";
        private const string SegmentChoiceTicketFallbackModelResourcePath = "RewardPickups/SegmentChoiceTicketModel/PF_SegmentChoiceTicketModel";
        private const float MinimumDropSpreadRadius = 1.08f;

        public static RewardDropService Active { get; private set; }

        public WorldRewardPickup ExperiencePickupPrefab;
        public WorldRewardPickup GoldPickupPrefab;
        public WorldRewardPickup DiamondPickupPrefab;
        public WorldRewardPickup SegmentChoiceTicketPickupPrefab;
        public Transform DropRoot;
        [Min(0f)] public float DropSpreadRadius = 0.42f;
        [Min(0f)] public float GroundHeightOffset = 0.02f;
        [Header("Pooling")]
        [Min(0)] public int InitialPoolSizePerKind = 16;
        public bool AllowPoolExpansion = true;

        private static WorldRewardPickup cachedExperiencePrefab;
        private static WorldRewardPickup cachedGoldPrefab;
        private static WorldRewardPickup cachedDiamondPrefab;
        private static WorldRewardPickup cachedSegmentChoiceTicketPrefab;
        private static int dropSerial;
        private readonly Dictionary<WorldRewardPickup, Queue<WorldRewardPickup>> pickupPools = new Dictionary<WorldRewardPickup, Queue<WorldRewardPickup>>();
        private Transform poolRoot;

        private void Awake()
        {
            Active = this;
            PrewarmPools();
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        public static void SpawnReward(RewardData reward, Vector3 position)
        {
            if (!reward.IsValid)
            {
                return;
            }

            if (Active != null)
            {
                Active.SpawnRewardInternal(reward, position);
                return;
            }

            SpawnRewardDefault(reward, position);
        }

        public static void SpawnSegmentChoiceTicket(int ticketCount, Vector3 position)
        {
            int safeCount = Mathf.Max(1, ticketCount);
            if (Active != null)
            {
                Active.SpawnSegmentChoiceTicketInternal(safeCount, position);
                return;
            }

            Vector3 basePosition = GroundService.ProjectToGround(position, 0.02f);
            SpawnPickup(GetCachedSegmentChoiceTicketPrefab(), RewardPickupKind.SegmentChoiceTicket, safeCount, 0, basePosition, basePosition + GetDefaultDropOffset(3), null, 0.02f);
        }

        public static void SpawnDiamond(int amount, Vector3 position, int enemyId = 0)
        {
            int safeAmount = Mathf.Max(0, amount);
            if (safeAmount <= 0)
            {
                return;
            }

            if (Active != null)
            {
                Active.SpawnDiamondInternal(safeAmount, position, enemyId);
                return;
            }

            Vector3 basePosition = GroundService.ProjectToGround(position, 0.02f);
            SpawnPickup(GetCachedDiamondPrefab(), RewardPickupKind.Diamond, safeAmount, enemyId, basePosition, basePosition + GetDefaultDropOffset(2), null, 0.02f);
        }

        private void SpawnRewardInternal(RewardData reward, Vector3 position)
        {
            Vector3 basePosition = GroundService.ProjectToGround(position, GroundHeightOffset);
            if (reward.Experience > 0)
            {
                SpawnPickupFromPool(ResolveExperiencePrefab(), RewardPickupKind.Experience, reward.Experience, reward.EnemyId, basePosition, basePosition + GetDropOffset(0), DropRoot, GroundHeightOffset);
            }

            if (reward.Gold > 0)
            {
                SpawnPickupFromPool(ResolveGoldPrefab(), RewardPickupKind.Gold, reward.Gold, reward.EnemyId, basePosition, basePosition + GetDropOffset(1), DropRoot, GroundHeightOffset);
            }

            if (reward.Diamond > 0)
            {
                SpawnPickupFromPool(ResolveDiamondPrefab(), RewardPickupKind.Diamond, reward.Diamond, reward.EnemyId, basePosition, basePosition + GetDropOffset(2), DropRoot, GroundHeightOffset);
            }
        }

        private void SpawnSegmentChoiceTicketInternal(int ticketCount, Vector3 position)
        {
            Vector3 basePosition = GroundService.ProjectToGround(position, GroundHeightOffset);
            SpawnPickupFromPool(ResolveSegmentChoiceTicketPrefab(), RewardPickupKind.SegmentChoiceTicket, Mathf.Max(1, ticketCount), 0, basePosition, basePosition + GetDropOffset(3), DropRoot, GroundHeightOffset);
        }

        private void SpawnDiamondInternal(int amount, Vector3 position, int enemyId)
        {
            Vector3 basePosition = GroundService.ProjectToGround(position, GroundHeightOffset);
            SpawnPickupFromPool(ResolveDiamondPrefab(), RewardPickupKind.Diamond, Mathf.Max(1, amount), enemyId, basePosition, basePosition + GetDropOffset(2), DropRoot, GroundHeightOffset);
        }

        private static void SpawnRewardDefault(RewardData reward, Vector3 position)
        {
            Vector3 basePosition = GroundService.ProjectToGround(position, 0.02f);
            if (reward.Experience > 0)
            {
                SpawnPickup(GetCachedExperiencePrefab(), RewardPickupKind.Experience, reward.Experience, reward.EnemyId, basePosition, basePosition + GetDefaultDropOffset(0), null, 0.02f);
            }

            if (reward.Gold > 0)
            {
                SpawnPickup(GetCachedGoldPrefab(), RewardPickupKind.Gold, reward.Gold, reward.EnemyId, basePosition, basePosition + GetDefaultDropOffset(1), null, 0.02f);
            }

            if (reward.Diamond > 0)
            {
                SpawnPickup(GetCachedDiamondPrefab(), RewardPickupKind.Diamond, reward.Diamond, reward.EnemyId, basePosition, basePosition + GetDefaultDropOffset(2), null, 0.02f);
            }
        }

        private WorldRewardPickup ResolveExperiencePrefab()
        {
            return ExperiencePickupPrefab != null ? ExperiencePickupPrefab : GetCachedExperiencePrefab();
        }

        private WorldRewardPickup ResolveGoldPrefab()
        {
            return GoldPickupPrefab != null ? GoldPickupPrefab : GetCachedGoldPrefab();
        }

        private WorldRewardPickup ResolveDiamondPrefab()
        {
            return DiamondPickupPrefab != null ? DiamondPickupPrefab : GetCachedDiamondPrefab();
        }

        private WorldRewardPickup ResolveSegmentChoiceTicketPrefab()
        {
            return SegmentChoiceTicketPickupPrefab != null ? SegmentChoiceTicketPickupPrefab : GetCachedSegmentChoiceTicketPrefab();
        }

        private Vector3 GetDropOffset(int index)
        {
            float radius = Mathf.Max(MinimumDropSpreadRadius, DropSpreadRadius);
            if (radius <= 0f)
            {
                return Vector3.zero;
            }

            float angle = ResolveDropAngle(index);
            Quaternion rotation = Quaternion.Euler(0f, angle + Random.Range(-18f, 18f), 0f);
            return rotation * Vector3.forward * Random.Range(radius * 0.45f, radius);
        }

        private static Vector3 GetDefaultDropOffset(int index)
        {
            float angle = ResolveDropAngle(index);
            Quaternion rotation = Quaternion.Euler(0f, angle + Random.Range(-18f, 18f), 0f);
            return rotation * Vector3.forward * Random.Range(MinimumDropSpreadRadius * 0.45f, MinimumDropSpreadRadius);
        }

        private static float ResolveDropAngle(int index)
        {
            switch (index)
            {
                case 0:
                    return -45f; // 경험치
                case 1:
                    return 20f; // 골드
                case 2:
                    return 75f; // 다이아
                default:
                    return 135f; // 선택권 등 추가 보상
            }
        }

        private static WorldRewardPickup GetCachedExperiencePrefab()
        {
            if (cachedExperiencePrefab == null)
            {
                cachedExperiencePrefab = LoadPickupPrefab(ExperiencePickupResourcePath);
            }

            return cachedExperiencePrefab;
        }

        private static WorldRewardPickup GetCachedGoldPrefab()
        {
            if (cachedGoldPrefab == null)
            {
                cachedGoldPrefab = LoadPickupPrefab(GoldPickupResourcePath);
            }

            return cachedGoldPrefab;
        }

        private static WorldRewardPickup GetCachedDiamondPrefab()
        {
            if (cachedDiamondPrefab == null)
            {
                cachedDiamondPrefab = LoadPickupPrefab(DiamondPickupResourcePath);
            }

            return cachedDiamondPrefab;
        }

        private static WorldRewardPickup GetCachedSegmentChoiceTicketPrefab()
        {
            if (cachedSegmentChoiceTicketPrefab == null)
            {
                cachedSegmentChoiceTicketPrefab = LoadPickupPrefab(SegmentChoiceTicketPickupResourcePath);
            }

            return cachedSegmentChoiceTicketPrefab;
        }

        private static WorldRewardPickup LoadPickupPrefab(string resourcePath)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            return prefab != null ? prefab.GetComponent<WorldRewardPickup>() : null;
        }

        private void PrewarmPools()
        {
            int count = Mathf.Max(0, InitialPoolSizePerKind);
            PrewarmPool(ResolveExperiencePrefab(), count);
            PrewarmPool(ResolveGoldPrefab(), count);
            PrewarmPool(ResolveDiamondPrefab(), count);
            PrewarmPool(ResolveSegmentChoiceTicketPrefab(), count);
        }

        private void PrewarmPool(WorldRewardPickup prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            Queue<WorldRewardPickup> pool = GetPool(prefab);
            for (int i = pool.Count; i < count; i++)
            {
                WorldRewardPickup pickup = CreatePooledPickup(prefab);
                if (pickup != null)
                {
                    pool.Enqueue(pickup);
                }
            }
        }

        private void SpawnPickupFromPool(WorldRewardPickup prefab, RewardPickupKind kind, int amount, int enemyId, Vector3 spawnPosition, Vector3 landingPosition, Transform parent, float groundHeightOffset)
        {
            if (amount <= 0)
            {
                return;
            }

            WorldRewardPickup pickup = prefab != null
                ? GetPickupFromPool(prefab, parent)
                : CreateFallbackPickup(kind, spawnPosition, parent, groundHeightOffset);

            if (pickup == null)
            {
                return;
            }

            pickup.name = $"{kind}RewardPickup_{++dropSerial:000}";
            pickup.transform.SetParent(parent, true);
            pickup.AttachPoolOwner(prefab != null ? this : null, prefab);
            pickup.Configure(kind, amount, enemyId, landingPosition, spawnPosition);
            if (!pickup.gameObject.activeSelf)
            {
                pickup.gameObject.SetActive(true);
            }
        }

        private WorldRewardPickup GetPickupFromPool(WorldRewardPickup prefab, Transform parent)
        {
            Queue<WorldRewardPickup> pool = GetPool(prefab);
            while (pool.Count > 0)
            {
                WorldRewardPickup pickup = pool.Dequeue();
                if (pickup != null)
                {
                    pickup.transform.SetParent(parent, true);
                    return pickup;
                }
            }

            return AllowPoolExpansion ? CreatePooledPickup(prefab) : null;
        }

        private WorldRewardPickup CreatePooledPickup(WorldRewardPickup prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            WorldRewardPickup pickup = Instantiate(prefab, GetPoolRoot());
            pickup.AttachPoolOwner(this, prefab);
            pickup.gameObject.SetActive(false);
            return pickup;
        }

        private Queue<WorldRewardPickup> GetPool(WorldRewardPickup prefab)
        {
            if (!pickupPools.TryGetValue(prefab, out Queue<WorldRewardPickup> pool))
            {
                pool = new Queue<WorldRewardPickup>();
                pickupPools.Add(prefab, pool);
            }

            return pool;
        }

        private Transform GetPoolRoot()
        {
            if (poolRoot != null)
            {
                return poolRoot;
            }

            GameObject root = new GameObject("RewardPickupPool");
            root.transform.SetParent(transform, false);
            poolRoot = root.transform;
            return poolRoot;
        }

        internal bool ReleasePickup(WorldRewardPickup pickup, WorldRewardPickup sourcePrefab)
        {
            if (pickup == null || sourcePrefab == null)
            {
                return false;
            }

            pickup.ResetForPool();
            pickup.gameObject.SetActive(false);
            pickup.transform.SetParent(GetPoolRoot(), false);
            GetPool(sourcePrefab).Enqueue(pickup);
            return true;
        }

        private static void SpawnPickup(WorldRewardPickup prefab, RewardPickupKind kind, int amount, int enemyId, Vector3 spawnPosition, Vector3 landingPosition, Transform parent, float groundHeightOffset)
        {
            if (amount <= 0)
            {
                return;
            }

            WorldRewardPickup pickup = prefab != null
                ? Instantiate(prefab, spawnPosition, Quaternion.identity, parent)
                : CreateFallbackPickup(kind, spawnPosition, parent, groundHeightOffset);

            pickup.name = $"{kind}RewardPickup_{++dropSerial:000}";
            pickup.Configure(kind, amount, enemyId, landingPosition, spawnPosition);
        }

        private static WorldRewardPickup CreateFallbackPickup(RewardPickupKind kind, Vector3 position, Transform parent, float groundHeightOffset)
        {
            if (kind == RewardPickupKind.SegmentChoiceTicket)
            {
                return CreateSegmentChoiceTicketFallbackPickup(position, parent, groundHeightOffset); // 전용 모델 fallback
            }

            PrimitiveType primitiveType = kind == RewardPickupKind.Gold ? PrimitiveType.Cylinder : PrimitiveType.Sphere;
            GameObject fallback = GameObject.CreatePrimitive(primitiveType);
            fallback.transform.SetParent(parent, true);
            fallback.transform.position = GroundService.ProjectToGround(position, groundHeightOffset);
            fallback.transform.localScale = kind == RewardPickupKind.Gold ? new Vector3(0.38f, 0.12f, 0.38f) : Vector3.one * 0.34f;

            Collider collider = fallback.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            WorldRewardPickup pickup = fallback.AddComponent<WorldRewardPickup>();
            return pickup;
        }

        private static WorldRewardPickup CreateSegmentChoiceTicketFallbackPickup(Vector3 position, Transform parent, float groundHeightOffset)
        {
            GameObject fallback = new GameObject("SegmentChoiceTicketFallbackPickup");
            fallback.transform.SetParent(parent, true);
            fallback.transform.position = GroundService.ProjectToGround(position, groundHeightOffset);

            Transform modelRoot = CreateFallbackChild(fallback.transform, "ModelRoot", new Vector3(0f, 0.62f, 0f));
            Transform idleVfxRoot = CreateFallbackChild(fallback.transform, "VFX_IdleRoot", new Vector3(0f, 0.62f, 0f));
            Transform collectVfxRoot = CreateFallbackChild(fallback.transform, "VFX_CollectRoot", new Vector3(0f, 0.62f, 0f));
            Transform magnetTrailRoot = CreateFallbackChild(fallback.transform, "VFX_MagnetTrailRoot", new Vector3(0f, 0.62f, 0f));
            CreateFallbackTrigger(fallback.transform, new Vector3(0f, 0.62f, 0f), 0.7f);
            CreateSegmentChoiceTicketFallbackModel(modelRoot);

            WorldRewardPickup pickup = fallback.AddComponent<WorldRewardPickup>();
            pickup.Kind = RewardPickupKind.SegmentChoiceTicket;
            pickup.Amount = 1;
            pickup.ModelRoot = modelRoot;
            pickup.IdleVfxRoot = idleVfxRoot;
            pickup.CollectVfxRoot = collectVfxRoot;
            pickup.MagnetTrailVfxRoot = magnetTrailRoot;
            pickup.HoverHeight = 0.7f;
            pickup.HoverAmplitude = 0.13f;
            pickup.HoverSpeed = 2.55f;
            pickup.RotationSpeed = 95f;
            pickup.GroundHeightOffset = groundHeightOffset;
            pickup.DropPopHeight = 1.15f;
            pickup.DropPopDuration = 0.42f;
            pickup.CollectDistance = 0.6f;
            return pickup;
        }

        private static Transform CreateFallbackChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static void CreateFallbackTrigger(Transform parent, Vector3 center, float radius)
        {
            GameObject trigger = new GameObject("TriggerCollider");
            trigger.transform.SetParent(parent, false);
            SphereCollider collider = trigger.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.center = center;
            collider.radius = Mathf.Max(0.05f, radius);
        }

        private static void CreateSegmentChoiceTicketFallbackModel(Transform modelRoot)
        {
            GameObject modelPrefab = Resources.Load<GameObject>(SegmentChoiceTicketFallbackModelResourcePath);
            if (modelPrefab == null)
            {
                CreatePrimitiveSegmentChoiceTicketModel(modelRoot); // 모델 import 전 fallback
                return;
            }

            GameObject model = Instantiate(modelPrefab, modelRoot);
            model.name = "SegmentChoiceTicket_Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            FitFallbackModel(model.transform, 0.74f);
        }

        private static void CreatePrimitiveSegmentChoiceTicketModel(Transform modelRoot)
        {
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            model.name = "SegmentChoiceTicket_TemporaryModel";
            model.transform.SetParent(modelRoot, false);
            model.transform.localScale = new Vector3(0.44f, 0.08f, 0.44f);

            Collider collider = model.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private static void FitFallbackModel(Transform model, float targetSize)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest <= 0.0001f)
            {
                return;
            }

            float scale = targetSize / largest;
            Vector3 localCenter = model.InverseTransformPoint(bounds.center);
            model.localScale = Vector3.one * scale;
            model.localPosition -= localCenter * scale;
        }
    }
}
