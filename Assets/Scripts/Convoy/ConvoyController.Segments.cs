using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class ConvoyController
    {
        private void EnsureHeadVisual() // 머리 보장
        {
            if (HeadVisual != null)
            {
                ApplyMaterial(HeadVisual, HeadMaterial); // 재질 보정
                return; // 기존 사용
            }

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube); // fallback 머리
            visual.name = "ConvoyHeadVisual";
            visual.transform.SetParent(transform, false); // 머리 자식
            visual.transform.localPosition = new Vector3(0f, VisualCenterHeight, 0f); // 표시 높이
            visual.transform.localScale = HeadScale; // 머리 크기
            DestroyUnityObject(visual.GetComponent<Collider>()); // 표시 전용
            HeadVisual = visual.transform; // 참조 저장
            ApplyMaterial(HeadVisual, HeadMaterial); // 재질 적용
        }

        private void EnsureHeadPhysicsCollider() // 머리 물리 보장
        {
            if (!EnableHeadPhysicsCollider || HeadVisual == null)
            {
                return; // 사용 안 함
            }

            BoxCollider collider = HeadVisual.GetComponent<BoxCollider>(); // 머리 콜라이더
            if (collider == null)
            {
                collider = HeadVisual.gameObject.AddComponent<BoxCollider>(); // 충돌체 추가
            }

            collider.enabled = true; // 충돌 사용
            collider.isTrigger = false; // 물리 충돌
            collider.center = Vector3.zero; // 중심 정렬
            collider.size = Vector3.one; // 스케일 기준

            Rigidbody rigidbody = HeadVisual.GetComponent<Rigidbody>(); // 머리 바디
            if (rigidbody == null)
            {
                rigidbody = HeadVisual.gameObject.AddComponent<Rigidbody>(); // 바디 추가
            }

            rigidbody.isKinematic = true; // 이동 스크립트 우선
            rigidbody.useGravity = false; // 중력 제외
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate; // 보간
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 관통 완화
        }

        private void EnsureHeadMonsterBlocker() // 머리 몬스터 차단 보장
        {
            if (HeadVisual == null)
            {
                return; // 대상 없음
            }

            SegmentBlocker blocker = HeadVisual.GetComponent<SegmentBlocker>(); // 머리 차단
            if (!EnableHeadMonsterBlocker)
            {
                if (blocker != null)
                {
                    blocker.enabled = false; // 차단 끔
                }

                return; // 사용 안 함
            }

            if (blocker == null)
            {
                blocker = HeadVisual.gameObject.AddComponent<SegmentBlocker>(); // 차단 추가
            }

            blocker.enabled = true; // 목록 등록
            blocker.Configure(HeadMonsterBlockRadius); // 머리 반경 적용
        }

        private void ConfigureGroundChecks() // 바닥 체크 연결
        {
            if (HeadGroundCheck == null)
            {
                Transform checkTransform = transform.Find("GroundCheck"); // 머리 체크 오브젝트
                HeadGroundCheck = checkTransform != null ? checkTransform.GetComponent<GroundCheck>() : null; // 컴포넌트 연결
            }

            ConfigureGroundCheck(HeadGroundCheck, 0f); // 머리 높이
        }

        private void ConfigureGroundCheck(GroundCheck groundCheck, float offset) // 체크값 설정
        {
            if (groundCheck == null)
            {
                return; // 체크 없음
            }

            groundCheck.GroundOffset = offset; // 바닥 위 높이
            GroundService service = GroundService.Active; // 월드 바닥
            if (service != null && groundCheck.GroundCollider == null)
            {
                groundCheck.GroundCollider = service.GroundCollider; // 씬 바닥 연결
            }
        }

        private void EnsureSegmentRoot() // 몸통 루트 보장
        {
            if (SegmentRoot != null)
            {
                return; // 기존 사용
            }

            GameObject root = new GameObject("ConvoySegments"); // fallback 루트
            SegmentRoot = root.transform; // 참조 저장
        }

        private void EnsureDetachedTailRoot() // 분리 루트 보장
        {
            if (DetachedTailRoot != null)
            {
                return; // 기존 사용
            }

            GameObject root = new GameObject("DetachedTails"); // fallback 루트
            Transform parent = FindWorldParent(); // 배치 기준
            root.transform.SetParent(parent); // 월드 계층
            DetachedTailRoot = root.transform; // 참조 저장
        }

        private void EnsureProjectileRoot() // 투사체 루트 보장
        {
            if (ProjectileRoot != null)
            {
                return; // 기존 사용
            }

            GameObject existing = GameObject.Find("Projectiles"); // 정식 루트 검색
            if (existing != null)
            {
                ProjectileRoot = existing.transform; // 기존 루트 사용
                return; // 완료
            }

            GameObject root = new GameObject("Projectiles"); // fallback 루트
            Transform parent = FindWorldParent(); // 월드 기준
            root.transform.SetParent(parent); // 월드 계층
            ProjectileRoot = root.transform; // 참조 저장
        }

        public Transform GetProjectileRoot() // 투사체 부모 제공
        {
            EnsureProjectileRoot(); // 루트 보장
            return ProjectileRoot; // 현재 루트
        }

        private Transform FindWorldParent() // 월드 부모
        {
            GameObject worldRoot = GameObject.Find("World"); // 월드 루트
            if (worldRoot != null)
            {
                return worldRoot.transform; // 정식 부모
            }

            return SegmentRoot != null ? SegmentRoot.parent : transform.parent; // 기본 부모
        }

        private void CollectExistingSegments() // 배치 몸통 수집
        {
            segments.Clear(); // 목록 초기화
            segmentGroundChecks.Clear(); // 바닥 체크 초기화
            segmentRuntimes.Clear(); // 런타임 초기화

            if (SegmentRoot == null)
            {
                return; // 루트 없음
            }

            for (int i = 0; i < SegmentRoot.childCount; i++)
            {
                Transform child = SegmentRoot.GetChild(i); // 후보 몸통
                if (child.name.StartsWith("ConvoySegment"))
                {
                    segments.Add(child); // 체인 등록
                    segmentGroundChecks.Add(GetSegmentGroundCheck(child)); // 바닥 체크 등록
                    segmentRuntimes.Add(GetSegmentRuntime(child, segments.Count - 1, true)); // 런타임 등록
                    ApplySegmentMaterial(child, i); // 교차 재질
                    HideLegacySegmentVisualIfModelExists(child); // 새 모델 사용 시 두부 렌더러 숨김
                    DisableAttachedSegmentPhysics(child); // 붙은 몸통 물리 끔
                    EnsureSegmentMonsterBlocker(child); // 몬스터 막기
                }
            }

            SyncSegmentRuntimes(true); // 런타임 보정
        }

        private Transform CreateSegment(int index, GameObject segmentPrefab) // 몸통 생성
        {
            if (segmentPrefab == null || SegmentRoot == null)
            {
                return null; // 프리팹 필요
            }

            GameObject segment = Instantiate(segmentPrefab); // 프리팹 생성
            segment.name = $"ConvoySegment_{index + 1:00}"; // 체인 이름
            segment.transform.SetParent(SegmentRoot, false); // 몸통 루트
            segment.transform.localScale = GetSafeSegmentScale(); // 몸통 크기
            DisableAttachedSegmentPhysics(segment.transform); // 붙은 몸통 상태
            ApplySegmentMaterial(segment.transform, index); // 교차 재질
            HideLegacySegmentVisualIfModelExists(segment.transform); // 새 모델 사용 시 두부 렌더러 숨김
            ConfigureGroundCheck(GetSegmentGroundCheck(segment.transform), VisualCenterHeight); // 바닥 체크
            ConvoySegmentRuntime runtime = GetSegmentRuntime(segment.transform, index, true); // 런타임 연결
            if (runtime != null)
            {
                runtime.SetSegmentLevel(InferSegmentLevel(segmentPrefab)); // 프리팹 레벨 기록
            }

            return segment.transform; // 생성 결과
        }

        public int LevelUpAttachedSegments(string segmentId, SegmentDefinition definition) // 테스트용 일괄 레벨업
        {
            return LevelUpAttachedSegments(segmentId, definition, out _); // 기존 호출 호환
        }

        public int LevelUpAttachedSegments(string segmentId, SegmentDefinition definition, out int appliedLevel) // 테스트용 일괄 레벨업
        {
            appliedLevel = GetCurrentSegmentLevel(segmentId, definition); // 현재 표시 레벨
            if (definition == null || string.IsNullOrWhiteSpace(segmentId))
            {
                return 0; // 대상 없음
            }

            RegisterSegmentDefinition(definition); // 이후 추가 세그먼트 레벨 추적
            SyncSegmentRuntimes(true); // 현재 목록 보정
            int changed = 0; // 교체 수
            string targetId = NormalizeSegmentId(segmentId); // 비교 ID
            int maxLevel = Mathf.Max(1, definition.MaxLevel); // 최대 레벨
            int currentLevel = Mathf.Clamp(GetCurrentSegmentLevelInternal(targetId, definition), 1, maxLevel); // 현재 모델 레벨
            int nextLevel = Mathf.Min(currentLevel + 1, maxLevel); // 다음 레벨
            if (nextLevel == currentLevel || !definition.TryGetLevel(nextLevel, out SegmentLevelDefinition levelData))
            {
                appliedLevel = currentLevel; // 최대 레벨
                return 0; // 더 이상 강화 없음
            }

            currentSegmentLevelsById[targetId] = nextLevel; // 신규 추가도 같은 레벨 사용
            appliedLevel = nextLevel; // UI 표시용
            for (int i = 0; i < segments.Count; i++)
            {
                ConvoySegmentRuntime runtime = i < segmentRuntimes.Count ? segmentRuntimes[i] : null; // 현재 런타임
                if (!IsSegmentId(runtime, targetId))
                {
                    continue; // 다른 세그먼트
                }

                if (segments[i] == starterSegment)
                {
                    if (TryResolveActiveStarterLevelPrefab(targetId, nextLevel, out GameObject starterLevelPrefab)
                        && ReplaceAttachedSegment(i, starterLevelPrefab, nextLevel))
                    {
                        changed++; // 스타터 레벨 프리팹 교체
                    }

                    continue; // 스타터 전용 프리팹 사용
                }

                if (ReplaceAttachedSegment(i, levelData.SegmentPrefab, nextLevel))
                {
                    changed++; // 성공
                }
            }

            if (changed > 0)
            {
                SyncSegmentRuntimes(true); // 교체 후 인덱스 보정
                SnapSegmentsToPath(); // 위치 정렬
                NotifySegmentCountChanged(); // HUD 갱신
            }

            return changed; // 교체 결과
        }

        public int GetCurrentSegmentLevel(string segmentId, SegmentDefinition definition = null) // 현재 세그먼트 모델 레벨
        {
            string targetId = NormalizeSegmentId(segmentId); // 비교 ID
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return 1; // 기본 레벨
            }

            if (definition != null)
            {
                RegisterSegmentDefinition(definition); // 정의 보관
            }

            return GetCurrentSegmentLevelInternal(targetId, definition); // 현재값
        }

        // 코어/레벨 UI가 해당 세그먼트가 실제로 붙어 있는지 확인하는 조회 API
        public int CountAttachedSegments(string segmentId)
        {
            string targetId = NormalizeSegmentId(segmentId); // 비교 ID 보정
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return 0; // 대상 없음
            }

            SyncSegmentRuntimes(true); // 현재 체인 런타임 보정
            int count = 0; // 일치 개수
            for (int i = 0; i < segmentRuntimes.Count; i++)
            {
                ConvoySegmentRuntime runtime = segmentRuntimes[i]; // 현재 런타임
                if (runtime != null && runtime.IsAttached && IsSegmentId(runtime, targetId))
                {
                    count++; // 해당 ID 세그먼트
                }
            }

            return count; // 결과 반환
        }

        // ===== 안건준 추가 0620 =====
        // CardUI 디버그 — 세그먼트 개수 조회용

        // 붙어 있는 세그먼트 전체 개수  — CardUI "전체 세그먼트 숫자" 출력용
        public int GetAttachedSegmentTotalCount()
        {
            SyncSegmentRuntimes(true); // 현재 체인 런타임 보정
            int total = 0; // 전체 개수
            for (int i = 0; i < segmentRuntimes.Count; i++)
            {
                ConvoySegmentRuntime runtime = segmentRuntimes[i]; // 현재 런타임
                if (runtime != null && runtime.IsAttached && runtime.Weapon != null)
                {
                    total++; // 세그먼트 집계
                }
            }

            return total; // 결과
        }

        // 세그먼트 ID별 개수 수집 
        public void CollectAttachedSegmentCounts(Dictionary<string, int> countsBySegmentId)
        {
            if (countsBySegmentId == null)
            {
                return; // null Dictionary 방지
            }

            countsBySegmentId.Clear(); // 이전 결과 제거
            SyncSegmentRuntimes(true); // 현재 체인 런타임 보정
            for (int i = 0; i < segmentRuntimes.Count; i++)
            {
                ConvoySegmentRuntime runtime = segmentRuntimes[i]; // 현재 런타임
                if (runtime == null || !runtime.IsAttached || runtime.Weapon == null)
                {
                    continue; // 분리됐거나 무기 없는 세그먼트 제외
                }

                string segmentId = runtime.Weapon.EffectiveSegmentId; 
                if (string.IsNullOrWhiteSpace(segmentId))
                {
                    continue; // ID 없으면 집계 불가
                }

                string normalizedId = segmentId.Trim(); // 비교·딕셔너리 키
                if (countsBySegmentId.TryGetValue(normalizedId, out int currentCount))
                {
                    countsBySegmentId[normalizedId] = currentCount + 1; // 같은 ID 누적
                }
                else
                {
                    countsBySegmentId[normalizedId] = 1; // 첫 등장
                }
            }
        }
        // ===== 안건준 추가 0620 끝 =====

        private int GetCurrentSegmentLevelInternal(string segmentId, SegmentDefinition definition) // 내부 레벨 조회
        {
            string targetId = NormalizeSegmentId(segmentId); // 비교 ID
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return 1; // 기본 레벨
            }

            if (definition == null && segmentDefinitionsById.TryGetValue(targetId, out SegmentDefinition registeredDefinition))
            {
                definition = registeredDefinition; // 등록 정의 사용
            }

            int maxLevel = definition != null ? Mathf.Max(1, definition.MaxLevel) : int.MaxValue; // 최대 레벨
            if (currentSegmentLevelsById.TryGetValue(targetId, out int storedLevel))
            {
                return Mathf.Clamp(storedLevel, 1, maxLevel); // 저장값 우선
            }

            int scannedLevel = 1; // 기본 레벨
            for (int i = 0; i < segmentRuntimes.Count; i++)
            {
                ConvoySegmentRuntime runtime = segmentRuntimes[i]; // 현재 런타임
                if (IsSegmentId(runtime, targetId))
                {
                    scannedLevel = Mathf.Max(scannedLevel, runtime.SegmentLevel); // 배치 상태 반영
                }
            }

            return Mathf.Clamp(scannedLevel, 1, maxLevel); // 최종 보정
        }

        private GameObject ResolveSegmentPrefabForCurrentLevel(GameObject segmentPrefab) // 현재 레벨 프리팹 변환
        {
            if (!TryGetSegmentIdFromPrefab(segmentPrefab, out string segmentId))
            {
                return segmentPrefab; // ID 없으면 원본
            }

            if (!segmentDefinitionsById.TryGetValue(segmentId, out SegmentDefinition definition))
            {
                return segmentPrefab; // 아직 레벨 정의 없음
            }

            int level = GetCurrentSegmentLevelInternal(segmentId, definition); // 현재 레벨
            return definition.TryGetSegmentPrefab(level, out GameObject leveledPrefab) ? leveledPrefab : segmentPrefab; // 레벨 프리팹
        }

        private void RegisterSegmentDefinition(SegmentDefinition definition) // 세그먼트 정의 보관
        {
            if (definition == null || !definition.HasId)
            {
                return; // 등록 불가
            }

            string segmentId = definition.NormalizedId; // 자기 ID
            segmentDefinitionsById[segmentId] = definition; // 추가/교체용

            string upgradeId = definition.UpgradeId; // 공유 강화 ID
            if (!string.IsNullOrWhiteSpace(upgradeId))
            {
                segmentDefinitionsById[upgradeId] = definition; // 스타터 공유 강화 대비
            }
        }

        private static string NormalizeSegmentId(string segmentId) // ID 보정
        {
            return string.IsNullOrWhiteSpace(segmentId) ? string.Empty : segmentId.Trim(); // 공백 제거
        }

        private bool ReplaceAttachedSegment(int index, GameObject prefab, int level) // 붙은 세그먼트 교체
        {
            if (prefab == null || index < 0 || index >= segments.Count || SegmentRoot == null)
            {
                return false; // 교체 불가
            }

            Transform oldSegment = segments[index]; // 기존 세그먼트
            if (oldSegment == null)
            {
                return false; // 대상 없음
            }

            ConvoySegmentRuntime oldRuntime = index < segmentRuntimes.Count ? segmentRuntimes[index] : oldSegment.GetComponent<ConvoySegmentRuntime>(); // DPS 키 계승 대상
            bool wasStarter = starterSegment == oldSegment; // 스타터 여부
            string oldName = oldSegment.name; // 이름 유지
            int oldSiblingIndex = oldSegment.GetSiblingIndex(); // 순서 유지
            Vector3 position = oldSegment.position; // 위치 유지
            Quaternion rotation = oldSegment.rotation; // 회전 유지
            Vector3 scale = oldSegment.localScale; // 런타임 크기 유지

            GameObject instance = Instantiate(prefab); // 새 레벨
            Transform newSegment = instance.transform; // 새 루트
            newSegment.name = oldName; // 기존 이름 유지
            newSegment.SetParent(SegmentRoot, true); // 월드 pose 유지
            newSegment.SetPositionAndRotation(position, rotation); // 위치/회전 복구
            newSegment.localScale = scale; // 크기 복구
            newSegment.SetSiblingIndex(Mathf.Clamp(oldSiblingIndex, 0, SegmentRoot.childCount - 1)); // 계층 순서

            DisableAttachedSegmentPhysics(newSegment); // 붙은 몸통 상태
            ApplySegmentMaterial(newSegment, index); // 교차 재질
            HideLegacySegmentVisualIfModelExists(newSegment); // 레거시 표시 정리
            GroundCheck groundCheck = GetSegmentGroundCheck(newSegment); // 바닥 체크
            ConvoySegmentRuntime runtime = GetSegmentRuntime(newSegment, index, true); // 런타임
            if (runtime != null)
            {
                runtime.AdoptDamageMeterKeyFrom(oldRuntime); // DPS 누적값 유지
                runtime.SetSegmentLevel(level); // 새 레벨 저장
            }

            segments[index] = newSegment; // 체인 교체
            if (index < segmentGroundChecks.Count)
            {
                segmentGroundChecks[index] = groundCheck; // 체크 교체
            }

            if (index < segmentRuntimes.Count)
            {
                segmentRuntimes[index] = runtime; // 런타임 교체
            }

            if (wasStarter)
            {
                starterSegment = newSegment; // 스타터 추적 유지
            }

            DestroyUnityObject(oldSegment.gameObject); // 기존 제거
            return true; // 완료
        }

        private static Transform FindChildRecursive(Transform root, string childName) // 하위 이름 검색
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null; // 검색 불가
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i); // 후보
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

        private static bool IsSegmentId(ConvoySegmentRuntime runtime, string segmentId) // ID 비교
        {
            if (runtime == null || runtime.Weapon == null || string.IsNullOrWhiteSpace(segmentId))
            {
                return false; // 비교 불가
            }

            return string.Equals(runtime.Weapon.EffectiveSegmentId, segmentId, System.StringComparison.OrdinalIgnoreCase); // ID 일치
        }

        private static int InferSegmentLevel(GameObject segmentPrefab) // 프리팹 이름에서 Lv 추정
        {
            string name = segmentPrefab != null ? segmentPrefab.name : string.Empty; // 이름
            int marker = name.LastIndexOf("_Lv", System.StringComparison.OrdinalIgnoreCase); // 레벨 표기
            if (marker >= 0 && int.TryParse(name.Substring(marker + 3), out int level))
            {
                return Mathf.Max(1, level); // 추정 성공
            }

            return 1; // 기본 Lv1
        }

        private void UpdateSegmentWeapons(float deltaTime) // 자동 사격 갱신
        {
            if (!EnableSegmentAutoFire)
            {
                return; // 사용 안 함
            }

            SyncSegmentRuntimes(true); // 런타임 보정

            for (int i = 0; i < segments.Count; i++)
            {
                ConvoySegmentRuntime runtime = segmentRuntimes[i]; // 세그먼트 런타임
                if (runtime != null)
                {
                    runtime.Tick(deltaTime); // 세그먼트 무기
                }
            }
        }

        private void SyncSegmentGroundChecks() // 바닥 체크 길이 보정
        {
            while (segmentGroundChecks.Count < segments.Count)
            {
                segmentGroundChecks.Add(GetSegmentGroundCheck(segments[segmentGroundChecks.Count])); // 부족분 추가
            }

            while (segmentGroundChecks.Count > segments.Count)
            {
                segmentGroundChecks.RemoveAt(segmentGroundChecks.Count - 1); // 초과분 제거
            }

            for (int i = 0; i < segments.Count; i++)
            {
                GroundCheck groundCheck = segmentGroundChecks[i]; // 현재 체크
                Transform segment = segments[i]; // 현재 몸통
                if (groundCheck == null || segment == null || !groundCheck.transform.IsChildOf(segment))
                {
                    segmentGroundChecks[i] = GetSegmentGroundCheck(segment); // 참조 복구
                }
            }
        }

        private void RemoveSegmentGroundCheck(int index) // 단일 체크 제거
        {
            if (index < 0 || index >= segmentGroundChecks.Count)
            {
                return; // 범위 밖
            }

            segmentGroundChecks.RemoveAt(index); // 체크 제거
        }

        private void RemoveSegmentGroundChecks(int index, int count) // 절단 체크 제거
        {
            if (count <= 0 || index < 0 || index >= segmentGroundChecks.Count)
            {
                return; // 제거 없음
            }

            int safeCount = Mathf.Min(count, segmentGroundChecks.Count - index); // 범위 보정
            segmentGroundChecks.RemoveRange(index, safeCount); // 체크 절단
        }

        private void SyncSegmentRuntimes(bool attached) // 런타임 길이 보정
        {
            while (segmentRuntimes.Count < segments.Count)
            {
                int index = segmentRuntimes.Count; // 추가 순번
                segmentRuntimes.Add(GetSegmentRuntime(segments[index], index, attached)); // 부족분 추가
            }

            while (segmentRuntimes.Count > segments.Count)
            {
                segmentRuntimes.RemoveAt(segmentRuntimes.Count - 1); // 초과분 제거
            }

            for (int i = 0; i < segments.Count; i++)
            {
                ConvoySegmentRuntime runtime = segmentRuntimes[i]; // 현재 런타임
                Transform segment = segments[i]; // 현재 몸통
                if (runtime == null || segment == null || runtime.transform != segment)
                {
                    runtime = GetSegmentRuntime(segment, i, attached); // 참조 복구
                    segmentRuntimes[i] = runtime; // 목록 갱신
                }

                if (runtime != null)
                {
                    runtime.Configure(this, i, attached); // 순번 보정
                }
            }
        }

        private void RemoveSegmentRuntime(int index) // 단일 런타임 제거
        {
            if (index < 0 || index >= segmentRuntimes.Count)
            {
                return; // 범위 밖
            }

            ConvoySegmentRuntime runtime = segmentRuntimes[index]; // 제거 대상
            if (runtime != null)
            {
                runtime.SetAttached(false); // 무기 정지
            }

            segmentRuntimes.RemoveAt(index); // 목록 제거
        }

        private void RemoveSegmentRuntimes(int index, int count) // 절단 런타임 제거
        {
            if (count <= 0 || index < 0 || index >= segmentRuntimes.Count)
            {
                return; // 제거 없음
            }

            int safeCount = Mathf.Min(count, segmentRuntimes.Count - index); // 범위 보정
            for (int i = index; i < index + safeCount; i++)
            {
                ConvoySegmentRuntime runtime = segmentRuntimes[i]; // 분리 대상
                if (runtime != null)
                {
                    runtime.SetAttached(false); // 무기 정지
                }
            }

            segmentRuntimes.RemoveRange(index, safeCount); // 런타임 절단
        }

        private ConvoySegmentRuntime GetSegmentRuntime(Transform segment, int index, bool attached) // 런타임 찾기
        {
            if (segment == null)
            {
                return null; // 대상 없음
            }

            ConvoySegmentRuntime runtime = segment.GetComponent<ConvoySegmentRuntime>(); // 루트 런타임
            if (runtime != null)
            {
                runtime.Configure(this, index, attached); // 체인 연결
            }

            return runtime; // 결과 반환
        }

        private GroundCheck GetSegmentGroundCheck(Transform segment) // 몸통 체크 찾기
        {
            if (segment == null)
            {
                return null; // 대상 없음
            }

            GroundCheck groundCheck = segment.GetComponentInChildren<GroundCheck>(true); // 자식 체크
            ConfigureGroundCheck(groundCheck, VisualCenterHeight); // 값 보정
            return groundCheck; // 결과 반환
        }

        private Vector3 SnapHeadToGround(Vector3 position) // 머리 바닥 보정
        {
            if (HeadGroundCheck != null)
            {
                return HeadGroundCheck.Snap(position, 0f); // 체크 기준
            }

            position.y = 0f; // 평면 fallback
            return position; // 결과 반환
        }

        private Vector3 SnapSegmentToGround(int index, Vector3 position) // 몸통 바닥 보정
        {
            GroundCheck groundCheck = index >= 0 && index < segmentGroundChecks.Count ? segmentGroundChecks[index] : null; // 체크 참조
            if (groundCheck != null)
            {
                return groundCheck.Snap(position, VisualCenterHeight); // 체크 기준
            }

            position.y = VisualCenterHeight; // 평면 fallback
            return position; // 결과 반환
        }

        private void DisableAttachedSegmentPhysics(Transform segment) // 붙은 몸통 물리 해제
        {
            if (segment == null)
            {
                return; // 대상 없음
            }

            ClearDetachedSegmentJoints(segment); // 링크 제거
            DestroyUnityObject(segment.GetComponent<Rigidbody>()); // 바디 제거
            DestroyUnityObject(segment.GetComponent<Collider>()); // 콜라이더 제거
            EnsureSegmentMonsterBlocker(segment); // 몬스터 막기
        }

        private void HideLegacySegmentVisualIfModelExists(Transform segment) // 기존 두부 표시 숨김
        {
            if (segment == null || segment.Find("Visual") == null)
            {
                return; // 새 모델 없음
            }

            MeshRenderer legacyRenderer = segment.GetComponent<MeshRenderer>(); // 루트 두부 렌더러
            if (legacyRenderer != null)
            {
                legacyRenderer.enabled = false; // 표시만 숨김
            }
        }

        private void EnsureSegmentMonsterBlocker(Transform segment) // 몬스터 차단 보장
        {
            if (segment == null)
            {
                return; // 대상 없음
            }

            SegmentBlocker blocker = segment.GetComponent<SegmentBlocker>(); // 차단 컴포넌트
            if (blocker == null)
            {
                blocker = segment.gameObject.AddComponent<SegmentBlocker>(); // 차단 추가
            }

            blocker.Configure(TailCollisionRadius); // 반경 적용
        }

        private void ApplySegmentMaterial(Transform segment, int index) // 몸통 재질
        {
            Material material = index % 2 == 0 ? SegmentMaterial : SegmentAltMaterial; // 교차 선택
            ApplyMaterial(segment, material != null ? material : SegmentMaterial); // fallback 포함
        }

        private void ApplyMaterial(Transform target, Material material) // 재질 적용
        {
            if (target == null || material == null)
            {
                return; // 대상 없음
            }

            Renderer renderer = target.GetComponent<Renderer>(); // 표시 renderer
            if (renderer != null)
            {
                renderer.sharedMaterial = material; // 공유 재질
            }
        }
    }
}
