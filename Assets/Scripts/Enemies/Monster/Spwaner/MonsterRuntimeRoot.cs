using UnityEngine;

namespace TeamProject01.Gameplay
{
    public static class MonsterRuntimeRoot
    {
        private const string RootObjectName = "Monsters";

        private static Transform cachedRoot;

        public static Transform Root
        {
            get
            {
                if (cachedRoot != null)
                {
                    return cachedRoot;
                }

                GameObject rootObject = GameObject.Find(RootObjectName);

                if (rootObject == null)
                {
                    return null;
                }

                cachedRoot = rootObject.transform;
                return cachedRoot;
            }
        }

        public static Transform GetRootOrFallback(Transform fallbackRoot)
        {
            Transform root = Root;

            if (root != null)
            {
                return root;
            }

            return fallbackRoot;
        }
    }
}