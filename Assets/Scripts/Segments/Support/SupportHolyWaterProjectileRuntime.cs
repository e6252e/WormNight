using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class SupportHolyWaterProjectileRuntime : MonoBehaviour
    {
        private const int ConeSegments = 28;

        private static Material coneMaterial;
        private static Color coneMaterialColor;

        private readonly List<EnemyController> enemyBuffer = new List<EnemyController>(32);
        private Mesh coneMesh;
        private Transform sourceAnchor;
        private Transform directionAnchor;
        private Vector3 anchoredLocalDirection;
        private Vector3 origin;
        private Vector3 direction;
        private float maxDistance;
        private float coneAngle;
        private float lifetime;
        private float age;
        private float incomingDamageMultiplier;
        private float debuffDuration;
        private float debuffTickInterval;
        private float debuffTickTimer;
        private int sourceSegmentIndex;
        private GameObject sourceObject;
        private CombatStatusEffectKind statusEffect;
        private GameObject statusVfxPrefab;

        public static void SpawnCone(
            Transform parent,
            Transform sourceAnchor,
            Transform directionAnchor,
            Vector3 position,
            Vector3 direction,
            float maxDistance,
            float coneAngle,
            float lifetime,
            float incomingDamageMultiplier,
            float debuffDuration,
            float debuffTickInterval,
            Color projectileColor,
            int sourceSegmentIndex,
            GameObject sourceObject,
            CombatStatusEffectKind statusEffect,
            GameObject statusVfxPrefab)
        {
            GameObject instance = new GameObject("SG54_HolyWaterCone_Runtime");
            if (parent != null)
            {
                instance.transform.SetParent(parent, true);
            }

            SupportHolyWaterProjectileRuntime runtime = instance.AddComponent<SupportHolyWaterProjectileRuntime>();
            runtime.Configure(
                sourceAnchor,
                directionAnchor,
                position,
                direction,
                maxDistance,
                coneAngle,
                lifetime,
                incomingDamageMultiplier,
                debuffDuration,
                debuffTickInterval,
                projectileColor,
                sourceSegmentIndex,
                sourceObject,
                statusEffect,
                statusVfxPrefab);
        }

        private void Configure(
            Transform sourceAnchor,
            Transform directionAnchor,
            Vector3 origin,
            Vector3 fireDirection,
            float maxDistance,
            float coneAngle,
            float lifetime,
            float incomingDamageMultiplier,
            float debuffDuration,
            float debuffTickInterval,
            Color projectileColor,
            int sourceSegmentIndex,
            GameObject sourceObject,
            CombatStatusEffectKind statusEffect,
            GameObject statusVfxPrefab)
        {
            this.sourceAnchor = sourceAnchor;
            this.directionAnchor = directionAnchor != null ? directionAnchor : sourceAnchor;
            this.origin = origin;
            direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector3.forward;
            anchoredLocalDirection = this.directionAnchor != null
                ? this.directionAnchor.InverseTransformDirection(direction)
                : direction;
            this.maxDistance = Mathf.Max(0.1f, maxDistance);
            this.coneAngle = Mathf.Clamp(coneAngle, 1f, 180f);
            this.lifetime = Mathf.Max(0.05f, lifetime);
            this.incomingDamageMultiplier = Mathf.Max(1f, incomingDamageMultiplier);
            this.debuffDuration = Mathf.Max(0f, debuffDuration);
            this.debuffTickInterval = Mathf.Max(0.05f, debuffTickInterval);
            this.sourceSegmentIndex = sourceSegmentIndex;
            this.sourceObject = sourceObject;
            this.statusEffect = statusEffect;
            this.statusVfxPrefab = statusVfxPrefab;
            debuffTickTimer = 0f;
            age = 0f;

            RefreshConeTransform();

            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            coneMesh = new Mesh
            {
                name = "SG54_HolyWaterConeMesh_Runtime"
            };
            coneMesh.MarkDynamic();
            meshFilter.sharedMesh = coneMesh;
            meshRenderer.sharedMaterial = GetConeMaterial(projectileColor);

            UpdateConeMesh(this.maxDistance);
            ApplyDebuffInCone(this.maxDistance);
            debuffTickTimer = this.debuffTickInterval;
        }

        private void Update()
        {
            age += Time.deltaTime;
            RefreshConeTransform();

            debuffTickTimer -= Time.deltaTime;
            if (debuffTickTimer <= 0f)
            {
                ApplyDebuffInCone(maxDistance);
                debuffTickTimer = debuffTickInterval;
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void RefreshConeTransform()
        {
            if (sourceAnchor != null)
            {
                origin = sourceAnchor.position;
            }

            if (directionAnchor != null)
            {
                Vector3 anchoredDirection = directionAnchor.TransformDirection(anchoredLocalDirection);
                anchoredDirection.y = 0f;
                if (anchoredDirection.sqrMagnitude > 0.0001f)
                {
                    direction = anchoredDirection.normalized;
                }
            }

            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void ApplyDebuffInCone(float distance)
        {
            if ((incomingDamageMultiplier <= 1f && statusEffect == CombatStatusEffectKind.None) || debuffDuration <= 0f || distance <= 0.01f)
            {
                return;
            }

            float endRadius = GetConeRadius(distance);
            Vector3 queryCenter = origin + direction * (distance * 0.5f);
            float queryRadius = distance * 0.5f + endRadius;
            EnemyController.CollectActiveInRange(queryCenter, queryRadius, enemyBuffer, SegmentTargetQuery.IsEnemyUsable);

            for (int i = 0; i < enemyBuffer.Count; i++)
            {
                EnemyController enemy = enemyBuffer[i];
                if (enemy == null || !IsEnemyInsideCone(enemy, distance))
                {
                    continue;
                }

                EnemySupportDebuffState state = EnemySupportDebuffState.GetOrAdd(enemy);
                if (state != null)
                {
                    if (statusEffect != CombatStatusEffectKind.None)
                    {
                        Vector3 hitPosition = SegmentTargetQuery.GetEnemyHitPosition(enemy, enemy.transform.position, 0.5f);
                        state.ApplyStatusEffect(
                            statusEffect,
                            sourceSegmentIndex,
                            sourceObject,
                            hitPosition,
                            statusVfxPrefab,
                            debuffDuration,
                            incomingDamageMultiplier);
                    }
                    else
                    {
                        state.ApplyIncomingDamageMultiplier(incomingDamageMultiplier, debuffDuration);
                    }
                }
            }
        }

        private bool IsEnemyInsideCone(EnemyController enemy, float distance)
        {
            Vector3 hitPosition = SegmentTargetQuery.GetEnemyHitPosition(enemy, enemy.transform.position, 0.5f);
            Vector3 toTarget = hitPosition - origin;
            toTarget.y = 0f;

            float projectedDistance = Vector3.Dot(toTarget, direction);
            if (projectedDistance < 0f || projectedDistance > distance)
            {
                return false;
            }

            Vector3 perpendicular = toTarget - direction * projectedDistance;
            float progress = distance > 0.0001f ? Mathf.Clamp01(projectedDistance / distance) : 1f;
            float allowedRadius = GetConeRadius(distance * progress);
            return perpendicular.sqrMagnitude <= allowedRadius * allowedRadius;
        }

        private void UpdateConeMesh(float distance)
        {
            if (coneMesh == null)
            {
                return;
            }

            float endRadius = GetConeRadius(distance);
            Vector3[] vertices = new Vector3[ConeSegments + 2];
            int[] triangles = new int[ConeSegments * 6];

            vertices[0] = Vector3.zero;
            int capCenterIndex = ConeSegments + 1;
            vertices[capCenterIndex] = new Vector3(0f, 0f, distance);

            for (int i = 0; i < ConeSegments; i++)
            {
                float angle = i / (float)ConeSegments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * endRadius, Mathf.Sin(angle) * endRadius, distance);
            }

            int triangleIndex = 0;
            for (int i = 0; i < ConeSegments; i++)
            {
                int current = i + 1;
                int next = i == ConeSegments - 1 ? 1 : current + 1;

                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;

                triangles[triangleIndex++] = capCenterIndex;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current;
            }

            coneMesh.Clear();
            coneMesh.vertices = vertices;
            coneMesh.triangles = triangles;
            coneMesh.RecalculateNormals();
            coneMesh.RecalculateBounds();
        }

        private float GetConeRadius(float distance)
        {
            float halfAngleRadians = coneAngle * 0.5f * Mathf.Deg2Rad;
            return Mathf.Max(0f, Mathf.Tan(halfAngleRadians) * distance);
        }

        private static Material GetConeMaterial(Color color)
        {
            if (coneMaterial != null && coneMaterialColor == color)
            {
                return coneMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            coneMaterial = new Material(shader)
            {
                name = "Runtime_SG54_HolyWaterCone"
            };
            coneMaterialColor = color;

            if (coneMaterial.HasProperty("_BaseColor"))
            {
                coneMaterial.SetColor("_BaseColor", color);
            }

            if (coneMaterial.HasProperty("_Color"))
            {
                coneMaterial.SetColor("_Color", color);
            }

            if (coneMaterial.HasProperty("_Surface"))
            {
                coneMaterial.SetFloat("_Surface", 1f);
            }

            if (coneMaterial.HasProperty("_Mode"))
            {
                coneMaterial.SetFloat("_Mode", 3f);
            }

            if (coneMaterial.HasProperty("_Blend"))
            {
                coneMaterial.SetFloat("_Blend", 0f);
            }

            if (coneMaterial.HasProperty("_Cull"))
            {
                coneMaterial.SetFloat("_Cull", 0f);
            }

            coneMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            coneMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            coneMaterial.SetInt("_ZWrite", 0);
            coneMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            coneMaterial.EnableKeyword("_ALPHABLEND_ON");
            coneMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return coneMaterial;
        }
    }
}
