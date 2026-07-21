using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class GroundService : MonoBehaviour // 3D 바닥 판정
    {
        private const string GeneratedMeadowTerrainName = "GeneratedMeadowDemoTerrain";

        public static GroundService Active { get; private set; } // 현재 서비스

        public Collider GroundCollider; // 바닥 콜라이더
        [Min(0.1f)] public float RaycastHeight = 40f; // 위쪽 시작 높이
        [Min(1f)] public float RaycastDistance = 160f; // 검사 거리
        public float FallbackY = 0f; // 대체 바닥 높이
        public bool PreferGeneratedVisualTerrain = true; // 생성 Terrain 높이 우선

        private Terrain generatedVisualTerrain; // 런타임 생성 지형

        private void Awake() // 서비스 등록
        {
            Active = this; // 현재 씬 기준
            EnsureGroundCollider(); // 콜라이더 보강
        }

        private void OnEnable() // 재활성 등록
        {
            Active = this; // 현재 씬 기준
            EnsureGroundCollider(); // 콜라이더 보강
        }

        private void OnDestroy() // 서비스 해제
        {
            if (Active == this)
            {
                Active = null; // 참조 정리
            }
        }

        public static Vector3 ProjectToGround(Vector3 position, float offset) // 바닥 위로 투영
        {
            GroundService service = Active; // 현재 서비스
            if (service != null && service.TryProject(position, offset, out Vector3 grounded))
            {
                return grounded; // 실제 바닥
            }

            position.y = offset; // fallback 평면
            return position; // 대체 위치
        }

        public static bool RaycastGround(Ray ray, out Vector3 point) // 바닥 raycast
        {
            GroundService service = Active; // 현재 서비스
            if (service != null && service.TryRaycast(ray, out point))
            {
                return true; // 실제 바닥
            }

            Plane fallbackPlane = new Plane(Vector3.up, Vector3.zero); // 대체 평면
            if (fallbackPlane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance); // 평면 교점
                return true; // fallback 성공
            }

            point = Vector3.zero; // 실패값
            return false; // 교점 없음
        }

        public bool TryProject(Vector3 position, float offset, out Vector3 grounded) // 인스턴스 투영
        {
            if (TryProjectGeneratedVisualTerrain(position, offset, out grounded))
            {
                return true; // 생성 지형 표면
            }

            Ray ray = new Ray(position + Vector3.up * RaycastHeight, Vector3.down); // 하향 검사
            if (TryRaycast(ray, out Vector3 hitPoint))
            {
                grounded = hitPoint + Vector3.up * offset; // 높이 보정
                return true; // 성공
            }

            grounded = position; // 원위치 기준
            grounded.y = FallbackY + offset; // fallback 높이
            return false; // 실패
        }

        public bool TryRaycast(Ray ray, out Vector3 point) // 인스턴스 raycast
        {
            EnsureGroundCollider(); // 참조 보장
            if (GroundCollider != null && GroundCollider.Raycast(ray, out RaycastHit groundHit, RaycastDistance))
            {
                point = groundHit.point; // 지정 바닥
                return true; // 성공
            }

            if (Physics.Raycast(ray, out RaycastHit physicsHit, RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                point = physicsHit.point; // 물리 fallback
                return true; // 성공
            }

            point = Vector3.zero; // 실패값
            return false; // 실패
        }

        private bool TryProjectGeneratedVisualTerrain(Vector3 position, float offset, out Vector3 grounded) // 생성 Terrain 높이 샘플
        {
            grounded = position;
            if (!PreferGeneratedVisualTerrain)
            {
                return false; // 기존 판정면 유지
            }

            Terrain terrain = ResolveGeneratedVisualTerrain();
            TerrainData data = terrain != null ? terrain.terrainData : null;
            if (data == null)
            {
                return false; // 생성 지형 없음
            }

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = data.size;
            if (position.x < terrainPosition.x || position.z < terrainPosition.z ||
                position.x > terrainPosition.x + terrainSize.x || position.z > terrainPosition.z + terrainSize.z)
            {
                return false; // Terrain 범위 밖
            }

            float y = terrainPosition.y + terrain.SampleHeight(position);
            grounded = new Vector3(position.x, y + offset, position.z);
            return true;
        }

        private Terrain ResolveGeneratedVisualTerrain() // 생성 Terrain 찾기
        {
            if (generatedVisualTerrain != null && generatedVisualTerrain.isActiveAndEnabled)
            {
                return generatedVisualTerrain; // 캐시 사용
            }

            GameObject generated = GameObject.Find(GeneratedMeadowTerrainName);
            generatedVisualTerrain = generated != null ? generated.GetComponent<Terrain>() : null;
            if (generatedVisualTerrain != null)
            {
                return generatedVisualTerrain; // 이름 기준
            }

            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain != null && terrain.name == GeneratedMeadowTerrainName)
                {
                    generatedVisualTerrain = terrain;
                    return generatedVisualTerrain; // active terrain fallback
                }
            }

            return null;
        }

        private void EnsureGroundCollider() // 콜라이더 찾기
        {
            if (GroundCollider != null)
            {
                return; // 이미 연결됨
            }

            GameObject ground = GameObject.Find("GroundPlane"); // 정식 바닥명
            if (ground == null)
            {
                ground = GameObject.Find("GroundPlane_80m"); // 이전명 호환
            }

            GroundCollider = ground != null ? ground.GetComponent<Collider>() : null; // 콜라이더 연결
        }
    }
}
