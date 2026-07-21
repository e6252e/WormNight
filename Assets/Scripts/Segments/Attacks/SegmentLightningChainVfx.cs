using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class SegmentLightningChainVfx : MonoBehaviour // 런타임 체인 번개 빔
    {
        private const int PointCount = 5; // 번개 꺾임점 수

        private readonly Vector3[] jitterOffsets = new Vector3[PointCount]; // 고정 번개 흔들림

        private float destroyTime; // 제거 시간
        private LineRenderer line; // 번개 선
        private Material runtimeMaterial; // 런타임 라인 머티리얼
        private Transform startAnchor; // 시작점 추적 대상
        private Transform endAnchor; // 끝점 추적 대상
        private Vector3 fallbackStart; // 시작점 fallback
        private Vector3 fallbackEnd; // 끝점 fallback

        public static void Spawn(Vector3 start, Vector3 end, float lifetime)
        {
            float resolvedLifetime = Mathf.Max(0.03f, lifetime); // 너무 짧은 제거 시간 방지

            GameObject instance = new GameObject("SegmentLightningChainBeam_Runtime");
            SegmentLightningChainVfx beam = instance.AddComponent<SegmentLightningChainVfx>();
            beam.Configure(null, null, start, end, resolvedLifetime);
        }

        public static void Spawn(Transform start, Vector3 fallbackStart, Vector3 end, float lifetime)
        {
            float resolvedLifetime = Mathf.Max(0.03f, lifetime); // 너무 짧은 제거 시간 방지

            GameObject instance = new GameObject("SegmentLightningChainBeam_Runtime");
            SegmentLightningChainVfx beam = instance.AddComponent<SegmentLightningChainVfx>();
            beam.Configure(start, null, fallbackStart, end, resolvedLifetime);
        }

        private void Configure(Transform start, Transform end, Vector3 startPosition, Vector3 endPosition, float lifetime)
        {
            startAnchor = start; // 머즐 추적
            endAnchor = end; // 필요 시 끝점 추적
            fallbackStart = startPosition; // fallback 저장
            fallbackEnd = endPosition; // fallback 저장

            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = PointCount;
            line.startWidth = 0.18f;
            line.endWidth = 0.08f;
            line.startColor = Color.cyan;
            line.endColor = Color.white;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = CreateRuntimeMaterial();

            InitializeJitterOffsets(); // 번개 모양 고정
            RefreshBeamPose(); // 최초 위치
            destroyTime = Time.time + lifetime;
        }

        private void InitializeJitterOffsets()
        {
            float jitter = 0.35f;
            for (int i = 0; i < PointCount; i++)
            {
                if (i <= 0 || i >= PointCount - 1)
                {
                    jitterOffsets[i] = Vector3.zero; // 양 끝은 정확히 연결
                    continue;
                }

                jitterOffsets[i] = new Vector3(Random.Range(-jitter, jitter), 0f, 0f); // 2D 전기줄처럼 좌우 꺾임만 사용
            }
        }

        private void RefreshBeamPose()
        {
            Vector3 start = ResolveStartPosition(); // 현재 시작점
            Vector3 end = ResolveEndPosition(); // 현재 끝점
            Vector3 direction = end - start;
            Vector3 side = ResolveViewSide(direction); // 화면 기준 좌우 흔들림
            if (side.sqrMagnitude <= 0.0001f)
            {
                side = Vector3.right;
            }

            side.Normalize();
            for (int i = 0; i < PointCount; i++)
            {
                float t = PointCount <= 1 ? 0f : (float)i / (PointCount - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                if (i > 0 && i < PointCount - 1)
                {
                    point += side * jitterOffsets[i].x; // 고정 흔들림 재적용
                }

                line.SetPosition(i, point);
            }
        }

        private static Vector3 ResolveViewSide(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.right; // 2D 렌더러 fallback
            }

            Vector3 normal = Camera.main != null ? Camera.main.transform.forward : Vector3.forward; // 화면 기준 면
            Vector3 side = Vector3.Cross(direction.normalized, normal);
            if (side.sqrMagnitude <= 0.0001f)
            {
                side = Vector3.Cross(Vector3.up, direction.normalized); // 카메라와 평행하면 월드 기준 fallback
            }

            if (side.sqrMagnitude <= 0.0001f)
            {
                side = Vector3.right;
            }

            return side.normalized;
        }

        private Vector3 ResolveStartPosition()
        {
            return startAnchor != null ? startAnchor.position : fallbackStart; // 머즐이 살아 있으면 추적
        }

        private Vector3 ResolveEndPosition()
        {
            return endAnchor != null ? endAnchor.position : fallbackEnd; // 대상 추적 또는 고정 위치
        }

        private void Update()
        {
            if (Time.time >= destroyTime)
            {
                Destroy(gameObject);
                return;
            }

            RefreshBeamPose(); // 머즐 이동 추적
        }

        private void OnDestroy()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }
        }

        private Material CreateRuntimeMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            runtimeMaterial = new Material(shader);
            runtimeMaterial.name = "Runtime_SegmentLightningChainBeam";
            SetColorIfPresent(runtimeMaterial, "_BaseColor", Color.cyan);
            SetColorIfPresent(runtimeMaterial, "_Color", Color.cyan);
            SetColorIfPresent(runtimeMaterial, "_EmissionColor", new Color(0.4f, 1f, 1f, 1f));
            SetFloatIfPresent(runtimeMaterial, "_Surface", 1f);
            SetFloatIfPresent(runtimeMaterial, "_Blend", 1f);
            SetFloatIfPresent(runtimeMaterial, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloatIfPresent(runtimeMaterial, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            SetFloatIfPresent(runtimeMaterial, "_ZWrite", 0f);
            runtimeMaterial.EnableKeyword("_EMISSION");
            runtimeMaterial.renderQueue = 3000;
            return runtimeMaterial;
        }

        private static void SetColorIfPresent(Material material, string property, Color color)
        {
            if (material != null && material.HasProperty(property))
            {
                material.SetColor(property, color);
            }
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }
    }
}
