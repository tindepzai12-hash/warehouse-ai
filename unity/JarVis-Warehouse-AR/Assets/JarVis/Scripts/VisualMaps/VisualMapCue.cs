using UnityEngine;

namespace JarVis.Warehouse.VisualMaps
{
    public sealed class VisualMapCue : MonoBehaviour
    {
        private readonly Transform[] faces = new Transform[3];
        private Material white,blue;
        private Mesh mesh;

        public void Initialize(Material source)
        {
            white = new Material(source); white.SetColor("_BaseColor",Color.white);
            blue = new Material(source); blue.SetColor("_BaseColor",new Color(.035f,.18f,.65f));
            mesh = new Mesh { name = "Navigation chevron" };
            mesh.vertices = new[] { new Vector3(-.44f,-.22f,0),new Vector3(0,.24f,0),new Vector3(.44f,-.22f,0),new Vector3(.27f,-.4f,0),new Vector3(0,-.11f,0),new Vector3(-.27f,-.4f,0) };
            mesh.triangles = new[] { 0,1,4,0,4,5,1,2,3,1,3,4,4,1,0,5,4,0,3,2,1,4,3,1 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            for (int index = 0; index < faces.Length; index++)
            {
                Transform part = new GameObject("Chevron " + index).transform; part.SetParent(transform,false); faces[index] = part;
                Make(part,"Blue border",blue,1.17f,.02f);
                Make(part,"White face",white,1,0);
            }
        }

        private void Make(Transform parent,string name,Material material,float size,float depth)
        {
            GameObject shape = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); shape.transform.SetParent(parent,false);
            shape.transform.localScale = Vector3.one * size; shape.transform.localPosition = Vector3.forward * depth;
            shape.GetComponent<MeshFilter>().sharedMesh = mesh; shape.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public void Show(Camera camera,Vector3 position,float bearing,bool turning)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(position - camera.transform.position,Vector3.up);
            float angle = Mathf.Clamp(bearing,-110,110);
            int count = turning ? 3 : 1;
            float distance = Vector3.Distance(position,camera.transform.position);
            float visibleWidth = 2 * distance * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * camera.aspect;
            float fit = Mathf.Min(1,visibleWidth * .65f / (turning ? 1.12f : .5f));
            for (int index = 0; index < faces.Length; index++)
            {
                faces[index].gameObject.SetActive(index < count);
                faces[index].localRotation = Quaternion.Euler(0,0,-angle);
                Vector3 offset = Quaternion.Euler(0,0,-angle) * Vector3.up * ((index - (count - 1) * .5f) * .34f);
                faces[index].localPosition = (offset + Quaternion.Euler(0,0,-angle) * Vector3.up * (.025f * Mathf.Sin(Time.unscaledTime * 2.5f))) * fit;
                faces[index].localScale = Vector3.one * (.48f * fit);
            }
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (white != null) Destroy(white);
                if (blue != null) Destroy(blue);
                if (mesh != null) Destroy(mesh);
            }
            else
            {
                if (white != null) DestroyImmediate(white);
                if (blue != null) DestroyImmediate(blue);
                if (mesh != null) DestroyImmediate(mesh);
            }
        }
    }
}
