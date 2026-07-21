using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZ/Map/Meadow Terrain Surface Profile", fileName = "MeadowTerrainSurfaceProfile")]
    public sealed class MeadowTerrainSurfaceProfile : ScriptableObject // 데모 Terrain 표면 빌드 참조 묶음
    {
        public TerrainData TerrainTemplate; // 프로젝트 소유 데모 TerrainData 복사본
        public TerrainLayer[] TerrainLayers = Array.Empty<TerrainLayer>(); // 01/02/03 포함 TerrainLayer 명시 참조
        public Material TerrainMaterial; // URP Terrain Lit 머티리얼
        public bool ApplyTerrainLayers = true; // 생성 TerrainData에 TerrainLayer 재적용
        public bool ApplyTerrainMaterial = true; // 생성 Terrain에 프로젝트 소유 Terrain 머티리얼 적용
        public bool LogSurfaceSummary = true; // 빌드 로그 진단

        public bool HasTerrainTemplate => TerrainTemplate != null; // 템플릿 존재

        public bool HasTerrainLayers // 유효 TerrainLayer 존재
        {
            get
            {
                if (TerrainLayers == null)
                {
                    return false;
                }

                for (int i = 0; i < TerrainLayers.Length; i++)
                {
                    if (TerrainLayers[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
