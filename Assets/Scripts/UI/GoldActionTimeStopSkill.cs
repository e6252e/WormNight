using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeamProject01.Gameplay
{
    public sealed class GoldActionTimeStopSkill : MonoBehaviour // 3번 액션 HUD 타임스탑
    {
        private const float MinimumRadius = 32f; // 최소 정지 반경
        private const float MinimumDuration = 0.5f; // 최소 지속 시간
        private const float MinimumRefreshInterval = 0.05f; // 최소 갱신 간격

        [Header("Scene References")]
        public NexusController Nexus; // 중심 넥서스
        public QuarterViewCamera ShakeCamera; // 발동 카메라 흔들림

        [Header("Area")]
        [Min(1f)] public float Radius = 180f; // 전장 정지 반경

        [Header("Time Stop")]
        [Min(0.1f)] public float BaseDuration = 4f; // Lv1 지속 시간
        [Min(0f)] public float DurationPerUpgrade = 1f; // 강화당 추가 시간
        [Min(0.01f)] public float RefreshInterval = 0.1f; // 신규 스폰 포함 갱신 간격
        [Min(0.01f)] public float FreezeTickDuration = 0.25f; // 한 번 적용하는 동결 시간
        public bool AffectBosses = true; // 보스도 정지
        [Range(0.1f, 1f)] public float BossFreezeMultiplier = 0.4f; // 보스 동결 잔여 감쇠

        [Header("Camera Shake")]
        [Min(0f)] public float ShakeDuration = 0.22f; // 발동 흔들림 시간
        [Min(0f)] public float ShakeAmplitude = 0.35f; // 발동 흔들림 세기
        [Min(1f)] public float ShakeFrequency = 12f; // 발동 흔들림 속도

        [Header("Clock VFX")]
        public bool ShowClockVfx = true; // 넥서스 상단 시계 연출
        [Min(0f)] public float ClockHeight = 8f; // 넥서스 위 높이
        [Min(0.1f)] public float ClockRadius = 3.4f; // 시계 반경
        [Min(0.001f)] public float ClockLineWidth = 0.055f; // 선 두께
        [Min(0f)] public float ClockSpinSpeed = 18f; // 링 회전 속도
        [Range(0f, 0.5f)] public float ClockPulseAmplitude = 0.08f; // 맥동 세기
        [Min(0.1f)] public float ClockPulseFrequency = 2.2f; // 맥동 속도
        public Color ClockPrimaryColor = new Color(0.32f, 0.88f, 1f, 0.88f); // 메인 청록
        public Color ClockSecondaryColor = new Color(0.9f, 1f, 1f, 0.5f); // 보조 흰빛
        public Color ClockHandColor = new Color(1f, 0.94f, 0.45f, 0.95f); // 시계바늘 금빛

        private readonly List<EnemyController> targets = new List<EnemyController>(192); // 재사용 대상 목록
        private Coroutine activeRoutine; // 진행 중 타임스탑
        private Coroutine clockRoutine; // 시계 VFX 진행
        private GameObject clockVfxRoot; // 시계 VFX 루트
        private Transform clockFaceRoot; // 카메라 빌보드 루트
        private Transform clockDialRoot; // 회전 링 루트
        private Transform clockHandsRoot; // 바늘 루트
        private LineRenderer hourHand; // 시침
        private LineRenderer minuteHand; // 분침
        private LineRenderer secondHand; // 초침
        private Material clockLineMaterial; // 절차형 선 재질
        private GameplaySfxEmitter.LoopHandle timeStopLoopA;
        private GameplaySfxEmitter.LoopHandle timeStopLoopB;

        public bool Play(int upgradeLevel) // 액션 HUD 3번 발동
        {
            EnsureReferences(); // 런타임 참조 보장

            Vector3 center = ResolveCenter(); // 중심 위치
            int level = Mathf.Max(1, upgradeLevel); // 강화 레벨
            float duration = GetDuration(level); // 실제 지속 시간
            float radius = Mathf.Max(Radius, MinimumRadius); // 실제 반경

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine); // 기존 정지 갱신 중단
                StopTimeStopSfx();
            }

            ShakeCamera?.AddShake(ShakeDuration, ShakeAmplitude, ShakeFrequency); // 발동 흔들림
            StartClockVfx(center, duration); // 넥서스 상단 시계 연출
            StartTimeStopSfx(center);
            activeRoutine = StartCoroutine(TimeStopRoutine(center, radius, duration)); // 정지 루프 시작
            Debug.Log($"[GoldActionTimeStop] Time stop cast: Lv{level}, duration {duration:0.0}s, radius {radius:0.0}", this);
            return true; // 몬스터가 없어도 발동 성공
        }

        private IEnumerator TimeStopRoutine(Vector3 center, float radius, float duration) // 타임스탑 루프
        {
            float endsAt = Time.time + duration; // 종료 시각
            int affected = 0; // 누적 적용 수
            float refreshInterval = Mathf.Max(RefreshInterval, MinimumRefreshInterval); // 갱신 간격

            while (Time.time < endsAt)
            {
                affected += FreezeEnemies(center, radius, refreshInterval); // 현재 범위 몬스터 동결
                yield return new WaitForSeconds(refreshInterval); // 신규 스폰 반영 간격
            }

            activeRoutine = null; // 완료
            StopClockVfx(); // 시계 연출 종료
            StopTimeStopSfx();
            Debug.Log($"[GoldActionTimeStop] Time stop ended: affected ticks {affected}", this);
        }

        private int FreezeEnemies(Vector3 center, float radius, float refreshInterval) // 범위 몬스터 동결
        {
            EnemyController.CollectActiveInRange(center, radius, targets); // 활성 몬스터 수집

            int affected = 0; // 적용 수
            float freezeDuration = Mathf.Max(FreezeTickDuration, refreshInterval + 0.02f); // 끊김 방지 동결 시간
            for (int i = 0; i < targets.Count; i++)
            {
                EnemyController enemy = targets[i]; // 대상
                if (enemy == null || enemy.IsDead)
                {
                    continue; // 무효 대상
                }

                if (enemy.Grade == EnemyGrade.Boss && !AffectBosses)
                {
                    continue; // 보스 제외
                }

                float resolvedDuration = enemy.Grade == EnemyGrade.Boss
                    ? Mathf.Max(refreshInterval + 0.02f, freezeDuration * BossFreezeMultiplier)
                    : freezeDuration; // 보스 잔여 감쇠
                EnemySupportDebuffState state = EnemySupportDebuffState.GetOrAdd(enemy); // 동결 상태
                if (state == null)
                {
                    continue; // 상태 없음
                }

                state.ApplyFreeze(resolvedDuration); // 이동/공격 정지
                affected++; // 적용 성공
            }

            targets.Clear(); // 다음 갱신 준비
            return affected; // 적용 수
        }

        private void EnsureReferences() // 참조 자동 보강
        {
            if (Nexus == null)
            {
                Nexus = NexusController.Active; // 활성 넥서스
            }

            if (Nexus == null)
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core"); // 이름 fallback
                Nexus = nexusObject != null ? nexusObject.GetComponent<NexusController>() : null; // 컴포넌트
            }

            if (ShakeCamera == null)
            {
                Camera mainCamera = Camera.main; // 메인 카메라
                ShakeCamera = mainCamera != null ? mainCamera.GetComponent<QuarterViewCamera>() : null; // 카메라 컴포넌트
            }

            if (ShakeCamera == null)
            {
                ShakeCamera = FindFirstObjectByType<QuarterViewCamera>(); // fallback
            }
        }

        private Vector3 ResolveCenter() // 정지 중심
        {
            if (Nexus != null)
            {
                return GroundService.ProjectToGround(Nexus.transform.position, 0f); // 넥서스 기준
            }

            return GroundService.ProjectToGround(Vector3.zero, 0f); // 최후 fallback
        }

        private void StartClockVfx(Vector3 center, float duration) // 시계 VFX 시작
        {
            if (!ShowClockVfx)
            {
                return; // 연출 비활성
            }

            StopClockVfx(); // 기존 연출 제거
            BuildClockVfx(center); // 절차형 시계 생성
            if (clockVfxRoot != null)
            {
                clockRoutine = StartCoroutine(ClockVfxRoutine(center, duration)); // 애니메이션 시작
            }
        }

        private void BuildClockVfx(Vector3 center) // 절차형 시계 구성
        {
            clockVfxRoot = new GameObject("GoldActionTimeStop_ClockVfx"); // 루트
            clockVfxRoot.transform.SetParent(transform, false); // HUD 스킬 하위
            clockVfxRoot.transform.position = ResolveClockPosition(center); // 넥서스 상단

            clockFaceRoot = new GameObject("ClockFace_Billboard").transform; // 카메라 방향 루트
            clockFaceRoot.SetParent(clockVfxRoot.transform, false);

            clockDialRoot = new GameObject("ClockDial_Rotating").transform; // 링/눈금
            clockDialRoot.SetParent(clockFaceRoot, false);

            clockHandsRoot = new GameObject("ClockHands").transform; // 바늘
            clockHandsRoot.SetParent(clockFaceRoot, false);

            float radius = Mathf.Max(0.1f, ClockRadius); // 반경 보정
            float width = Mathf.Max(0.001f, ClockLineWidth); // 두께 보정
            CreateCircle(clockDialRoot, "OuterRing", radius, 128, width * 1.2f, ClockPrimaryColor); // 외곽 링
            CreateCircle(clockDialRoot, "InnerRing", radius * 0.78f, 128, width * 0.55f, ClockSecondaryColor); // 내부 링
            CreateCircle(clockDialRoot, "CoreRing", radius * 0.12f, 48, width * 0.8f, ClockHandColor); // 중심 링
            CreateMinuteTicks(clockDialRoot, radius, width); // 60분 눈금
            CreateHourTicks(clockDialRoot, radius, width); // 12시 눈금
            hourHand = CreateHand(clockHandsRoot, "HourHand", width * 1.9f, ClockHandColor); // 시침
            minuteHand = CreateHand(clockHandsRoot, "MinuteHand", width * 1.55f, ClockSecondaryColor); // 분침
            secondHand = CreateHand(clockHandsRoot, "SecondHand", width * 0.9f, ClockPrimaryColor); // 초침
        }

        private IEnumerator ClockVfxRoutine(Vector3 center, float duration) // 시계 VFX 애니메이션
        {
            float startedAt = Time.time; // 시작 시각
            float safeDuration = Mathf.Max(0.01f, duration); // 지속 시간
            while (clockVfxRoot != null && Time.time < startedAt + safeDuration)
            {
                float elapsed = Time.time - startedAt; // 진행 시간
                float progress = Mathf.Clamp01(elapsed / safeDuration); // 진행률
                UpdateClockPose(center, elapsed, progress); // 위치/회전/바늘 갱신
                yield return null; // 매 프레임
            }

            clockRoutine = null; // 완료
            DestroyClockVfxRoot(); // 정리
        }

        private void UpdateClockPose(Vector3 center, float elapsed, float progress) // 시계 위치와 바늘 갱신
        {
            if (clockVfxRoot == null || clockFaceRoot == null)
            {
                return; // 대상 없음
            }

            clockVfxRoot.transform.position = ResolveClockPosition(center); // 넥서스 상단 유지
            Camera camera = Camera.main; // 메인 카메라
            if (camera != null)
            {
                Vector3 cameraDirection = camera.transform.position - clockFaceRoot.position; // 카메라 방향
                if (cameraDirection.sqrMagnitude > 0.0001f)
                {
                    clockFaceRoot.rotation = Quaternion.LookRotation(cameraDirection.normalized, camera.transform.up); // 화면을 향하게
                }
            }

            float pulse = 1f + Mathf.Sin(elapsed * ClockPulseFrequency * Mathf.PI * 2f) * ClockPulseAmplitude; // 은은한 맥동
            clockFaceRoot.localScale = Vector3.one * pulse; // 크기 적용
            if (clockDialRoot != null)
            {
                clockDialRoot.localRotation = Quaternion.Euler(0f, 0f, elapsed * ClockSpinSpeed); // 링 회전
            }

            UpdateHand(hourHand, -30f - progress * 120f, ClockRadius * 0.45f); // 시침
            UpdateHand(minuteHand, 60f - progress * 360f, ClockRadius * 0.68f); // 분침
            UpdateHand(secondHand, -progress * 1080f, ClockRadius * 0.86f); // 초침
        }

        private Vector3 ResolveClockPosition(Vector3 center) // 시계 위치
        {
            Vector3 position = GroundService.ProjectToGround(center, 0f); // 지형 기준
            position.y += ClockHeight; // 넥서스 위
            return position; // 최종 위치
        }

        private void StopClockVfx() // 시계 VFX 제거
        {
            if (clockRoutine != null)
            {
                StopCoroutine(clockRoutine); // 애니메이션 중단
                clockRoutine = null; // 참조 제거
            }

            DestroyClockVfxRoot(); // 루트 제거
        }

        private void DestroyClockVfxRoot() // 시계 루트 제거
        {
            if (clockVfxRoot != null)
            {
                Destroy(clockVfxRoot); // 런타임 오브젝트 제거
            }

            clockVfxRoot = null; // 루트 제거
            clockFaceRoot = null; // 참조 제거
            clockDialRoot = null; // 참조 제거
            clockHandsRoot = null; // 참조 제거
            hourHand = null; // 참조 제거
            minuteHand = null; // 참조 제거
            secondHand = null; // 참조 제거
        }

        private void StartTimeStopSfx(Vector3 center)
        {
            StopTimeStopSfx();
            timeStopLoopA = GameplaySfxEmitter.StartCatalogLoop(GameplaySfxCue.HudSkill3LoopA, center, transform);
            timeStopLoopB = GameplaySfxEmitter.StartCatalogLoop(GameplaySfxCue.HudSkill3LoopB, center, transform);
        }

        private void StopTimeStopSfx()
        {
            if (timeStopLoopA != null)
            {
                timeStopLoopA.Stop();
                timeStopLoopA = null;
            }

            if (timeStopLoopB != null)
            {
                timeStopLoopB.Stop();
                timeStopLoopB = null;
            }
        }

        private void CreateMinuteTicks(Transform parent, float radius, float width) // 60분 눈금
        {
            for (int i = 0; i < 60; i++)
            {
                if (i % 5 == 0)
                {
                    continue; // 큰 눈금은 별도 생성
                }

                float angle = i * 6f * Mathf.Deg2Rad; // 분 각도
                Vector3 inner = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (radius * 0.9f); // 안쪽
                Vector3 outer = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (radius * 0.98f); // 바깥쪽
                CreateLine(parent, $"MinuteTick_{i:00}", width * 0.35f, ClockSecondaryColor, inner, outer); // 얇은 눈금
            }
        }

        private void CreateHourTicks(Transform parent, float radius, float width) // 12시 큰 눈금
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f * Mathf.Deg2Rad; // 시 각도
                Vector3 inner = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (radius * 0.78f); // 안쪽
                Vector3 outer = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (radius * 1.02f); // 바깥쪽
                Color color = i % 3 == 0 ? ClockHandColor : ClockPrimaryColor; // 12/3/6/9 강조
                CreateLine(parent, $"HourTick_{i:00}", width * 1.25f, color, inner, outer); // 굵은 눈금
            }
        }

        private void CreateCircle(Transform parent, string name, float radius, int segments, float width, Color color) // 원형 선
        {
            int count = Mathf.Max(12, segments); // 세그먼트 보정
            Vector3[] points = new Vector3[count + 1]; // 닫힌 원
            for (int i = 0; i <= count; i++)
            {
                float angle = (Mathf.PI * 2f) * i / count; // 각도
                points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f); // 점
            }

            CreateLine(parent, name, width, color, points); // 선 생성
        }

        private LineRenderer CreateHand(Transform parent, string name, float width, Color color) // 바늘 생성
        {
            return CreateLine(parent, name, width, color, Vector3.zero, Vector3.up); // 초기 바늘
        }

        private void UpdateHand(LineRenderer hand, float angleDegrees, float length) // 바늘 방향 갱신
        {
            if (hand == null)
            {
                return; // 대상 없음
            }

            float angle = angleDegrees * Mathf.Deg2Rad; // 라디안
            Vector3 tip = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * length; // 끝점
            hand.SetPosition(0, -tip.normalized * (ClockRadius * 0.08f)); // 중심 뒤쪽 꼬리
            hand.SetPosition(1, tip); // 끝점
        }

        private LineRenderer CreateLine(Transform parent, string name, float width, Color color, params Vector3[] points) // 선 생성
        {
            GameObject lineObject = new GameObject(name); // 선 오브젝트
            lineObject.transform.SetParent(parent, false); // 부모 연결
            LineRenderer line = lineObject.AddComponent<LineRenderer>(); // 렌더러
            line.useWorldSpace = false; // 로컬 좌표
            line.positionCount = points.Length; // 점 개수
            line.SetPositions(points); // 점 적용
            line.widthMultiplier = Mathf.Max(0.001f, width); // 두께
            line.numCapVertices = 8; // 둥근 끝
            line.numCornerVertices = 8; // 둥근 코너
            line.alignment = LineAlignment.TransformZ; // 시계 평면 기준
            line.textureMode = LineTextureMode.Stretch; // 텍스처 늘림
            line.shadowCastingMode = ShadowCastingMode.Off; // 그림자 없음
            line.receiveShadows = false; // 그림자 수신 없음
            line.material = GetClockLineMaterial(); // 공유 재질
            line.startColor = color; // 시작색
            line.endColor = color; // 끝색
            return line; // 생성 결과
        }

        private Material GetClockLineMaterial() // 선 재질
        {
            if (clockLineMaterial != null)
            {
                return clockLineMaterial; // 재사용
            }

            Shader shader = Shader.Find("Sprites/Default"); // 투명 지원
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit"); // URP fallback
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color"); // 기본 fallback
            }

            if (shader == null)
            {
                shader = Shader.Find("Hidden/Internal-Colored"); // 최후 fallback
            }

            clockLineMaterial = new Material(shader)
            {
                name = "Runtime_TimeStopClock_Line"
            }; // 런타임 재질
            return clockLineMaterial; // 재질 반환
        }

        private float GetDuration(int upgradeLevel) // 강화 반영 지속 시간
        {
            int level = Mathf.Max(1, upgradeLevel); // 레벨 보정
            return Mathf.Max(MinimumDuration, BaseDuration + (level - 1) * DurationPerUpgrade); // 지속 시간
        }

        private void OnDisable() // 비활성 정리
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine); // 정지 루프 중단
                activeRoutine = null; // 참조 제거
            }

            StopClockVfx(); // 시계 제거
            StopTimeStopSfx();
        }

        private void OnDestroy() // 런타임 재질 정리
        {
            StopClockVfx(); // 남은 시계 제거
            StopTimeStopSfx();
            if (clockLineMaterial != null)
            {
                Destroy(clockLineMaterial); // 런타임 재질 제거
                clockLineMaterial = null; // 참조 제거
            }
        }

#if UNITY_EDITOR
        private void OnValidate() // 에디터 저장값 보정
        {
            Radius = Mathf.Max(Radius, MinimumRadius);
            BaseDuration = Mathf.Max(BaseDuration, MinimumDuration);
            RefreshInterval = Mathf.Max(RefreshInterval, MinimumRefreshInterval);
            FreezeTickDuration = Mathf.Max(FreezeTickDuration, RefreshInterval + 0.02f);
            BossFreezeMultiplier = Mathf.Clamp(BossFreezeMultiplier, 0.1f, 1f);
            ShakeFrequency = Mathf.Max(1f, ShakeFrequency);
            ClockRadius = Mathf.Max(0.1f, ClockRadius);
            ClockLineWidth = Mathf.Max(0.001f, ClockLineWidth);
            ClockPulseFrequency = Mathf.Max(0.1f, ClockPulseFrequency);
        }
#endif
    }
}
