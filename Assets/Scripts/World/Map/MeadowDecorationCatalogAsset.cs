using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZ/Map/Meadow Decoration Catalog", fileName = "MeadowDecorationCatalog")]
    public sealed class MeadowDecorationCatalogAsset : ScriptableObject // 초원 자연물 런타임 프리팹 묶음
    {
        [Header("Meadow Nature")]
        public GameObject[] TreePrefabs = Array.Empty<GameObject>(); // 큰 활엽수/침엽수
        public GameObject[] LightTreePrefabs = Array.Empty<GameObject>(); // 자작나무 등 가벼운 나무
        public GameObject[] BushPrefabs = Array.Empty<GameObject>(); // 덤불
        public GameObject[] GrassPrefabs = Array.Empty<GameObject>(); // 풀/잎 식물
        public GameObject[] FlowerGroupPrefabs = Array.Empty<GameObject>(); // 묶음 꽃
        public GameObject[] FlowerAccentPrefabs = Array.Empty<GameObject>(); // 단일/키 큰 꽃
        public GameObject[] MushroomPrefabs = Array.Empty<GameObject>(); // 버섯 소품
        public GameObject[] RockGroupPrefabs = Array.Empty<GameObject>(); // 작은 돌 묶음

        [Header("Pandazole Addons")]
        public GameObject[] PandazoleFlowerPrefabs = Array.Empty<GameObject>(); // Flower_*
        public GameObject[] PandazoleGrassPrefabs = Array.Empty<GameObject>(); // Grass_*
        public GameObject[] PandazoleFoliagePrefabs = Array.Empty<GameObject>(); // Foliage_*
        public GameObject[] PandazoleMushroomPrefabs = Array.Empty<GameObject>(); // Mashroom_*
        public GameObject[] PandazoleLogPrefabs = Array.Empty<GameObject>(); // Log_*
        public GameObject[] PandazoleBonePrefabs = Array.Empty<GameObject>(); // Bones_*
        public GameObject[] PandazoleSkullPrefabs = Array.Empty<GameObject>(); // Skull_*

        public bool HasMeadowPrefab => // 기본 Meadow 자연물 존재 여부
            HasAny(TreePrefabs) || HasAny(LightTreePrefabs) || HasAny(BushPrefabs) || HasAny(GrassPrefabs) ||
            HasAny(FlowerGroupPrefabs) || HasAny(FlowerAccentPrefabs) || HasAny(MushroomPrefabs) || HasAny(RockGroupPrefabs);

        public bool HasPandazolePrefab => // Pandazole 추가 자연물 존재 여부
            HasAny(PandazoleFlowerPrefabs) || HasAny(PandazoleGrassPrefabs) || HasAny(PandazoleFoliagePrefabs) ||
            HasAny(PandazoleMushroomPrefabs) || HasAny(PandazoleLogPrefabs) || HasAny(PandazoleBonePrefabs) ||
            HasAny(PandazoleSkullPrefabs);

        private static bool HasAny(GameObject[] prefabs)
        {
            if (prefabs == null)
            {
                return false;
            }

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
