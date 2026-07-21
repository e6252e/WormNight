using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class SegmentProjectileRuntime
    {
        private void UpdateStraightProjectile() // 직선 이동
        {
            Vector3 previousPosition = transform.position; // 이동 전 위치
            transform.position += direction * (GetProjectileSpeed() * Time.deltaTime); // 강화 속도 이동
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up); // 방향
            }

            if (TryExplodeOnGroundContact(previousPosition, transform.position))
            {
                return; // 바닥 충돌 폭발
            }

            TryApplyHitAt(transform.position); // 명중 확인
        }

        private void UpdateHomingProjectile() // 추적 이동
        {
            if (!SegmentTargetQuery.IsEnemyUsable(target))
            {
                target = TryReacquireProjectileTarget(out EnemyController nextTarget) ? nextTarget : null; // 죽은 대상이면 즉시 재탐색
            }

            if (SegmentTargetQuery.IsEnemyUsable(target))
            {
                Vector3 targetPosition = target.transform.position + Vector3.up * profile.TargetAimHeight; // 목표 중심
                Vector3 offset = targetPosition - transform.position; // 목표 방향
                if (offset.sqrMagnitude > 0.0001f)
                {
                    direction = offset.normalized; // 방향 갱신
                }
            }

            UpdateStraightProjectile(); // 이동 공유
        }

        private bool TryReacquireProjectileTarget(out EnemyController nextTarget) // 투사체 이동 중 새 대상 탐색
        {
            float range = profile != null ? Mathf.Max(profile.SearchRange, profile.ProjectileHitRadius) : 0.1f; // 프로필 사거리
            return EnemyController.TryFindNearest(transform.position, range, SegmentTargetQuery.IsEnemyUsable, out nextTarget); // 현재 위치 기준 재탐색
        }

        private void UpdateExpandingFlameSphere() // 전진하며 커지는 화염 판정 구체
        {
            flameSphereTimer += Time.deltaTime; // 진행 시간
            transform.position += direction * (GetProjectileSpeed() * Time.deltaTime); // 발사 당시 방향으로 전진
            ApplyExpandingFlameMuzzleInfluence(); // 총구 이동량 약한 보정
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up); // 이동 방향 표시
            }

            float radius = GetCurrentExpandingFlameRadius(); // 현재 반경
            ApplyExpandingFlameScale(radius); // 숨겨진 판정 구체 크기
            RefreshExpandingFlameDebugVisual();
            flameSphereTickTimer -= Time.deltaTime; // 틱 대기
            if (flameSphereTickTimer <= 0f)
            {
                ApplyExpandingFlameDamage(radius); // 범위 지속 피해
                flameSphereTickTimer = GetExpandingFlameTickInterval(); // 다음 틱 예약
            }
        }

        private void ApplyExpandingFlameMuzzleInfluence() // 가까운 화염 구체만 총구 이동을 약하게 따라감
        {
            if (flameInfluenceAnchor == null)
            {
                hasLastFlameInfluenceAnchorPosition = false;
                return;
            }

            float strength = profile != null ? Mathf.Clamp01(profile.FlameMuzzleInfluenceStrength) : 0f;
            if (strength <= 0f)
            {
                return;
            }

            Vector3 anchorPosition = flameInfluenceAnchor.position;
            if (!hasLastFlameInfluenceAnchorPosition)
            {
                lastFlameInfluenceAnchorPosition = anchorPosition;
                hasLastFlameInfluenceAnchorPosition = true;
                return;
            }

            Vector3 anchorDelta = anchorPosition - lastFlameInfluenceAnchorPosition;
            lastFlameInfluenceAnchorPosition = anchorPosition;
            anchorDelta.y = 0f;
            if (anchorDelta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 offset = transform.position - anchorPosition;
            offset.y = 0f;
            float profileRange = profile != null ? profile.FlameMuzzleInfluenceRange : 0f;
            float influenceRange = profileRange > 0f ? profileRange : GetProjectileSpeed() * Mathf.Max(0.1f, flameSphereDuration);
            influenceRange = Mathf.Max(0.5f, influenceRange);
            float nearFactor = Mathf.Clamp01(1f - (offset.magnitude / influenceRange));
            float influence = strength * nearFactor * nearFactor;
            transform.position += anchorDelta * influence;
        }

        private float GetCurrentExpandingFlameRadius() // 시작 반경에서 최종 반경까지 확장
        {
            float startRadius = profile != null ? Mathf.Max(0.05f, profile.ProjectileHitRadius) : 0.05f; // 시작 판정
            float endRadius = Mathf.Max(startRadius, GetExplosionRadius()); // 최종 판정
            float progress = flameSphereDuration > 0f ? Mathf.Clamp01(flameSphereTimer / flameSphereDuration) : 1f; // 진행률
            progress = Mathf.SmoothStep(0f, 1f, progress); // 초반 급확장 방지
            return Mathf.Lerp(startRadius, endRadius, progress); // 현재 반경
        }

        private float GetExpandingFlameTickInterval() // 화염 피해 간격
        {
            float interval = profile != null ? weaponBonus.ResolveLaserTickInterval(profile.LaserTickInterval) : 0.2f; // 기존 지속 피해 간격 재사용
            return Mathf.Max(0.02f, interval); // 최소값
        }

        private void ApplyExpandingFlameDamage(float radius) // 화염 구체 안 몬스터 지속 피해
        {
            flameTickEnemyIds.Clear(); // 이번 틱 중복 초기화
            DamageData flameDamage = DamageData.Create(damage.Amount, damage.Type, damage.SourceSegmentIndex, transform.position, damage.SourceObject); // 화염 틱 피해
            Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(0.05f, radius)); // 현재 구체 반경 검색
            for (int i = 0; i < hits.Length; i++)
            {
                EnemyController enemy = hits[i].GetComponentInParent<EnemyController>(); // 몬스터
                if (!SegmentTargetQuery.IsEnemyUsable(enemy) || flameTickEnemyIds.Contains(enemy.EnemyId))
                {
                    continue; // 대상 아님/같은 틱 중복
                }

                flameTickEnemyIds.Add(enemy.EnemyId); // 이번 틱 중복 방지
                Vector3 hitPosition = GetEnemyHitPosition(enemy); // 중심 위치
                SegmentHitResolver.ApplyDamageAndFeedback(enemy, flameDamage, profile, hitPosition, transform.position, SegmentMonsterFeedbackKind.Continuous); // 지속 피해 + 약한 피드백
                PlayHitVfx(hitPosition); // 지정된 경우만 재생
            }
        }

        private void ApplyExpandingFlameScale(float radius) // 숨겨진 판정 구체 크기
        {
            float diameter = Mathf.Max(0.05f, radius) * 2f; // Unity Sphere 지름 기준
            transform.localScale = new Vector3(diameter, diameter, diameter); // 시각 크기와 판정 일치
        }

        private void SetupExpandingFlameSphereVisual() // SG05 1차 디버그용 반투명 구체
        {
            if (profile == null || profile.MoveType != SegmentAttackMoveType.ExpandingFlameSphere)
            {
                return; // 화염 구체 아님
            }

            gameObject.name = "SG05_FlameHitSphere_Runtime";
            RefreshExpandingFlameDebugVisual();
            ApplyExpandingFlameScale(GetCurrentExpandingFlameRadius()); // 시작 크기 즉시 반영
        }

        private void RefreshExpandingFlameDebugVisual()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            bool showDebugVisual = RuntimeCombatDebugVisuals.TemporaryCombatDebugVisualsEnabled;
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = showDebugVisual;
                if (showDebugVisual)
                {
                    renderers[i].sharedMaterial = GetExpandingFlameDebugMaterial();
                }
            }
        }

        private static Material GetExpandingFlameDebugMaterial() // 런타임 반투명 재질
        {
            if (expandingFlameDebugMaterial != null)
            {
                return expandingFlameDebugMaterial; // 재사용
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit"); // URP 우선
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit"); // URP fallback
            }

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Transparent/Diffuse"); // 내장 fallback
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard"); // 최후 fallback
            }

            expandingFlameDebugMaterial = new Material(shader);
            expandingFlameDebugMaterial.name = "Runtime_SG05_FlameDebugSphere";
            Color color = new Color(1f, 0.28f, 0.03f, 0.28f); // 반투명 화염색
            if (expandingFlameDebugMaterial.HasProperty("_BaseColor"))
            {
                expandingFlameDebugMaterial.SetColor("_BaseColor", color);
            }

            if (expandingFlameDebugMaterial.HasProperty("_Color"))
            {
                expandingFlameDebugMaterial.SetColor("_Color", color);
            }

            if (expandingFlameDebugMaterial.HasProperty("_Surface"))
            {
                expandingFlameDebugMaterial.SetFloat("_Surface", 1f); // URP Transparent
            }

            if (expandingFlameDebugMaterial.HasProperty("_Mode"))
            {
                expandingFlameDebugMaterial.SetFloat("_Mode", 3f); // Standard Transparent
            }

            if (expandingFlameDebugMaterial.HasProperty("_Blend"))
            {
                expandingFlameDebugMaterial.SetFloat("_Blend", 0f); // Alpha blend
            }

            expandingFlameDebugMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            expandingFlameDebugMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            expandingFlameDebugMaterial.SetInt("_ZWrite", 0);
            expandingFlameDebugMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            expandingFlameDebugMaterial.EnableKeyword("_ALPHABLEND_ON");
            expandingFlameDebugMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return expandingFlameDebugMaterial;
        }
    }
}
