using UnityEngine;
using UnityEngine.VFX;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class StatusBodyVfxController : MonoBehaviour
    {
        private static readonly string[] PreferredAttachRootNames =
        {
            "VFX_DebuffBodyRoot",
            "VFX_BuffBodyRoot",
            "VFX_BodyRoot",
            "BodyVfxRoot"
        };

        private GameObject currentPrefab;
        private GameObject currentInstance;
        private Transform generatedAttachRoot;

        public void Show(GameObject effectPrefab, string instanceLabel = null, Transform explicitRoot = null)
        {
            if (effectPrefab == null)
            {
                Clear();
                return;
            }

            Transform root = explicitRoot != null ? explicitRoot : ResolveAttachRoot();
            if (root == null)
            {
                return;
            }

            if (currentPrefab == effectPrefab && currentInstance != null && currentInstance.transform.parent == root)
            {
                return;
            }

            Clear();
            currentPrefab = effectPrefab;
            currentInstance = Instantiate(effectPrefab, root);
            currentInstance.name = string.IsNullOrWhiteSpace(instanceLabel)
                ? effectPrefab.name + " (Runtime Body VFX)"
                : instanceLabel + " Body VFX";
            Transform instanceTransform = currentInstance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;
            PlayInstance(currentInstance);
        }

        public void Clear()
        {
            if (currentInstance != null)
            {
                DestroyRuntimeObject(currentInstance);
            }

            currentInstance = null;
            currentPrefab = null;
        }

        private Transform ResolveAttachRoot()
        {
            for (int i = 0; i < PreferredAttachRootNames.Length; i++)
            {
                Transform root = FindChildRecursive(transform, PreferredAttachRootNames[i]);
                if (root != null)
                {
                    return root;
                }
            }

            return ResolveGeneratedAttachRoot();
        }

        private Transform ResolveGeneratedAttachRoot()
        {
            if (generatedAttachRoot == null)
            {
                GameObject rootObject = new GameObject("RuntimeBodyVfxRoot");
                rootObject.hideFlags = HideFlags.DontSave;
                generatedAttachRoot = rootObject.transform;
                generatedAttachRoot.SetParent(transform, false);
            }

            Renderer boundsRenderer = ResolveBoundsRenderer();
            if (boundsRenderer != null)
            {
                Bounds bounds = boundsRenderer.bounds;
                generatedAttachRoot.position = bounds.center;
                generatedAttachRoot.rotation = transform.rotation;
            }
            else
            {
                generatedAttachRoot.localPosition = Vector3.zero;
                generatedAttachRoot.localRotation = Quaternion.identity;
            }

            return generatedAttachRoot;
        }

        private Renderer ResolveBoundsRenderer()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                {
                    continue;
                }

                if (currentInstance != null && renderer.transform.IsChildOf(currentInstance.transform))
                {
                    continue;
                }

                if (renderer.GetComponentInParent<VisualEffect>() != null)
                {
                    continue;
                }

                return renderer;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void PlayInstance(GameObject instance)
        {
            VisualEffect[] visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
            for (int i = 0; i < visualEffects.Length; i++)
            {
                visualEffects[i].Reinit();
                visualEffects[i].Play();
            }

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                particleSystems[i].Play(true);
            }
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
