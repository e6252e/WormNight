using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon
    {
        private Vector3 GetFireDirection(EnemyController target, Vector3 spawnPosition) // 발사 방향
        {
            if (IsTargetUsable(target))
            {
                Vector3 targetPosition = target.transform.position + Vector3.up * AttackProfile.TargetAimHeight; // 목표 중심
                Vector3 direction = targetPosition - spawnPosition; // 포구 -> 목표
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized; // 목표 방향
                }
            }

            Transform muzzle = ResolveMuzzle(); // 포구 fallback
            return muzzle != null ? muzzle.forward : transform.forward; // 현재 방향
        }

        private bool AimHeadAtTarget(EnemyController target, float deltaTime, float turnSpeedMultiplier = 1f) // 머리 조준
        {
            Transform pivot = ResolveHeadYawPivot(); // 회전축
            if (pivot == null || !IsTargetUsable(target))
            {
                return true; // 회전축 없음
            }

            Transform muzzle = ResolveMuzzle(); // 포구
            if (!TryGetHorizontalAim(target, pivot, muzzle, out Vector3 currentDirection, out Vector3 targetDirection))
            {
                return true; // 방향 없음
            }

            float signedAngle = Vector3.SignedAngle(currentDirection, targetDirection, Vector3.up); // 목표 각도
            float maxStep = AttackProfile.HeadTurnSpeed * Mathf.Clamp(turnSpeedMultiplier, 0.01f, 1f) * deltaTime; // 회전량
            float step = Mathf.Clamp(signedAngle, -maxStep, maxStep); // 과회전 방지
            pivot.Rotate(Vector3.up, step, Space.World); // 회전

            if (!TryGetHorizontalAim(target, pivot, muzzle, out currentDirection, out targetDirection))
            {
                return true; // 방향 없음
            }

            return Mathf.Abs(Vector3.SignedAngle(currentDirection, targetDirection, Vector3.up)) <= AttackProfile.FireAngleTolerance; // 조준 완료
        }

        private bool TryGetHorizontalAim(EnemyController target, Transform pivot, Transform muzzle, out Vector3 currentDirection, out Vector3 targetDirection) // 수평 조준
        {
            currentDirection = Vector3.zero; // 현재 방향
            targetDirection = Vector3.zero; // 목표 방향
            Vector3 aimOrigin = muzzle != null ? muzzle.position : pivot.position; // 포구 우선
            Vector3 targetPosition = target.transform.position + Vector3.up * AttackProfile.TargetAimHeight; // 목표 중심
            targetDirection = targetPosition - aimOrigin; // 목표 벡터
            targetDirection.y = 0f; // 수평 회전만
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                return false; // 방향 없음
            }

            currentDirection = GetCurrentMuzzleDirection(pivot, muzzle); // 투석기는 머즐 위치가 아닌 머즐 방향 기준 조준
            if (currentDirection.sqrMagnitude <= 0.0001f)
            {
                return false; // 기준 없음
            }

            currentDirection.Normalize(); // 정규화
            targetDirection.Normalize(); // 정규화
            return true; // 계산 가능
        }

        private Vector3 GetCurrentMuzzleDirection(Transform pivot, Transform muzzle) // 현재 포신 방향
        {
            if (ShouldAimByMuzzleForward())
            {
                Vector3 muzzleForwardDirection = GetTrebuchetAimDirection(muzzle, pivot); // SG03은 머즐 X축을 정면 조준축으로 사용
                if (muzzleForwardDirection.sqrMagnitude > 0.0001f)
                {
                    return muzzleForwardDirection; // 머즐 Z+ 방향을 조준 기준으로 사용
                }
            }

            if (muzzle != null)
            {
                Vector3 pivotToMuzzle = muzzle.position - pivot.position; // 피벗 -> 포구
                pivotToMuzzle.y = 0f; // 수평
                if (pivotToMuzzle.sqrMagnitude > 0.0001f)
                {
                    return pivotToMuzzle; // 모델 기준
                }

                Vector3 muzzleForward = muzzle.forward; // 포구 방향
                muzzleForward.y = 0f;
                if (muzzleForward.sqrMagnitude > 0.0001f)
                {
                    return muzzleForward;
                }
            }

            Vector3 pivotForward = pivot.forward; // 피벗 방향
            pivotForward.y = 0f;
            return pivotForward;
        }

        // 투석기처럼 머즐 위치와 조준 정면이 다른 무기는 머즐 방향으로 조준한다.
        private bool ShouldAimByMuzzleForward()
        {
            return ResolveTrebuchetFireMotion() != null; // SG03 투석기 전용 보정
        }

        // 투석기 머즐 X+ 방향을 수평 조준 벡터로 변환
        private static Vector3 GetTrebuchetAimDirection(Transform primary, Transform fallback)
        {
            if (primary != null)
            {
                Vector3 direction = primary.right; // 투석기 머즐의 빨간 X축
                direction.y = 0f; // 좌우 회전만 사용
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction; // 머즐 X축 방향
                }
            }

            Vector3 fallbackDirection = fallback != null ? fallback.forward : Vector3.zero; // 피벗 fallback
            fallbackDirection.y = 0f; // 수평화
            return fallbackDirection; // 결과
        }

        private Transform ResolveHeadYawPivot() // 회전축 찾기
        {
            if (HeadYawPivot != null)
            {
                return HeadYawPivot; // 수동 연결
            }

            Transform root = Segment != null ? Segment.transform : transform; // 검색 루트
            HeadYawPivot = FindChildRecursive(root, "YawPivot"); // 머리 프리팹 기준 회전축
            if (HeadYawPivot == null)
            {
                HeadYawPivot = FindChildRecursive(root, "Joint_HeadMount"); // 기존 조립 기준 fallback
            }

            if (HeadYawPivot == null)
            {
                HeadYawPivot = FindChildRecursive(root, "Joint"); // 구버전 fallback
            }

            return HeadYawPivot;
        }

        private Transform ResolveMuzzle() // 포구 찾기
        {
            if (Muzzle != null)
            {
                return Muzzle; // 수동 연결
            }

            Transform pivot = ResolveHeadYawPivot(); // 회전축
            Transform root = pivot != null ? pivot : (Segment != null ? Segment.transform : transform); // 검색 루트
            Muzzle = FindChildRecursive(root, "Muzzle"); // 포구
            return Muzzle;
        }

        private Transform ResolveProjectileMuzzle(int projectileIndex, Transform fallbackMuzzle) // 다중 포구 발사 위치
        {
            CacheProjectileMuzzles(); // 수동/자동 포구 목록 갱신
            if (projectileMuzzleBuffer.Count <= 0)
            {
                return fallbackMuzzle != null ? fallbackMuzzle : ResolveMuzzle(); // 단일 포구 fallback
            }

            int index = ResolveProjectileMuzzleBufferIndex(projectileIndex); // 순차/짝발사 순서
            return projectileMuzzleBuffer[index]; // 이번 탄 포구
        }

        private int ResolveProjectileMuzzleBufferIndex(int projectileIndex)
        {
            int muzzleCount = projectileMuzzleBuffer.Count; // 현재 다중 포구 수
            int index = Mathf.Abs(projectileIndex) % muzzleCount; // 기본 순차 반복
            if (ShouldUseCannonLv3PairedMuzzleOrder(muzzleCount))
            {
                return ResolveCannonLv3PairedMuzzleIndex(index); // 1+4, 2+5, 3+6
            }

            return index;
        }

        private bool ShouldUseCannonLv3PairedMuzzleOrder(int muzzleCount)
        {
            return muzzleCount == 6
                && AttackProfile != null
                && AttackProfile.FireProjectilesSequentially
                && AttackProfile.ProjectileCount == 6
                && AttackProfile.ProjectileVolleySize == 2
                && string.Equals(GetEffectiveSegmentId(), "SG01_Cannon", System.StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveCannonLv3PairedMuzzleIndex(int sequentialIndex)
        {
            switch (sequentialIndex)
            {
                case 1:
                    return 3; // 2번째 탄은 4번 포구
                case 2:
                    return 1; // 3번째 탄은 2번 포구
                case 3:
                    return 4; // 4번째 탄은 5번 포구
                case 4:
                    return 2; // 5번째 탄은 3번 포구
                case 5:
                    return 5; // 6번째 탄은 6번 포구
                default:
                    return 0; // 1번째 탄은 1번 포구
            }
        }

        private void CacheProjectileMuzzles() // 다중 포구 목록 수집
        {
            projectileMuzzleBuffer.Clear();
            if (ProjectileMuzzles != null)
            {
                for (int i = 0; i < ProjectileMuzzles.Length; i++)
                {
                    if (ProjectileMuzzles[i] != null)
                    {
                        projectileMuzzleBuffer.Add(ProjectileMuzzles[i]); // 인스펙터 연결 우선
                    }
                }
            }

            if (projectileMuzzleBuffer.Count > 0)
            {
                return; // 수동 연결 사용
            }

            Transform pivot = ResolveHeadYawPivot(); // 머리 기준
            Transform root = pivot != null ? pivot : (Segment != null ? Segment.transform : transform); // 검색 루트
            Transform muzzleRoot = FindChildRecursive(root, "Muzzles"); // SG01 Lv3 다중 포구 루트
            if (muzzleRoot != null)
            {
                for (int i = 0; i < muzzleRoot.childCount; i++)
                {
                    Transform child = muzzleRoot.GetChild(i); // 계층 순서
                    if (child != null && child.name.StartsWith("Muzzle_"))
                    {
                        projectileMuzzleBuffer.Add(child); // Muzzle_01~ 순서
                    }
                }
            }

            if (projectileMuzzleBuffer.Count > 0)
            {
                return; // 자동 포구 사용
            }

            for (int i = 1; i <= 12; i++)
            {
                Transform muzzle = FindChildRecursive(root, $"Muzzle_{i:00}"); // 이름 fallback
                if (muzzle != null)
                {
                    projectileMuzzleBuffer.Add(muzzle);
                }
            }
        }

        private Transform ResolveMuzzleVfxSocket(Transform muzzle) // 발사 VFX 기준점
        {
            if (muzzle != null && muzzle.name.StartsWith("Muzzle_"))
            {
                Transform socket = FindChildRecursive(muzzle, "VFX_Muzzle"); // 다중 포구별 VFX
                if (socket == null)
                {
                    socket = FindChildRecursive(muzzle, "MuzzleVFX"); // fallback
                }

                return socket; // 다중 포구는 전역 캐시를 오염시키지 않음
            }

            if (MuzzleVfxSocket != null)
            {
                return MuzzleVfxSocket; // 수동 연결
            }

            Transform root = muzzle != null ? muzzle : ResolveMuzzle(); // 포구 기준
            MuzzleVfxSocket = FindChildRecursive(root, "VFX_Muzzle"); // 정식 이름
            if (MuzzleVfxSocket == null)
            {
                MuzzleVfxSocket = FindChildRecursive(root, "MuzzleVFX"); // fallback
            }

            return MuzzleVfxSocket;
        }


        private bool ShouldUseSustainedMuzzleVfx()
        {
            return AttackProfile != null
                && AttackProfile.MoveType == SegmentAttackMoveType.ExpandingFlameSphere
                && AttackProfile.MuzzleVfxPrefab != null;
        }

        private bool IsSustainedMuzzleVfxActive()
        {
            return sustainedMuzzleVfxInstance != null;
        }

        private void StartSustainedMuzzleVfx(Transform muzzle)
        {
            if (!ShouldUseSustainedMuzzleVfx() || sustainedMuzzleVfxInstance != null)
            {
                return;
            }

            Transform socket = ResolveMuzzleVfxSocket(muzzle);
            sustainedMuzzleVfxInstance = socket != null
                ? Instantiate(AttackProfile.MuzzleVfxPrefab, socket)
                : Instantiate(AttackProfile.MuzzleVfxPrefab);
            sustainedMuzzleVfxInstance.name = AttackProfile.MuzzleVfxPrefab.name + "_MuzzleRuntime";
            sustainedMuzzleVfxParticles = sustainedMuzzleVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureSustainedMuzzleVfxParticles();
            UpdateSustainedMuzzleVfx(muzzle);
        }

        private void ConfigureSustainedMuzzleVfxParticles()
        {
            if (sustainedMuzzleVfxParticles == null)
            {
                return;
            }

            for (int i = 0; i < sustainedMuzzleVfxParticles.Length; i++)
            {
                ParticleSystem particle = sustainedMuzzleVfxParticles[i];
                if (particle == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particle.main;
                main.playOnAwake = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                if (!particle.isPlaying)
                {
                    particle.Play(true);
                }
            }
        }

        private void UpdateSustainedMuzzleVfx(Transform muzzle)
        {
            if (sustainedMuzzleVfxInstance == null)
            {
                return;
            }

            Transform vfxTransform = sustainedMuzzleVfxInstance.transform;
            Transform socket = ResolveMuzzleVfxSocket(muzzle);
            if (socket != null)
            {
                if (vfxTransform.parent != socket)
                {
                    vfxTransform.SetParent(socket, false);
                }

                vfxTransform.localPosition = Vector3.zero;
                vfxTransform.localRotation = GetFlamethrowerVfxLocalRotation();
                vfxTransform.localScale = GetMuzzleVfxScale();
                return;
            }

            Transform fallback = muzzle != null ? muzzle : transform;
            vfxTransform.position = GetMuzzleVfxPosition(muzzle);
            vfxTransform.rotation = fallback.rotation * GetFlamethrowerVfxLocalRotation();
            vfxTransform.localScale = GetMuzzleVfxScale();
        }

        private Vector3 GetMuzzleVfxPosition(Transform muzzle)
        {
            Transform socket = ResolveMuzzleVfxSocket(muzzle);
            if (socket != null)
            {
                return socket.position;
            }

            return muzzle != null ? muzzle.position : transform.position + Vector3.up * AttackProfile.AttackSpawnHeight;
        }

        private static Quaternion GetFlamethrowerVfxLocalRotation()
        {
            return Quaternion.Euler(0f, 90f, 0f);
        }

        private Vector3 GetMuzzleVfxScale()
        {
            if (AttackProfile == null)
            {
                return Vector3.one;
            }

            Vector3 scale = AttackProfile.MuzzleVfxScale;
            return scale.x > 0f && scale.y > 0f && scale.z > 0f ? scale : Vector3.one;
        }

        private void StopSustainedMuzzleVfx(bool immediate)
        {
            if (sustainedMuzzleVfxInstance == null)
            {
                return;
            }

            if (sustainedMuzzleVfxParticles != null)
            {
                for (int i = 0; i < sustainedMuzzleVfxParticles.Length; i++)
                {
                    ParticleSystem particle = sustainedMuzzleVfxParticles[i];
                    if (particle != null)
                    {
                        particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            float profileLifetime = AttackProfile != null ? AttackProfile.MuzzleVfxLifetime : 0.1f;
            float tailLifetime = immediate ? 0f : Mathf.Max(0.1f, profileLifetime);
            Destroy(sustainedMuzzleVfxInstance, tailLifetime);
            sustainedMuzzleVfxInstance = null;
            sustainedMuzzleVfxParticles = null;
        }


        private void PlayMuzzleVfx(Transform muzzle) // 발사 VFX
        {
            Transform socket = ResolveMuzzleVfxSocket(muzzle); // 기준점
            Transform parent = socket != null ? socket : muzzle; // 부착 대상
            if (parent != null)
            {
                GameObject attachedInstance = SegmentAttackVfxPlayer.PlayAttached(AttackProfile.MuzzleVfxPrefab, parent, Vector3.zero, Quaternion.identity, GetMuzzleVfxScale(), AttackProfile.MuzzleVfxLifetime); // 총구 추적
                ConfigureAttachedMuzzleVfxParticles(attachedInstance); // 부착 이동 보정
                return;
            }

            Vector3 position = socket != null ? socket.position : (muzzle != null ? muzzle.position : transform.position + Vector3.up * AttackProfile.AttackSpawnHeight); // 위치
            Quaternion rotation = socket != null ? socket.rotation : (muzzle != null ? muzzle.rotation : transform.rotation); // 방향
            GameObject instance = SegmentAttackVfxPlayer.Play(AttackProfile.MuzzleVfxPrefab, position, rotation, AttackProfile.MuzzleVfxLifetime); // 공용 생성
            if (instance != null)
            {
                instance.transform.localScale = GetMuzzleVfxScale(); // 프로필 스케일
            }
        }

        private static void ConfigureAttachedMuzzleVfxParticles(GameObject instance) // 부착형 파티클 보정
        {
            if (instance == null)
            {
                return; // 생성 없음
            }

            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true); // 하위 파티클
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particle.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local; // 총구 이동 추적
                main.scalingMode = ParticleSystemScalingMode.Hierarchy; // 부모 스케일 반영
            }
        }

        private void PlayHitVfx(Vector3 position) // 명중 VFX
        {
            SegmentAttackVfxPlayer.Play(AttackProfile.HitVfxPrefab, position, Quaternion.identity, AttackProfile.HitVfxLifetime); // 공용 생성
        }

        private static Transform FindDirectChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null; // 검색 불가
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i); // 직계 자식
                if (child.name == childName)
                {
                    return child; // 발견
                }
            }

            return null; // 없음
        }

        // 특정 하위 오브젝트를 포함하는 직계 자식 찾기
        private static Transform FindDirectChildContaining(Transform root, Transform descendant)
        {
            if (root == null || descendant == null)
            {
                return null; // 검색 불가
            }

            Transform current = descendant; // 시작점
            while (current != null && current.parent != null && current.parent != root)
            {
                current = current.parent; // 직계 자식까지 상승
            }

            return current != null && current.parent == root ? current : null; // 직계 자식이면 반환
        }

        private static Transform FindChildRecursive(Transform root, string childName) // 이름 검색
        {
            if (root == null)
            {
                return null; // 검색 불가
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i); // 하위
                if (child.name == childName)
                {
                    return child; // 발견
                }

                Transform found = FindChildRecursive(child, childName); // 재귀
                if (found != null)
                {
                    return found; // 발견
                }
            }

            return null; // 없음
        }
    }
}
