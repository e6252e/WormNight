using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeamProject01.Gameplay
{
    public sealed class MapVisualMeshBuilder : MonoBehaviour // 판정면 위 비주얼 지형
    {
        private const string VisualMeshName = "MapVisualMesh";

        public MapVisualProfile Profile; // 비주얼 설정
        public Collider SourceGroundCollider; // 판정용 바닥
        public Material VisualMaterial; // 표시 머티리얼
        public Transform VisualParent; // 메시 부모
        public bool BuildOnAwake = true; // 시작 시 생성
        public bool HideSourceRenderer = true; // 판정 평면 숨김

        private GameObject visualMeshObject; // 생성된 메시 오브젝트

        private void Awake() // 런타임 생성
        {
            if (BuildOnAwake)
            {
                Build(); // 자동 생성
            }
        }

        [ContextMenu("Rebuild Visual Mesh")]
        public void Build() // 메시 재생성
        {
            MapVisualProfile profile = MapVisualProfile.Resolve(Profile); // 설정 보정
            if (profile == null)
            {
                Debug.LogWarning("[MapVisualMeshBuilder] 정식 MapVisualProfile 에셋이 없어 비주얼 맵을 생성하지 않습니다.", this);
                return;
            }

            Material material = ResolveVisualMaterial(); // 표시 머티리얼
            if (material == null)
            {
                Debug.LogWarning("[MapVisualMeshBuilder] 정식 VisualMaterial 에셋이 없어 비주얼 맵을 생성하지 않습니다.", this);
                return;
            }

            Collider source = ResolveSourceGroundCollider(); // 판정면 찾기
            Transform parent = ResolveVisualParent(source); // 부모 찾기
            Bounds bounds = ResolveBounds(source, profile); // 생성 범위

            SetSourceRendererVisible(source, !HideSourceRenderer); // 판정면 렌더 숨김
            DestroyVisualMesh(); // 기존 메시 정리

            visualMeshObject = new GameObject(VisualMeshName);
            visualMeshObject.transform.SetParent(parent, false);
            visualMeshObject.transform.localPosition = Vector3.zero;
            visualMeshObject.transform.localRotation = Quaternion.identity;
            visualMeshObject.transform.localScale = Vector3.one;

            Mesh mesh = CreateMesh(bounds, parent, profile); // 지형 메시 생성
            MeshFilter filter = visualMeshObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = visualMeshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material; // 머티리얼 연결
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        public void Configure(Collider sourceGroundCollider, MapVisualProfile profile, Material material, Transform visualParent, bool hideSourceRenderer) // 외부 설정
        {
            SourceGroundCollider = sourceGroundCollider; // 판정면
            Profile = profile; // 설정
            VisualMaterial = material; // 머티리얼
            VisualParent = visualParent; // 부모
            HideSourceRenderer = hideSourceRenderer; // 평면 숨김 여부
        }

        public static Mesh CreateMesh(Bounds bounds, Transform localSpace, MapVisualProfile profile) // 메시 구성
        {
            if (profile == null)
            {
                return null; // 설정 없음
            }

            float cellSize = Mathf.Max(1f, profile.CellSize);
            int columns = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / cellSize));
            int rows = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / cellSize));
            int vertexColumns = columns + 1;
            int vertexRows = rows + 1;
            int vertexCount = vertexColumns * vertexRows;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Color[] colors = new Color[vertexCount];
            List<int> triangles = new List<int>(columns * rows * 6);

            Vector3 nexusPosition = ResolveNexusPosition(profile);
            float baseY = bounds.center.y + profile.VisualYOffset;

            for (int z = 0; z < vertexRows; z++)
            {
                for (int x = 0; x < vertexColumns; x++)
                {
                    float worldX = x == vertexColumns - 1 ? bounds.max.x : bounds.min.x + x * cellSize;
                    float worldZ = z == vertexRows - 1 ? bounds.max.z : bounds.min.z + z * cellSize;
                    float heightWeight = CalculateFlattenWeight(worldX, worldZ, nexusPosition, profile);
                    float noise = SampleFractalNoise(worldX, worldZ, profile);
                    float worldY = baseY + noise * profile.HeightAmplitude * heightWeight;
                    int index = z * vertexColumns + x;

                    Vector3 worldPosition = new Vector3(worldX, worldY, worldZ);
                    vertices[index] = localSpace != null ? localSpace.InverseTransformPoint(worldPosition) : worldPosition;
                    uvs[index] = new Vector2(worldX / 8f, worldZ / 8f);
                    colors[index] = EvaluateVertexColor(noise, heightWeight, profile);
                }
            }

            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int a = z * vertexColumns + x;
                    int b = a + 1;
                    int c = a + vertexColumns;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh { name = VisualMeshName };
            mesh.indexFormat = vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Collider ResolveSourceGroundCollider() // 판정면 찾기
        {
            if (SourceGroundCollider != null)
            {
                return SourceGroundCollider; // 지정값
            }

            GroundService service = GroundService.Active;
            if (service != null && service.GroundCollider != null)
            {
                SourceGroundCollider = service.GroundCollider; // 서비스 기준
                return SourceGroundCollider;
            }

            GameObject ground = GameObject.Find("GroundPlane");
            if (ground == null)
            {
                ground = GameObject.Find("GroundPlane_80m"); // 이전명 호환
            }

            SourceGroundCollider = ground != null ? ground.GetComponent<Collider>() : null;
            return SourceGroundCollider;
        }

        private Transform ResolveVisualParent(Collider source) // 메시 부모 결정
        {
            if (VisualParent != null)
            {
                return VisualParent; // 지정 부모
            }

            if (source != null && source.transform.parent != null)
            {
                return source.transform.parent; // GroundPlane의 부모
            }

            return transform.parent != null ? transform.parent : transform; // fallback
        }

        private Bounds ResolveBounds(Collider source, MapVisualProfile profile) // 생성 영역
        {
            if (source != null)
            {
                return source.bounds; // 실제 판정면 기준
            }

            Vector2 size = profile.FallbackSize;
            size.x = Mathf.Max(1f, size.x);
            size.y = Mathf.Max(1f, size.y);
            return new Bounds(transform.position, new Vector3(size.x, 0f, size.y)); // 기본 영역
        }

        private Material ResolveVisualMaterial() // 머티리얼 결정
        {
            if (VisualMaterial != null)
            {
                return VisualMaterial; // 지정 머티리얼
            }

            return null; // 정식 머티리얼 필요
        }

        private static Vector3 ResolveNexusPosition(MapVisualProfile profile) // 넥서스 위치
        {
            if (!profile.FlattenAroundNexus || string.IsNullOrWhiteSpace(profile.NexusObjectName))
            {
                return new Vector3(float.NaN, 0f, float.NaN); // 미사용
            }

            GameObject nexus = GameObject.Find(profile.NexusObjectName);
            return nexus != null ? nexus.transform.position : new Vector3(float.NaN, 0f, float.NaN);
        }

        private static float CalculateFlattenWeight(float worldX, float worldZ, Vector3 nexusPosition, MapVisualProfile profile) // 평탄화 가중치
        {
            if (!profile.FlattenAroundNexus || float.IsNaN(nexusPosition.x))
            {
                return 1f; // 전체 굴곡
            }

            float distance = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(nexusPosition.x, nexusPosition.z));
            if (distance <= profile.NexusFlatRadius)
            {
                return 0f; // 완전 평탄
            }

            float t = Mathf.Clamp01((distance - profile.NexusFlatRadius) / Mathf.Max(0.01f, profile.NexusBlendDistance));
            return t * t * (3f - 2f * t); // smoothstep
        }

        private static float SampleFractalNoise(float worldX, float worldZ, MapVisualProfile profile) // fBM 노이즈
        {
            int octaveCount = Mathf.Clamp(profile.Octaves, 1, 5);
            float frequency = 1f / Mathf.Max(0.001f, profile.NoiseWorldScale);
            float amplitude = 1f;
            float total = 0f;
            float amplitudeTotal = 0f;
            float seedX = profile.Seed * 0.0137f;
            float seedZ = profile.Seed * 0.0173f;

            for (int i = 0; i < octaveCount; i++)
            {
                float sampleX = worldX * frequency + seedX + i * 19.31f;
                float sampleZ = worldZ * frequency + seedZ + i * 41.73f;
                float value = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                total += value * amplitude;
                amplitudeTotal += amplitude;
                amplitude *= profile.Persistence;
                frequency *= profile.Lacunarity;
            }

            if (amplitudeTotal <= 0.0001f)
            {
                return 0f; // 방어값
            }

            return Mathf.Clamp(total / amplitudeTotal, -1f, 1f);
        }

        private static Color EvaluateVertexColor(float noise, float heightWeight, MapVisualProfile profile) // 높이별 색
        {
            Color color = noise < 0f
                ? Color.Lerp(profile.MidColor, profile.LowColor, Mathf.Abs(noise))
                : Color.Lerp(profile.MidColor, profile.HighColor, noise);

            return Color.Lerp(profile.MidColor, color, heightWeight); // 평탄부는 기본색
        }

        private void SetSourceRendererVisible(Collider source, bool visible) // 판정면 렌더 표시
        {
            if (source == null)
            {
                return; // 대상 없음
            }

            Renderer renderer = source.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = visible; // 콜라이더는 유지
            }
        }

        private void DestroyVisualMesh() // 기존 메시 제거
        {
            if (visualMeshObject == null)
            {
                Transform parent = ResolveVisualParent(SourceGroundCollider);
                Transform existing = parent != null ? parent.Find(VisualMeshName) : null;
                visualMeshObject = existing != null ? existing.gameObject : null; // 이전 생성물
            }

            if (visualMeshObject == null)
            {
                return; // 제거 대상 없음
            }

            MeshFilter filter = visualMeshObject.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;

            if (Application.isPlaying)
            {
                Destroy(visualMeshObject);
                if (mesh != null)
                {
                    Destroy(mesh); // 런타임 메시
                }
            }
            else
            {
                DestroyImmediate(visualMeshObject);
                if (mesh != null)
                {
                    DestroyImmediate(mesh); // 에디터 메시
                }
            }

            visualMeshObject = null;
        }
    }
}
