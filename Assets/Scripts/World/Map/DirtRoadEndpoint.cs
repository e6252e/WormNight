using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class DirtRoadEndpoint : MonoBehaviour // 브릿지 입구 기준점
    {
        [Min(0.5f)] public float Width = 4f; // 입구 길 폭

        public Vector3 Position => transform.position; // 월드 위치

        private void OnDrawGizmosSelected() // 편집 기준 표시
        {
            Gizmos.color = new Color(0.55f, 0.34f, 0.16f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.25f, Width * 0.5f));
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
