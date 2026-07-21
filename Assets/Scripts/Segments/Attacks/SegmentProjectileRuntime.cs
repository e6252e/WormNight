using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class SegmentProjectileRuntime : MonoBehaviour // 데이터 기반 투사체
    {
        private readonly List<int> hitEnemyIds = new List<int>(8); // 관통 중복 방지
        private readonly List<int> explosionEnemyIds = new List<int>(16); // 폭발 중복 방지
        private readonly List<int> flameTickEnemyIds = new List<int>(16); // 화염 틱 중복 방지
        private static Material expandingFlameDebugMaterial; // SG05 1차 디버그 표시 재질

        private SegmentAttackProfile profile; // 공격 데이터
        private EnemyController target; // 목표
        private DamageData damage; // 피해값
        private WeaponStatBonusData weaponBonus; // 발사 시점 무기 강화 누적값
        private Transform hitVfxSocket; // 명중 VFX 기준점
        private Vector3 direction; // 직선 방향
        private Vector3 startPosition; // 곡사 시작
        private Vector3 endPosition; // 곡사 도착
        private float lifeTimer; // 남은 시간
        private float arcTimer; // 곡사 진행 시간
        private float arcDuration; // 곡사 전체 시간
        private int remainingPierces; // 남은 관통 수
        // 투석기 돌이 곡사 착지 후 바닥을 구르는 상태인지 저장
        private bool isRollingAfterArcLanding; // 착지 후 구르기 진행 중
        // 착지 후 굴러가기 시작/끝 위치
        private Vector3 landingRollStartPosition; // 구르기 시작 위치
        private Vector3 landingRollEndPosition; // 구르기 종료 위치
        // 착지 후 굴러가는 방향과 회전축
        private Vector3 landingRollDirection; // 바닥 구르기 방향
        private Vector3 landingRollSpinAxis; // 돌이 굴러가는 회전축
        // 착지 후 굴러가기 시간 계산값
        private float landingRollTimer; // 구르기 진행 시간
        private float landingRollDuration; // 구르기 전체 시간
        private int remainingSawBounces; // 남은 톱날 연쇄 수
        private int currentSawTargetId; // 현재 톱날 목표 ID
        private float sawSpinAngle; // 톱날 회전 누적 각도
        private float effectiveProjectileSpeed; // 강화 반영 속도
        private float effectiveExplosionRadius; // 강화 반영 폭발 반경
        private float flameSphereTimer; // 화염 구체 진행 시간
        private float flameSphereTickTimer; // 다음 화염 틱까지 남은 시간
        private float flameSphereDuration; // 화염 구체 전체 지속 시간
        private Transform flameInfluenceAnchor; // 화염 구체가 약하게 따라갈 총구
        private Vector3 lastFlameInfluenceAnchorPosition; // 직전 총구 위치
        private bool hasLastFlameInfluenceAnchorPosition; // 총구 위치 초기화 여부
        private GameObject areaTelegraphInstance; // 바닥 피해 범위 표시
        private bool useVerticalImpactDrop; // 착탄점 위 수직 낙하 사용

        public static SegmentProjectileRuntime Spawn(Transform root, GameObject prefab, Vector3 position, Vector3 direction, EnemyController target, SegmentAttackProfile profile, DamageData damage, WeaponStatBonusData weaponBonus = default, Transform flameInfluenceAnchor = null) // 생성 (weaponBonus=카드 강화 누적값)
        {
            return SpawnInternal(root, prefab, position, direction, target, false, Vector3.zero, profile, damage, weaponBonus, flameInfluenceAnchor); // 적 대상 투사체
        }

        public static SegmentProjectileRuntime SpawnAtPoint(Transform root, GameObject prefab, Vector3 position, Vector3 direction, Vector3 impactPoint, SegmentAttackProfile profile, DamageData damage, WeaponStatBonusData weaponBonus = default, Transform flameInfluenceAnchor = null) // 지점 타격 생성
        {
            return SpawnInternal(root, prefab, position, direction, null, true, impactPoint, profile, damage, weaponBonus, flameInfluenceAnchor); // 바닥 지점 투사체
        }

        private static SegmentProjectileRuntime SpawnInternal(Transform root, GameObject prefab, Vector3 position, Vector3 direction, EnemyController target, bool useImpactPoint, Vector3 impactPoint, SegmentAttackProfile profile, DamageData damage, WeaponStatBonusData weaponBonus, Transform flameInfluenceAnchor)
        {
            if (ShouldSpawnAsVerticalImpactDrop(useImpactPoint, profile))
            {
                Vector3 groundPoint = GroundService.ProjectToGround(impactPoint, 0f); // 착탄 바닥
                position = groundPoint + Vector3.up * Mathf.Max(0f, profile.VerticalImpactDropHeight); // 하늘 생성
                direction = Vector3.down; // 수직 낙하 방향
            }

            GameObject instance;
            if (prefab != null)
            {
                Quaternion rotation = ResolveProjectileRotation(direction); // 방향
                instance = Instantiate(prefab, position, rotation, root); // 프리팹 생성
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Sphere); // fallback 탄
                instance.name = "GenericProjectile";
                instance.transform.SetParent(root, false);
                instance.transform.position = position;
                instance.transform.localScale = Vector3.one * 0.35f;
                Destroy(instance.GetComponent<Collider>()); // 표시 전용
            }

            SegmentProjectileRuntime runtime = instance.GetComponent<SegmentProjectileRuntime>(); // 런타임
            if (runtime == null)
            {
                runtime = instance.AddComponent<SegmentProjectileRuntime>(); // 자동 보강
            }

            runtime.Configure(direction, target, useImpactPoint, impactPoint, profile, damage, weaponBonus, flameInfluenceAnchor); // 값 주입
            return runtime;
        }

        private static Quaternion ResolveProjectileRotation(Vector3 direction) // 수직 방향도 안전한 회전
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity; // 방향 없음
            }

            Vector3 forward = direction.normalized; // 진행 방향
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up; // 수직 낙하 보정
            return Quaternion.LookRotation(forward, up); // 안전 회전
        }

        private void Configure(Vector3 fireDirection, EnemyController target, bool useImpactPoint, Vector3 impactPoint, SegmentAttackProfile profile, DamageData damage, WeaponStatBonusData weaponBonus, Transform flameInfluenceAnchor) // 값 주입 (프로필+강화 합산)
        {
            this.profile = profile; // 프로필
            this.target = target; // 목표
            this.damage = damage; // 피해
            this.weaponBonus = weaponBonus; // 발사 시점 강화값
            useVerticalImpactDrop = ShouldSpawnAsVerticalImpactDrop(useImpactPoint, profile); // 수직 낙하 여부
            this.flameInfluenceAnchor = profile != null
                && profile.MoveType == SegmentAttackMoveType.ExpandingFlameSphere
                && profile.UseFlameMuzzleInfluence
                ? flameInfluenceAnchor
                : null;
            hasLastFlameInfluenceAnchorPosition = this.flameInfluenceAnchor != null;
            lastFlameInfluenceAnchorPosition = hasLastFlameInfluenceAnchorPosition ? this.flameInfluenceAnchor.position : Vector3.zero;
            direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : transform.forward; // 방향
            ApplyProjectileScale(); // 프로필 크기 적용
            ApplyProjectileVfxScale(); // 투사체 안 VFX만 별도 보정
            lifeTimer = profile != null ? Mathf.Max(0.1f, profile.ProjectileLifetime) : 0.1f; // 수명
            effectiveProjectileSpeed = profile != null
                ? weaponBonus.ResolveProjectileSpeed(profile.ProjectileSpeed) // 기본+강화 속도
                : 0.1f;
            remainingPierces = profile != null
                ? weaponBonus.ResolvePierceCount(profile.PierceCount) // 기본+강화 관통
                : 1;
            effectiveExplosionRadius = profile != null
                ? weaponBonus.ResolveExplosionRadius(profile.ExplosionRadius) // 기본+강화 폭발
                : 0.1f;
            startPosition = transform.position; // 시작
            float targetAimHeight = profile != null ? profile.TargetAimHeight : 0.45f; // 조준 높이
            endPosition = useImpactPoint
                ? GroundService.ProjectToGround(impactPoint, 0f)
                : target != null ? target.transform.position + Vector3.up * targetAimHeight : startPosition + direction * 8f; // 도착
            float distance = Vector3.Distance(startPosition, endPosition); // 거리
            arcDuration = profile != null ? Mathf.Max(0.05f, distance / effectiveProjectileSpeed) : 0.05f; // 곡사 시간
            arcTimer = 0f; // 진행 초기화
            isRollingAfterArcLanding = false; // 착지 후 구르기 초기화
            landingRollTimer = 0f; // 구르기 진행 초기화
            hitEnemyIds.Clear(); // 중복 초기화
            explosionEnemyIds.Clear(); // 중복 초기화
            remainingSawBounces = profile != null ? weaponBonus.ResolveMaxChainDepth(profile.MaxChainDepth) : 0; // 톱날 연쇄 초기화
            currentSawTargetId = target != null ? target.EnemyId : 0; // 최초 목표 저장
            sawSpinAngle = 0f; // 톱날 회전 초기화
            flameSphereTimer = 0f; // 화염 구체 시간 초기화
            flameSphereTickTimer = 0f; // 첫 프레임부터 피해 판정
            flameSphereDuration = profile != null ? Mathf.Max(0.05f, profile.ProjectileLifetime) : 0.05f; // 전체 지속 시간
            SetupExpandingFlameSphereVisual(); // 화염 디버그 구체 표시
            SpawnAreaTelegraph(); // 피해 범위 장판
        }

        private static bool ShouldSpawnAsVerticalImpactDrop(bool useImpactPoint, SegmentAttackProfile profile) // 수직 낙하 생성 조건
        {
            return useImpactPoint
                && profile != null
                && profile.MoveType == SegmentAttackMoveType.ArcProjectile
                && profile.UseVerticalImpactDrop;
        }

        private float GetProjectileSpeed() // 강화 반영 속도
        {
            float speed = effectiveProjectileSpeed > 0f ? effectiveProjectileSpeed : (profile != null ? profile.ProjectileSpeed : 0.1f); // fallback
            return Mathf.Max(0.1f, speed); // 최소 속도
        }

        private float GetExplosionRadius() // 강화 반영 폭발 반경
        {
            float radius = effectiveExplosionRadius > 0f ? effectiveExplosionRadius : (profile != null ? profile.ExplosionRadius : 0.1f); // fallback
            return Mathf.Max(0.1f, radius); // 최소 반경
        }

        private void SpawnAreaTelegraph() // 실제 피해 범위 표시
        {
            if (!RuntimeCombatDebugVisuals.TemporaryCombatDebugVisualsEnabled)
            {
                return; // 임시 범위 표시 비활성화
            }

            if (profile == null || profile.AreaTelegraphPrefab == null || profile.ImpactType != SegmentAttackImpactType.ExplosionArea)
            {
                return; // 표시 없음
            }

            Vector3 telegraphPosition = GroundService.ProjectToGround(endPosition, profile.AreaTelegraphGroundOffset); // 바닥 위치
            float lifetime = Mathf.Max(0.05f, arcDuration + 0.1f); // 낙하 중 유지
            areaTelegraphInstance = SegmentAttackVfxPlayer.PlayExplosion(profile.AreaTelegraphPrefab, telegraphPosition, GetExplosionRadius(), lifetime, profile.AreaTelegraphAlpha); // 반경 기준 장판
        }

        private void ClearAreaTelegraph() // 장판 제거
        {
            if (areaTelegraphInstance == null)
            {
                return; // 제거할 대상 없음
            }

            Destroy(areaTelegraphInstance);
            areaTelegraphInstance = null;
        }

        private void Update() // 이동 루프
        {
            if (profile == null || !damage.IsValid)
            {
                Destroy(gameObject); // 데이터 없음
                return;
            }

            lifeTimer -= Time.deltaTime; // 수명 감소
            if (lifeTimer <= 0f)
            {
                Destroy(gameObject); // 만료
                return;
            }

            if (isRollingAfterArcLanding)
            {
                UpdateLandingRoll(); // 투석기 돌 착지 후 구르기
                return;
            }

            switch (profile.MoveType)
            {
                case SegmentAttackMoveType.ArcProjectile:
                    UpdateArcProjectile(); // 곡사
                    break;
                case SegmentAttackMoveType.HomingProjectile:
                    UpdateHomingProjectile(); // 추적
                    break;
                case SegmentAttackMoveType.SawBounceProjectile:
                    UpdateSawBounceProjectile(); // 톱날 관통 연쇄
                    break;
                case SegmentAttackMoveType.ExpandingFlameSphere:
                    UpdateExpandingFlameSphere(); // 전진 확장 화염 구체
                    break;
                default:
                    UpdateStraightProjectile(); // 직선/관통
                    break;
            }
        }

        private void OnDestroy()
        {
            ClearAreaTelegraph(); // 투사체 제거 시 장판 정리
        }


        private void ApplyProjectileScale() // 프로필 투사체 크기 적용
        {
            if (profile == null)
            {
                return;
            }

            Vector3 scale = profile.ProjectileScale; // 프로필 크기
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            {
                return; // 기본 프리팹 크기 유지
            }

            transform.localScale = scale; // 런타임 투사체 크기
        }

        private void ApplyProjectileVfxScale() // 투사체 자식 VFX만 별도 크기 적용
        {
            if (profile == null)
            {
                return;
            }

            Vector3 scale = profile.ProjectileVfxScale; // 보이는 VFX 목표 크기
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            {
                return; // VFX 기본 크기 유지
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i); // 투사체 직속 자식
                if (!IsProjectileVfxChild(child))
                {
                    continue; // 소켓/모델 제외
                }

                child.localScale = ResolveLocalScaleForWorldScale(child, scale); // 부모 스케일 보정
            }
        }

        private static bool IsProjectileVfxChild(Transform child) // 투사체 본체 VFX 대상 판별
        {
            if (child == null || child.name == "VFX_Hit")
            {
                return false; // 명중 위치 소켓 제외
            }

            return child.name.StartsWith("VFX_"); // VFX_ 자식만 조절
        }

        private static Vector3 ResolveLocalScaleForWorldScale(Transform target, Vector3 worldScale) // 부모 스케일 보정
        {
            Transform parent = target.parent;
            if (parent == null)
            {
                return worldScale; // 루트면 그대로
            }

            Vector3 parentScale = parent.lossyScale; // 루트 투사체 스케일 포함
            return new Vector3(
                DivideScale(worldScale.x, parentScale.x),
                DivideScale(worldScale.y, parentScale.y),
                DivideScale(worldScale.z, parentScale.z));
        }

        private static float DivideScale(float value, float divisor) // 0 스케일 보호
        {
            return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
        }
    }
}
