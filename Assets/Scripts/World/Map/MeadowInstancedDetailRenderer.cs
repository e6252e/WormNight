using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeamProject01.Gameplay
{
    public sealed class MeadowInstancedDetailRenderer : MonoBehaviour // 풀/꽃 GPU 인스턴스 렌더러
    {
        private readonly List<DrawBatch> batches = new List<DrawBatch>();
        private float drawDistance;
        private Vector3 drawCenter;

        public void Initialize(IReadOnlyList<DrawBatch> sourceBatches, float distance, Vector3 center)
        {
            batches.Clear();
            if (sourceBatches != null)
            {
                batches.AddRange(sourceBatches);
            }

            drawDistance = Mathf.Max(0f, distance);
            drawCenter = center;

            int instanceCount = 0;
            for (int i = 0; i < batches.Count; i++)
            {
                instanceCount += batches[i]?.InstanceCount ?? 0;
            }

            Debug.Log($"[MeadowInstancedDetailRenderer] 풀/꽃 인스턴스 렌더 준비 완료: batches={batches.Count}, instances={instanceCount}, drawDistance={drawDistance:0.0}", this);
        }

        private void LateUpdate()
        {
            if (batches.Count == 0 || !IsWithinDrawDistance())
            {
                return;
            }

            int layer = gameObject.layer;
            for (int i = 0; i < batches.Count; i++)
            {
                batches[i]?.Draw(layer);
            }
        }

        private bool IsWithinDrawDistance()
        {
            if (drawDistance <= 0f)
            {
                return true;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return true;
            }

            float maxDistance = drawDistance + 120f;
            return (camera.transform.position - drawCenter).sqrMagnitude <= maxDistance * maxDistance;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < batches.Count; i++)
            {
                batches[i]?.Dispose();
            }

            batches.Clear();
        }

        public sealed class DrawBatch // DrawMeshInstanced 배치
        {
            private const int MaxBatchSize = 1023;

            private readonly Mesh mesh;
            private readonly int subMeshIndex;
            private readonly Material material;
            private readonly Matrix4x4[][] matrixChunks;
            private readonly bool receiveShadows;

            public int InstanceCount { get; }

            public DrawBatch(Mesh mesh, int subMeshIndex, Material material, IReadOnlyList<Matrix4x4> matrices, bool receiveShadows)
            {
                this.mesh = mesh;
                this.subMeshIndex = subMeshIndex;
                this.material = material;
                this.receiveShadows = receiveShadows;
                InstanceCount = matrices != null ? matrices.Count : 0;
                matrixChunks = CreateChunks(matrices);
            }

            public void Draw(int layer)
            {
                if (mesh == null || material == null || matrixChunks == null)
                {
                    return;
                }

                for (int i = 0; i < matrixChunks.Length; i++)
                {
                    Matrix4x4[] chunk = matrixChunks[i];
                    if (chunk == null || chunk.Length == 0)
                    {
                        continue;
                    }

                    Graphics.DrawMeshInstanced(
                        mesh,
                        subMeshIndex,
                        material,
                        chunk,
                        chunk.Length,
                        null,
                        ShadowCastingMode.Off,
                        receiveShadows,
                        layer,
                        null,
                        LightProbeUsage.Off);
                }
            }

            public void Dispose()
            {
                if (material == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }

            private static Matrix4x4[][] CreateChunks(IReadOnlyList<Matrix4x4> matrices)
            {
                if (matrices == null || matrices.Count == 0)
                {
                    return Array.Empty<Matrix4x4[]>();
                }

                int chunkCount = Mathf.CeilToInt(matrices.Count / (float)MaxBatchSize);
                Matrix4x4[][] chunks = new Matrix4x4[chunkCount][];
                for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
                {
                    int start = chunkIndex * MaxBatchSize;
                    int count = Mathf.Min(MaxBatchSize, matrices.Count - start);
                    Matrix4x4[] chunk = new Matrix4x4[count];
                    for (int i = 0; i < count; i++)
                    {
                        chunk[i] = matrices[start + i];
                    }

                    chunks[chunkIndex] = chunk;
                }

                return chunks;
            }
        }
    }
}
