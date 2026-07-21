using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class ManaOrbPickup : MonoBehaviour
    {
        private static readonly Quaternion ManaOrbModelUprightRotation = Quaternion.Euler(90f, 0f, 0f);

        [Header("Collect")]
        [SerializeField] private float collectDistance = 0.6f;

        [Header("Visual Motion")]
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform collectVfxRoot;
        [SerializeField] private float hoverHeight = 0.54f;
        [SerializeField] private float hoverAmplitude = 0.1f;
        [SerializeField] private float hoverSpeed = 2.4f;
        [SerializeField] private float rotationSpeed = 125f;

        [Header("Magnet")]
        [SerializeField] private float attractRadius = 4f;
        [SerializeField] private float pullStrength = 570f;
        [SerializeField] private float maxPullSpeed = 252f;

        private ManaOrbCollectSpecialWave owner;
        private ConvoyController cachedConvoy;
        private Vector3 velocity;
        private float hoverPhase;
        private bool collected;
        private bool initialized;

        private void Awake()
        {
            InitializeVisual();
        }

        private void OnEnable()
        {
            InitializeVisual();
            velocity = Vector3.zero;
            collected = false;
        }

        public void Configure(ManaOrbCollectSpecialWave waveOwner, float radius)
        {
            owner = waveOwner;
            velocity = Vector3.zero;
            collected = false;
            hoverPhase = Random.Range(0f, Mathf.PI * 2f);

            // The special wave may pass a wider pickup radius, but this object should
            // keep the normal pickup feel: close approach first, then fast attraction.
            collectDistance = Mathf.Min(Mathf.Max(0.05f, radius), Mathf.Max(0.05f, collectDistance));

            InitializeVisual();
        }

        private void Update()
        {
            if (collected)
            {
                return;
            }

            UpdateVisualMotion();

            Transform pickupTarget = ResolvePickupTarget();
            if (pickupTarget == null)
            {
                return;
            }

            TryAttractAndCollect(pickupTarget.position);
        }

        private Transform ResolvePickupTarget()
        {
            if (!MonsterInteractionApi.TryGetConvoyTarget(out Transform convoyTarget))
            {
                return null;
            }

            if (cachedConvoy == null || cachedConvoy.transform != convoyTarget || !cachedConvoy.gameObject.activeInHierarchy)
            {
                cachedConvoy = convoyTarget.GetComponent<ConvoyController>();
            }

            if (cachedConvoy != null && cachedConvoy.HeadVisual != null && cachedConvoy.HeadVisual.gameObject.activeInHierarchy)
            {
                return cachedConvoy.HeadVisual;
            }

            return convoyTarget;
        }

        private void TryAttractAndCollect(Vector3 targetPosition)
        {
            Vector3 target = targetPosition;
            target.y = transform.position.y;

            Vector3 offset = target - transform.position;
            offset.y = 0f;

            float safeAttractRadius = Mathf.Max(0.05f, attractRadius);
            float safeCollectDistance = Mathf.Max(0.05f, collectDistance);
            float distanceSqr = offset.sqrMagnitude;

            if (distanceSqr > safeAttractRadius * safeAttractRadius)
            {
                velocity = Vector3.zero;
                return;
            }

            if (distanceSqr <= safeCollectDistance * safeCollectDistance)
            {
                Collect();
                return;
            }

            float distance = Mathf.Sqrt(distanceSqr);
            if (distance <= 0.001f)
            {
                return;
            }

            Vector3 direction = offset / distance;
            float rangeFactor = 1f - Mathf.Clamp01(distance / safeAttractRadius);
            float targetSpeed = Mathf.Min(
                Mathf.Max(0.1f, maxPullSpeed),
                velocity.magnitude + Mathf.Max(0f, pullStrength) * (0.45f + rangeFactor) * Time.deltaTime);

            velocity = Vector3.Lerp(velocity, direction * targetSpeed, 1f - Mathf.Exp(-12f * Time.deltaTime));

            Vector3 nextPosition = transform.position + velocity * Time.deltaTime;
            nextPosition.y = transform.position.y;
            transform.position = nextPosition;
        }

        private void Collect()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            Vector3 collectPosition = ResolveCollectVfxPosition();
            RewardPickupCollectVfxPlayer.Play(collectPosition); // 획득 VFX
            PlayCollectSfx(collectPosition);

            if (owner != null)
            {
                owner.NotifyManaOrbCollected(this);
            }

            Destroy(gameObject);
        }

        private void PlayCollectSfx(Vector3 position)
        {
            if (GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.ManaOrbPickup, position, true))
            {
                return;
            }

            GameplaySfxEmitter.TryPlayCatalogAt(GameplaySfxCue.ManaOrbPickup, position);
        }

        private void InitializeVisual()
        {
            if (initialized)
            {
                return;
            }

            modelRoot = ResolveModelRoot();
            collectVfxRoot = ResolveCollectVfxRoot();
            hoverPhase = Random.Range(0f, Mathf.PI * 2f);

            Transform primaryModel = FindPrimaryModelTransform();
            if (primaryModel != null)
            {
                primaryModel.localRotation = ManaOrbModelUprightRotation;
            }

            initialized = true;
        }

        private Transform ResolveModelRoot()
        {
            if (modelRoot != null)
            {
                return modelRoot;
            }

            Transform foundModelRoot = transform.Find("ModelRoot");
            return foundModelRoot != null ? foundModelRoot : transform;
        }

        private Transform ResolveCollectVfxRoot()
        {
            if (collectVfxRoot != null)
            {
                return collectVfxRoot;
            }

            return transform.Find("VFX_CollectRoot");
        }

        private Transform FindPrimaryModelTransform()
        {
            Transform root = modelRoot != null ? modelRoot : transform;
            MeshRenderer renderer = root.GetComponentInChildren<MeshRenderer>(true);
            return renderer != null && renderer.transform != root ? renderer.transform : root;
        }

        private void UpdateVisualMotion()
        {
            if (!initialized)
            {
                InitializeVisual();
            }

            if (modelRoot != null)
            {
                float bob = hoverHeight + Mathf.Sin(Time.time * hoverSpeed + hoverPhase) * hoverAmplitude;
                modelRoot.localPosition = Vector3.up * Mathf.Max(0f, bob);
            }

            Transform visual = modelRoot != null ? modelRoot : transform;
            if (rotationSpeed > 0f)
            {
                visual.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
            }
        }

        private Vector3 ResolveCollectVfxPosition()
        {
            return collectVfxRoot != null ? collectVfxRoot.position : transform.position + Vector3.up * hoverHeight;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1.0f, 0.82f, 0.05f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, collectDistance);

            Gizmos.color = new Color(0.25f, 0.9f, 1.0f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, attractRadius);
        }
    }
}
