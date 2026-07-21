using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeamProject01.Gameplay
{
    public sealed class DirtRoadNetworkGenerator : MonoBehaviour // 플랫폼 배치부터 흙길까지 생성
    {
        private const string GeneratedRootName = "GeneratedDirtRoadNetwork";
        private const string BridgeNamePrefix = "Bridge_";
        private const string RoadNamePrefix = "DirtRoadSurface_";
        private static readonly Color DirtWeight = new Color(1f, 0f, 0f, 1f); // 브릿지는 01 흙길

        public DirtRoadNetworkProfile NetworkProfile; // 기준점/브릿지 설정
        public DirtRoadSurfaceProfile SurfaceProfile; // 흙길 지형 설정
        public Material BridgeMaterial; // 브릿지 표시 머티리얼
        public Material SurfaceMaterial; // 흙길 표시 머티리얼
        public Transform GeneratedParent; // 생성물 부모
        public bool BuildOnAwake = true; // 시작 시 생성

        private GameObject generatedRoot; // 전체 생성 루트
        private static Material fallbackMaterial; // 머티리얼 미지정 fallback

        private struct GuidePlatform // 실제 오브젝트가 아닌 교차로 기준점
        {
            public Vector3 Position;
            public float Radius;

            public GuidePlatform(Vector3 position, float radius)
            {
                Position = position;
                Radius = radius;
            }
        }

        private struct BridgeConnection // 기준점 연결
        {
            public int A;
            public int B;

            public BridgeConnection(int a, int b)
            {
                A = a;
                B = b;
            }
        }

        private void Awake() // 런타임 생성
        {
            if (BuildOnAwake)
            {
                Build(); // 전체 파이프라인 실행
            }
        }

        [ContextMenu("Rebuild Dirt Road Network")]
        public void Build() // 기준 플랫폼 -> 브릿지 -> 흙길 메시
        {
            DirtRoadNetworkProfile networkProfile = NetworkProfile != null ? NetworkProfile : DirtRoadNetworkProfile.RuntimeDefault;
            DirtRoadSurfaceProfile surfaceProfile = SurfaceProfile != null ? SurfaceProfile : DirtRoadSurfaceProfile.RuntimeDefault;
            List<GuidePlatform> platforms = CreateGuidePlatforms(networkProfile);
            List<BridgeConnection> bridges = CreateBridgeConnections(platforms);
            if (platforms.Count < 2 || bridges.Count == 0)
            {
                Debug.LogWarning("[DirtRoadNetworkGenerator] 플랫폼 기준점과 브릿지 연결이 부족합니다.", this);
                return;
            }

            Transform parent = GeneratedParent != null ? GeneratedParent : transform;
            DestroyGeneratedRoot(parent);

            generatedRoot = new GameObject(GeneratedRootName);
            generatedRoot.transform.SetParent(parent, false);
            generatedRoot.transform.localPosition = Vector3.zero;
            generatedRoot.transform.localRotation = Quaternion.identity;
            generatedRoot.transform.localScale = Vector3.one;

            BuildBridgeMeshes(generatedRoot.transform, platforms, bridges, networkProfile);
            BuildRoadSurfaceMeshes(generatedRoot.transform, platforms, bridges, networkProfile, surfaceProfile);
        }

        private List<GuidePlatform> CreateGuidePlatforms(DirtRoadNetworkProfile profile) // 기준 플랫폼 배치
        {
            int count = Mathf.Max(2, profile.PlatformCount);
            float radius = Mathf.Max(0.5f, profile.PlatformRadius);
            float spacing = Mathf.Max(radius * 2f + 0.5f, profile.PlatformSpacing);
            Vector3 direction = ResolveInitialDirection(profile);
            Vector3 position = transform.position;
            System.Random random = new System.Random(profile.Seed);
            List<GuidePlatform> platforms = new List<GuidePlatform>(count)
            {
                new GuidePlatform(position, radius)
            };

            for (int i = 1; i < count; i++)
            {
                float turn = RandomRange(random, -profile.MaxTurnAngle, profile.MaxTurnAngle);
                direction = Quaternion.AngleAxis(turn, Vector3.up) * direction;
                direction.y = 0f;
                direction = direction.sqrMagnitude <= 0.0001f ? Vector3.forward : direction.normalized;

                Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 jitter = right * RandomRange(random, -profile.PositionJitter, profile.PositionJitter);
                jitter += direction * RandomRange(random, -profile.PositionJitter * 0.5f, profile.PositionJitter * 0.5f);
                position += direction * spacing + jitter;
                platforms.Add(new GuidePlatform(position, radius));
            }

            return platforms;
        }

        private static List<BridgeConnection> CreateBridgeConnections(IReadOnlyList<GuidePlatform> platforms) // 순차 브릿지
        {
            List<BridgeConnection> bridges = new List<BridgeConnection>();
            for (int i = 0; i < platforms.Count - 1; i++)
            {
                bridges.Add(new BridgeConnection(i, i + 1));
            }

            return bridges;
        }

        private void BuildBridgeMeshes(Transform root, IReadOnlyList<GuidePlatform> platforms, IReadOnlyList<BridgeConnection> bridges, DirtRoadNetworkProfile profile) // 브릿지 생성
        {
            Material material = BridgeMaterial != null ? BridgeMaterial : ResolveFallbackMaterial();

            for (int i = 0; i < bridges.Count; i++)
            {
                BridgeConnection bridge = bridges[i];
                GuidePlatform a = platforms[bridge.A];
                GuidePlatform b = platforms[bridge.B];
                if (!TryGetBridgeEndpoints(a, b, out Vector3 start, out Vector3 end))
                {
                    continue; // 너무 가까움
                }

                Mesh mesh = CreateBridgeMesh(start, end, Mathf.Max(0.5f, profile.BridgeWidth), profile, root);
                if (mesh == null)
                {
                    continue; // 생성 실패
                }

                GameObject bridgeObject = new GameObject($"{BridgeNamePrefix}{bridge.A:00}_{bridge.B:00}");
                bridgeObject.transform.SetParent(root, false);

                MeshFilter filter = bridgeObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;

                MeshRenderer renderer = bridgeObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                if (profile.AddBridgeColliders)
                {
                    MeshCollider collider = bridgeObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                }
            }
        }

        private void BuildRoadSurfaceMeshes(Transform root, IReadOnlyList<GuidePlatform> platforms, IReadOnlyList<BridgeConnection> bridges, DirtRoadNetworkProfile networkProfile, DirtRoadSurfaceProfile surfaceProfile) // 교차로 흙길 생성
        {
            Material material = SurfaceMaterial != null ? SurfaceMaterial : ResolveFallbackMaterial();

            for (int platformIndex = 0; platformIndex < platforms.Count; platformIndex++)
            {
                GuidePlatform platform = platforms[platformIndex];
                List<DirtRoadSurfaceBuildPoint> points = CollectRoadEndpoints(platformIndex, platforms, bridges, networkProfile);
                if (points.Count == 0)
                {
                    continue; // 연결 없음
                }

                Mesh mesh = DirtRoadSurfaceGenerator.CreateMesh(points, platform.Position, root, surfaceProfile);
                if (mesh == null)
                {
                    continue; // 생성 실패
                }

                GameObject roadObject = new GameObject($"{RoadNamePrefix}{platformIndex:00}");
                roadObject.transform.SetParent(root, false);

                MeshFilter filter = roadObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;

                MeshRenderer renderer = roadObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                if (networkProfile.AddRoadColliders)
                {
                    MeshCollider collider = roadObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                }
            }
        }

        private static List<DirtRoadSurfaceBuildPoint> CollectRoadEndpoints(int platformIndex, IReadOnlyList<GuidePlatform> platforms, IReadOnlyList<BridgeConnection> bridges, DirtRoadNetworkProfile profile) // 브릿지 접점 수집
        {
            GuidePlatform platform = platforms[platformIndex];
            List<DirtRoadSurfaceBuildPoint> points = new List<DirtRoadSurfaceBuildPoint>();

            for (int i = 0; i < bridges.Count; i++)
            {
                BridgeConnection bridge = bridges[i];
                int otherIndex = -1;
                if (bridge.A == platformIndex)
                {
                    otherIndex = bridge.B;
                }
                else if (bridge.B == platformIndex)
                {
                    otherIndex = bridge.A;
                }

                if (otherIndex < 0)
                {
                    continue; // 이 플랫폼과 무관
                }

                Vector3 direction = platforms[otherIndex].Position - platform.Position;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    continue; // 겹침 방어
                }

                Vector3 endpoint = platform.Position + direction.normalized * platform.Radius;
                points.Add(new DirtRoadSurfaceBuildPoint(endpoint, profile.BridgeWidth));
            }

            return points;
        }

        private static Mesh CreateBridgeMesh(Vector3 start, Vector3 end, float width, DirtRoadNetworkProfile profile, Transform localSpace) // 직선 브릿지 메시
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length <= 0.01f)
            {
                return null; // 너무 짧음
            }

            direction /= length;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            int segments = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.25f, profile.BridgeCellLength)));
            int vertexCount = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Color[] colors = new Color[vertexCount];
            List<int> triangles = new List<int>(segments * 6);

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 center = Vector3.Lerp(start, end, t);
                center.y += profile.BridgeYOffset;
                Vector3 left = center - right * (width * 0.5f);
                Vector3 rightPoint = center + right * (width * 0.5f);
                int index = i * 2;

                vertices[index] = localSpace != null ? localSpace.InverseTransformPoint(left) : left;
                vertices[index + 1] = localSpace != null ? localSpace.InverseTransformPoint(rightPoint) : rightPoint;
                uvs[index] = new Vector2(0f, t * length / Mathf.Max(0.1f, width));
                uvs[index + 1] = new Vector2(1f, t * length / Mathf.Max(0.1f, width));
                colors[index] = DirtWeight;
                colors[index + 1] = DirtWeight;
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }

            Mesh mesh = new Mesh { name = "DirtRoadBridgeMesh" };
            mesh.indexFormat = vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool TryGetBridgeEndpoints(GuidePlatform a, GuidePlatform b, out Vector3 start, out Vector3 end) // 기준점 사이 실제 브릿지 범위
        {
            Vector3 direction = b.Position - a.Position;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance <= a.Radius + b.Radius + 0.01f)
            {
                start = a.Position;
                end = b.Position;
                return false; // 기준 반경이 겹침
            }

            direction /= distance;
            start = a.Position + direction * a.Radius;
            end = b.Position - direction * b.Radius;
            return true;
        }

        private Vector3 ResolveInitialDirection(DirtRoadNetworkProfile profile) // 시작 진행 방향
        {
            Vector3 direction = profile.UseTransformForward ? transform.forward : Vector3.forward;
            direction.y = 0f;
            return direction.sqrMagnitude <= 0.0001f ? Vector3.forward : direction.normalized;
        }

        private Material ResolveFallbackMaterial() // 01/02/03 블렌드 fallback
        {
            if (fallbackMaterial != null)
            {
                return fallbackMaterial;
            }

            Shader shader = Shader.Find("OZ/Map/Dirt Road Texture Blend");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            fallbackMaterial = new Material(shader) { name = "DirtRoadNetwork_Fallback_Runtime" };
            return fallbackMaterial;
        }

        private static float RandomRange(System.Random random, float min, float max) // System.Random float
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private void DestroyGeneratedRoot(Transform parent) // 기존 생성물 제거
        {
            if (generatedRoot == null)
            {
                Transform existing = parent != null ? parent.Find(GeneratedRootName) : null;
                generatedRoot = existing != null ? existing.gameObject : null;
            }

            if (generatedRoot == null)
            {
                return; // 제거 대상 없음
            }

            if (Application.isPlaying)
            {
                Destroy(generatedRoot);
            }
            else
            {
                DestroyImmediate(generatedRoot);
            }

            generatedRoot = null;
        }

        private void OnDrawGizmosSelected() // 생성 전 배치 미리보기
        {
            DirtRoadNetworkProfile profile = NetworkProfile != null ? NetworkProfile : DirtRoadNetworkProfile.RuntimeDefault;
            List<GuidePlatform> platforms = CreateGuidePlatforms(profile);
            List<BridgeConnection> bridges = CreateBridgeConnections(platforms);

            Gizmos.color = new Color(0.55f, 0.34f, 0.16f, 0.9f);
            for (int i = 0; i < platforms.Count; i++)
            {
                Gizmos.DrawWireSphere(platforms[i].Position, platforms[i].Radius);
            }

            Gizmos.color = new Color(0.85f, 0.55f, 0.2f, 0.9f);
            for (int i = 0; i < bridges.Count; i++)
            {
                BridgeConnection bridge = bridges[i];
                if (TryGetBridgeEndpoints(platforms[bridge.A], platforms[bridge.B], out Vector3 start, out Vector3 end))
                {
                    Gizmos.DrawLine(start, end);
                }
            }
        }
    }
}
