using UnityEngine;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class LoadedProjectileRegrowVisual : MonoBehaviour // 장전 표시를 쿨타임 동안 스케일로 재생성
    {
        [SerializeField, Range(0.001f, 1f)] private float minimumScaleRatio = 0.01f;
        [SerializeField, Range(0f, 0.99f)] private float growthStartProgress = 0.5f;
        [SerializeField, Range(0f, 1f)] private float particleRgbMultiplier = 0.28f;
        [SerializeField, Range(0f, 1f)] private float particleAlphaMultiplier = 0.55f;

        private Vector3 fullScale;
        private bool hasCapturedScale;
        private ParticleSystem[] particles;
        private ParticleSystem.MinMaxGradient[] originalStartColors;
        private bool hasCapturedParticleColors;
        private bool particlesPlayingForReload;

        private void Awake()
        {
            CaptureScaleIfNeeded();
            ApplyParticleIntensity();
        }

        private void OnEnable()
        {
            CaptureScaleIfNeeded();
            ApplyParticleIntensity();
        }

        public void HideImmediate()
        {
            CaptureScaleIfNeeded();
            StopParticles();
            gameObject.SetActive(true);
            transform.localScale = fullScale * minimumScaleRatio;
        }

        public void ShowImmediate()
        {
            CaptureScaleIfNeeded();
            gameObject.SetActive(true);
            transform.localScale = fullScale;
            RestartParticles();
        }

        public void SetReloadProgress(float progress)
        {
            CaptureScaleIfNeeded();
            gameObject.SetActive(true);
            if (!particlesPlayingForReload)
            {
                RestartParticles();
            }

            float delayedProgress = ResolveGrowthProgress(progress); // 쿨 후반부부터 성장
            float eased = Mathf.SmoothStep(0f, 1f, delayedProgress);
            float scaleRatio = Mathf.Lerp(minimumScaleRatio, 1f, eased);
            transform.localScale = fullScale * scaleRatio;
        }

        private float ResolveGrowthProgress(float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);
            float growthStart = Mathf.Clamp(growthStartProgress, 0f, 0.99f);
            return Mathf.Clamp01(Mathf.InverseLerp(growthStart, 1f, clampedProgress));
        }

        private void CaptureScaleIfNeeded()
        {
            if (hasCapturedScale)
            {
                return;
            }

            fullScale = transform.localScale;
            if (fullScale.sqrMagnitude <= 0.0001f)
            {
                fullScale = Vector3.one;
            }

            hasCapturedScale = true;
        }

        public void ApplyParticleIntensity()
        {
            CaptureParticleColorsIfNeeded();
            if (particles == null || originalStartColors == null)
            {
                return;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particle.main;
                main.startColor = ScaleGradient(originalStartColors[i]);
            }
        }

        private void RestartParticles()
        {
            CaptureParticleColorsIfNeeded();
            if (particles == null || particles.Length == 0)
            {
                return;
            }

            ApplyParticleIntensity();
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null)
                {
                    continue;
                }

                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }

            particlesPlayingForReload = true;
        }

        private void StopParticles()
        {
            CaptureParticleColorsIfNeeded();
            if (particles == null || particles.Length == 0)
            {
                return;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle != null)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            particlesPlayingForReload = false;
        }

        private void CaptureParticleColorsIfNeeded()
        {
            if (hasCapturedParticleColors)
            {
                return;
            }

            particles = GetComponentsInChildren<ParticleSystem>(true);
            originalStartColors = new ParticleSystem.MinMaxGradient[particles.Length];
            for (int i = 0; i < particles.Length; i++)
            {
                originalStartColors[i] = particles[i].main.startColor;
            }

            hasCapturedParticleColors = true;
        }

        private ParticleSystem.MinMaxGradient ScaleGradient(ParticleSystem.MinMaxGradient source)
        {
            ParticleSystem.MinMaxGradient scaled = source;
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                    scaled.color = ScaleColor(source.color);
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    scaled.colorMin = ScaleColor(source.colorMin);
                    scaled.colorMax = ScaleColor(source.colorMax);
                    break;
                case ParticleSystemGradientMode.Gradient:
                    scaled.gradient = ScaleGradient(source.gradient);
                    break;
                case ParticleSystemGradientMode.TwoGradients:
                    scaled.gradientMin = ScaleGradient(source.gradientMin);
                    scaled.gradientMax = ScaleGradient(source.gradientMax);
                    break;
                case ParticleSystemGradientMode.RandomColor:
                    scaled.gradient = ScaleGradient(source.gradient);
                    break;
            }

            return scaled;
        }

        private Gradient ScaleGradient(Gradient source)
        {
            if (source == null)
            {
                return null;
            }

            Gradient gradient = new Gradient();
            GradientColorKey[] colorKeys = source.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                colorKeys[i].color = ScaleColor(colorKeys[i].color);
            }

            GradientAlphaKey[] alphaKeys = source.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                alphaKeys[i].alpha *= particleAlphaMultiplier;
            }

            gradient.mode = source.mode;
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        private Color ScaleColor(Color color)
        {
            color.r *= particleRgbMultiplier;
            color.g *= particleRgbMultiplier;
            color.b *= particleRgbMultiplier;
            color.a *= particleAlphaMultiplier;
            return color;
        }
    }
}
