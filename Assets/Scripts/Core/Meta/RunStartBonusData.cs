using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    [Serializable]
    public struct RunStartBonusData // 타이틀 → StageScene 시작 보너스
    {
        public string SelectedWormId; // 선택 지렁이
        public string SelectedMapId; // 선택 맵
        public float NexusMaxHealthPercentBonus; // 넥서스 최대 체력 비율 보너스
        public float NexusRegenPerMinuteBonus; // 넥서스 분당 회복
        public float GoldGainPercentBonus; // 인게임 골드 획득 비율
        public float DiamondGainPercentBonus; // 종료 다이아 획득 비율
        public float TurnPercentBonus; // 회전력 비율 보너스
        public float CollisionForcePercentBonus; // 직접 충돌힘 비율 보너스
        public int BaseAttackFlatBonus; // 기본 공격력 고정 보너스
        public float AttackSpeedPercentBonus; // 공격속도 비율 보너스
        public int StartingSegmentBonus; // 시작 세그먼트 추가 수
        public float RejoinRangeBonus; // 재결합 범위 고정 보너스

        public bool HasAnyValue => NexusMaxHealthPercentBonus != 0f
            || NexusRegenPerMinuteBonus != 0f
            || GoldGainPercentBonus != 0f
            || DiamondGainPercentBonus != 0f
            || TurnPercentBonus != 0f
            || CollisionForcePercentBonus != 0f
            || BaseAttackFlatBonus != 0
            || AttackSpeedPercentBonus != 0f
            || StartingSegmentBonus != 0
            || RejoinRangeBonus != 0f; // 보너스 존재

        public static RunStartBonusData Create(string wormId, string mapId) // 기본 생성
        {
            RunStartBonusData data = default; // 값 준비
            data.SelectedWormId = MetaWormIds.Normalize(wormId); // 지렁이 보정
            data.SelectedMapId = string.IsNullOrWhiteSpace(mapId) ? MetaMapIds.Map1 : mapId; // 맵 보정
            return data; // 결과 반환
        }

        public void AddValues(RunStartBonusData other) // 보너스 누적
        {
            NexusMaxHealthPercentBonus += other.NexusMaxHealthPercentBonus; // 체력
            NexusRegenPerMinuteBonus += other.NexusRegenPerMinuteBonus; // 회복
            GoldGainPercentBonus += other.GoldGainPercentBonus; // 골드
            DiamondGainPercentBonus += other.DiamondGainPercentBonus; // 다이아
            TurnPercentBonus += other.TurnPercentBonus; // 회전
            CollisionForcePercentBonus += other.CollisionForcePercentBonus; // 충돌
            BaseAttackFlatBonus += other.BaseAttackFlatBonus; // 공격력
            AttackSpeedPercentBonus += other.AttackSpeedPercentBonus; // 공속
            StartingSegmentBonus += other.StartingSegmentBonus; // 세그먼트
            RejoinRangeBonus += other.RejoinRangeBonus; // 재결합
        }

        public int ApplyDiamondGainBonus(int baseAmount) // 다이아 보상 적용
        {
            float multiplier = 1f + DiamondGainPercentBonus; // 보너스 배율
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, baseAmount) * multiplier)); // 보정 결과
        }
    }
}
