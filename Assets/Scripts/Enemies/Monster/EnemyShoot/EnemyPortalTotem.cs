using UnityEngine;

namespace TeamProject01.Gameplay
{
    public enum EnemyPortalTotemType // 포탈 토템의 역할을 구분
    {
        Entry, // 몬스터들이 모여드는 입구 토템
        Exit   // 몬스터들이 순간이동되어 나오는 출구 토템
    }

    public sealed class EnemyPortalTotem : MonoBehaviour // 입구/출구 포탈 토템의 정보를 관리
    {
        private EnemyPortalTotemType totemType;// Caster에게 전달받은 토템 종류를 저장한다.

        private float attractRadius;// Caster에게 전달받은 몬스터 유도 범위를 저장한다.

        private float entryRadius;// Caster에게 전달받은 순간이동 판정 범위를 저장한다.

        private bool isConfigured;// Caster에게 토템 설정값을 전달받았는지 저장한다.

        public EnemyPortalTotemType TotemType // 외부에서 토템 종류를 읽는 property
        {
            get
            {
                return totemType; // 현재 토템 종류를 반환한다.
            }
        }

        public float AttractRadius // 외부에서 몬스터 유도 범위를 읽는 property
        {
            get
            {
                return attractRadius; // 현재 유도 범위를 반환한다.
            }
        }

        public float EntryRadius // 외부에서 순간이동 판정 범위를 읽는 property
        {
            get
            {
                return entryRadius; // 현재 순간이동 판정 범위를 반환한다.
            }
        }

        public bool IsActive { get; private set; }// 이 토템이 현재 몬스터를 유도하거나 순간이동에 사용할 수 있는 상태인지 저장한다.

        public bool IsEntry // 이 토템이 입구 토템인지 확인하는 property
        {
            get
            {
                return totemType == EnemyPortalTotemType.Entry;
            }
        }

        private void OnEnable() // 토템 GameObject가 활성화될 때 실행되는 Unity 이벤트 함수
        {
            IsActive = true; // 토템을 활성 상태로 저장한다.

            RefreshRegistryRegistration(); // 현재 토템 타입에 맞게 Registry 등록 상태를 갱신한다.
        }

        private void OnDisable() // 토템 GameObject가 비활성화되거나 제거될 때 실행되는 Unity 이벤트 함수
        {
            IsActive = false; // 더 이상 사용할 수 없는 토템으로 저장한다.

            EnemyPortalTotemRegistry.UnregisterEntryTotem(this);// 이 토템이 입구 토템 목록에 등록되어 있다면 제거한다.
        }

        public void Configure(EnemyPortalTotemType newTotemType, float newAttractRadius, float newEntryRadius)// Caster가 토템을 생성한 뒤 토템 종류와 범위 수치를 전달하는 함수
        {
            EnemyPortalTotemRegistry.UnregisterEntryTotem(this);// 기존 타입이 입구였을 수도 있으므로 먼저 Registry 등록을 해제한다.

            totemType = newTotemType;// 전달받은 토템 종류를 저장한다.

            attractRadius = Mathf.Max(0.1f, newAttractRadius); // 유도 범위가 0 이하가 되지 않도록 최소값을 적용해 저장한다.

            entryRadius = Mathf.Max(0.1f, newEntryRadius); // 순간이동 판정 범위가 0 이하가 되지 않도록 최소값을 적용해 저장한다.

            isConfigured = true;// Caster에게 토템 설정값을 전달받았다고 저장한다.

            RefreshRegistryRegistration();// 변경된 토템 종류에 맞게 Registry에 다시 등록한다.
        }

        public void Deactivate() // 토템 기능을 중지하는 함수
        {
            if (!IsActive) // 이미 비활성 상태라면
            {
                return; // 중복 처리하지 않는다.
            }

            IsActive = false; // 토템을 비활성 상태로 저장한다.

            EnemyPortalTotemRegistry.UnregisterEntryTotem(this);// 몬스터들이 더 이상 이 토템으로 이동하지 않도록 Registry에서 제거한다.
        }

        private void RefreshRegistryRegistration() // 토템 종류에 맞게 Registry 등록 상태를 정리하는 함수
        {
            EnemyPortalTotemRegistry.UnregisterEntryTotem(this);// 중복 등록을 막기 위해 기존 등록을 먼저 제거한다.

            if (!isConfigured)// Caster에게 토템 설정값을 아직 전달받지 않았다면
            {
                return;// Registry에 등록하지 않는다.
            }

            if (!IsActive) // 현재 비활성 상태라면
            {
                return; // Registry에 등록하지 않는다.
            }

            if (!isActiveAndEnabled) // GameObject 또는 Script Component가 비활성 상태라면
            {
                return; // Registry에 등록하지 않는다.
            }

            if (!IsEntry) // 출구 토템이라면
            {
                return; // 몬스터 유도 대상 목록에 등록하지 않는다.
            }

            EnemyPortalTotemRegistry.RegisterEntryTotem(this); // 활성화된 입구 토템만 Registry에 등록한다.
        }
    }
}