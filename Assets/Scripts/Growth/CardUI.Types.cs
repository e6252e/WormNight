using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DG.Tweening;
using TeamProject01.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class CardUI
{
    private enum SegmentWeaponStatViewTarget // 스탯 UI 표시 대상
    {
        Cannon = 0, // SG01_Cannon
        Missile = 1, // SG02_Missile
        Trebuchet = 2, // SG03_Trebuchet
        SawLauncher = 3, // SG04_SawLauncher
        Flamethrower = 4, // SG05_Flamethrower
        LightningObelisk = 5, // SG20_LightningObelisk
        FireballTower = 6 // SG21_FireballTower
    }

    [System.Flags]
    private enum SegmentWeaponStatDisplayFlags
    {
        None = 0,
        BaseDamage = 1 << 0,
        ProjectileSpeed = 1 << 1,
        SearchRange = 1 << 2,
        Cooldown = 1 << 3,
        ProjectileCount = 1 << 4,
        PierceCount = 1 << 5,
        ExplosionRadius = 1 << 6,
        MaxChainDepth = 1 << 7,
        ChainRange = 1 << 8,
        ChainDamageFalloff = 1 << 9,
        SideConeAngle = 1 << 10,
        LaserDuration = 1 << 11,
        LaserTickInterval = 1 << 12,
        LandingRollDistance = 1 << 13,
        LandingRollDuration = 1 << 14,
        SawPierceDamageRatio = 1 << 15
    }

    private readonly struct SegmentWeaponStatDebugContext
    {
        public readonly string SegmentId;
        public readonly string Title;
        public readonly int Level;
        public readonly SegmentAttackProfile Profile;
        public readonly WeaponStatBonusData Bonus;
        public readonly SegmentWeaponStatDisplayFlags DisplayFlags;

        public SegmentWeaponStatDebugContext(
            string segmentId,
            string title,
            int level,
            SegmentAttackProfile profile,
            WeaponStatBonusData bonus,
            SegmentWeaponStatDisplayFlags displayFlags)
        {
            SegmentId = segmentId;
            Title = title;
            Level = level;
            Profile = profile;
            Bonus = bonus;
            DisplayFlags = displayFlags;
        }

        public bool HasProfile => Profile != null;
    }
    // 건춘추가 - 0621 ======

    private enum LevelUpCardPhase
    {
        Upgrade = 0, // 공통 강화 + 보유 세그먼트 무기 강화 통합 풀
        StatUpgrade = 1, // 스탯 강화 단독 fallback
        WeaponEnhance = 2, // 세그먼트 무기 강화 단독 fallback
        SegmentAction = 3 // 세그먼트 추가/레벨업
    }

    private enum CardPanelMode
    {
        LevelUp = 0, // 경험치 레벨업 카드
        RewardChoice = 1, // 상자/보상 선택 카드
        SegmentTicketChoice = 2 // 세그먼트 선택권으로 열린 카드
    }

    private enum RewardChoiceKind
    {
        None = 0,
        Gold = 1,
        Experience = 2,
        SegmentChoiceTicket = 3
    }

    // 세그먼트 ADD 풀에서 후보/액션 카드 구분
    private enum SegmentCardRole
    {
        None = 0, // 일반 카드
        Candidate = 1, // 세그먼트 후보 카드
        AddAction = 2, // 세그먼트 추가 카드
        LevelUpAction = 3, // 세그먼트 레벨업 카드
        Empty = 4, // 후보 없음 카드
        EnhanceChoice = 5 // 무기 강화 선택 카드 (2단계)
    }

    private enum CommonStatSegmentFilter
    {
        All = 0, // 모든 공격 세그먼트
        Melee = 1, // 밀리 공격 세그먼트
        Magic = 2 // 마법 공격 세그먼트
    }

    private struct WeightedPrefabEntry // 가중치 뽑기용 임시 구조체
    {
        public GameObject Prefab; // 카드 프리팹
        public float Weight; // 등장 가중치
    }

    private struct WeightedDefinitionEntry // 데이터 에셋 가중치 뽑기용 임시 구조체
    {
        public StatUpgradeDefinition Definition; // 카드 데이터
        public float Weight; // 등장 가중치
    }

    private struct WeightedSegmentCatalogEntry // A 모드 세그먼트 선택 가중치용
    {
        public SegmentCatalogEntry Entry; // 카탈로그 후보
        public float Weight; // 등장 가중치
    }

    private enum UpgradePoolCardKind
    {
        StatDefinition = 0, // 데이터 에셋 기반 공통 강화
        StatPrefab = 1, // 기존 프리팹 기반 공통 강화
        WeaponEnhancement = 2 // 세그먼트 무기 강화
    }

    private struct WeightedUpgradePoolEntry // 공통+무기 강화 통합 풀 후보
    {
        public UpgradePoolCardKind Kind; // 후보 종류
        public StatUpgradeDefinition StatDefinition; // 데이터 에셋 공통 강화
        public GameObject StatPrefab; // 프리팹 공통 강화
        public WeaponDefinition WeaponDefinition; // 무기 강화
        public float Weight; // 등장 가중치
    }

    private sealed class SpawnedCardEntry
    {
        public GameObject Root;
        public RectTransform RootTransform;
        public CanvasGroup CanvasGroup;
        public StatUpgrade StatUpgrade;
        public StatUpgradeDefinition StatUpgradeDefinition; // 데이터 에셋 기반 공통 강화 카드
        public SegmentAddCard SegmentAddCard;
        public GameObject SourcePrefab; // 생성에 사용한 프리팹 (선택 가중치용)
        public Vector2 OriginalPosition;
        public Vector3 OriginalScale;
        public bool IsClickable;
        // 세그먼트 ADD 흐름용 역할
        public SegmentCardRole SegmentRole;
        // 세그먼트 ADD 흐름용 카탈로그 데이터
        public SegmentCatalogEntry SegmentCatalogEntry;
        // 세그먼트 ADD 흐름용 대상 ID
        public string SegmentId;
        // 세그먼트 ADD 흐름용 레벨 소비량
        public int LevelDelta = 1;
        // 2단계 무기 강화 선택 데이터
        public WeaponDefinition WeaponDefinition;
        // 건준수정 - 0621 ======
        public StatUpgrade.StatCardTier WeaponEnhancementTier = StatUpgrade.StatCardTier.Normal; // 레어/유니크 등급별 수치
        // 건준수정 - 0621 ======
        public RewardChoiceKind RewardChoice = RewardChoiceKind.None; // 보상 선택 카드 종류
        public StatUpgrade.StatCardTier RewardTier = StatUpgrade.StatCardTier.Normal; // 보상 카드 등급
        public int RewardAmount; // 골드/경험치 지급량
        public int RewardTicketCount; // 세그먼트 선택권 횟수
        public GameObject CardTooltipRoot; // 프리팹에 배치된 카드 툴팁 루트
        public CanvasGroup CardTooltipCanvasGroup; // 툴팁 페이드
        public TMP_Text CardTooltipText; // 카드 툴팁 텍스트
        public bool TooltipReady; // 오픈 트윈 완료 후에만 true
        public bool IsPointerOver; // 현재 포인터가 카드 위인지
        public bool HasTooltipPointer; // 툴팁 배치용 좌표 보유 여부
        public bool IsHoverVisualActive; // 호버 확대 중복 방지
        public Vector2 LastTooltipScreenPosition; // 마지막 포인터 화면 좌표
        public Camera LastTooltipCamera; // 마지막 포인터 이벤트 카메라
        // 없음/불가 카드 클릭 차단
        public bool CanSelect = true;
    }

    private sealed class CardInstanceBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private CardUI manager;
        private SpawnedCardEntry entry;

        public void Initialize(CardUI owner, SpawnedCardEntry spawnedEntry)
        {
            manager = owner;
            entry = spawnedEntry;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (manager != null && manager.IsAutoSelectInProgress)
            {
                return;
            }

            manager?.NotifySpawnedCardPointerEnter(entry, eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (manager != null && manager.IsAutoSelectInProgress)
            {
                return;
            }

            manager?.NotifySpawnedCardPointerMove(entry, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (manager != null && manager.IsAutoSelectInProgress)
            {
                return;
            }

            manager?.NotifySpawnedCardPointerExit(entry, eventData);
        }
    }

    private sealed class CardChildInputBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
    {
        private CardUI manager;
        private SpawnedCardEntry entry;

        public void Initialize(CardUI owner, SpawnedCardEntry spawnedEntry)
        {
            manager = owner;
            entry = spawnedEntry;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (manager != null && manager.IsAutoSelectInProgress)
            {
                return;
            }

            manager?.NotifySpawnedCardPointerEnter(entry, eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (manager != null && manager.IsAutoSelectInProgress)
            {
                return;
            }

            manager?.NotifySpawnedCardPointerMove(entry, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (manager != null && manager.IsAutoSelectInProgress)
            {
                return;
            }

            manager?.NotifySpawnedCardPointerExit(entry, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (manager != null && manager.IsAutoSelectInProgress)
            {
                return;
            }

            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            manager?.NotifySpawnedCardClicked(entry);
        }
    }

    // 안건준 추가 - 0622
    private sealed class SegmentListHoverBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CardUI manager;

        public void Initialize(CardUI owner)
        {
            manager = owner;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            manager?.NotifySegmentListHoverEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            manager?.NotifySegmentListHoverExit();
        }
    }

    private sealed class RerollButtonHoverBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CardUI manager;

        public void Initialize(CardUI owner)
        {
            manager = owner;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            manager?.NotifyRerollButtonPointerEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            manager?.NotifyRerollButtonPointerExit();
        }
    }
}
