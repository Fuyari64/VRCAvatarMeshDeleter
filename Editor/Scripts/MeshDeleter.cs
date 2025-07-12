using MeshDeleter.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MeshDeleter
{
    public static class MeshDeleter
    {
        private const int INVALID_TRIANGLE_INDEX = -1;

        /// <summary>
        /// Removes triangles from mesh based on direct vertex index selection
        /// </summary>
        public static Mesh RemoveTrianglesByVertexIndices(Mesh mesh, List<int> vertexIndicesToDelete)
        {
            if (!vertexIndicesToDelete.Any())
            {
                throw new NotFoundVerticesException();
            }

            var uniqueVertexIndices = vertexIndicesToDelete.Distinct().ToList();
            uniqueVertexIndices.Sort();

            var meshWithoutVertices = RemoveVerticesFromMesh(mesh, uniqueVertexIndices);
            var verticesToDeleteDescending = uniqueVertexIndices.OrderByDescending(v => v).ToArray();
            
            var (finalMesh, _) = RemoveTrianglesFromSubmeshes(
                mesh, 
                meshWithoutVertices, 
                verticesToDeleteDescending);

            var meshWithBlendShapes = PreserveBlendShapes(mesh, finalMesh, uniqueVertexIndices);

            return meshWithBlendShapes;
        }

        /// <summary>
        /// Extracts mesh data excluding specified indices
        /// </summary>
        private static IEnumerable<T> ExtractMeshDataExcludingIndices<T>(T[] meshData, List<int> indicesToExclude)
            => meshData.Where((data, index) => indicesToExclude.BinarySearch(index) < 0);

        /// <summary>
        /// Removes specified vertices from mesh and updates all mesh data
        /// </summary>
        private static Mesh RemoveVerticesFromMesh(Mesh mesh, List<int> vertexIndicesToRemove)
        {
            var modifiedMesh = UnityEngine.Object.Instantiate(mesh);
            modifiedMesh.Clear();
            modifiedMesh.MarkDynamic();

            var remainingVertices = ExtractMeshDataExcludingIndices(mesh.vertices, vertexIndicesToRemove);
            var remainingBoneWeights = ExtractMeshDataExcludingIndices(mesh.boneWeights, vertexIndicesToRemove);
            var remainingNormals = ExtractMeshDataExcludingIndices(mesh.normals, vertexIndicesToRemove);
            var remainingTangents = ExtractMeshDataExcludingIndices(mesh.tangents, vertexIndicesToRemove);
            var remainingColors = ExtractMeshDataExcludingIndices(mesh.colors, vertexIndicesToRemove);
            var remainingColors32 = ExtractMeshDataExcludingIndices(mesh.colors32, vertexIndicesToRemove);
            var remainingUVs = ExtractMeshDataExcludingIndices(mesh.uv, vertexIndicesToRemove);
            var remainingUV2s = ExtractMeshDataExcludingIndices(mesh.uv2, vertexIndicesToRemove);
            var remainingUV3s = ExtractMeshDataExcludingIndices(mesh.uv3, vertexIndicesToRemove);
            var remainingUV4s = ExtractMeshDataExcludingIndices(mesh.uv4, vertexIndicesToRemove);

            modifiedMesh.SetVertices(remainingVertices.ToList());
            modifiedMesh.boneWeights = remainingBoneWeights.ToArray();
            modifiedMesh.SetNormals(remainingNormals.ToList());
            modifiedMesh.SetTangents(remainingTangents.ToList());
            modifiedMesh.SetColors(remainingColors.ToList());
            modifiedMesh.SetColors(remainingColors32.ToList());
            modifiedMesh.SetUVs(0, remainingUVs.ToList());
            modifiedMesh.SetUVs(1, remainingUV2s.ToList());
            modifiedMesh.SetUVs(2, remainingUV3s.ToList());
            modifiedMesh.SetUVs(3, remainingUV4s.ToList());

            return modifiedMesh;
        }

        /// <summary>
        /// Removes triangles from all submeshes that contain deleted vertices
        /// </summary>
        private static (Mesh modifiedMesh, bool[] deletedSubmeshes) RemoveTrianglesFromSubmeshes(
            Mesh originalMesh, 
            Mesh meshWithoutVertices, 
            int[] verticesToDeleteDescending)
        {
            var deletedSubmeshes = new bool[originalMesh.subMeshCount];
            meshWithoutVertices.subMeshCount = originalMesh.subMeshCount;

            int activeSubmeshIndex = 0;

            for (int submeshIndex = 0; submeshIndex < originalMesh.subMeshCount; submeshIndex++)
            {
                var submeshTriangles = originalMesh.GetTriangles(submeshIndex);
                var processedTriangles = ProcessTrianglesForVertexDeletion(submeshTriangles, verticesToDeleteDescending);

                var validTriangles = processedTriangles.Where(triangleIndex => triangleIndex != INVALID_TRIANGLE_INDEX).ToArray();

                if (!validTriangles.Any())
                {
                    deletedSubmeshes[submeshIndex] = true;
                    continue;
                }

                meshWithoutVertices.SetTriangles(validTriangles, activeSubmeshIndex++);
            }

            if (deletedSubmeshes.Any(isDeleted => isDeleted))
            {
                meshWithoutVertices.subMeshCount = activeSubmeshIndex;
            }

            meshWithoutVertices.bindposes = originalMesh.bindposes;

            return (meshWithoutVertices, deletedSubmeshes);
        }

        /// <summary>
        /// Processes triangles to handle vertex deletion and index remapping
        /// </summary>
        private static int[] ProcessTrianglesForVertexDeletion(int[] triangles, int[] verticesToDeleteDescending)
        {
            var processedTriangles = (int[])triangles.Clone();

            foreach (var deletedVertexIndex in verticesToDeleteDescending)
            {
                for (int triangleIndex = 0; triangleIndex < processedTriangles.Length; triangleIndex += 3)
                {
                    var vertex1 = processedTriangles[triangleIndex];
                    var vertex2 = processedTriangles[triangleIndex + 1];
                    var vertex3 = processedTriangles[triangleIndex + 2];

                    // Mark triangle for deletion if any vertex is being deleted
                    if (vertex1 == deletedVertexIndex || vertex2 == deletedVertexIndex || vertex3 == deletedVertexIndex)
                    {
                        processedTriangles[triangleIndex] = INVALID_TRIANGLE_INDEX;
                        processedTriangles[triangleIndex + 1] = INVALID_TRIANGLE_INDEX;
                        processedTriangles[triangleIndex + 2] = INVALID_TRIANGLE_INDEX;
                    }
                    else
                    {
                        // Remap indices for vertices that come after the deleted vertex
                        processedTriangles[triangleIndex] = vertex1 > deletedVertexIndex ? vertex1 - 1 : vertex1;
                        processedTriangles[triangleIndex + 1] = vertex2 > deletedVertexIndex ? vertex2 - 1 : vertex2;
                        processedTriangles[triangleIndex + 2] = vertex3 > deletedVertexIndex ? vertex3 - 1 : vertex3;
                    }
                }
            }

            return processedTriangles;
        }

        /// <summary>
        /// Preserves blend shapes from original mesh to modified mesh
        /// </summary>
        private static Mesh PreserveBlendShapes(Mesh originalMesh, Mesh modifiedMesh, List<int> deletedVertexIndices)
        {
            var deltaVertices = new Vector3[originalMesh.vertexCount];
            var deltaNormals = new Vector3[originalMesh.vertexCount];
            var deltaTangents = new Vector3[originalMesh.vertexCount];

            for (int blendShapeIndex = 0; blendShapeIndex < originalMesh.blendShapeCount; blendShapeIndex++)
            {
                var blendShapeName = originalMesh.GetBlendShapeName(blendShapeIndex);
                var frameWeight = originalMesh.GetBlendShapeFrameWeight(blendShapeIndex, 0);

                originalMesh.GetBlendShapeFrameVertices(blendShapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);

                var remainingDeltaVertices = ExtractMeshDataExcludingIndices(deltaVertices, deletedVertexIndices);
                var remainingDeltaNormals = ExtractMeshDataExcludingIndices(deltaNormals, deletedVertexIndices);
                var remainingDeltaTangents = ExtractMeshDataExcludingIndices(deltaTangents, deletedVertexIndices);

                modifiedMesh.AddBlendShapeFrame(
                    blendShapeName, 
                    frameWeight,
                    remainingDeltaVertices.ToArray(),
                    remainingDeltaNormals.ToArray(),
                    remainingDeltaTangents.ToArray());
            }

            return modifiedMesh;
        }
    }
}