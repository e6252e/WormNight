using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    /// <summary>
    /// 보스 EnemyHealth → BossHpBarUI(우선) 또는 BossHpUI(레거시) 연결.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class BossHpUIBinder : MonoBehaviour
    {
        //안건준 수정 - 0630: UI 인스턴스 탐색 최대 대기 시간(초)
        private const float BindTimeoutSeconds = 15f;

        private EnemyHealth health;
        private Coroutine bindCoroutine;

        //안건준 수정 - 0630: BossHpBarUI에 바인딩됐는지 여부 (Unbind 시 올바른 UI 해제용)
        private bool boundToBarUi;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            bindCoroutine = StartCoroutine(BindRoutine());
        }

        private void OnDisable()
        {
            if (bindCoroutine != null)
            {
                StopCoroutine(bindCoroutine);
                bindCoroutine = null;
            }

            //안건준 수정 - 0630: 바인딩된 UI 종류에 따라 Unbind 분기
            if (boundToBarUi && BossHpBarUI.Instance != null)
            {
                BossHpBarUI.Instance.Unbind();
            }
            else if (BossHpUI.Instance != null)
            {
                BossHpUI.Instance.Unbind();
            }

            boundToBarUi = false;
        }

        //안건준 수정 - 0630: BossHpBarUI 우선 탐색, 없으면 레거시 BossHpUI로 폴백
        private IEnumerator BindRoutine()
        {
            float elapsed = 0f;

            while (elapsed < BindTimeoutSeconds)
            {
                //안건준 수정 - 0630: DNF 스타일 3레이어 HP 바(BossHpBarUI) 우선 연결
                if (BossHpBarUI.TryResolveInstance(out BossHpBarUI barUi))
                {
                    barUi.Bind(health);
                    boundToBarUi = true;
                    bindCoroutine = null;
                    yield break;
                }

                //안건준 수정 - 0630: BossHpBarUI가 없을 때 기존 BossHpUI 사용
                if (BossHpUI.Instance != null)
                {
                    BossHpUI.Instance.Bind(health);
                    boundToBarUi = false;
                    bindCoroutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            //안건준 수정 - 0630: 타임아웃 시 경고 로그 출력
            Debug.LogWarning(
                "[BossHpUIBinder] BossHpBarUI / BossHpUI를 찾지 못했습니다. 씬 Canvas에 BossHpBarUI 컴포넌트가 있는지 확인하세요.");
            bindCoroutine = null;
        }
    }
}
