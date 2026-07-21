using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon
    {
        private bool ShouldFireProjectilesSequentially() // 순차 발사 여부
        {
            return AttackProfile != null && AttackProfile.FireProjectilesSequentially; // 장전 표시와 순차 발사는 별도 설정
        }

        private bool ShouldUseLoadedProjectileVisuals() // 장전 표시 사용 여부
        {
            return AttackProfile != null && AttackProfile.UseLoadedProjectileVisuals; // 프로필 설정
        }

        private Vector3 GetProjectileSpawnPosition(int projectileIndex, Transform muzzle) // 발사 위치
        {
            CacheLoadedProjectileVisuals(); // 표시 목록 보정
            Vector3 fallback = muzzle != null ? muzzle.position : transform.position + Vector3.up * AttackProfile.AttackSpawnHeight; // fallback
            return LoadedProjectileVisualController.GetSpawnPosition(loadedProjectileVisuals, projectileIndex, fallback); // 장전 위치 우선
        }

        private void UpdateLoadedProjectileReloadVisuals() // 장전 표시 복구
        {
            if (!ShouldUseLoadedProjectileVisuals() || loadedProjectilesRestored || fireIntervalDuration <= 0f)
            {
                return; // 복구 대상 없음
            }

            float progress = fireTimer <= 0f ? 1f : 1f - Mathf.Clamp01(fireTimer / fireIntervalDuration); // 쿨타임 진행률
            int count = AttackProfile != null ? Mathf.Max(1, AttackProfile.ProjectileCount) : loadedProjectileVisuals.Count; // 표시 수
            if (LoadedProjectileVisualController.HasRegrowVisual(loadedProjectileVisuals, count))
            {
                float reloadStart = Mathf.Clamp01(AttackProfile.LoadedProjectileReloadRatio);
                float reloadProgress = reloadStart >= 1f ? 1f : Mathf.InverseLerp(reloadStart, 1f, progress);
                LoadedProjectileVisualController.SetReloadProgress(loadedProjectileVisuals, count, reloadProgress); // 쿨타임에 맞춰 0.01배에서 1배까지 성장
                loadedProjectilesRestored = reloadProgress >= 1f;
                return;
            }

            if (progress >= AttackProfile.LoadedProjectileReloadRatio)
            {
                RestoreLoadedProjectileVisuals(); // 50% 시점 복구
            }
        }

        private void RestoreLoadedProjectileVisuals() // 장전 표시 전체 복구
        {
            CacheLoadedProjectileVisuals(); // 목록 보정
            int count = AttackProfile != null ? Mathf.Max(1, AttackProfile.ProjectileCount) : loadedProjectileVisuals.Count; // 표시 수
            LoadedProjectileVisualController.Restore(loadedProjectileVisuals, count); // 공용 복구
            loadedProjectilesRestored = true; // 복구 완료
        }

        private void HideLoadedProjectileVisual(int projectileIndex) // 사용한 장전 표시 숨김
        {
            if (!ShouldUseLoadedProjectileVisuals())
            {
                return; // 장전 표시 미사용
            }

            CacheLoadedProjectileVisuals(); // 목록 보정
            LoadedProjectileVisualController.Hide(loadedProjectileVisuals, projectileIndex); // 공용 숨김
        }

        private void CacheLoadedProjectileVisuals() // 장전 표시 수집
        {
            Transform root = ResolveLoadedProjectileRoot(); // 표시 루트
            LoadedProjectileVisualController.Cache(root, loadedProjectileVisuals); // 공용 수집
        }

        private Transform ResolveLoadedProjectileRoot() // 장전 표시 루트 찾기
        {
            Transform pivot = ResolveHeadYawPivot(); // 머리 기준
            Transform root = pivot != null ? pivot : (Segment != null ? Segment.transform : transform); // 검색 루트
            LoadedProjectileRoot = LoadedProjectileVisualController.ResolveRoot(LoadedProjectileRoot, root); // 공용 검색
            return LoadedProjectileRoot;
        }
    }
}
