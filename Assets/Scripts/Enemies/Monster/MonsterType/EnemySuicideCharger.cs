using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemySuicideCharger : MonoBehaviour // 플레이어 컨보이를 향해 돌진한 뒤 자폭하는 몬스터
    {
        private Transform target; // 추적 대상 Transform

        [Header("Movement")]
        [Min(0.1f)]
        [SerializeField] private float moveSpeed = 7.0f; // 자폭 몬스터 돌진 속도

        [Min(0.0f)]
        [SerializeField] private float groundHeight = 0.72f; // 바닥 위에 몬스터를 올려둘 높이

        [Min(0.1f)]
        [SerializeField] private float explosionStartRange = 1.2f; // 이 거리 안에 들어오면 자폭 준비 시작

        [Header("Explosion")]
        [Min(0.1f)]
        [SerializeField] private float explosionRadius = 3.0f; // 실제 폭발 판정 반경

        [Min(0.1f)]
        [SerializeField] private float chargeTime = 1.2f; // 몸이 커지고 범위가 진해지는 시간

        [Min(0.1f)]
        [SerializeField] private float knockbackDistance = 10.0f; // 플레이어를 밀어낼 거리

        [Min(0.01f)]
        [SerializeField] private float knockbackDuration = 1.0f; // 플레이어가 밀려나는 시간

        [Min(0.0f)]
        [SerializeField] private float knockbackHeight = 3.0f; // 플레이어가 공중으로 떠오를 높이

        [Min(1.0f)]
        [SerializeField] private float bodyScaleMultiplier = 1.8f; // 자폭 준비 중 몸이 커질 배율

        [Header("Charge Visual")]
        [SerializeField] private Color chargeColor = Color.red; // 자폭 준비 중 변할 몸 색

        [Header("Telegraph")]
        [SerializeField] private GameObject areaTelegraphPrefab; // 폭발 범위 표시 Prefab

        [Min(0.0f)]
        [SerializeField] private float telegraphGroundHeight = 0.03f; // 범위 표시를 바닥보다 살짝 위에 둘 높이

        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphStartAlpha = 0.1f; // 범위 표시 시작 투명도

        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphEndAlpha = 1.0f; // 폭발 직전 범위 표시 투명도

        private Vector3 originalScale; // 처음 몸 크기
        private float chargeTimer; // 자폭 준비가 진행된 시간
        private bool isCharging; // 자폭 준비 중인지 확인
        private bool hasExploded; // 이미 폭발했는지 확인

        public bool IsMoving => enabled && !hasExploded && !isCharging && target != null;
        public bool IsCharging => isCharging;
        public event System.Action Exploded;

        private GameObject currentTelegraph; // 현재 생성된 폭발 범위 표시

        private Renderer[] bodyRenderers; // 몸 색을 바꿀 Renderer 목록
        private MaterialPropertyBlock bodyPropertyBlock; // Material을 직접 바꾸지 않고 색만 덮어쓸 도구
        private Color originalBodyColor = Color.white; // 처음 몸 색
        private int bodyColorPropertyId; // Material 색상 속성 ID
        private bool hasBodyColorProperty; // 색상 속성을 찾았는지 여부

        private void Awake()
        {
            originalScale = transform.localScale; // 시작 몸 크기를 저장한다.
            CacheBodyRenderers(); // 몸 색을 바꿀 Renderer 정보를 준비한다.
            TryFindTarget(); // PlayerConvoy를 찾는다.
        }

        private void Update()
        {
            if (hasExploded) // 이미 폭발했다면
            {
                return; // 더 이상 처리하지 않는다.
            }

            if (target == null) // 추적 대상이 없다면
            {
                TryFindTarget(); // 다시 찾는다.
            }

            if (target == null) // 그래도 대상이 없다면
            {
                return; // 추적할 수 없으므로 종료한다.
            }

            if (isCharging) // 자폭 준비 중이라면
            {
                UpdateCharge(); // 몸 크기, 몸 색, 범위 표시를 갱신한다.
                return; // 돌진 이동은 하지 않는다.
            }

            ChaseTarget(); // 아직 자폭 준비 전이면 PlayerConvoy를 향해 달려간다.
        }

        private void OnDisable()
        {
            DestroyTelegraph(); // 비활성화될 때 범위 표시가 남지 않도록 제거한다.
            ClearBodyColorOverride(); // 색 덮어쓰기를 제거한다.
        }

        private void TryFindTarget()
        {
            if (MonsterInteractionApi.TryGetConvoyTarget(out Transform apiTarget)) // 등록된 컨보이 타겟이 있는지 확인한다.
            {
                target = apiTarget; // 조회된 컨보이 Transform을 자폭 몬스터의 추적 대상으로 저장한다.
                return; // 타겟을 찾았으므로 메서드를 종료한다.
            }

            target = null; // 등록된 컨보이가 없으면 추적 대상을 비워둔다.
        }

        private void ChaseTarget()
        {
            Vector3 offset = target.position - transform.position; // 몬스터에서 PlayerConvoy까지의 방향과 거리 벡터를 구한다.
            offset.y = 0.0f; // 높이 차이는 제거한다.

            if (offset.sqrMagnitude <= explosionStartRange * explosionStartRange) // 거의 붙었다면
            {
                BeginCharge(); // 자폭 준비를 시작한다.
                return; // 이번 프레임 이동은 하지 않는다.
            }

            Vector3 direction = offset.normalized; // PlayerConvoy 방향을 길이 1짜리 방향으로 만든다.
            Vector3 desiredPosition = transform.position + direction * (moveSpeed * Time.deltaTime); // 이번 프레임 이동하려는 위치를 계산한다.
            desiredPosition = GroundService.ProjectToGround(desiredPosition, groundHeight); // 바닥 높이에 맞춘다.

            transform.position = desiredPosition; // 계산된 위치를 적용한다.
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up); // 이동 방향을 바라보게 회전한다.
        }

        private void BeginCharge()
        {
            isCharging = true; // 자폭 준비 상태로 전환한다.
            chargeTimer = 0.0f; // 자폭 준비 시간을 초기화한다.

            CacheBodyRenderers(); // 몸 색을 바꿀 Renderer 정보를 준비한다.
            SetBodyChargeColor(0.0f); // 처음에는 원래 색으로 시작한다.

            CreateTelegraph(); // 폭발 범위 표시를 생성한다.
        }

        private void UpdateCharge()
        {
            chargeTimer += Time.deltaTime; // 지난 시간만큼 자폭 준비 시간을 증가시킨다.

            float progress = chargeTimer / chargeTime; // 자폭 준비 진행률을 계산한다.
            progress = Mathf.Clamp01(progress); // 진행률을 0에서 1 사이로 제한한다.

            Vector3 currentScale = Vector3.Lerp(originalScale, originalScale * bodyScaleMultiplier, progress); // 현재 진행률에 맞는 몸 크기를 계산한다.
            transform.localScale = currentScale; // 계산된 몸 크기를 적용한다.

            float scaleLift = (currentScale.y - originalScale.y) * 0.7f; // 커진 Y 크기의 절반만큼 위로 올릴 높이를 계산한다.
            transform.position = GroundService.ProjectToGround(transform.position, groundHeight + scaleLift); // 몸이 커진 만큼 위로 올려 땅에 박히지 않게 한다.

            SetBodyChargeColor(progress); // 몸 색을 점점 빨간색으로 바꾼다.

            UpdateTelegraph(progress); // 범위 표시 크기와 투명도를 갱신한다.

            if (progress >= 1.0f) // 자폭 준비 시간이 끝났다면
            {
                Explode(); // 폭발한다.
            }
        }

        private void Explode()
        {
            hasExploded = true; // 폭발 완료 상태로 표시한다.

            TryKnockbackTarget(); // 폭발 범위 안에 PlayerConvoy가 있으면 넉백을 요청한다.

            Exploded?.Invoke();

            DestroyTelegraph(); // 폭발 범위 표시를 제거한다.
            Destroy(gameObject); // 자폭 몬스터를 제거한다.
        }

        private void TryKnockbackTarget()
        {
            if (target == null) // 대상이 없다면
            {
                return; // 넉백할 수 없다.
            }

            Vector3 offset = target.position - transform.position; // 폭발 중심에서 PlayerConvoy까지의 벡터를 구한다.
            offset.y = 0.0f; // 높이 차이는 제거한다.

            if (offset.sqrMagnitude > explosionRadius * explosionRadius) // 폭발 범위 밖이라면
            {
                return; // 넉백하지 않는다.
            }

            MonsterInteractionApi.RequestConvoyKnockback(transform.position, explosionRadius, knockbackDistance, knockbackDuration, knockbackHeight); // 폭발 범위 안의 컨보이에게 넉백을 요청한다.
        }

        private void CacheBodyRenderers()
        {
            if (bodyRenderers != null && bodyRenderers.Length > 0) // 이미 Renderer를 찾았다면
            {
                return; // 다시 찾지 않는다.
            }

            bodyRenderers = GetComponentsInChildren<Renderer>(); // 현재 오브젝트와 자식의 모든 Renderer를 가져온다.
            bodyPropertyBlock = new MaterialPropertyBlock(); // 색 덮어쓰기용 PropertyBlock을 만든다.

            hasBodyColorProperty = false; // 아직 색 속성을 찾지 못한 상태로 시작한다.
            bodyColorPropertyId = 0; // 색 속성 ID 초기화

            if (bodyRenderers == null || bodyRenderers.Length == 0) // Renderer가 없다면
            {
                return; // 색을 바꿀 수 없다.
            }

            Material sharedMaterial = bodyRenderers[0].sharedMaterial; // 첫 번째 Renderer의 공유 Material을 가져온다.

            if (sharedMaterial == null) // Material이 없다면
            {
                return; // 색을 읽을 수 없다.
            }

            if (sharedMaterial.HasProperty("_BaseColor")) // URP Lit Material이면
            {
                bodyColorPropertyId = Shader.PropertyToID("_BaseColor"); // BaseColor 속성 ID를 저장한다.
                originalBodyColor = sharedMaterial.GetColor("_BaseColor"); // 원래 색을 저장한다.
                hasBodyColorProperty = true; // 색 속성을 찾았다고 표시한다.
            }
            else if (sharedMaterial.HasProperty("_Color")) // 기본 Material이면
            {
                bodyColorPropertyId = Shader.PropertyToID("_Color"); // Color 속성 ID를 저장한다.
                originalBodyColor = sharedMaterial.GetColor("_Color"); // 원래 색을 저장한다.
                hasBodyColorProperty = true; // 색 속성을 찾았다고 표시한다.
            }
        }

        private void SetBodyChargeColor(float progress)
        {
            CacheBodyRenderers(); // Renderer 정보가 준비되어 있는지 확인한다.

            if (!hasBodyColorProperty || bodyRenderers == null || bodyPropertyBlock == null) // 색을 바꿀 준비가 안 됐다면
            {
                return; // 종료한다.
            }

            progress = Mathf.Clamp01(progress); // 진행률을 0에서 1 사이로 제한한다.

            Color targetColor = chargeColor; // 목표 색을 가져온다.
            targetColor.a = originalBodyColor.a; // 투명도는 원래 몸 색을 유지한다.

            Color currentColor = Color.Lerp(originalBodyColor, targetColor, progress); // 원래 색에서 목표 색으로 점점 바꾼다.

            for (int i = 0; i < bodyRenderers.Length; i++) // 모든 Renderer에 적용한다.
            {
                if (bodyRenderers[i] == null) // Renderer가 없다면
                {
                    continue; // 건너뛴다.
                }

                bodyRenderers[i].GetPropertyBlock(bodyPropertyBlock); // 기존 PropertyBlock을 가져온다.
                bodyPropertyBlock.SetColor(bodyColorPropertyId, currentColor); // 색만 덮어쓴다.
                bodyRenderers[i].SetPropertyBlock(bodyPropertyBlock); // Renderer에 적용한다.
            }
        }

        private void ClearBodyColorOverride()
        {
            if (bodyRenderers == null) // Renderer 목록이 없다면
            {
                return; // 처리하지 않는다.
            }

            for (int i = 0; i < bodyRenderers.Length; i++) // 모든 Renderer를 순회한다.
            {
                if (bodyRenderers[i] == null) // Renderer가 없다면
                {
                    continue; // 건너뛴다.
                }

                bodyRenderers[i].SetPropertyBlock(null); // 색 덮어쓰기를 제거한다.
            }
        }

        private void CreateTelegraph()
        {
            if (areaTelegraphPrefab == null) // 범위 표시 Prefab이 없다면
            {
                return; // 생성하지 않는다.
            }

            Vector3 telegraphPosition = GroundService.ProjectToGround(transform.position, telegraphGroundHeight); // 현재 몬스터 위치를 바닥 기준으로 보정한다.

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // Monsters를 찾고, 없으면 현재 몬스터의 부모를 사용한다.

            currentTelegraph = Instantiate(areaTelegraphPrefab, telegraphPosition, Quaternion.identity, runtimeRoot); // 범위 표시를 Monsters 밑에 생성한다.

            float diameter = explosionRadius * 2.0f; // 반경을 지름으로 바꾼다.
            currentTelegraph.transform.localScale = new Vector3(diameter, currentTelegraph.transform.localScale.y, diameter); // 범위 표시 크기를 폭발 반경에 맞춘다.

            SetTelegraphAlpha(currentTelegraph, telegraphStartAlpha); // 처음에는 흐리게 표시한다.
        }

        private void UpdateTelegraph(float progress)
        {
            if (currentTelegraph == null) // 범위 표시가 없다면
            {
                return; // 갱신하지 않는다.
            }

            currentTelegraph.transform.position = GroundService.ProjectToGround(transform.position, telegraphGroundHeight); // 몬스터 위치를 따라오게 한다.

            float alpha = Mathf.Lerp(telegraphStartAlpha, telegraphEndAlpha, progress); // 진행도에 따라 점점 진하게 만든다.
            SetTelegraphAlpha(currentTelegraph, alpha); // 계산된 투명도를 적용한다.
        }

        private void DestroyTelegraph()
        {
            if (currentTelegraph != null) // 범위 표시가 있다면
            {
                Destroy(currentTelegraph); // 범위 표시를 제거한다.
                currentTelegraph = null; // 참조를 비운다.
            }
        }

        private void SetTelegraphAlpha(GameObject telegraph, float alpha)
        {
            if (telegraph == null) // 범위 표시 오브젝트가 없다면
            {
                return; // 처리하지 않는다.
            }

            alpha = Mathf.Clamp01(alpha); // 투명도를 0에서 1 사이로 제한한다.

            Renderer[] renderers = telegraph.GetComponentsInChildren<Renderer>(); // 범위 표시의 모든 Renderer를 가져온다.

            for (int i = 0; i < renderers.Length; i++) // Renderer를 하나씩 순회한다.
            {
                Material material = renderers[i].material; // 현재 Renderer의 Material을 가져온다.

                if (material.HasProperty("_BaseColor")) // URP Lit 계열 Material이면
                {
                    Color color = material.GetColor("_BaseColor"); // 현재 BaseColor를 가져온다.
                    color.a = alpha; // Alpha 값을 바꾼다.
                    material.SetColor("_BaseColor", color); // 변경된 색을 다시 넣는다.
                }
                else if (material.HasProperty("_Color")) // 기본 Material 계열이면
                {
                    Color color = material.color; // 현재 색을 가져온다.
                    color.a = alpha; // Alpha 값을 바꾼다.
                    material.color = color; // 변경된 색을 다시 넣는다.
                }
            }
        }
    }
}