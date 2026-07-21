using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeamProject01.Gameplay
{
    public readonly struct DirtRoadSurfaceBuildPoint // 길 생성 입력점
    {
        public readonly Vector3 Position; // 월드 위치
        public readonly float Width; // 지점별 길 폭

        public DirtRoadSurfaceBuildPoint(Vector3 position, float width)
        {
            Position = position;
            Width = width;
        }
    }

    public sealed class DirtRoadSurfaceGenerator : MonoBehaviour // 브릿지 접점 기반 흙길 메시
    {
        private const string GeneratedObjectName = "GeneratedDirtRoadSurface";
        private static readonly Color DirtWeight = new Color(1f, 0f, 0f, 1f); // 01 흙길

        public DirtRoadSurfaceProfile Profile; // 생성 설정
        public Material SurfaceMaterial; // 01/02/03 블렌드 머티리얼
        public DirtRoadEndpoint[] Endpoints; // 브릿지 입구 컴포넌트
        public Transform[] EndpointTransforms; // 간단 입력용 Transform
        public Transform CenterOverride; // 교차로 중심 수동 지정
        public Transform VisualParent; // 생성물 부모
        public bool BuildOnAwake = true; // 시작 시 생성
        public bool AddMeshCollider; // 실제 판정 연결

        private GameObject generatedObject; // 생성된 메시
        private static Material fallbackMaterial; // 머티리얼 미지정 fallback

        private void Awake() // 런타임 생성
        {
            if (BuildOnAwake)
            {
                Build(); // 자동 생성
            }
        }

        [ContextMenu("Rebuild Dirt Road Surface")]
        public void Build() // 길 지형 재생성
        {
            DirtRoadSurfaceProfile profile = Profile != null ? Profile : DirtRoadSurfaceProfile.RuntimeDefault;

            List<DirtRoadSurfaceBuildPoint> points = CollectBuildPoints(profile);
            if (points.Count < 1)
            {
                Debug.LogWarning("[DirtRoadSurfaceGenerator] 브릿지 입구가 1개 이상 필요합니다.", this);
                return;
            }

            Transform parent = VisualParent != null ? VisualParent : transform;
            Vector3 center = ResolveCenter(points);
            Mesh mesh = CreateMesh(points, center, parent, profile);
            if (mesh == null)
            {
                return; // 생성 실패
            }

            DestroyGeneratedObject(parent); // 기존 결과 정리

            generatedObject = new GameObject(GeneratedObjectName);
            generatedObject.transform.SetParent(parent, false);
            generatedObject.transform.localPosition = Vector3.zero;
            generatedObject.transform.localRotation = Quaternion.identity;
            generatedObject.transform.localScale = Vector3.one;

            MeshFilter filter = generatedObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = generatedObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ResolveSurfaceMaterial(); // 표시 머티리얼
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (AddMeshCollider)
            {
                MeshCollider collider = generatedObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh; // 선택형 판정
            }
        }

        public static Mesh CreateMesh(IReadOnlyList<DirtRoadSurfaceBuildPoint> points, Vector3 center, Transform localSpace, DirtRoadSurfaceProfile profile) // 메시 구성
        {
            if (profile == null || points == null || points.Count < 1)
            {
                return null; // 입력 부족
            }

            Bounds bounds = CalculateBounds(points, center, profile);
            float cellSize = Mathf.Max(0.15f, profile.CellSize);
            int columns = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / cellSize));
            int rows = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / cellSize));
            int vertexColumns = columns + 1;
            int vertexRows = rows + 1;
            int vertexCount = vertexColumns * vertexRows;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Color[] colors = new Color[vertexCount];
            List<int> triangles = new List<int>(columns * rows * 6);

            Vector2 center2 = ToXZ(center);
            float baseY = center.y + profile.VisualYOffset;

            for (int z = 0; z < vertexRows; z++)
            {
                for (int x = 0; x < vertexColumns; x++)
                {
                    float worldX = x == vertexColumns - 1 ? bounds.max.x : bounds.min.x + x * cellSize;
                    float worldZ = z == vertexRows - 1 ? bounds.max.z : bounds.min.z + z * cellSize;
                    float heightNoise = SampleFractalNoise(worldX, worldZ, profile);
                    float worldY = baseY + heightNoise * profile.HeightAmplitude;
                    float signedDistance = CalculateSignedRoadDistance(new Vector2(worldX, worldZ), center2, points, profile);
                    int index = z * vertexColumns + x;

                    Vector3 worldPosition = new Vector3(worldX, worldY, worldZ);
                    vertices[index] = localSpace != null ? localSpace.InverseTransformPoint(worldPosition) : worldPosition;
                    uvs[index] = new Vector2(worldX / Mathf.Max(0.1f, profile.TextureWorldSize), worldZ / Mathf.Max(0.1f, profile.TextureWorldSize));
                    colors[index] = EvaluateBlendColor(signedDistance, heightNoise, profile);
                }
            }

            float outerDistance = Mathf.Max(0f, profile.MidBlendWidth) + Mathf.Max(0f, profile.GrassBlendWidth);
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float worldX = bounds.min.x + (x + 0.5f) * cellSize;
                    float worldZ = bounds.min.z + (z + 0.5f) * cellSize;
                    float signedDistance = CalculateSignedRoadDistance(new Vector2(worldX, worldZ), center2, points, profile);
                    if (signedDistance > outerDistance)
                    {
                        continue; // 길 영역 밖
                    }

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

            Mesh mesh = new Mesh { name = GeneratedObjectName };
            mesh.indexFormat = vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private List<DirtRoadSurfaceBuildPoint> CollectBuildPoints(DirtRoadSurfaceProfile profile) // 입력점 수집
        {
            List<DirtRoadSurfaceBuildPoint> points = new List<DirtRoadSurfaceBuildPoint>();

            if (Endpoints != null)
            {
                for (int i = 0; i < Endpoints.Length; i++)
                {
                    DirtRoadEndpoint endpoint = Endpoints[i];
                    if (endpoint != null)
                    {
                        points.Add(new DirtRoadSurfaceBuildPoint(endpoint.Position, endpoint.Width));
                    }
                }
            }

            if (EndpointTransforms != null)
            {
                for (int i = 0; i < EndpointTransforms.Length; i++)
                {
                    Transform endpoint = EndpointTransforms[i];
                    if (endpoint != null)
                    {
                        points.Add(new DirtRoadSurfaceBuildPoint(endpoint.position, profile.DefaultRoadWidth));
                    }
                }
            }

            return points;
        }

        private Vector3 ResolveCenter(IReadOnlyList<DirtRoadSurfaceBuildPoint> points) // 교차로 중심
        {
            if (CenterOverride != null)
            {
                return CenterOverride.position; // 수동 지정
            }

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                sum += points[i].Position;
            }

            return sum / Mathf.Max(1, points.Count); // 접점 평균
        }

        private Material ResolveSurfaceMaterial() // 표시 머티리얼
        {
            if (SurfaceMaterial != null)
            {
                return SurfaceMaterial; // 정식 연결
            }

            if (fallbackMaterial != null)
            {
                return fallbackMaterial; // 런타임 fallback
            }

            Shader shader = Shader.Find("OZ/Map/Dirt Road Texture Blend");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit"); // 최후 fallback
            }

            fallbackMaterial = new Material(shader) { name = "DirtRoadSurface_Fallback_Runtime" };
            return fallbackMaterial;
        }

        private static Bounds CalculateBounds(IReadOnlyList<DirtRoadSurfaceBuildPoint> points, Vector3 center, DirtRoadSurfaceProfile profile) // 생성 범위
        {
            Bounds bounds = new Bounds(center, Vector3.zero);
            for (int i = 0; i < points.Count; i++)
            {
                bounds.Encapsulate(points[i].Position);
            }

            float maxWidth = Mathf.Max(profile.DefaultRoadWidth, profile.CenterRadius * 2f);
            for (int i = 0; i < points.Count; i++)
            {
                maxWidth = Mathf.Max(maxWidth, points[i].Width);
            }

            float margin = maxWidth + profile.MidBlendWidth + profile.GrassBlendWidth + profile.EdgeNoiseAmplitude + profile.CellSize * 2f;
            bounds.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
            return bounds;
        }

        private static float CalculateSignedRoadDistance(Vector2 point, Vector2 center, IReadOnlyList<DirtRoadSurfaceBuildPoint> points, DirtRoadSurfaceProfile profile) // 길 경계 거리
        {
            float edgeNoise = SampleEdgeNoise(point.x, point.y, profile) * profile.EdgeNoiseAmplitude;
            float centerSigned = Vector2.Distance(point, center) - (profile.CenterRadius + edgeNoise);
            float bestSigned = centerSigned;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 endpoint = ToXZ(points[i].Position);
                float width = Mathf.Max(profile.DefaultRoadWidth, points[i].Width);
                float halfWidth = width * 0.5f;
                float signed = DistanceToSegment(point, center, endpoint, out float t) - (halfWidth + edgeNoise);

                if (profile.EndpointWidenDistance > 0f && profile.EndpointWidenRatio > 0f)
                {
                    float endpointDistance = Vector2.Distance(point, endpoint);
                    float endpointWeight = 1f - Mathf.Clamp01(endpointDistance / profile.EndpointWidenDistance);
                    float widen = halfWidth * profile.EndpointWidenRatio * endpointWeight;
                    signed -= widen * Smooth01(t); // 입구 쪽만 확장
                }

                if (signed < bestSigned)
                {
                    bestSigned = signed;
                }
            }

            return bestSigned;
        }

        private static Color EvaluateBlendColor(float signedDistance, float heightNoise, DirtRoadSurfaceProfile profile) // 01/02/03 가중치
        {
            float midWidth = Mathf.Max(0.001f, profile.MidBlendWidth);
            float grassWidth = Mathf.Max(0.001f, profile.GrassBlendWidth);
            Color color;

            if (signedDistance <= 0f)
            {
                color = DirtWeight; // 01 흙길
            }
            else if (signedDistance <= midWidth)
            {
                float t = Mathf.Clamp01(signedDistance / midWidth);
                color = new Color(1f - t, t, 0f, 1f); // 01 -> 02
            }
            else
            {
                float t = Mathf.Clamp01((signedDistance - midWidth) / grassWidth);
                color = new Color(0f, 1f - t, t, 1f); // 02 -> 03
            }

            if (profile.LowHeightUsesDirtTexture && heightNoise <= profile.LowHeightThreshold)
            {
                float depthWeight = Mathf.Clamp01((profile.LowHeightThreshold - heightNoise) / Mathf.Max(0.001f, profile.LowHeightThreshold + 1f));
                color = Color.Lerp(color, DirtWeight, depthWeight * profile.LowHeightDirtBlend); // 웅덩이 후보는 01
            }

            return color;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b, out float t) // 선분 거리
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 0.0001f)
            {
                t = 0f;
                return Vector2.Distance(point, a);
            }

            t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            Vector2 closest = a + ab * t;
            return Vector2.Distance(point, closest);
        }

        private static float SampleFractalNoise(float worldX, float worldZ, DirtRoadSurfaceProfile profile) // 높이 fBM
        {
            int octaveCount = Mathf.Clamp(profile.HeightOctaves, 1, 5);
            float frequency = 1f / Mathf.Max(0.001f, profile.HeightNoiseWorldScale);
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
                amplitude *= profile.HeightPersistence;
                frequency *= profile.HeightLacunarity;
            }

            return amplitudeTotal <= 0.0001f ? 0f : Mathf.Clamp(total / amplitudeTotal, -1f, 1f);
        }

        private static float SampleEdgeNoise(float worldX, float worldZ, DirtRoadSurfaceProfile profile) // 가장자리 노이즈
        {
            float scale = Mathf.Max(0.001f, profile.EdgeNoiseWorldScale);
            float seedX = profile.Seed * 0.0211f + 79.3f;
            float seedZ = profile.Seed * 0.0317f + 13.7f;
            return Mathf.PerlinNoise(worldX / scale + seedX, worldZ / scale + seedZ) * 2f - 1f;
        }

        private static float Smooth01(float value) // smoothstep
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static Vector2 ToXZ(Vector3 value) // XZ 평면
        {
            return new Vector2(value.x, value.z);
        }

        private void DestroyGeneratedObject(Transform parent) // 이전 메시 제거
        {
            if (generatedObject == null)
            {
                Transform existing = parent != null ? parent.Find(GeneratedObjectName) : null;
                generatedObject = existing != null ? existing.gameObject : null;
            }

            if (generatedObject == null)
            {
                return; // 제거 대상 없음
            }

            MeshFilter filter = generatedObject.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;

            if (Application.isPlaying)
            {
                Destroy(generatedObject);
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }
            else
            {
                DestroyImmediate(generatedObject);
                if (mesh != null)
                {
                    DestroyImmediate(mesh);
                }
            }

            generatedObject = null;
        }

        private void OnDrawGizmosSelected() // 연결 미리보기
        {
            DirtRoadSurfaceProfile profile = Profile;
            if (profile == null)
            {
                return; // 설정 없음
            }

            List<DirtRoadSurfaceBuildPoint> points = CollectBuildPoints(profile);
            if (points.Count == 0)
            {
                return; // 입력 없음
            }

            Vector3 center = ResolveCenter(points);
            Gizmos.color = new Color(0.7f, 0.45f, 0.18f, 0.85f);
            Gizmos.DrawWireSphere(center, Mathf.Max(0.25f, profile.CenterRadius));

            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.DrawLine(center, points[i].Position);
                Gizmos.DrawWireSphere(points[i].Position, Mathf.Max(0.25f, points[i].Width * 0.5f));
            }
        }
    }
}
