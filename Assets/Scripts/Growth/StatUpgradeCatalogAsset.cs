using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "OZ/Growth/Stat Upgrade Catalog", fileName = "StatUpgradeCatalog")]
public sealed class StatUpgradeCatalogAsset : ScriptableObject // 공통 강화 카드 카탈로그
{
    public GameObject DefaultCardPrefab; // 공통 강화 카드 UI 템플릿
    public StatUpgradeDefinition[] Cards = Array.Empty<StatUpgradeDefinition>(); // 카드 데이터 목록

    public void AppendValidDefinitions(List<StatUpgradeDefinition> results)
    {
        if (results == null || Cards == null)
        {
            return;
        }

        for (int i = 0; i < Cards.Length; i++)
        {
            StatUpgradeDefinition definition = Cards[i];
            if (definition == null || !definition.HasAnyStatValue || results.Contains(definition))
            {
                continue;
            }

            results.Add(definition);
        }
    }
}
