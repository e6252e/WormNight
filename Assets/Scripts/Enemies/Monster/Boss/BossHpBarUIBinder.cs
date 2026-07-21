using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    /// <summary>
    /// 보스 EnemyHealth를 BossHpBarUI(DNF 스타일 3레이어 HP 바)에 전용 연결하는 바인더.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class BossHpBarUIBinder : MonoBehaviour
    {
        // UI 인스턴스 탐색 최대 대기 시간(초)
        private const float BindTimeoutSeconds = 15f;

        private EnemyHealth health;
        private Coroutine bindCoroutine;

        private void Awake()
        {
            // 같은 오브젝트의 체력 컴포넌트 캐시
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            // 활성화 시 씬에서 BossHpBarUI를 찾아 바인딩 시도
            bindCoroutine = StartCoroutine(BindRoutine());
        }

        private void OnDisable()
        {
            // 진행 중인 바인딩 코루틴 중단
            if (bindCoroutine != null)
            {
                StopCoroutine(bindCoroutine);
                bindCoroutine = null;
            }

            // BossHpBarUI 연결 해제 및 HUD 숨김
            if (BossHpBarUI.Instance != null)
            {
                BossHpBarUI.Instance.Unbind();
            }
        }

        // BossHpBarUI 인스턴스가 준비될 때까지 대기 후 Bind 호출
        private IEnumerator BindRoutine()
        {
            float elapsed = 0f;

            while (elapsed < BindTimeoutSeconds)
            {
                // 비활성 오브젝트 포함 탐색 후 체력 데이터 연결
                if (BossHpBarUI.TryResolveInstance(out BossHpBarUI barUi))
                {
                    barUi.Bind(health);
                    bindCoroutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 타임아웃 시 경고 로그
            Debug.LogWarning(
                "[BossHpBarUIBinder] BossHpBarUI를 찾지 못했습니다. 씬 Canvas에 BossHpBarUI가 붙은 BossHp 패널이 있는지 확인하세요.");
            bindCoroutine = null;
        }
    }
}
