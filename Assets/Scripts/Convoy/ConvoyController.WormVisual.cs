using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class ConvoyController
    {
        private static readonly string[] WormVisualNames =
        {
            "PlayerWorm",
            "PlayerWorm_Attack",
            "PlayerWorm_Mobility",
            "PlayerWorm_Support",
            "PlayerWorm_Magic"
        }; // 선택 후보

        private static readonly string[] StarterBodySampleNames =
        {
            "SegmentBody_StarterCannon",
            "SegmentBody_AttackWormStarter",
            "SegmentBody_MobilityWormStarter",
            "SegmentBody_SupportWormStarter",
            "SegmentBody_MagicWormStarter"
        }; // 씬 배치 샘플

        private void ApplySelectedWormVisualFromCurrentLoadout() // 현재 선택 외형
        {
            ApplySelectedWormVisual(RunLoadoutContext.CurrentStartBonus.SelectedWormId); // 컨텍스트 기준
        }

        private void ApplySelectedWormVisual(string wormId) // 캐릭터 외형 반영
        {
            HideSceneStarterBodySamples(); // 샘플 겹침 방지
            if (HeadVisual == null)
            {
                return; // 헤드 없음
            }

            string targetName = GetWormVisualName(wormId); // 선택 모델명
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 전체 후보
            Transform selected = FindPreferredWormVisual(transforms, targetName); // 대상 모델
            Transform layoutSource = FindHeadChildByName("PlayerWorm") ?? selected; // 기준 배치
            if (selected == null)
            {
                return; // 대상 없음
            }

            Vector3 localPosition = layoutSource != null ? layoutSource.localPosition : Vector3.zero; // 위치 기준
            Quaternion localRotation = layoutSource != null ? layoutSource.localRotation : Quaternion.identity; // 회전 기준
            Vector3 localScale = layoutSource != null ? layoutSource.localScale : Vector3.one; // 크기 기준

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform visual = transforms[i]; // 후보
                if (visual == null || !IsWormVisualName(visual.name))
                {
                    continue; // 대상 아님
                }

                bool active = visual == selected; // 선택 여부
                if (active)
                {
                    AttachWormVisualToHead(visual, localPosition, localRotation, localScale); // 헤드 밑으로 이동
                }

                visual.gameObject.SetActive(active); // 하나만 표시
            }
        }

        private void AttachWormVisualToHead(Transform visual, Vector3 localPosition, Quaternion localRotation, Vector3 localScale) // 헤드 모델 부착
        {
            if (visual == null || HeadVisual == null)
            {
                return; // 대상 없음
            }

            if (visual.parent != HeadVisual)
            {
                visual.SetParent(HeadVisual, false); // 헤드 자식화
            }

            visual.localPosition = localPosition; // 위치 통일
            visual.localRotation = localRotation; // 회전 통일
            visual.localScale = localScale; // 크기 통일
            visual.SetSiblingIndex(0); // 먼저 렌더링
        }

        private void HideSceneStarterBodySamples() // 씬 샘플 바디 숨김
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 전체 검색
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i]; // 후보
                if (candidate == null || !IsStarterBodySampleName(candidate.name))
                {
                    continue; // 대상 아님
                }

                if (SegmentRoot != null && IsDescendantOf(candidate, SegmentRoot))
                {
                    continue; // 실제 런타임 세그먼트
                }

                candidate.gameObject.SetActive(false); // 비교용 샘플 숨김
            }
        }

        private Transform FindHeadChildByName(string objectName) // 헤드 자식 검색
        {
            if (HeadVisual == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null; // 대상 없음
            }

            Transform[] children = HeadVisual.GetComponentsInChildren<Transform>(true); // 헤드 하위
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i]; // 후보
                if (child != null && string.Equals(child.name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return child; // 찾음
                }
            }

            return null; // 없음
        }

        private Transform FindPreferredWormVisual(Transform[] transforms, string objectName) // 선택 모델 우선 검색
        {
            Transform headChild = FindHeadChildByName(objectName); // 헤드 자식 우선
            if (headChild != null)
            {
                return headChild; // 이미 부착됨
            }

            if (transforms == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null; // 대상 없음
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i]; // 후보
                if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate; // 찾음
                }
            }

            return null; // 없음
        }

        private static bool IsWormVisualName(string objectName) // 지렁이 모델명 여부
        {
            for (int i = 0; i < WormVisualNames.Length; i++)
            {
                if (string.Equals(objectName, WormVisualNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true; // 모델명
                }
            }

            return false; // 아님
        }

        private static bool IsStarterBodySampleName(string objectName) // 샘플 이름 여부
        {
            for (int i = 0; i < StarterBodySampleNames.Length; i++)
            {
                if (string.Equals(objectName, StarterBodySampleNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true; // 샘플
                }
            }

            return false; // 아님
        }

        private static bool IsDescendantOf(Transform candidate, Transform root) // 하위 여부
        {
            Transform current = candidate; // 현재 노드
            while (current != null)
            {
                if (current == root)
                {
                    return true; // 포함
                }

                current = current.parent; // 부모 이동
            }

            return false; // 외부
        }

        private static string GetWormVisualName(string wormId) // 지렁이 모델명
        {
            switch (MetaWormIds.Normalize(wormId))
            {
                case MetaWormIds.Attack:
                    return "PlayerWorm_Attack"; // 공격형
                case MetaWormIds.Mobility:
                    return "PlayerWorm_Mobility"; // 이속형
                case MetaWormIds.Support:
                    return "PlayerWorm_Support"; // 지원형
                case MetaWormIds.Magic:
                    return "PlayerWorm_Magic"; // 마법형
                default:
                    return "PlayerWorm"; // 기본형
            }
        }
    }
}
