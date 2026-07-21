using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyPortalTotemCaster : MonoBehaviour //토템 소환 몬스터 클래스
    {
        private Transform nexus; // 출구 토템 위치를 계산할 Nexus

        [SerializeField] private EnemyPortalTotem entryTotemPrefab;// 몬스터들이 모여드는 입구 토템 Prefab

        [SerializeField] private EnemyPortalTotem exitTotemPrefab;// 몬스터들이 순간이동되어 나오는 출구 토템 Prefab

        [Header("Gather Setting")]
        [Min(1.0f)]
        [SerializeField] private float gatherDuration = 10.0f;// 토템 생성 후 실제 순간이동까지 몬스터를 모으는 시간

        [Min(1.0f)]
        [SerializeField] private float attractRadius = 12.0f;// 몬스터가 Nexus 대신 입구 토템으로 이동하기 시작하는 범위

        [Min(0.1f)]
        [SerializeField] private float entryRadius = 6.0f;// 시간이 끝났을 때 실제로 순간이동되는 몬스터 판정 범위

        [Header("Exit Setting")]
        [Min(5.0f)]
        [SerializeField] private float exitDistanceFromNexus = 5.0f;// Nexus 중심에서 출구 토템을 떨어뜨릴 거리

        [Min(0.1f)]
        [SerializeField] private float exitScatterRadius = 2.5f;// 출구 주변에 순간이동 몬스터를 흩어놓는 기본 반경

        [Min(0.0f)]
        [SerializeField] private float totemGroundHeight = 0.03f;// 입구와 출구 토템을 바닥 위에 표시할 높이

        [Min(0.0f)]
        [SerializeField] private float teleportGroundHeight = 0.72f;// 순간이동된 몬스터를 바닥 위에 배치할 높이

        private readonly List<EnemyController> gatheredEnemies = new List<EnemyController>(32);// 입구 토템 안에 모인 순간이동 대상 목록

        private EnemyPortalTotem entryTotem;// 현재 생성된 입구 토템

        private EnemyPortalTotem exitTotem;// 현재 생성된 출구 토템

        private Coroutine portalRoutine;// 현재 진행 중인 집결 및 순간이동 Coroutine

        public bool IsChanneling { get; private set; }// 현재 몬스터들을 모으는 중인지 외부에서 확인하는 Property
        public event System.Action<Vector3> Teleported;

        private void Awake()
        {
            GameObject nexusObject = GameObject.Find("Nexus_Core");// 씬에서 Nexus_Core를 찾는다.

            nexus = nexusObject != null ? nexusObject.transform : null;// 찾았다면 Nexus Transform을 저장한다.
        }

        private void Start()
        {
            BeginPortalSequence();// 몬스터가 등장하면 즉시 토템 설치 과정을 시작한다.
        }

        private void OnDisable()
        {
            CancelPortalSequence();// Caster가 죽거나 비활성화되면 순간이동을 취소하고 토템을 제거한다.
        }

        private void BeginPortalSequence()
        {
            if (portalRoutine != null)
            {
                return;// 이미 토템 과정이 진행 중이면 중복 실행하지 않는다.
            }

            if (nexus == null)
            {
                return;// 출구 위치를 계산할 Nexus가 없으면 실행하지 않는다.
            }

            if (entryTotemPrefab == null || exitTotemPrefab == null)
            {
                return;// 필요한 토템 Prefab이 연결되지 않았다면 실행하지 않는다.
            }

            CreateTotems();// 입구와 출구 토템을 생성한다.

            if (entryTotem == null || exitTotem == null)
            {
                DestroyTotems();// 생성에 실패한 토템을 정리한다.

                return;// 토템 과정을 시작하지 않고 종료한다.
            }

            portalRoutine = StartCoroutine(GatherAndTeleportRoutine());// 몬스터 집결 후 순간이동하는 Coroutine을 시작한다.
        }

        private void CreateTotems()
        {
            Transform spawnRoot = transform.parent;// Caster와 같은 부모 아래에 토템을 생성한다.

            Vector3 entryPosition = GroundService.ProjectToGround(transform.position, totemGroundHeight);// Caster가 생성된 위치를 입구 토템 위치로 사용하고 바닥 높이를 보정한다.

            Vector3 exitPosition = CalculateExitPosition(entryPosition);// 입구 방향을 기준으로 Nexus 근처 출구 위치를 계산한다.

            entryTotem = Instantiate(entryTotemPrefab, entryPosition, Quaternion.identity, spawnRoot);// 입구 토템을 생성한다.

            entryTotem.Configure(EnemyPortalTotemType.Entry, attractRadius, entryRadius);// 입구 타입과 몬스터 유도·순간이동 범위를 전달한다.

            exitTotem = Instantiate(exitTotemPrefab, exitPosition, Quaternion.identity, spawnRoot);// Nexus 근처에 출구 토템을 생성한다.

            exitTotem.Configure(EnemyPortalTotemType.Exit, 0.1f, 0.1f);// 출구 토템은 몬스터를 유도하지 않으므로 최소 범위만 전달한다.
        }

        private Vector3 CalculateExitPosition(Vector3 entryPosition)
        {
            Vector3 outwardDirection = entryPosition - nexus.position;// Nexus에서 입구 토템 방향으로 향하는 벡터를 구한다.

            outwardDirection.y = 0.0f;// 높이 차이는 제외한다.

            if (outwardDirection.sqrMagnitude <= 0.0001f)
            {
                outwardDirection = -transform.forward;// 입구 방향을 계산할 수 없다면 Caster의 뒤쪽 방향을 사용한다.

                outwardDirection.y = 0.0f;// 높이 방향은 제외한다.
            }

            if (outwardDirection.sqrMagnitude <= 0.0001f)
            {
                outwardDirection = Vector3.forward;// 예비 방향도 없다면 월드 앞 방향을 사용한다.
            }

            outwardDirection.Normalize();// 방향 벡터의 길이를 1로 만든다.

            Vector3 exitPosition = nexus.position + outwardDirection * exitDistanceFromNexus;// 입구 토템과 같은 방향의 Nexus 외곽에 출구 위치를 만든다.

            return GroundService.ProjectToGround(exitPosition, totemGroundHeight);// 출구 위치를 바닥 높이에 맞게 보정해 반환한다.
        }

        private IEnumerator GatherAndTeleportRoutine()
        {
            IsChanneling = true;// 현재 토템을 유지하면서 몬스터를 모으는 중이라고 저장한다.

            float timer = 0.0f;// 집결이 진행된 시간을 저장한다.

            while (timer < gatherDuration)
            {
                if (entryTotem == null || exitTotem == null)
                {
                    IsChanneling = false;// 토템 집결 상태를 종료한다.

                    portalRoutine = null;// 진행 중인 Coroutine 참조를 비운다.

                    DestroyTotems();// 남아 있는 토템을 제거한다.

                    yield break;// 토템이 사라졌으므로 순간이동하지 않고 Coroutine을 종료한다.
                }

                timer += Time.deltaTime;// 지난 프레임 시간만큼 집결 시간을 증가시킨다.

                yield return null;// 다음 프레임까지 기다린다.
            }

            TeleportGatheredEnemies();// 집결 시간이 끝나면 입구 범위 안 몬스터 전부를 이동시킨다.

            FinishPortalSequence();// 순간이동 완료 후 토템을 제거하고 과정을 끝낸다.
        }

        private void TeleportGatheredEnemies()
        {
            if (entryTotem == null || exitTotem == null)
            {
                return;// 입구나 출구가 없다면 순간이동할 수 없다.
            }

            gatheredEnemies.Clear();// 이전 집결 대상이 남아 있지 않도록 목록을 비운다.

            EnemyController.CollectActiveInRange(entryTotem.transform.position, entryTotem.EntryRadius, gatheredEnemies, IsTeleportCandidate);// 입구 토템의 Entry Radius 안에 모인 몬스터를 모두 수집한다.

            for (int i = 0; i < gatheredEnemies.Count; i++)
            {
                EnemyController enemy = gatheredEnemies[i];// 현재 순간이동시킬 몬스터를 가져온다.

                if (enemy == null)
                {
                    continue;// 순간이동 전에 사라진 몬스터는 제외한다.
                }

                Vector3 teleportPosition = CalculateTeleportPosition(i);// 각 몬스터가 겹치지 않도록 출구 주변 위치를 계산한다.

                enemy.transform.position = teleportPosition;// 몬스터 위치를 출구 토템 근처로 즉시 이동시킨다.
            }

            Teleported?.Invoke(exitTotem.transform.position);
        }

        private bool IsTeleportCandidate(EnemyController enemy)
        {
            if (enemy == null)
            {
                return false;// 대상이 없으면 제외한다.
            }

            if (enemy.Grade == EnemyGrade.Boss)
            {
                return false;// Boss 등급 몬스터는 순간이동시키지 않는다.
            }

            return true;// 입구 토템 범위 안에 모인 일반 몬스터, 엘리트 몬스터, PortalTotemCaster 자신도 순간이동시킨다.
        }

        private Vector3 CalculateTeleportPosition(int index)
        {
            Vector3 center = exitTotem.transform.position;// 출구 토템 위치를 배치 중심으로 사용한다.

            const int slotsPerRing = 12;// 원형 한 줄에 배치할 몬스터 수

            int ringIndex = index / slotsPerRing + 1;// 몇 번째 원형 줄에 배치할지 계산한다.

            int slotIndex = index % slotsPerRing;// 현재 원형 줄에서 몇 번째 위치인지 계산한다.

            float angle = 360.0f / slotsPerRing * slotIndex + ringIndex * 15.0f;// 원형 줄마다 각도를 조금 돌려 몬스터 겹침을 줄인다.

            float radius = exitScatterRadius * ringIndex;// 몬스터가 많으면 바깥쪽 원형 줄을 추가한다.

            Vector3 offset = Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward * radius;// 각도와 반경으로 출구 주변 배치 위치를 만든다.

            Vector3 position = center + offset;// 출구 중심에 분산 위치를 더한다.

            return GroundService.ProjectToGround(position, teleportGroundHeight);// 몬스터 높이에 맞춰 바닥 위치를 보정한다.
        }

        private void FinishPortalSequence()
        {
            IsChanneling = false;// 더 이상 집결 중이 아니라고 저장한다.

            portalRoutine = null;// 진행 중인 Coroutine 참조를 비운다.

            DestroyTotems();// 사용이 끝난 입구와 출구 토템을 제거한다.

            enabled = false;// 한 번만 사용하는 몬스터이므로 이 Script Component를 종료한다.
        }

        private void CancelPortalSequence()
        {
            IsChanneling = false;// 집결 상태를 종료한다.

            if (portalRoutine != null)
            {
                StopCoroutine(portalRoutine);// 진행 중인 집결 Coroutine을 중지한다.

                portalRoutine = null;// Coroutine 참조를 비운다.
            }

            DestroyTotems();// 취소되면 입구와 출구 토템을 모두 제거한다.
        }

        private void DestroyTotems()
        {
            if (entryTotem != null)
            {
                entryTotem.Deactivate();// 몬스터들이 더 이상 입구 토템으로 이동하지 않게 한다.

                Destroy(entryTotem.gameObject);// 입구 토템을 제거한다.

                entryTotem = null;// 입구 토템 참조를 비운다.
            }

            if (exitTotem != null)
            {
                exitTotem.Deactivate();// 출구 토템 기능을 종료한다.

                Destroy(exitTotem.gameObject);// 출구 토템을 제거한다.

                exitTotem = null;// 출구 토템 참조를 비운다.
            }
        }
    }
}
