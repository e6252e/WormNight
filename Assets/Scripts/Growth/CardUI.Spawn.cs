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
    private void ResolveManagerReferences()
    {
        if (levelUpUi == null)
        {
            levelUpUi = FindFirstObjectByType<LevelUpUi>();
        }

        if (levelUpPanelCanvasGroup == null && levelUpUi != null)
        {
            levelUpPanelCanvasGroup = levelUpUi.GetComponent<CanvasGroup>();
        }
    }

    private bool IsLevelUpPanelOpen()
    {
        return levelUpPanelCanvasGroup != null
            && levelUpPanelCanvasGroup.blocksRaycasts
            && levelUpPanelCanvasGroup.interactable;
    }

    private void SpawnLevelUpCards()
    {
        ClearSpawnedCards();
        rerollAllowedForCurrentChoices = false; // 기본 비활성

        if (cardSlots == null || cardSlots.Length == 0)
        {
            Debug.LogWarning("[CardUI] 카드 생성 슬롯이 비어 있습니다.", this);
            RefreshRerollUi();
            return;
        }

        if (activePanelMode == CardPanelMode.RewardChoice)
        {
            SpawnRewardChoiceCards(); // 골드/경험치/세그먼트 선택권 3장 고정
            RefreshRerollUi();
            return;
        }

        currentSpawnPhase = activePanelMode == CardPanelMode.SegmentTicketChoice
            ? LevelUpCardPhase.SegmentAction // 선택권은 기존 세그먼트 선택 흐름만 사용
            : ResolveLevelUpCardPhase(); // 레벨 구간별 세그먼트 선택/강화풀 로테이션
        rerollAllowedForCurrentChoices = true; // 1차 랜덤 선택지만 리롤 가능
        SpawnCardsForCurrentPhase();
        RefreshRerollUi();
    }

    private void SpawnCardsForCurrentPhase()
    {
        switch (currentSpawnPhase)
        {
            case LevelUpCardPhase.Upgrade:
                SpawnUpgradePoolCards(); // 공통 강화 + 보유 세그먼트 무기 강화 통합 풀
                return;
            case LevelUpCardPhase.WeaponEnhance:
                if (useSegmentSelectWeaponEnhanceFlow)
                {
                    SpawnWeaponEnhanceCandidateCards(); // A: 세그먼트 선택 1단계
                }
                else
                {
                    SpawnRandomWeaponEnhancementCards(); // B: 강화 카드 랜덤 (세그먼트 선택 없음)
                }

                return;
            case LevelUpCardPhase.SegmentAction:
                SpawnSegmentCandidateCards(); // 1단계: 세그먼트 후보 → 추가/레벨업
                return;
            default:
                SpawnStatUpgradeCards(); // 스탯 강화 3장
                return;
        }
    }

    private void SpawnStatUpgradeCards()
    {
        List<StatUpgradeDefinition> definitionPool = BuildStatUpgradeDefinitionPool(); // 데이터 에셋 카드 풀
        if (definitionPool.Count > 0)
        {
            SpawnStatUpgradeDefinitionCards(definitionPool);
            return;
        }

        GameObject[] sourcePrefabs = BuildStatUpgradeSourcePrefabs(); // 데이터 카탈로그 없을 때 기존 프리팹 fallback
        string poolName = "Stat Upgrade"; // 로그용 풀 이름

        if (sourcePrefabs == null || sourcePrefabs.Length == 0)
        {
            Debug.LogWarning($"[CardUI] {poolName} 카드 프리팹이 비어 있습니다.", this);
            return;
        }

        List<GameObject> pool = BuildPrefabPool(sourcePrefabs);
        if (pool.Count == 0)
        {
            return;
        }

        int spawnCount = Mathf.Min(cardsToSpawn, cardSlots.Length, pool.Count);
        List<GameObject> picked = PickWeightedStatPrefabs(pool, spawnCount); // 가중치 랜덤으로 3장 선택

        for (int i = 0; i < picked.Count; i++)
        {
            SpawnedCardEntry entry = CreateStatUpgradeCard(picked[i], cardSlots[i]); // 등급·프리팹 resolve 후 생성
            if (entry != null)
            {
                spawnedCards.Add(entry); // 생성 목록 등록
            }
        }

        PlaySpawnOpenTween(spawnedCards); // 등장 연출
    }

    private void SpawnStatUpgradeDefinitionCards(List<StatUpgradeDefinition> pool)
    {
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[CardUI] Stat Upgrade 데이터 에셋 풀이 비어 있습니다.", this);
            return;
        }

        int spawnCount = Mathf.Min(cardsToSpawn, cardSlots.Length, pool.Count);
        List<StatUpgradeDefinition> picked = PickWeightedStatDefinitions(pool, spawnCount); // 데이터 에셋 가중치 선택
        for (int i = 0; i < picked.Count; i++)
        {
            SpawnedCardEntry entry = CreateStatUpgradeCard(picked[i], cardSlots[i]); // 데이터 에셋 기반 생성
            if (entry != null)
            {
                spawnedCards.Add(entry);
            }
        }

        PlaySpawnOpenTween(spawnedCards);
    }

    private void SpawnUpgradePoolCards()
    {
        List<WeightedUpgradePoolEntry> pool = BuildUpgradePoolEntries(); // 공통+무기 강화 통합 후보
        int spawnCount = Mathf.Min(cardsToSpawn, cardSlots.Length); // 표시할 카드 수
        if (pool.Count == 0)
        {
            Debug.LogWarning("[CardUI] 강화풀 카드 후보가 비어 있습니다.", this);
            return;
        }

        List<WeightedUpgradePoolEntry> picked = PickWeightedUpgradePoolEntries(pool, spawnCount); // 통합 풀에서 3장
        for (int i = 0; i < spawnCount; i++)
        {
            RectTransform slot = cardSlots[i]; // 슬롯별 배치
            if (slot == null || i >= picked.Count)
            {
                continue; // 슬롯/후보 없음
            }

            SpawnedCardEntry entry = CreateUpgradePoolCard(picked[i], slot, i); // 후보 종류별 카드 생성
            if (entry == null)
            {
                continue; // 생성 실패
            }

            spawnedCards.Add(entry); // 생성 목록 등록
        }

        PlaySpawnOpenTween(spawnedCards); // 등장 연출
    }

    private List<WeightedUpgradePoolEntry> BuildUpgradePoolEntries()
    {
        List<WeightedUpgradePoolEntry> results = new List<WeightedUpgradePoolEntry>(); // 통합 후보
        List<StatUpgradeDefinition> statDefinitions = BuildStatUpgradeDefinitionPool(); // 데이터 에셋 우선
        if (statDefinitions.Count > 0)
        {
            for (int i = 0; i < statDefinitions.Count; i++)
            {
                StatUpgradeDefinition definition = statDefinitions[i]; // 공통 강화 데이터
                if (definition == null)
                {
                    continue; // null 제외
                }

                results.Add(new WeightedUpgradePoolEntry
                {
                    Kind = UpgradePoolCardKind.StatDefinition,
                    StatDefinition = definition,
                    Weight = ResolveStatDefinitionWeight(definition)
                });
            }
        }
        else
        {
            GameObject[] statPrefabs = BuildStatUpgradeSourcePrefabs(); // 기존 프리팹 fallback
            for (int i = 0; statPrefabs != null && i < statPrefabs.Length; i++)
            {
                GameObject prefab = statPrefabs[i]; // 공통 강화 프리팹
                if (prefab == null)
                {
                    continue; // null 제외
                }

                results.Add(new WeightedUpgradePoolEntry
                {
                    Kind = UpgradePoolCardKind.StatPrefab,
                    StatPrefab = prefab,
                    Weight = ResolveStatPrefabWeight(prefab)
                });
            }
        }

        List<WeaponDefinition> weaponDefinitions = new List<WeaponDefinition>(); // 보유 세그먼트 무기 강화
        BuildWeaponEnhancementPool(weaponDefinitions, ownedSegmentsOnly: true);
        for (int i = 0; i < weaponDefinitions.Count; i++)
        {
            WeaponDefinition definition = weaponDefinitions[i]; // 무기 강화 데이터
            if (definition == null || !definition.HasAnyStatBonus || !definition.HasTarget)
            {
                continue; // 적용 불가 제외
            }

            results.Add(new WeightedUpgradePoolEntry
            {
                Kind = UpgradePoolCardKind.WeaponEnhancement,
                WeaponDefinition = definition,
                Weight = baseCardSpawnWeight
            });
        }

        return results;
    }

    private float ResolveStatDefinitionWeight(StatUpgradeDefinition definition)
    {
        float weight = baseCardSpawnWeight; // 기본 가중치
        if (lastSelectedStatCardDefinition != null && definition == lastSelectedStatCardDefinition)
        {
            weight += selectedCardWeightBonus; // 직전 선택 공통 카드 보정
        }

        return weight;
    }

    private float ResolveStatPrefabWeight(GameObject prefab)
    {
        float weight = baseCardSpawnWeight; // 기본 가중치
        if (lastSelectedStatCardPrefab != null && prefab == lastSelectedStatCardPrefab)
        {
            weight += selectedCardWeightBonus; // 직전 선택 공통 카드 보정
        }

        return weight;
    }

    private SpawnedCardEntry CreateUpgradePoolCard(WeightedUpgradePoolEntry candidate, RectTransform slot, int slotIndex)
    {
        switch (candidate.Kind)
        {
            case UpgradePoolCardKind.StatDefinition:
                return CreateStatUpgradeCard(candidate.StatDefinition, slot); // 공통 강화 데이터
            case UpgradePoolCardKind.StatPrefab:
                return CreateStatUpgradeCard(candidate.StatPrefab, slot); // 공통 강화 프리팹
            case UpgradePoolCardKind.WeaponEnhancement:
                GameObject template = GetSegmentCardTemplate(slotIndex); // 세그먼트 강화 카드 양식
                return CreateWeaponEnhancementCard(candidate.WeaponDefinition, template, slotIndex, slot, 1); // 보유 세그먼트 무기 강화
            default:
                return null;
        }
    }

    private static List<WeightedUpgradePoolEntry> PickWeightedUpgradePoolEntries(List<WeightedUpgradePoolEntry> pool, int count)
    {
        List<WeightedUpgradePoolEntry> picked = new List<WeightedUpgradePoolEntry>(Mathf.Max(0, count)); // 선택 결과
        if (pool == null || pool.Count == 0 || count <= 0)
        {
            return picked; // 후보 없음
        }

        List<WeightedUpgradePoolEntry> remaining = new List<WeightedUpgradePoolEntry>(pool); // 1차 중복 방지
        while (picked.Count < count && remaining.Count > 0)
        {
            if (!TryPickWeightedUpgradePoolEntry(remaining, out int selectedIndex))
            {
                break; // 선택 실패
            }

            picked.Add(remaining[selectedIndex]); // 선택 후보 추가
            remaining.RemoveAt(selectedIndex); // 같은 카드 우선 중복 방지
        }

        while (picked.Count < count && pool.Count > 0)
        {
            if (!TryPickWeightedUpgradePoolEntry(pool, out int selectedIndex))
            {
                break; // fallback 실패
            }

            picked.Add(pool[selectedIndex]); // 풀 부족 시 중복 허용
        }

        return picked;
    }

    private static bool TryPickWeightedUpgradePoolEntry(List<WeightedUpgradePoolEntry> pool, out int selectedIndex)
    {
        selectedIndex = -1;
        if (pool == null || pool.Count == 0)
        {
            return false; // 후보 없음
        }

        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            totalWeight += Mathf.Max(0f, pool[i].Weight); // 음수 방지
        }

        if (totalWeight <= 0f)
        {
            selectedIndex = pool.Count - 1; // fallback
            return true;
        }

        float roll = Random.Range(0f, totalWeight); // 0~합계 난수
        float cumulative = 0f;
        selectedIndex = pool.Count - 1; // 부동소수 오차 fallback
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += Mathf.Max(0f, pool[i].Weight);
            if (roll < cumulative)
            {
                selectedIndex = i;
                return true;
            }
        }

        return true;
    }

    private List<StatUpgradeDefinition> BuildStatUpgradeDefinitionPool()
    {
        List<StatUpgradeDefinition> results = new List<StatUpgradeDefinition>();
        StatUpgradeCatalogAsset catalog = GetStatUpgradeCatalog();
        if (catalog != null)
        {
            catalog.AppendValidDefinitions(results);
        }

        return results;
    }

    private GameObject[] BuildStatUpgradeSourcePrefabs() // 공통카드 풀 병합
    {
        List<GameObject> results = new List<GameObject>();
        AppendUniquePrefabs(results, statUpgradeCards); // 씬에 연결된 기존 카드
        CardUiPrefabReferences references = GetPrefabReferences();
        if (references != null)
        {
            AppendUniquePrefabs(results, references.ExtraStatUpgradeCards); // 중앙 추가 카드
        }

        return results.ToArray();
    }

    private StatUpgradeCatalogAsset GetStatUpgradeCatalog()
    {
        if (statUpgradeCatalogAsset != null)
        {
            return statUpgradeCatalogAsset; // 인스펙터 직접 연결
        }

        CardUiPrefabReferences references = GetPrefabReferences();
        return references != null ? references.StatUpgradeCatalog : null; // Resources 중앙 참조
    }

    private static void AppendUniquePrefabs(List<GameObject> results, GameObject[] prefabs)
    {
        if (results == null || prefabs == null)
        {
            return;
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab != null && !results.Contains(prefab))
            {
                results.Add(prefab); // 중복 방지
            }
        }
    }

    private SpawnedCardEntry CreateStatUpgradeCard(GameObject sourcePrefab, RectTransform slot)
    {
        if (sourcePrefab == null || slot == null)
        {
            return null; // 프리팹/슬롯 없음
        }

        StatUpgrade templateStat = GetStatUpgradePresentation(sourcePrefab); // statUpgradeCards 풀 프리팹
        StatUpgrade.StatCardTier tier = StatUpgrade.RollTier(rareCardChancePercent, uniqueCardChancePercent); // 등급 선정
        StatUpgrade.CardSpawnResolve resolve = templateStat != null
            ? templateStat.ResolveCardSpawn(tier, sourcePrefab)
            : new StatUpgrade.CardSpawnResolve(sourcePrefab); // StatUpgrade 없으면 기본
        GameObject spawnPrefab = resolve.Prefab != null ? resolve.Prefab : sourcePrefab; // fallback
        SpawnedCardEntry entry = CreateSpawnedCard(spawnPrefab, slot, sourcePrefab, skipStatUpgradeRoll: true); // 생성
        if (entry == null)
        {
            return null; // 생성 실패
        }

        if (entry.StatUpgrade != null)
        {
            if (templateStat != null && spawnPrefab != sourcePrefab)
            {
                entry.StatUpgrade.CopyStatValuesFrom(templateStat); // 등급 프리팹 → 풀 프리팹 수치 복사
            }

            entry.StatUpgrade.ApplySpawnTier(tier); // 등급·배율 반영
            ApplyTierCardFrame(entry.Root, tier); // 등급별 카드 프레임 교체
            ApplyStatUpgradeCardPresentation(entry.Root, entry.StatUpgrade, tier); // 공통카드 이름/설명 및 (N) 치환
        }

        return entry;
    }

    private SpawnedCardEntry CreateStatUpgradeCard(StatUpgradeDefinition definition, RectTransform slot)
    {
        if (definition == null || slot == null)
        {
            return null;
        }

        GameObject templatePrefab = ResolveStatUpgradeTemplatePrefab(definition);
        if (templatePrefab == null)
        {
            Debug.LogWarning($"[CardUI] 공통 강화 카드 템플릿 프리팹이 없습니다. card={definition.name}", definition);
            return null;
        }

        StatUpgrade.StatCardTier tier = StatUpgrade.RollTier(rareCardChancePercent, uniqueCardChancePercent);
        SpawnedCardEntry entry = CreateSpawnedCard(templatePrefab, slot, templatePrefab, skipStatUpgradeRoll: true);
        if (entry == null)
        {
            return null;
        }

        entry.StatUpgradeDefinition = definition;
        if (entry.StatUpgrade != null)
        {
            entry.StatUpgrade.ConfigureFromDefinition(definition, tier); // 데이터 에셋 주입
            ApplyTierCardFrame(entry.Root, tier);
            ApplyStatUpgradeCardPresentation(entry.Root, entry.StatUpgrade, tier);
            ApplyStatUpgradeCardIcon(entry.Root, definition);
        }

        return entry;
    }

    private GameObject ResolveStatUpgradeTemplatePrefab(StatUpgradeDefinition definition)
    {
        if (definition != null && definition.CardPrefabOverride != null)
        {
            return definition.CardPrefabOverride; // 카드별 예외 프리팹
        }

        StatUpgradeCatalogAsset catalog = GetStatUpgradeCatalog();
        if (catalog != null && catalog.DefaultCardPrefab != null)
        {
            return catalog.DefaultCardPrefab; // 데이터 카탈로그 기본 템플릿
        }

        for (int i = 0; statUpgradeCards != null && i < statUpgradeCards.Length; i++)
        {
            if (statUpgradeCards[i] != null)
            {
                return statUpgradeCards[i]; // 기존 인스펙터 연결 fallback
            }
        }

        GameObject[] fallbackPrefabs = GetPrefabReferences()?.ExtraStatUpgradeCards;
        for (int i = 0; fallbackPrefabs != null && i < fallbackPrefabs.Length; i++)
        {
            if (fallbackPrefabs[i] != null)
            {
                return fallbackPrefabs[i]; // 중앙 프리팹 fallback
            }
        }

        return null;
    }

    private static StatUpgrade GetStatUpgradePresentation(GameObject prefab) // statUpgradeCards 프리팹의 StatUpgrade (Instantiate 전)
    {
        if (prefab == null)
        {
            return null; // 프리팹 없음
        }

        StatUpgrade presentation = prefab.GetComponent<StatUpgrade>(); // 루트
        if (presentation != null)
        {
            return presentation;
        }

        return prefab.GetComponentInChildren<StatUpgrade>(true); // 자식 fallback
    }

    private static LevelUpCardPhase ResolveLevelUpCardPhase() // 현재 레벨 → 이번 카드 종류
    {
        CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
        int level = core != null ? Mathf.Max(1, core.CurrentLevel) : 1; // 선택 전 현재 레벨
        if (level <= 5)
        {
            return LevelUpCardPhase.SegmentAction; // 1~5: 세그먼트 선택만
        }

        if (level <= 20)
        {
            int index = level - 6; // 6레벨부터 S/S/U 반복
            return index % 3 < 2 ? LevelUpCardPhase.SegmentAction : LevelUpCardPhase.Upgrade;
        }

        if (level <= 30)
        {
            int index = level - 21; // 21레벨부터 S/U 반복
            return index % 2 == 0 ? LevelUpCardPhase.SegmentAction : LevelUpCardPhase.Upgrade;
        }

        if (level <= 45)
        {
            int index = level - 31; // 31레벨부터 S/U/U 반복
            return index % 3 == 0 ? LevelUpCardPhase.SegmentAction : LevelUpCardPhase.Upgrade;
        }

        return LevelUpCardPhase.Upgrade; // 46+: 강화풀만
    }

    // 세그먼트 ADD 풀: 카탈로그 후보 3장 생성, 부족하면 없음 카드 표시
    private void SpawnSegmentCandidateCards()
    {
        GameObject template = GetSegmentChoiceCardTemplate(); // 세그먼트 선택 전용 프리팹
        if (template == null)
        {
            Debug.LogWarning("[CardUI] Add Segment 카드 프리팹이 비어 있습니다.", this); // 템플릿 누락
            return;
        }

        List<SegmentCatalogEntry> candidates = new List<SegmentCatalogEntry>(); // 카탈로그 후보
        if (CoreStatProvider.Active != null)
        {
            CoreStatProvider.Active.TryGetSegmentChoiceCandidates(candidates); // 추가/레벨업 가능한 후보 수집
        }

        int spawnCount = Mathf.Min(cardsToSpawn, cardSlots.Length); // 표시할 카드 수
        List<SegmentCatalogEntry> picked = PickSegmentChoiceEntriesWithOwnedChance(candidates, spawnCount); // 보유 50% + 지원형 0~1장 제한
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = GetSegmentChoiceCardTemplate(); // 후보 선택 1단계 전용 템플릿
            SpawnedCardEntry entry = CreateSpawnedCard(prefab, cardSlots[i]); // 카드 생성
            if (entry == null)
            {
                continue; // 생성 실패
            }

            if (i < picked.Count)
            {
                ConfigureSegmentCandidateEntry(entry, picked[i]); // 실제 후보 카드
                ApplySegmentChoiceTicketOverrides(entry); // 선택권 모드면 레벨/경험치 소비 제거
            }
            else
            {
                ConfigureEmptySegmentEntry(entry); // 후보 부족 → 없음 카드
            }

            spawnedCards.Add(entry); // 생성 목록 등록
        }

        PlaySpawnOpenTween(spawnedCards); // 기존 DOTween 등장 연출 재사용
    }

    // 무기 강화 1단계 - 강화 가능한 세그먼트 후보 3장
    private void SpawnWeaponEnhanceCandidateCards()
    {
        GameObject template = GetSegmentChoiceCardTemplate(); // 세그먼트 선택 전용 프리팹
        if (template == null)
        {
            Debug.LogWarning("[CardUI] Add Segment 카드 프리팹이 비어 있습니다.", this);
            return;
        }

        List<SegmentCatalogEntry> candidates = new List<SegmentCatalogEntry>(); // 강화 후보
        if (CoreStatProvider.Active != null)
        {
            CoreStatProvider.Active.TryGetWeaponEnhanceChoiceCandidates(candidates); // Segment Catalog 풀
        }

        int spawnCount = Mathf.Min(cardsToSpawn, cardSlots.Length); // 표시할 카드 수
        Dictionary<string, int> ownedCountsBySegmentId = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase); // ID별 보유 개수
        ConvoyController convoy = CoreStatProvider.Active != null ? CoreStatProvider.Active.Convoy : null;
        if (convoy != null)
        {
            convoy.CollectAttachedSegmentCounts(ownedCountsBySegmentId); // 캐논 5개 등 집계
        }

        List<SegmentCatalogEntry> picked = PickWeightedWeaponEnhanceSegmentEntries(candidates, spawnCount, ownedCountsBySegmentId); // 보유 개수 가중치
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = GetSegmentChoiceCardTemplate(); // 후보 선택 1단계 전용 템플릿
            SpawnedCardEntry entry = CreateSpawnedCard(prefab, cardSlots[i]); // 카드 생성
            if (entry == null)
            {
                continue; // 생성 실패
            }

            if (i < picked.Count)
            {
                ConfigureWeaponEnhanceCandidateEntry(entry, picked[i]); // 강화 대상 세그먼트
            }
            else
            {
                ConfigureEmptySegmentEntry(entry); // 후보 부족
            }

            spawnedCards.Add(entry); // 생성 목록 등록
        }

        PlaySpawnOpenTween(spawnedCards); // 등장 연출
    }

    // B 기준 — 보유 세그먼트 강화만 랜덤 3장 (세그먼트 선택 단계 없음)
    private void SpawnRandomWeaponEnhancementCards()
    {
        List<WeaponDefinition> pool = new List<WeaponDefinition>(); // 카탈로그 강화 풀
        BuildWeaponEnhancementPool(pool, ownedSegmentsOnly: true); // B: Convoy에 붙은 세그먼트 ID만
        int resolvedLevelDelta = 1; // 레벨업 1회 소비
        int spawnCount = Mathf.Min(cardsToSpawn, cardSlots.Length); // 표시할 카드 수
        List<WeaponDefinition> picked = PickRandomWeaponDefinitions(pool, spawnCount); // 중복 없이 우선 뽑기

        for (int i = 0; i < spawnCount; i++)
        {
            RectTransform slot = cardSlots[i]; // 슬롯별 배치
            if (slot == null)
            {
                continue; // 슬롯 없음
            }

            GameObject prefab = GetSegmentCardTemplate(i); // 세그먼트 카드 프리팹 재사용
            SpawnedCardEntry entry;
            if (i < picked.Count)
            {
                entry = CreateWeaponEnhancementCard(picked[i], prefab, i, slot, resolvedLevelDelta); // 등급·프리팹 resolve 후 생성
            }
            else
            {
                entry = CreateSpawnedCard(prefab, slot, prefab); // 풀 부족 — 빈 껍데기
                if (entry != null)
                {
                    ConfigureEmptySegmentEntry(entry); // 없음 카드
                }
            }

            if (entry == null)
            {
                continue; // 생성 실패
            }

            spawnedCards.Add(entry); // 생성 목록 등록
        }

        PlaySpawnOpenTween(spawnedCards); // 등장 연출
    }

    private void BuildWeaponEnhancementPool(List<WeaponDefinition> results, bool ownedSegmentsOnly = false) // WeaponCatalog → 유효 강화 목록
    {
        results.Clear(); // 이전 결과 제거
        WeaponCatalogAsset catalog = ResolveWeaponCatalog(); // 카탈로그
        if (catalog == null)
        {
            return; // 카탈로그 없음
        }

        catalog.AppendAllEnhancements(results); // Cannon/Missile/AdditionalSegments 전체
        if (!ownedSegmentsOnly)
        {
            return; // A·기타 — 필터 없음
        }

        FilterWeaponEnhancementPoolByOwnedSegments(results); // B — 보유 세그먼트 TargetSegmentId 만
    }

    private static void FilterWeaponEnhancementPoolByOwnedSegments(List<WeaponDefinition> pool) // Convoy 보유 ID 외 강화 제거
    {
        if (pool == null || pool.Count == 0)
        {
            return; // 풀 없음
        }

        HashSet<string> ownedSegmentIds = CollectOwnedSegmentIds(); // SG01_Cannon 등
        if (ownedSegmentIds.Count == 0)
        {
            pool.Clear(); // 붙은 세그먼트 없음
            return;
        }

        for (int i = pool.Count - 1; i >= 0; i--)
        {
            WeaponDefinition definition = pool[i]; // 후보 강화
            if (definition == null || !ownedSegmentIds.Contains(definition.NormalizedTargetSegmentId))
            {
                pool.RemoveAt(i); // 미보유 세그먼트 강화 제외
            }
        }
    }

    private static HashSet<string> CollectOwnedSegmentIds() // ConvoySegments 에 붙은 세그먼트 ID 집합
    {
        HashSet<string> ownedSegmentIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase); // 대소문자 무시
        CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
        ConvoyController convoy = core != null ? core.Convoy : null; // 플레이어 컨보이
        if (convoy == null)
        {
            return ownedSegmentIds; // 빈 집합
        }

        Dictionary<string, int> countsBySegmentId = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase); // ID별 개수
        convoy.CollectAttachedSegmentCounts(countsBySegmentId); // 1개 이상이면 보유
        foreach (string segmentId in countsBySegmentId.Keys)
        {
            if (!string.IsNullOrWhiteSpace(segmentId))
            {
                ownedSegmentIds.Add(segmentId.Trim()); // 보유 목록 등록
            }
        }

        return ownedSegmentIds;
    }

    private static List<WeaponDefinition> PickRandomWeaponDefinitions(List<WeaponDefinition> pool, int count) // 풀에서 랜덤 N장
    {
        List<WeaponDefinition> picked = new List<WeaponDefinition>(count); // 결과
        if (pool == null || pool.Count == 0 || count <= 0)
        {
            return picked; // 빈 결과
        }

        List<WeaponDefinition> working = new List<WeaponDefinition>(pool); // 중복 방지용 임시 풀
        int pickCount = Mathf.Min(count, working.Count); // 풀보다 많이 요청하면 풀 크기만큼
        for (int i = 0; i < pickCount; i++)
        {
            int index = UnityEngine.Random.Range(0, working.Count); // 랜덤 인덱스
            picked.Add(working[index]); // 선택
            working.RemoveAt(index); // 중복 제외
        }

        while (picked.Count < count && pool.Count > 0)
        {
            picked.Add(pool[UnityEngine.Random.Range(0, pool.Count)]); // 풀보다 많으면 중복 허용
        }

        return picked;
    }

    private void ConfigureWeaponEnhanceCandidateEntry(SpawnedCardEntry entry, SegmentCatalogEntry catalogEntry)
    {
        entry.SegmentRole = SegmentCardRole.Candidate; // 1단계 후보 (무기 강화 흐름)
        entry.SegmentCatalogEntry = catalogEntry; // 선택 후 2단계에 전달
        entry.SegmentId = catalogEntry.NormalizedId; // 대상 세그먼트 ID
        entry.LevelDelta = entry.SegmentAddCard != null ? entry.SegmentAddCard.LevelDelta : 1; // 소비 레벨
        entry.CanSelect = catalogEntry.HasId; // 카탈로그 풀 — 세그먼트 추가와 동일하게 선택
        entry.SegmentAddCard?.ConfigureCandidate(catalogEntry); // 세그먼트 추가와 동일 UI
        // 안건준 추가 - 0623 : 커스텀 프리팹 Card_Text / DescText + 현재 레벨 아이콘 주입
        if (entry.Root != null)
        {
            string segId = catalogEntry.NormalizedId;
            int currentLevel = CoreStatProvider.Active?.Convoy?.GetCurrentSegmentLevel(segId) ?? 1;
            Sprite icon = GetSegmentIconSprite(segId, currentLevel);
            string title = string.IsNullOrWhiteSpace(catalogEntry.DisplayName) ? segId : catalogEntry.DisplayName;
            string desc = string.IsNullOrWhiteSpace(catalogEntry.Description) ? $"{catalogEntry.NormalizedId} 선택" : catalogEntry.Description;
            ApplyCardTextsDirectly(entry.Root, title, desc, icon, GetSegmentIconSizeOffset(segId));
        }
    }

    // 세그먼트 추가/레벨업 2차 선택 카드 2장 생성
    private void SpawnSegmentActionCards(SegmentCatalogEntry entry, int levelDelta, bool canAdd, bool canLevelUp)
    {
        rerollAllowedForCurrentChoices = false; // 추가/레벨업 결정 화면은 리롤 제외
        RefreshRerollUi();
        ClearSpawnedCards(); // 후보 카드 제거
        RectTransform parentSlot = GetCenteredActionParentSlot(); // 2장 중앙 배치 기준
        if (parentSlot == null)
        {
            return;
        }

        bool[] selectable = { canAdd, canLevelUp }; // 각 액션 선택 가능 여부
        SegmentCardRole[] roles = { SegmentCardRole.AddAction, SegmentCardRole.LevelUpAction }; // 액션 종류
        int spawnCount = 2; // 추가/레벨업 2장
        for (int i = 0; i < spawnCount; i++)
        {
            SegmentCardRole role = roles[i]; // 추가 / 레벨업
            GameObject defaultTemplate = GetSegmentCardTemplate(i); // 기본 껍데기
            GameObject spawnPrefab = ResolveSegmentActionCardPrefab(role, defaultTemplate); // CardUI 교체 프리팹 (있을 때)
            SpawnedCardEntry spawnedEntry = CreateSpawnedCard(spawnPrefab, parentSlot, defaultTemplate); // 중앙 슬롯에 생성
            if (spawnedEntry == null)
            {
                continue; // 생성 실패
            }

            ConfigureSegmentActionEntry(spawnedEntry, entry, levelDelta, role, selectable[i]); // 액션 데이터 주입
            ApplyCenteredActionCardPosition(spawnedEntry, i, spawnCount); // 좌우 중앙 배치
            spawnedCards.Add(spawnedEntry); // 목록 등록
        }

        PlaySpawnOpenTween(spawnedCards); // 등장 연출
    }

    // 2단계 - 선택 세그먼트에 맞는 무기 강화 카드 생성
    private void SpawnSegmentEnhancementCards(string targetSegmentId, int levelDelta)
    {
        rerollAllowedForCurrentChoices = false; // 선택 세그먼트의 강화 카드 화면은 리롤 제외
        RefreshRerollUi();
        ClearSpawnedCards(); // 2단계 카드 제거
        int resolvedLevelDelta = Mathf.Max(1, levelDelta); // 소비 레벨
        WeaponCatalogAsset catalog = ResolveWeaponCatalog(); // 카탈로그
        WeaponDefinition[] enhancements = System.Array.Empty<WeaponDefinition>(); // 기본값
        bool hasEnhancements = catalog != null
            && !string.IsNullOrWhiteSpace(targetSegmentId)
            && catalog.TryGetEnhancementsForSegment(targetSegmentId, out enhancements)
            && enhancements != null
            && enhancements.Length > 0; // 카탈로그 조회

        int spawnCount = Mathf.Min(cardsToSpawn, cardSlots.Length); // 3장
        for (int i = 0; i < spawnCount; i++)
        {
            RectTransform slot = cardSlots[i]; // 슬롯별 배치
            if (slot == null)
            {
                continue; // 슬롯 없음
            }

            GameObject defaultTemplate = GetSegmentCardTemplate(i); // 세그먼트 카드 프리팹 재사용
            WeaponDefinition definition = hasEnhancements && i < enhancements.Length ? enhancements[i] : null;
            SpawnedCardEntry entry = CreateWeaponEnhancementCard(definition, defaultTemplate, i, slot, resolvedLevelDelta, targetSegmentId); // 등급·프리팹 resolve
            if (entry == null)
            {
                continue; // 생성 실패
            }

            if (definition == null)
            {
                ConfigureEmptySegmentEntry(entry); // 강화 없음/부족
            }

            spawnedCards.Add(entry); // 생성 목록 등록
        }

        PlaySpawnOpenTween(spawnedCards); // 등장 연출
    }

    private WeaponCatalogAsset ResolveWeaponCatalog() // CardUI 또는 CoreStatProvider 카탈로그
    {
        if (weaponCatalogAsset != null)
        {
            return weaponCatalogAsset; // Inspector 연결 우선
        }

        CoreStatProvider core = CoreStatProvider.Active; // 코어 fallback
        return core != null ? core.WeaponCatalogAsset : null;
    }

    private void ConfigureWeaponEnhancementEntry(
        SpawnedCardEntry entry,
        WeaponDefinition definition,
        string targetSegmentId,
        int levelDelta,
        StatUpgrade.StatCardTier tier)
    {
        entry.SegmentRole = SegmentCardRole.EnhanceChoice; // 2단계 강화 카드
        entry.WeaponDefinition = definition; // 선택 강화
        entry.SegmentId = targetSegmentId; // 대상 세그먼트
        entry.LevelDelta = Mathf.Max(1, levelDelta); // 소비 레벨
        entry.CanSelect = definition != null && definition.HasAnyStatBonus; // 선택 가능
        string tieredDescription = BuildTierPrefixedWeaponDescription(definition, tier); // (N) 값 치환이 반영된 설명
        entry.SegmentAddCard?.ConfigureWeaponEnhancement(definition, entry.LevelDelta, tieredDescription); // 카드 문구·아이콘
        entry.SegmentAddCard?.ApplyWeaponEnhancementTier(tier); // 등급 저장
        // 건준수정 - 0621 ======
        entry.WeaponEnhancementTier = tier; // 적용 시 등급별 수치
        // 건준수정 - 0621 ======
        ApplyTierCardFrame(entry.Root, tier); // 등급별 카드 프레임 교체

        // 안건준 추가 - 0623 : SegmentAddCard 텍스트 주입 후 실제 텍스트가 바뀌었는지 확인 — Card_Text / DescText / Image 직접 fallback
        if (definition != null && entry.Root != null)
        {
            // 현재 세그먼트 레벨 조회 → 레벨에 맞는 아이콘 선택
            int segLevel = CoreStatProvider.Active?.Convoy?.GetCurrentSegmentLevel(definition.TargetSegmentId) ?? 1;
            Sprite iconSprite = definition.GetIconSpriteForLevel(segLevel);

            if (iconSprite == null)
            {
                Debug.LogWarning($"[CardUI] '{definition.name}' 레벨 {segLevel} 아이콘 없음. " +
                    $"CardIconSpritesPerLevel 또는 CardIconSprite 를 Inspector 에서 할당하세요. (TargetSegmentId={definition.TargetSegmentId})", definition);
            }

            ApplyCardTextsDirectly(entry.Root, definition.DisplayName, tieredDescription, iconSprite, definition.CardIconSizeOffset, iconSizeAlreadyApplied: entry.SegmentAddCard != null);
        }

        if (definition != null && !definition.HasAnyStatBonus)
        {
            Debug.LogWarning($"[CardUI] 강화 카드 '{definition.name}' 수치가 0 입니다. Inspector 에서 BaseDamage/ProjectileSpeed/PierceCount/ExplosionRadius 를 확인하세요.", definition);
        }
    }

    private SpawnedCardEntry CreateWeaponEnhancementCard(
        WeaponDefinition definition,
        GameObject defaultTemplate,
        int slotIndex,
        RectTransform slot,
        int levelDelta,
        string targetSegmentId = null)
    {
        if (defaultTemplate == null || slot == null)
        {
            return null; // 템플릿/슬롯 없음
        }

        if (definition == null)
        {
            return CreateSpawnedCard(defaultTemplate, slot, defaultTemplate); // 빈 슬롯용 기본 껍데기
        }

        string resolvedTargetSegmentId = string.IsNullOrWhiteSpace(targetSegmentId)
            ? definition.NormalizedTargetSegmentId
            : targetSegmentId.Trim(); // B 모드는 definition 대상, A 모드는 선택 세그먼트
        StatUpgrade.StatCardTier tier = StatUpgrade.RollTier(rareCardChancePercent, uniqueCardChancePercent); // 등급 선정
        SegmentAddCard templatePresentation = GetSegmentCardPresentation(slotIndex); // addSegmentCards 템플릿
        WeaponDefinition.CardSpawnResolve resolve = definition.ResolveCardSpawn(tier, defaultTemplate, templatePresentation); // 프리팹 결정
        GameObject spawnPrefab = resolve.Prefab != null ? resolve.Prefab : defaultTemplate; // fallback
        SpawnedCardEntry entry = CreateSpawnedCard(spawnPrefab, slot, defaultTemplate, skipStatUpgradeRoll: true); // 무기 강화 등급은 WeaponEnhancementTier만 사용
        if (entry == null)
        {
            return null; // 생성 실패
        }

        ConfigureWeaponEnhancementEntry(entry, definition, resolvedTargetSegmentId, levelDelta, tier); // 문구·등급
        return entry;
    }

    private SegmentAddCard GetSegmentCardPresentation(int index) // addSegmentCards 프리팹의 SegmentAddCard (Instantiate 전)
    {
        GameObject template = GetSegmentCardTemplate(index); // 슬롯 템플릿
        if (template == null)
        {
            return null; // 템플릿 없음
        }

        SegmentAddCard presentation = template.GetComponent<SegmentAddCard>(); // 루트
        if (presentation != null)
        {
            return presentation;
        }

        return template.GetComponentInChildren<SegmentAddCard>(true); // 자식 fallback
    }

    // 2차 액션 카드 부모로 가장 중앙에 가까운 슬롯 사용
    private RectTransform GetCenteredActionParentSlot()
    {
        if (cardSlots == null || cardSlots.Length == 0)
        {
            return null; // 슬롯 없음
        }

        RectTransform result = cardSlots[0]; // 기본값
        float bestDistance = result != null ? Mathf.Abs(result.anchoredPosition.x) : float.MaxValue; // 중앙 거리
        for (int i = 1; i < cardSlots.Length; i++)
        {
            RectTransform slot = cardSlots[i]; // 후보 슬롯
            if (slot == null)
            {
                continue; // 빈 슬롯 제외
            }

            float distance = Mathf.Abs(slot.anchoredPosition.x); // 중앙에서 떨어진 거리
            if (distance < bestDistance)
            {
                result = slot; // 더 중앙에 가까운 슬롯
                bestDistance = distance; // 거리 갱신
            }
        }

        return result; // 중앙 슬롯 반환
    }

    // 2차 액션 카드 2장을 중앙 기준 좌우로 배치
    private void ApplyCenteredActionCardPosition(SpawnedCardEntry entry, int index, int count)
    {
        if (entry == null || entry.RootTransform == null)
        {
            return; // 대상 없음
        }

        Vector2 targetPosition = GetCenteredActionCardPosition(index, count); // 목표 위치 계산
        entry.RootTransform.anchoredPosition = targetPosition; // 현재 위치 보정
        entry.OriginalPosition = targetPosition; // DOTween 등장/복귀 기준도 보정
    }

    // 기존 3슬롯 폭을 기준으로 2장 배치 간격 계산
    private Vector2 GetCenteredActionCardPosition(int index, int count)
    {
        if (count <= 1)
        {
            return Vector2.zero; // 1장이면 중앙
        }

        float halfSpacing = CalculateCenteredActionHalfSpacing(); // 중앙에서 좌우 거리
        float x = index == 0 ? -halfSpacing : halfSpacing; // 첫 장 왼쪽, 둘째 장 오른쪽
        return new Vector2(x, 0f); // y는 중앙 슬롯 기준 유지
    }

    // 후보 카드 3장 슬롯의 전체 폭에서 2장용 절반 간격 산출
    private float CalculateCenteredActionHalfSpacing()
    {
        if (cardSlots == null || cardSlots.Length < 2)
        {
            return 175f; // 기본 간격
        }

        float minX = float.MaxValue; // 가장 왼쪽
        float maxX = float.MinValue; // 가장 오른쪽
        for (int i = 0; i < cardSlots.Length; i++)
        {
            RectTransform slot = cardSlots[i]; // 후보 슬롯
            if (slot == null)
            {
                continue; // 빈 슬롯 제외
            }

            float x = slot.anchoredPosition.x; // 슬롯 x 위치
            minX = Mathf.Min(minX, x); // 왼쪽 갱신
            maxX = Mathf.Max(maxX, x); // 오른쪽 갱신
        }

        if (minX == float.MaxValue || maxX == float.MinValue || Mathf.Approximately(minX, maxX))
        {
            return 175f; // 계산 불가 fallback
        }

        return Mathf.Max(120f, (maxX - minX) * 0.25f); // 3슬롯 폭의 1/4 지점에 2장 배치
    }

    // 세그먼트 카드 템플릿 선택 (안건준 수정 - 0623 : segmentCardBasePrefab 사용)
    private GameObject GetSegmentCardTemplate(int index)
    {
        return segmentCardBasePrefab; // SegmentUpgradeCard 공통 프리팹
    }

    private GameObject GetSegmentChoiceCardTemplate() // 후보 선택 1단계 전용 템플릿
    {
        if (segmentChoiceCardPrefab != null)
        {
            return segmentChoiceCardPrefab; // Inspector 지정 우선
        }

        CardUiPrefabReferences references = GetPrefabReferences();
        if (references != null && references.SegmentChoiceCardPrefab != null)
        {
            return references.SegmentChoiceCardPrefab; // 이동된 프리팹 참조
        }

        return segmentCardBasePrefab; // 누락 시 기존 카드 유지
    }

    private CardUiPrefabReferences GetPrefabReferences()
    {
        if (prefabReferences != null)
        {
            return prefabReferences; // Inspector 지정 우선
        }

        if (cachedPrefabReferences == null)
        {
            cachedPrefabReferences = Resources.Load<CardUiPrefabReferences>(CardUiPrefabReferencesResourcePath); // 씬 수정 없는 fallback
        }

        return cachedPrefabReferences;
    }

    private void ApplyTierCardFrame(GameObject cardRoot, StatUpgrade.StatCardTier tier) // 스탯/무기 강화 카드 등급 프레임
    {
        if (cardRoot == null)
        {
            return; // 카드 없음
        }

        Sprite frameSprite = GetTierFrameSprite(tier); // 등급별 프레임
        if (frameSprite == null)
        {
            return; // 에셋 누락 시 기존 프레임 유지
        }

        Image rootImage = cardRoot.GetComponent<Image>(); // 카드 루트 배경 이미지
        if (rootImage == null)
        {
            return; // 루트 이미지가 없는 특수 카드
        }

        rootImage.sprite = frameSprite;
        rootImage.type = Image.Type.Simple;
        rootImage.preserveAspect = false;
        rootImage.color = Color.white;
    }

    private Sprite GetTierFrameSprite(StatUpgrade.StatCardTier tier) // Resources 캐시 로드
    {
        switch (tier)
        {
            case StatUpgrade.StatCardTier.Unique:
                if (cachedTierFrameUniqueSprite == null)
                {
                    cachedTierFrameUniqueSprite = Resources.Load<Sprite>(TierFrameUniqueResourcePath);
                }
                return cachedTierFrameUniqueSprite;
            case StatUpgrade.StatCardTier.Rare:
                if (cachedTierFrameRareSprite == null)
                {
                    cachedTierFrameRareSprite = Resources.Load<Sprite>(TierFrameRareResourcePath);
                }
                return cachedTierFrameRareSprite;
            default:
                if (cachedTierFrameNormalSprite == null)
                {
                    cachedTierFrameNormalSprite = Resources.Load<Sprite>(TierFrameNormalResourcePath);
                }
                return cachedTierFrameNormalSprite;
        }
    }

    private GameObject ResolveSegmentActionCardPrefab(SegmentCardRole role, GameObject defaultTemplate) // 2차 액션 카드 — CardUI 교체 프리팹
    {
        // 선택 후 2장 분기는 후보 선택카드가 아니라 강화카드 이미지 계열을 사용한다.
        return segmentCardBasePrefab != null ? segmentCardBasePrefab : defaultTemplate;
    }

    // A 모드 1단계 — 보유 세그먼트 개수에 비례한 가중치로 후보 선택 (중복 없음)
    private List<SegmentCatalogEntry> PickWeightedWeaponEnhanceSegmentEntries(
        List<SegmentCatalogEntry> candidates,
        int count,
        Dictionary<string, int> ownedCountsBySegmentId)
    {
        List<WeightedSegmentCatalogEntry> remaining = new List<WeightedSegmentCatalogEntry>(); // 남은 후보+가중치
        if (candidates != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                SegmentCatalogEntry entry = candidates[i]; // 카탈로그 후보
                if (!entry.HasId)
                {
                    continue; // ID 없는 후보 제외
                }

                int ownedCount = 0; // 보유 개수
                if (ownedCountsBySegmentId != null
                    && ownedCountsBySegmentId.TryGetValue(entry.NormalizedId, out int countForId))
                {
                    ownedCount = Mathf.Max(0, countForId);
                }

                float weight = weaponEnhanceSegmentBaseWeight + ownedCount * weaponEnhanceSegmentWeightPerOwned; // 기본 + (개수 × 보너스)
                remaining.Add(new WeightedSegmentCatalogEntry
                {
                    Entry = entry,
                    Weight = weight
                });
            }
        }

        List<SegmentCatalogEntry> picked = new List<SegmentCatalogEntry>(count); // 선택 결과
        int pickCount = Mathf.Min(count, remaining.Count); // 뽑을 수량
        for (int pickIndex = 0; pickIndex < pickCount; pickIndex++)
        {
            if (!TryPickWeightedSegmentEntry(remaining, out WeightedSegmentCatalogEntry selected))
            {
                break; // 더 이상 선택 불가
            }

            picked.Add(selected.Entry); // 선택된 세그먼트
            remaining.Remove(selected); // 중복 방지
        }

        return picked;
    }

    private static bool TryPickWeightedSegmentEntry(List<WeightedSegmentCatalogEntry> pool, out WeightedSegmentCatalogEntry selected)
    {
        selected = default;
        if (pool == null || pool.Count == 0)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            totalWeight += pool[i].Weight;
        }

        if (totalWeight <= 0f)
        {
            selected = pool[pool.Count - 1];
            return true;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        selected = pool[pool.Count - 1];
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += pool[i].Weight;
            if (roll < cumulative)
            {
                selected = pool[i];
                return true;
            }
        }

        return true;
    }

    // 세그먼트 선택카드: 50% 확률 보유 Lv3 미만 1장 + 지원형 최대 1장
    private List<SegmentCatalogEntry> PickSegmentChoiceEntriesWithOwnedChance(List<SegmentCatalogEntry> candidates, int count)
    {
        List<SegmentCatalogEntry> results = new List<SegmentCatalogEntry>(Mathf.Max(0, count)); // 최종 선택
        if (candidates == null || candidates.Count == 0 || count <= 0)
        {
            return results; // 후보 없음
        }

        List<SegmentCatalogEntry> validCandidates = BuildValidSegmentChoiceCandidates(candidates); // ID 있는 후보만
        if (validCandidates.Count == 0)
        {
            return results; // 유효 후보 없음
        }

        int supportPickCount = 0; // 지원형 표시 수
        if (TryPickOwnedSegmentChoiceCandidate(validCandidates, out SegmentCatalogEntry ownedPick))
        {
            results.Add(ownedPick); // 보유 후보 확정
            if (IsSupportSegmentChoiceEntry(ownedPick))
            {
                supportPickCount++; // 지원형 슬롯 사용
            }

            RemoveSegmentCandidateById(validCandidates, ownedPick.NormalizedId); // 나머지 랜덤 중복 방지
        }

        while (results.Count < count && validCandidates.Count > 0)
        {
            if (!TryTakeRandomSegmentChoiceCandidate(validCandidates, supportPickCount, out SegmentCatalogEntry randomPick))
            {
                break; // 지원형 제한 등으로 더 뽑을 후보 없음
            }

            results.Add(randomPick); // 남은 칸 랜덤
            if (IsSupportSegmentChoiceEntry(randomPick))
            {
                supportPickCount++; // 지원형 1장 제한 추적
            }

            RemoveSegmentCandidateById(validCandidates, randomPick.NormalizedId); // ID 중복 방지
        }

        return results;
    }

    private bool TryPickOwnedSegmentChoiceCandidate(List<SegmentCatalogEntry> candidates, out SegmentCatalogEntry picked)
    {
        picked = default; // 기본값
        if (candidates == null || candidates.Count == 0 || Random.value >= Mathf.Clamp01(ownedSegmentChoiceGuaranteeChance))
        {
            return false; // 확정 확률 미발동
        }

        HashSet<string> ownedSegmentIds = CollectOwnedSegmentIds(); // 현재 보유 세그먼트
        if (ownedSegmentIds.Count == 0)
        {
            return false; // 보유 없음
        }

        List<int> ownedCandidateIndexes = new List<int>(); // 보유 후보 인덱스
        for (int i = 0; i < candidates.Count; i++)
        {
            SegmentCatalogEntry entry = candidates[i]; // 후보
            if (ownedSegmentIds.Contains(entry.NormalizedId) && IsOwnedSegmentChoiceGuaranteeLevelEligible(entry))
            {
                ownedCandidateIndexes.Add(i); // 보유 + Lv3 미만
            }
        }

        if (ownedCandidateIndexes.Count == 0)
        {
            return false; // 확정 가능한 보유 후보 없음
        }

        int selectedIndex = ownedCandidateIndexes[Random.Range(0, ownedCandidateIndexes.Count)]; // 보유 후보 랜덤
        picked = candidates[selectedIndex];
        return true;
    }

    private static bool TryTakeRandomSegmentChoiceCandidate(List<SegmentCatalogEntry> candidates, int supportPickCount, out SegmentCatalogEntry picked)
    {
        picked = default; // 기본값
        if (candidates == null || candidates.Count == 0)
        {
            return false; // 후보 없음
        }

        List<int> availableIndexes = new List<int>(candidates.Count); // 제한 통과 후보
        for (int i = 0; i < candidates.Count; i++)
        {
            SegmentCatalogEntry entry = candidates[i]; // 후보
            if (IsSupportSegmentChoiceEntry(entry) && supportPickCount >= MaxSupportSegmentChoiceCount)
            {
                continue; // 지원형은 0~1장만 허용
            }

            availableIndexes.Add(i); // 선택 가능
        }

        if (availableIndexes.Count == 0)
        {
            return false; // 제한으로 뽑을 후보 없음
        }

        int selectedIndex = availableIndexes[Random.Range(0, availableIndexes.Count)]; // 랜덤 후보
        picked = candidates[selectedIndex];
        candidates.RemoveAt(selectedIndex); // 선택 후보 제거
        return true;
    }

    private static bool IsOwnedSegmentChoiceGuaranteeLevelEligible(SegmentCatalogEntry entry)
    {
        CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
        if (core == null || !core.TryGetSegmentModelLevelInfo(entry.NormalizedId, out int currentLevel, out _))
        {
            return true; // 조회 불가 시 기존 후보성을 유지
        }

        return currentLevel < OwnedSegmentChoiceGuaranteeExcludedLevel; // Lv3 제외
    }

    private static bool IsSupportSegmentChoiceEntry(SegmentCatalogEntry entry)
    {
        return !string.IsNullOrWhiteSpace(entry.Description)
            && entry.Description.IndexOf("[지원]", System.StringComparison.OrdinalIgnoreCase) >= 0; // 카드 태그 기준
    }

    private static List<SegmentCatalogEntry> BuildValidSegmentChoiceCandidates(List<SegmentCatalogEntry> candidates)
    {
        List<SegmentCatalogEntry> valid = new List<SegmentCatalogEntry>(); // 유효 후보
        if (candidates == null)
        {
            return valid; // 없음
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].HasId)
            {
                valid.Add(candidates[i]); // ID 있는 후보만 사용
            }
        }

        return valid;
    }

    private static void RemoveSegmentCandidateById(List<SegmentCatalogEntry> candidates, string segmentId)
    {
        if (candidates == null || string.IsNullOrWhiteSpace(segmentId))
        {
            return; // 제거 대상 없음
        }

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (string.Equals(candidates[i].NormalizedId, segmentId, System.StringComparison.OrdinalIgnoreCase))
            {
                candidates.RemoveAt(i); // 같은 ID 중복 제거
            }
        }
    }

    // 세그먼트 후보 카드 데이터 주입
    private void ConfigureSegmentCandidateEntry(SpawnedCardEntry entry, SegmentCatalogEntry catalogEntry)
    {
        entry.SegmentRole = SegmentCardRole.Candidate; // 후보 카드
        entry.SegmentCatalogEntry = catalogEntry; // 선택 후보 저장
        entry.SegmentId = catalogEntry.NormalizedId; // 대상 ID 저장
        entry.LevelDelta = entry.SegmentAddCard != null ? entry.SegmentAddCard.LevelDelta : 1; // 소비 레벨
        entry.CanSelect = true; // 후보 선택 가능
        entry.SegmentAddCard?.ConfigureCandidate(catalogEntry); // 카드 문구 세팅

        // 안건준 추가 - 0623 : 현재 세그먼트 레벨에 맞는 아이콘 적용
        if (entry.Root != null)
        {
            string segId = catalogEntry.NormalizedId;
            int currentLevel = CoreStatProvider.Active?.Convoy?.GetCurrentSegmentLevel(segId) ?? 1;
            Sprite icon = GetSegmentIconSprite(segId, currentLevel);
            string title = string.IsNullOrWhiteSpace(catalogEntry.DisplayName) ? segId : catalogEntry.DisplayName;
            string desc = string.IsNullOrWhiteSpace(catalogEntry.Description) ? $"{catalogEntry.NormalizedId} 선택" : catalogEntry.Description;
            ApplyCardTextsDirectly(entry.Root, title, desc, icon, GetSegmentIconSizeOffset(segId));
        }
    }

    // 후보 부족 시 없음 카드 데이터 주입
    private void ConfigureEmptySegmentEntry(SpawnedCardEntry entry)
    {
        entry.SegmentRole = SegmentCardRole.Empty; // 없음 카드
        entry.SegmentId = string.Empty; // 대상 없음
        entry.LevelDelta = 1; // 기본값
        entry.CanSelect = false; // 클릭 불가
        entry.SegmentAddCard?.ConfigureEmpty(); // 카드 문구 세팅
    }

    // 추가/레벨업 액션 카드 데이터 주입
    private void ConfigureSegmentActionEntry(SpawnedCardEntry entry, SegmentCatalogEntry catalogEntry, int levelDelta, SegmentCardRole role, bool selectable)
    {
        string segId = catalogEntry.NormalizedId; // 대상 ID
        string displayName = string.IsNullOrWhiteSpace(catalogEntry.DisplayName) ? segId : catalogEntry.DisplayName; // 표시명
        // 안건준 수정 - 0623 : Card_Text = 세그먼트 이름만
        string title = displayName;
        string description = BuildSegmentActionDescription(segId, displayName, role, selectable); // 액션 설명
        entry.SegmentRole = role; // 액션 역할
        entry.SegmentCatalogEntry = catalogEntry; // 대상 후보 저장
        entry.SegmentId = segId; // 대상 ID
        entry.LevelDelta = Mathf.Max(0, levelDelta); // 선택권 모드는 0으로 소비 없음
        entry.CanSelect = selectable; // 선택 가능 여부
        entry.SegmentAddCard?.ConfigureAction(segId, title, description, selectable); // 카드 문구 세팅

        // 안건준 추가 - 0623 : 레벨에 맞는 아이콘 적용 (레벨업 카드는 다음 레벨 이미지)
        if (entry.Root != null)
        {
            int currentLevel = CoreStatProvider.Active?.Convoy?.GetCurrentSegmentLevel(segId) ?? 1;
            int iconLevel = (role == SegmentCardRole.LevelUpAction) ? currentLevel + 1 : currentLevel; // 레벨업=다음레벨
            Sprite icon = GetSegmentIconSprite(segId, iconLevel);
            ApplyTierCardFrame(entry.Root, StatUpgrade.StatCardTier.Normal); // 액션카드는 강화카드 기본 이미지 사용
            ApplyCardTextsDirectly(entry.Root, title, description, icon, GetSegmentIconSizeOffset(segId));
        }
    }

    // 액션 카드 설명 생성 (안건준 수정 - 0623 : Card_Text에 이름이 있으므로 DescText는 상태 정보만)
    private static string BuildSegmentActionDescription(string segmentId, string displayName, SegmentCardRole role, bool selectable)
    {
        if (role == SegmentCardRole.AddAction)
        {
            return selectable ? "추가 +1" : "추가 불가"; // 이름은 Card_Text에, 여기선 상태만
        }

        if (role == SegmentCardRole.LevelUpAction)
        {
            if (CoreStatProvider.Active != null && CoreStatProvider.Active.TryGetSegmentModelLevelInfo(segmentId, out int currentLevel, out int maxLevel))
            {
                int nextLevel = Mathf.Min(currentLevel + 1, maxLevel); // 다음 레벨
                return selectable ? $"Lv.{currentLevel} → Lv.{nextLevel}" : "MAX"; // 이름은 Card_Text에
            }

            return selectable ? "레벨업 가능" : "레벨업 불가"; // fallback
        }

        return string.Empty;
    }

    // 무기 강화 디버그 - 시작 시 1회 (CoreStatProvider 현재값)
    private void LogWeaponEnhancementInitialOnce()
    {
        if (loggedWeaponEnhancementInitial)
        {
            return;
        }

        loggedWeaponEnhancementInitial = true; // 1회만 출력
        CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
        if (core == null)
        {
            Debug.LogWarning("[CardUI] 무기 강화 초기: CoreStatProvider 없음");
            return;
        }

        WeaponCatalogAsset catalog = ResolveWeaponCatalog(); // 카탈로그
        if (catalog == null)
        {
            LogWeaponEnhancementState(core, string.Empty);
            return;
        }

        LogWeaponEnhancementState(core, "SG01_Cannon"); // 캐논
        LogWeaponEnhancementState(core, "SG02_Missile"); // 미사일
        catalog.ForEachAdditionalSegmentId(segmentId => LogWeaponEnhancementState(core, segmentId)); // 추가 무기
    }

    //전찬우 수정-0622
    private void LogWeaponEnhancementState(CoreStatProvider core, string segmentId)
    {
        if (!TryBuildSegmentWeaponStatDebugContext(core, segmentId, out SegmentWeaponStatDebugContext context))
        {
            return;
        }

        Debug.Log($"[CardUI] 무기 강화 초기 | 세그먼트: {context.SegmentId}\n  현재 →\n{FormatSegmentWeaponStatDebugText(context)}");
    }

    private static bool TryGetSegmentAttackProfile(CoreStatProvider core, string segmentId, out SegmentAttackProfile profile)
    {
        profile = null; // 기본값
        if (core == null || core.SegmentCatalogAsset == null || string.IsNullOrWhiteSpace(segmentId))
        {
            return false; // 조회 불가
        }

        if (!core.SegmentCatalogAsset.TryFind(segmentId, out SegmentDefinition definition))
        {
            return false; // 정의 없음
        }

        int level = 1; // 기본 레벨
        if (core.TryGetSegmentModelLevelInfo(segmentId, out int currentLevel, out _))
        {
            level = currentLevel; // 장착 중이면 현재 모델 레벨
        }

        if (definition.TryGetLevel(level, out SegmentLevelDefinition levelDef) && levelDef.AttackProfile != null)
        {
            profile = levelDef.AttackProfile; // 레벨 정의에서 프로필
            return true;
        }

        if (!definition.TryGetSegmentPrefab(level, out GameObject prefab) || prefab == null)
        {
            return false; // 프리팹 없음
        }

        GenericSegmentWeapon weapon = prefab.GetComponentInChildren<GenericSegmentWeapon>(true); // 무기 컴포넌트
        if (weapon == null || weapon.AttackProfile == null)
        {
            return false; // 프로필 없음
        }

        profile = weapon.AttackProfile; // 프리팹에서 프로필
        return true;
    }

    // 무기 강화 디버그 - 카드 선택 후 누적 보너스
    private void LogWeaponEnhancementIncrease(string segmentId, WeaponDefinition definition, CoreStatProvider core)
    {
        if (definition == null || !TryBuildSegmentWeaponStatDebugContext(core, segmentId, out SegmentWeaponStatDebugContext context))
        {
            return;
        }

        string cardName = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NormalizedId : definition.DisplayName; // 카드명
        Debug.Log($"[CardUI] 무기 강화 | 세그먼트: {context.SegmentId} | 카드: {cardName}\n  현재 →\n{FormatSegmentWeaponStatDebugText(context)}");
    }

    // 건춘추가 - 0621 ======
}
