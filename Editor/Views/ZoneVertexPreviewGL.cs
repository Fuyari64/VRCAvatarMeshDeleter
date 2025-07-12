using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MeshDeleter.Views
{
    public static class SelectBoxVertexPreviewGL
    {
        private static Material _pointMaterial;
        private static readonly Color DefaultColor = Color.red;
        private static readonly float VertexVisualizerSize = 0.001f;

        private static void InitMaterial()
        {
            if (_pointMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                _pointMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                // Enable alpha blending
                _pointMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _pointMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _pointMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _pointMaterial.SetInt("_ZWrite", 0);
                _pointMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            }
        }

        public static void DrawPoints(List<Vector3> worldPositions, Color? color = null)
        {
            if (worldPositions == null || worldPositions.Count == 0)
                return;
            InitMaterial();
            Camera cam = SceneView.lastActiveSceneView?.camera;
            if (cam == null) return;
            _pointMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.QUADS);
            Color c = color ?? DefaultColor;
            GL.Color(c);
            foreach (var pos in worldPositions)
            {
                Vector3 right = cam.transform.right * SelectBoxVertexPreviewGL.VertexVisualizerSize;
                Vector3 up = cam.transform.up * SelectBoxVertexPreviewGL.VertexVisualizerSize;
                GL.Vertex(pos - right - up);
                GL.Vertex(pos - right + up);
                GL.Vertex(pos + right + up);
                GL.Vertex(pos + right - up);
            }
            GL.End();
            GL.PopMatrix();
        }
    }
} 