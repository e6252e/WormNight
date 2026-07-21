using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    internal static class LoadedProjectileVisualController // 장전 투사체 표시 공용 처리
    {
        public static Vector3 GetSpawnPosition(List<Transform> visuals, int projectileIndex, Vector3 fallbackPosition) // 장전 위치 우선
        {
            if (visuals != null && projectileIndex >= 0 && projectileIndex < visuals.Count)
            {
                Transform visual = visuals[projectileIndex]; // 장전탄
                if (visual != null)
                {
                    return visual.position; // 장전 위치
                }
            }

            return fallbackPosition; // 포구 fallback
        }

        public static void Restore(List<Transform> visuals, int visibleCount) // 장전 표시 복구
        {
            if (visuals == null)
            {
                return; // 목록 없음
            }

            for (int i = 0; i < visuals.Count; i++)
            {
                Transform visual = visuals[i]; // 장전탄
                if (visual != null)
                {
                    if (i < visibleCount)
                    {
                        LoadedProjectileRegrowVisual regrow = visual.GetComponent<LoadedProjectileRegrowVisual>();
                        if (regrow != null)
                        {
                            regrow.ShowImmediate();
                        }
                        else
                        {
                            visual.gameObject.SetActive(true); // 필요한 수만 표시
                        }
                    }
                    else
                    {
                        visual.gameObject.SetActive(false); // 필요한 수 아님
                    }
                }
            }
        }

        public static void Hide(List<Transform> visuals, int projectileIndex) // 발사된 장전 표시 숨김
        {
            if (visuals == null || projectileIndex < 0 || projectileIndex >= visuals.Count)
            {
                return; // 슬롯 없음
            }

            Transform visual = visuals[projectileIndex]; // 대상
            if (visual != null)
            {
                LoadedProjectileRegrowVisual regrow = visual.GetComponent<LoadedProjectileRegrowVisual>();
                if (regrow != null)
                {
                    regrow.HideImmediate(); // 발사 직후 0.01배 상태
                }
                else
                {
                    visual.gameObject.SetActive(false); // 발사됨
                }
            }
        }

        public static bool HasRegrowVisual(List<Transform> visuals, int visibleCount) // 서서히 재생성되는 장전 표시 여부
        {
            if (visuals == null)
            {
                return false;
            }

            int count = Mathf.Min(visibleCount, visuals.Count);
            for (int i = 0; i < count; i++)
            {
                Transform visual = visuals[i];
                if (visual != null && visual.GetComponent<LoadedProjectileRegrowVisual>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static void SetReloadProgress(List<Transform> visuals, int visibleCount, float progress) // 쿨타임 기반 재생성 진행
        {
            if (visuals == null)
            {
                return;
            }

            for (int i = 0; i < visuals.Count; i++)
            {
                Transform visual = visuals[i];
                if (visual == null)
                {
                    continue;
                }

                if (i >= visibleCount)
                {
                    visual.gameObject.SetActive(false);
                    continue;
                }

                LoadedProjectileRegrowVisual regrow = visual.GetComponent<LoadedProjectileRegrowVisual>();
                if (regrow != null)
                {
                    regrow.SetReloadProgress(progress);
                }
                else if (progress >= 1f)
                {
                    visual.gameObject.SetActive(true);
                }
            }
        }

        public static void Cache(Transform root, List<Transform> visuals) // 장전 표시 수집
        {
            if (visuals == null)
            {
                return; // 목록 없음
            }

            visuals.Clear(); // 재수집
            if (root == null)
            {
                return; // 표시 없음
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i); // 슬롯
                if (child != null)
                {
                    visuals.Add(child); // 순서 유지
                }
            }
        }

        public static Transform ResolveRoot(Transform cachedRoot, Transform searchRoot) // 장전 루트 찾기
        {
            if (cachedRoot != null)
            {
                return cachedRoot; // 수동 연결
            }

            Transform root = FindChildRecursive(searchRoot, "LoadedProjectiles"); // 정식 이름
            if (root == null)
            {
                root = FindChildRecursive(searchRoot, "MissileList"); // 기존 이름
            }

            if (root == null)
            {
                root = FindChildRecursive(searchRoot, "MisslieList"); // 오타 fallback
            }

            return root;
        }

        private static Transform FindChildRecursive(Transform root, string childName) // 이름 검색
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null; // 검색 불가
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i); // 하위
                if (child.name == childName)
                {
                    return child; // 발견
                }

                Transform found = FindChildRecursive(child, childName); // 재귀
                if (found != null)
                {
                    return found; // 발견
                }
            }

            return null; // 없음
        }
    }
}
