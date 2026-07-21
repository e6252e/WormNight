using UnityEngine;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class NexusEggColliderRig : MonoBehaviour // 넥서스 알 모양 콜라이더 리그
    {
        private const string DefaultRootName = "Nexus_EggColliders";
        private const string BottomName = "Egg_Bottom_Sphere";
        private const string BodyName = "Egg_Body_Capsule";
        private const string TopName = "Egg_Top_Sphere";

        [Header("Block Radius Source")]
        public CapsuleCollider BlockerRadiusCollider; // 차단 반경 기준 콜라이더
        public NexusBlocker NexusBlocker; // 몬스터 차단 연동
        public bool KeepBlockerColliderAsTrigger = true; // 원본 캡슐은 반경 기준용 트리거로 사용
        [Min(0.1f)] public float BlockerRadius = 3f; // 차단 반경 기준
        [Min(0.1f)] public float BlockerHeight = 8f; // 차단 높이 기준
        public Vector3 BlockerCenter = new Vector3(0.17539215f, 2.1807785f, 0f); // 기존 넥서스 중심 보정

        [Header("Egg Colliders")]
        public bool EnableEggColliders = true; // 알 모양 물리 콜라이더 사용
        public string ColliderRootName = DefaultRootName; // 자식 루트 이름

        [Min(0.1f)] public float BottomRadius = 2.65f; // 아래쪽 넓은 구
        public Vector3 BottomLocalPosition = new Vector3(0.17539215f, 1.25f, 0f); // 아래쪽 위치

        [Min(0.1f)] public float BodyRadius = 2.25f; // 몸통 캡슐 반경
        [Min(0.1f)] public float BodyHeight = 4.25f; // 몸통 캡슐 높이
        public Vector3 BodyLocalPosition = new Vector3(0.17539215f, 3.0f, 0f); // 몸통 위치

        [Min(0.1f)] public float TopRadius = 1.45f; // 위쪽 좁은 구
        public Vector3 TopLocalPosition = new Vector3(0.17539215f, 5.05f, 0f); // 위쪽 위치

        private void Reset() // 기본 연결
        {
            ResolveReferences(); // 참조 찾기
            ApplyRig(); // 구성 적용
        }

        private void Awake() // 런타임 보정
        {
            ApplyRig(); // 구성 적용
        }

        [ContextMenu("Apply Egg Collider Rig")]
        public void ApplyRig() // 리그 구성 적용
        {
            ResolveReferences(); // 참조 찾기
            ConfigureBlockerCollider(); // 반경 기준 콜라이더
            ConfigureEggColliders(); // 알 모양 콜라이더

            if (NexusBlocker != null)
            {
                NexusBlocker.RadiusSource = BlockerRadiusCollider; // 기준 콜라이더 고정
                NexusBlocker.UseColliderBounds = true; // bounds 기준
                NexusBlocker.RefreshBlocker(); // 차단 반경 갱신
            }
        }

        private void ResolveReferences() // 참조 보강
        {
            if (BlockerRadiusCollider == null)
            {
                BlockerRadiusCollider = GetComponent<CapsuleCollider>(); // 기존 캡슐
            }

            if (NexusBlocker == null)
            {
                NexusBlocker = GetComponent<NexusBlocker>(); // 같은 오브젝트
            }
        }

        private void ConfigureBlockerCollider() // 기존 캡슐 설정
        {
            if (BlockerRadiusCollider == null)
            {
                BlockerRadiusCollider = gameObject.AddComponent<CapsuleCollider>(); // 없으면 생성
            }

            BlockerRadiusCollider.enabled = true; // 기준 유지
            BlockerRadiusCollider.isTrigger = KeepBlockerColliderAsTrigger; // 물리 차단은 자식 콜라이더가 담당
            BlockerRadiusCollider.direction = 1; // Y축
            BlockerRadiusCollider.radius = Mathf.Max(0.1f, BlockerRadius); // 반경
            BlockerRadiusCollider.height = Mathf.Max(BlockerHeight, BlockerRadius * 2f); // 높이
            BlockerRadiusCollider.center = BlockerCenter; // 중심
        }

        private void ConfigureEggColliders() // 알 모양 콜라이더 설정
        {
            Transform root = EnsureChild(transform, string.IsNullOrWhiteSpace(ColliderRootName) ? DefaultRootName : ColliderRootName); // 루트
            root.localPosition = Vector3.zero; // 위치
            root.localRotation = Quaternion.identity; // 회전
            root.localScale = Vector3.one; // 스케일

            SphereCollider bottom = EnsureCollider<SphereCollider>(root, BottomName); // 아래 구
            bottom.enabled = EnableEggColliders; // 활성
            bottom.isTrigger = false; // 물리 충돌
            bottom.transform.localPosition = BottomLocalPosition; // 위치
            bottom.transform.localRotation = Quaternion.identity; // 회전
            bottom.transform.localScale = Vector3.one; // 스케일
            bottom.center = Vector3.zero; // 중심
            bottom.radius = Mathf.Max(0.1f, BottomRadius); // 반경

            CapsuleCollider body = EnsureCollider<CapsuleCollider>(root, BodyName); // 몸통 캡슐
            body.enabled = EnableEggColliders; // 활성
            body.isTrigger = false; // 물리 충돌
            body.transform.localPosition = BodyLocalPosition; // 위치
            body.transform.localRotation = Quaternion.identity; // 회전
            body.transform.localScale = Vector3.one; // 스케일
            body.direction = 1; // Y축
            body.center = Vector3.zero; // 중심
            body.radius = Mathf.Max(0.1f, BodyRadius); // 반경
            body.height = Mathf.Max(BodyHeight, BodyRadius * 2f); // 높이

            SphereCollider top = EnsureCollider<SphereCollider>(root, TopName); // 위 구
            top.enabled = EnableEggColliders; // 활성
            top.isTrigger = false; // 물리 충돌
            top.transform.localPosition = TopLocalPosition; // 위치
            top.transform.localRotation = Quaternion.identity; // 회전
            top.transform.localScale = Vector3.one; // 스케일
            top.center = Vector3.zero; // 중심
            top.radius = Mathf.Max(0.1f, TopRadius); // 반경
        }

        private static Transform EnsureChild(Transform parent, string childName) // 자식 보장
        {
            Transform child = parent.Find(childName); // 기존 찾기
            if (child != null)
            {
                return child; // 기존 사용
            }

            GameObject childObject = new GameObject(childName); // 새 자식
            childObject.transform.SetParent(parent, false); // 부모 연결
            return childObject.transform; // 반환
        }

        private static T EnsureCollider<T>(Transform parent, string childName) where T : Collider // 콜라이더 보장
        {
            Transform child = EnsureChild(parent, childName); // 자식 보장
            T collider = child.GetComponent<T>(); // 기존 콜라이더
            if (collider == null)
            {
                collider = child.gameObject.AddComponent<T>(); // 새 콜라이더
            }

            return collider; // 반환
        }

#if UNITY_EDITOR
        private void OnValidate() // 인스펙터 변경 반영
        {
            if (!gameObject.scene.IsValid())
            {
                return; // 씬 오브젝트가 아니면 무시
            }

            ApplyRig(); // 즉시 반영
        }
#endif
    }
}
