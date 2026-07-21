using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class WaveSpawnedEliteMarker : MonoBehaviour
    {
        [SerializeField] private string combinationType; // 이 엘리트가 어떤 조합 성격으로 생성되었는지 기록합니다.

        public string CombinationType => combinationType;

        public void Initialize(string type)
        {
            combinationType = string.IsNullOrWhiteSpace(type) ? string.Empty : type.Trim();
        }

        public static void CollectActiveCombinationTypes(HashSet<string> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            WaveSpawnedEliteMarker[] markers = FindObjectsByType<WaveSpawnedEliteMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < markers.Length; i++)
            {
                WaveSpawnedEliteMarker marker = markers[i];

                if (marker == null || string.IsNullOrWhiteSpace(marker.combinationType))
                {
                    continue;
                }

                results.Add(marker.combinationType.Trim());
            }
        }
    }
}
