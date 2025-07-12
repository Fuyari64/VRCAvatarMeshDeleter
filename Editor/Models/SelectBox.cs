using UnityEngine;

namespace MeshDeleter.Models
{
    [System.Serializable]
    public class SelectBox
    {
        public Vector3 center = Vector3.zero;
        public Vector3 size = Vector3.one; 
        public Color edgeColor = new Color(0f, 1f, 0f, 0.5f);
        public bool mirroredX = false;

        public SelectBox() { }
        public SelectBox(Vector3 center, Vector3 size, Color color, bool mirroredX = false)
        {
            this.center = center;
            this.size = size;
            this.edgeColor = color;
            this.mirroredX = mirroredX;
        }

        public Bounds GetBounds() => new Bounds(center, size);
        public Bounds GetMirroredBounds() => new Bounds(new Vector3(-center.x, center.y, center.z), size);
    }
} 