using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using MeshDeleter.Models;
using System.Linq;
using MeshDeleter;

namespace MeshDeleter.Views
{
    [System.Serializable]
    public class MeshDeleterSelectWindow : EditorWindow
    {
        [SerializeField] private SelectBox selectBox = new SelectBox(); 
        private GameObject targetObject;
        private Mesh targetMesh;
        private MeshFilter targetMeshFilter;
        private SkinnedMeshRenderer targetSkinnedMeshRenderer;
        private List<int> selectedVertexIndices = new List<int>();
        private bool previewVertices = false;

        private static readonly Color xColor = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color yColor = new Color(0.2f, 1f, 0.2f, 1f);
        private static readonly Color zColor = new Color(0.2f, 0.4f, 1f, 1f);

        [MenuItem("Tools/MeshDeleter")]
        public static void ShowWindow()
        {
            var window = GetWindow<MeshDeleterSelectWindow>("MeshDeleter");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            // Initialize with default selectbox if not already set
            if (selectBox == null)
            {
                selectBox = new SelectBox();
            }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            GUILayout.Label("SelectBox-Based Mesh Deleter", EditorStyles.boldLabel);
            EditorGUILayout.Space(); 

            GameObject newTargetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);
            if (newTargetObject != targetObject)
            {
                targetObject = newTargetObject;
                if (targetObject != null)
                {
                    targetMeshFilter = targetObject.GetComponent<MeshFilter>();
                    targetSkinnedMeshRenderer = targetObject.GetComponent<SkinnedMeshRenderer>();
                    targetMesh = targetMeshFilter ? targetMeshFilter.sharedMesh : (targetSkinnedMeshRenderer ? targetSkinnedMeshRenderer.sharedMesh : null);
                    
                    // Always create/initialize selectbox when target object is selected
                    if (targetMesh != null)
                    {
                        selectBox = new SelectBox();
                        selectBox.center = targetObject.transform.position;
                        selectBox.size = new Vector3(0.2f, 0.2f, 0.2f);
                        selectBox.edgeColor = new Color(0f, 1f, 0f, 0.5f);
                    }
                }
                else
                {
                    targetMesh = null;
                    selectBox = null; // Clear selectbox when no target object
                }
            }

            if (targetMesh == null)
            {
                EditorGUILayout.HelpBox("Please assign a GameObject with a MeshFilter or SkinnedMeshRenderer.", MessageType.Info);
                return;
            }

            // Always show selectbox UI - selectbox should always exist when target object is selected
            EditorGUILayout.Space();
            GUILayout.Label("SelectBox", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            selectBox.center = EditorGUILayout.Vector3Field("Center", selectBox.center);
            selectBox.size = EditorGUILayout.Vector3Field("Size", selectBox.size);
            selectBox.edgeColor = EditorGUILayout.ColorField("Edge Color", selectBox.edgeColor);
            selectBox.mirroredX = EditorGUILayout.Toggle("Mirror X", selectBox.mirroredX);
            if (GUILayout.Button("Reset SelectBox to Default"))
            {
                Undo.RecordObject(this, "Reset SelectBox to Default");
                selectBox.center = targetObject.transform.position;
                selectBox.size = new Vector3(0.2f, 0.2f, 0.2f);
                selectBox.edgeColor = new Color(0f, 1f, 0f, 0.5f);
                selectBox.mirroredX = false;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            if (GUILayout.Button("Select Vertices in SelectBoxes"))
            {
                SelectVerticesInSelectBoxes();
                previewVertices = true;
            }
            if (selectedVertexIndices.Count > 0)
            {
                EditorGUILayout.HelpBox($"{selectedVertexIndices.Count} vertices selected. They are shown as red dots in the Scene view.", MessageType.Info);
                EditorGUILayout.HelpBox("Warning: Deleting vertices is destructive! Please make a backup of your mesh asset before proceeding.", MessageType.Warning);
                if (GUILayout.Button("Clear Selection"))
                {
                    selectedVertexIndices.Clear();
                    previewVertices = false;
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Delete Selected Vertices (Undoable)"))
                {
                    if (EditorUtility.DisplayDialog("Confirm Deletion", $"Are you sure you want to delete {selectedVertexIndices.Count} vertices? This action is undoable.", "Delete", "Cancel"))
                    {
                        DeleteSelectedVertices();
                        selectedVertexIndices.Clear();
                        previewVertices = false;
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (targetObject == null || targetMesh == null || selectBox == null) return;
            Handles.color = selectBox.edgeColor;
            Handles.DrawWireCube(selectBox.center, selectBox.size);
            if (selectBox.mirroredX)
            {
                Handles.DrawWireCube(new Vector3(-selectBox.center.x, selectBox.center.y, selectBox.center.z), selectBox.size);
            }
            // Always draw handles for editing
            DrawSelectBoxHandles(selectBox);
            if (previewVertices && selectedVertexIndices.Count > 0 && targetMesh != null)
            {
                // Use GL-based fast point cloud preview
                var vertices = targetMesh.vertices;
                List<Vector3> worldPositions = new List<Vector3>(selectedVertexIndices.Count);
                foreach (var idx in selectedVertexIndices)
                {
                    worldPositions.Add(targetObject.transform.TransformPoint(vertices[idx]));
                }
                SelectBoxVertexPreviewGL.DrawPoints(worldPositions, Color.red);
            }
        }

        private void DrawSelectBoxHandles(SelectBox selectBox)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = Handles.PositionHandle(selectBox.center, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Move SelectBox");
                selectBox.center = newCenter;
                Repaint();
            }
            // Face handles for resizing
            Vector3 halfSize = selectBox.size * 0.5f;
            Vector3[] faceCenters = new Vector3[]
            {
                selectBox.center + Vector3.right * halfSize.x,
                selectBox.center + Vector3.left * halfSize.x,
                selectBox.center + Vector3.up * halfSize.y,
                selectBox.center + Vector3.down * halfSize.y,
                selectBox.center + Vector3.forward * halfSize.z,
                selectBox.center + Vector3.back * halfSize.z
            };
            Vector3[] faceNormals = new Vector3[]
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
            };
            for (int i = 0; i < faceCenters.Length; i++)
            {
                Color handleColor = (i < 2) ? xColor : (i < 4) ? yColor : zColor;
                Handles.color = handleColor;
                EditorGUI.BeginChangeCheck();
                float handleSize = HandleUtility.GetHandleSize(faceCenters[i]) * 0.15f; // smaller handle
                Vector3 newFaceCenter = Handles.Slider(faceCenters[i], faceNormals[i], handleSize, Handles.CubeHandleCap, 0.05f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Resize SelectBox Face");
                    Vector3 delta = newFaceCenter - faceCenters[i];
                    Vector3 newSize = selectBox.size;
                    if (i == 0) newSize.x += delta.x; // Right
                    else if (i == 1) newSize.x -= delta.x; // Left
                    else if (i == 2) newSize.y += delta.y; // Up
                    else if (i == 3) newSize.y -= delta.y; // Down
                    else if (i == 4) newSize.z += delta.z; // Forward
                    else if (i == 5) newSize.z -= delta.z; // Back
                    newSize = Vector3.Max(newSize, Vector3.one * 0.01f);
                    selectBox.size = newSize;
                    Repaint();
                }
            }
        }

        private void SelectVerticesInSelectBoxes()
        {
            selectedVertexIndices.Clear();
            if (targetMesh == null || targetObject == null || selectBox == null) return;
            var vertices = targetMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = targetObject.transform.TransformPoint(vertices[i]);
                if (selectBox.GetBounds().Contains(worldPos) || (selectBox.mirroredX && selectBox.GetMirroredBounds().Contains(worldPos)))
                {
                    selectedVertexIndices.Add(i);
                }
            }
        }

        private void DeleteSelectedVertices()
        {
            if (targetMesh == null || selectedVertexIndices.Count == 0) return;
            Undo.RecordObject(targetMesh, "Delete Vertices in SelectBox");
            var deletedMesh = MeshDeleter.RemoveTrianglesByVertexIndices(targetMesh, selectedVertexIndices.Distinct().ToList());
            if (targetMeshFilter)
            {
                targetMeshFilter.sharedMesh = deletedMesh;
            }
            else if (targetSkinnedMeshRenderer)
            {
                targetSkinnedMeshRenderer.sharedMesh = deletedMesh;
            }
            AssetDatabase.CreateAsset(deletedMesh, AssetDatabase.GenerateUniqueAssetPath("Assets/" + targetMesh.name + "_selectBoxDeleted.asset"));
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Vertices Deleted", "A new mesh asset with deleted vertices has been created and assigned.", "OK");
        }
    }
} 