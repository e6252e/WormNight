using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyHpBarBillboard : MonoBehaviour // HP바가 카메라를 바라보게 하는 스크립트
    {
        [SerializeField] private bool keepWorldScale = true; 

        private Camera targetCamera;

        private Vector3 baseWorldScale; // 시작 시점의 HP바 월드 크기

        private void Awake()
        {
            targetCamera = Camera.main; // MainCamera 태그가 붙은 카메라를 찾는다.

            baseWorldScale = transform.lossyScale; // 처음 HP바가 가진 월드 크기를 저장한다.
        }

        private void LateUpdate()
        {
            if (targetCamera == null) // 카메라를 못 찾았다면
            {
                targetCamera = Camera.main; // 다시 찾아본다.
            }

            if (targetCamera != null) // 카메라가 있다면
            {
                transform.forward = targetCamera.transform.forward; // HP바가 카메라와 같은 방향을 보게 한다.
            }

            if (keepWorldScale) // 부모 몬스터 성장 때문에 HP바가 같이 커지는 것을 막아야 한다면
            {
                ApplyFixedWorldScale(); // 시작 시점의 월드 크기를 유지한다.
            }
        }

        private void ApplyFixedWorldScale() // 부모 스케일을 역보정해서 HP바 월드 크기를 유지하는 함수
        {
            Transform parent = transform.parent; // HP바의 부모 Transform

            if (parent == null) // 부모가 없다면
            {
                return; // 보정할 필요가 없다.
            }

            Vector3 parentScale = parent.lossyScale; // 부모가 현재 월드에서 가진 스케일

            float safeParentX = Mathf.Abs(parentScale.x) > 0.0001f ? parentScale.x : 1.0f; // 0 나누기 방지
            float safeParentY = Mathf.Abs(parentScale.y) > 0.0001f ? parentScale.y : 1.0f; // 0 나누기 방지
            float safeParentZ = Mathf.Abs(parentScale.z) > 0.0001f ? parentScale.z : 1.0f; // 0 나누기 방지

            transform.localScale = new Vector3(baseWorldScale.x / safeParentX, baseWorldScale.y / safeParentY, baseWorldScale.z / safeParentZ); // 부모가 커진 만큼 자식 스케일을 줄여서 HP바 월드 크기를 유지한다.
        }
    }
}