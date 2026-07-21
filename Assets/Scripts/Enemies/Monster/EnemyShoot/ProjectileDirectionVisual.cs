using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class ProjectileDirectionVisual : MonoBehaviour // 투사체의 실제 이동 방향에 맞춰 마법 그림을 회전시키는 Script
    {
        [Header("Reference")]
        [SerializeField] private Transform projectileRoot; // Nexus를 향해 실제로 이동하는 투사체 루트

        [Header("Rotation")]
        [SerializeField] private float rotationOffset; // 마법 이미지 원본 앞 방향을 보정할 회전값

        private Camera viewCamera; // 평면 마법 그림이 화면을 바라보게 만들 때만 사용하는 카메라

        private Vector3 previousPosition; // 이전 프레임의 투사체 위치

        private Vector3 lastMoveDirection; // 마지막으로 확인된 실제 이동 방향

        private bool hasPreviousPosition; // 이전 위치가 준비되었는지 확인하는 값

        private void Awake()
        {
            ResolveReferences(); // 투사체 루트와 화면 카메라를 자동으로 찾는다.
        }

        private void OnEnable()
        {
            ResolveReferences(); // 활성화될 때 참조를 다시 확인한다.

            if (projectileRoot == null) // 투사체 루트가 없다면
            {
                hasPreviousPosition = false; // 위치 계산을 준비하지 않는다.

                return; // 초기화를 중단한다.
            }

            previousPosition = projectileRoot.position; // 현재 위치를 이전 위치로 저장한다.

            lastMoveDirection = projectileRoot.forward; // 이동 전에는 투사체 루트의 앞 방향을 임시로 사용한다.

            hasPreviousPosition = true; // 다음 프레임부터 이동 방향을 계산할 수 있다.
        }

        private void LateUpdate()
        {
            ResolveReferences(); // 참조가 늦게 준비됐을 가능성을 다시 확인한다.

            if (projectileRoot == null || viewCamera == null) // 투사체 루트나 화면 카메라가 없다면
            {
                return; // 그림 방향을 계산하지 않는다.
            }

            Vector3 currentPosition = projectileRoot.position; // 현재 투사체 위치를 가져온다.

            if (!hasPreviousPosition) // 이전 위치가 준비되지 않았다면
            {
                previousPosition = currentPosition; // 현재 위치를 이전 위치로 저장한다.

                lastMoveDirection = projectileRoot.forward; // 초기 방향으로 투사체의 앞 방향을 사용한다.

                hasPreviousPosition = true; // 다음 프레임부터 이동 방향을 계산한다.

                return; // 이번 프레임에는 회전하지 않는다.
            }

            Vector3 moveDirection = currentPosition - previousPosition; // 이전 위치부터 현재 위치까지의 실제 이동 방향을 계산한다.

            previousPosition = currentPosition; // 다음 프레임을 위해 현재 위치를 저장한다.

            if (moveDirection.sqrMagnitude > 0.000001f) // 실제로 움직였다면
            {
                lastMoveDirection = moveDirection.normalized; // Nexus를 향하는 현재 포물선 진행 방향을 저장한다.
            }

            if (lastMoveDirection.sqrMagnitude <= 0.000001f) // 사용할 수 있는 이동 방향이 없다면
            {
                return; // 현재 회전을 유지한다.
            }

            Vector3 cameraFacingDirection = -viewCamera.transform.forward; // 마법 그림이 화면을 향할 방향을 계산한다.

            Vector3 visualMoveDirection = Vector3.ProjectOnPlane(lastMoveDirection, cameraFacingDirection); // 실제 비행 방향을 화면 평면에 맞춘다.

            if (visualMoveDirection.sqrMagnitude <= 0.000001f) // 화면에서 사용할 수 있는 진행 방향이 없다면
            {
                return; // 현재 회전을 유지한다.
            }

            visualMoveDirection.Normalize(); // 화면 기준 진행 방향을 정규화한다.

            Quaternion directionRotation = Quaternion.LookRotation(cameraFacingDirection, visualMoveDirection); // 마법 그림의 앞부분을 실제 비행 방향에 맞춘다.

            transform.rotation = directionRotation * Quaternion.Euler(0.0f, 0.0f, rotationOffset); // 원본 이미지 방향 보정값을 추가한다.
        }

        private void ResolveReferences()
        {
            if (projectileRoot == null) // Inspector에서 투사체 루트를 연결하지 않았다면
            {
                projectileRoot = transform.parent; // 바로 위 부모인 EnemyProjectile_BloodMagic을 자동 사용한다.
            }

            if (viewCamera == null) // 화면 카메라를 아직 찾지 못했다면
            {
                viewCamera = Camera.main; // MainCamera를 자동으로 찾는다.
            }
        }
    }
}