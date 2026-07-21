using System;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerHudWormProfilePresenter : MonoBehaviour // 선택 지렁이 프로필 HUD
    {
        [Header("Binding")]
        [SerializeField] private Image profileImage; // 표시 이미지
        [SerializeField] private MetaProgressionManager meta; // 타이틀/테스트 선택값
        [SerializeField] private bool autoResolveMeta = true; // 씬 검색 허용
        [SerializeField] private string fallbackWormId = MetaWormIds.Basic; // 기본 표시

        [Header("Sprites")]
        [SerializeField] private Sprite basicSprite; // 기본형
        [SerializeField] private Sprite attackSprite; // 공격형
        [SerializeField] private Sprite mobilitySprite; // 이속형
        [SerializeField] private Sprite supportSprite; // 지원형
        [SerializeField] private Sprite magicSprite; // 마법형

        private MetaProgressionManager subscribedMeta; // 이벤트 연결 대상
        private string activeWormId; // 현재 표시 ID

        private void Awake()
        {
            ResolveReferences();
            ConfigureImage();
            RefreshNow();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindMetaEvent();
            ConfigureImage();
            RefreshNow();
        }

        private void OnDisable()
        {
            UnbindMetaEvent();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            BindMetaEvent();
            RefreshIfChanged();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
            ConfigureImage();
            RefreshNow();
        }
#endif

        public void RefreshNow()
        {
            ApplyWorm(ResolveCurrentWormId());
        }

        private void RefreshIfChanged()
        {
            string wormId = MetaWormIds.Normalize(ResolveCurrentWormId());
            if (string.Equals(activeWormId, wormId, StringComparison.Ordinal))
            {
                return;
            }

            ApplyWorm(wormId);
        }

        private void ResolveReferences()
        {
            if (profileImage == null)
            {
                profileImage = GetComponent<Image>(); // 같은 오브젝트 우선
            }

            if (autoResolveMeta && meta == null)
            {
                meta = FindFirstObjectByType<MetaProgressionManager>(); // 타이틀/테스트 fallback
            }
        }

        private void BindMetaEvent()
        {
            if (subscribedMeta == meta)
            {
                return;
            }

            UnbindMetaEvent();
            if (meta == null)
            {
                return;
            }

            subscribedMeta = meta;
            subscribedMeta.SelectedWormChanged += OnSelectedWormChanged;
        }

        private void UnbindMetaEvent()
        {
            if (subscribedMeta == null)
            {
                return;
            }

            subscribedMeta.SelectedWormChanged -= OnSelectedWormChanged;
            subscribedMeta = null;
        }

        private void OnSelectedWormChanged(string wormId)
        {
            if (!RunLoadoutContext.HasStartBonus)
            {
                ApplyWorm(wormId); // 타이틀/테스트 씬 직접 변경
            }
        }

        private string ResolveCurrentWormId()
        {
            if (RunLoadoutContext.HasStartBonus)
            {
                return RunLoadoutContext.CurrentStartBonus.SelectedWormId; // 스테이지 시작값 우선
            }

            if (meta != null)
            {
                return meta.SelectedWormId; // 씬 선택값
            }

            return fallbackWormId; // 직접 실행 기본값
        }

        private void ApplyWorm(string wormId)
        {
            string normalized = MetaWormIds.Normalize(wormId);
            Sprite sprite = ResolveSprite(normalized);
            activeWormId = normalized;

            if (profileImage == null)
            {
                return;
            }

            profileImage.sprite = sprite;
            profileImage.enabled = sprite != null;
        }

        private Sprite ResolveSprite(string wormId)
        {
            switch (MetaWormIds.Normalize(wormId))
            {
                case MetaWormIds.Attack:
                    return attackSprite != null ? attackSprite : basicSprite; // 공격형
                case MetaWormIds.Mobility:
                    return mobilitySprite != null ? mobilitySprite : basicSprite; // 이속형
                case MetaWormIds.Support:
                    return supportSprite != null ? supportSprite : basicSprite; // 지원형
                case MetaWormIds.Magic:
                    return magicSprite != null ? magicSprite : basicSprite; // 마법형
                default:
                    return basicSprite; // 기본형
            }
        }

        private void ConfigureImage()
        {
            if (profileImage == null)
            {
                return;
            }

            profileImage.color = Color.white;
            profileImage.type = Image.Type.Simple;
            profileImage.preserveAspect = true;
            profileImage.raycastTarget = false;
        }
    }
}
