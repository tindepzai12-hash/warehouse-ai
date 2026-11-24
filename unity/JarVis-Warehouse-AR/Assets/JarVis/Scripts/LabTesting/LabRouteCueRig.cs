using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace JarVis.Warehouse.Lab
{
    // Three pooled physical glyphs shared between path and turn-board modes.
    public sealed class LabRouteCueRig:MonoBehaviour
    {
        readonly Transform[] glyphs=new Transform[3];
        readonly MeshRenderer[][] renderers=new MeshRenderer[3][];
        MaterialPropertyBlock tint;
        Material faceMaterial,edgeMaterial,sideMaterial;
        Mesh faceMesh,rimMesh,sideMesh;
        public int VisibleCount {get;private set;}
        public Transform Glyph(int i)=>glyphs[i];
        public MeshRenderer[] Renderers(int i)=>renderers[i];
        public void Initialize(Material source)
        {
            source=Resources.Load<Material>("LabTest/LiveCueTransparent")??source;
            tint=new MaterialPropertyBlock();
            faceMaterial=Transparent(source,"JarVis cue face");
            edgeMaterial=Transparent(source,"JarVis cue edge");
            sideMaterial=Transparent(source,"JarVis cue depth");
            faceMesh=new Mesh{name="JarVis shared chevron face"};
            var points=LabChevronShape.Points;var vertices=new Vector3[points.Length];
            for(int i=0;i<points.Length;i++)vertices[i]=points[i];
            faceMesh.vertices=vertices;
            var t=LabChevronShape.Triangles;var both=new int[t.Length*2];
            for(int i=0;i<t.Length;i+=3){both[i]=t[i];both[i+1]=t[i+1];both[i+2]=t[i+2];both[t.Length+i]=t[i+2];both[t.Length+i+1]=t[i+1];both[t.Length+i+2]=t[i];}
            faceMesh.triangles=both;faceMesh.RecalculateBounds();faceMesh.RecalculateNormals();
            var outline=LabChevronShape.Outline;var rimVertices=new Vector3[outline.Length];
            for(int i=0;i<outline.Length;i++)rimVertices[i]=outline[i];
            rimMesh=new Mesh{name="JarVis constant-width rim"};rimMesh.vertices=rimVertices;rimMesh.triangles=both;rimMesh.RecalculateBounds();rimMesh.RecalculateNormals();
            sideMesh=new Mesh{name="JarVis shared chevron extrusion"};
            var sides=new Vector3[points.Length*4];var indices=new int[points.Length*6];
            for(int i=0;i<points.Length;i++)
            {
                int j=(i+1)%points.Length,n=i*4,k=i*6;
                sides[n]=new Vector3(outline[i].x,outline[i].y,.003f);sides[n+1]=new Vector3(outline[j].x,outline[j].y,.003f);
                sides[n+2]=new Vector3(outline[j].x,outline[j].y,.04f);sides[n+3]=new Vector3(outline[i].x,outline[i].y,.04f);
                indices[k]=n;indices[k+1]=n+1;indices[k+2]=n+2;indices[k+3]=n;indices[k+4]=n+2;indices[k+5]=n+3;
            }
            sideMesh.vertices=sides;sideMesh.triangles=indices;sideMesh.RecalculateBounds();sideMesh.RecalculateNormals();
            for(int i=0;i<3;i++)
            {
                glyphs[i]=new GameObject("Route chevron "+i).transform;glyphs[i].SetParent(transform,false);
                renderers[i]=new[]{Make(glyphs[i],"Deep blue rim",rimMesh,edgeMaterial,1,.012f),
                    Make(glyphs[i],"Cool white face",faceMesh,faceMaterial,1,0),
                    Make(glyphs[i],"Extruded sides",sideMesh,sideMaterial,1,0)};
                glyphs[i].gameObject.SetActive(false);
            }
        }
        static Material Transparent(Material source,string name)
        {
            var m=new Material(source){name=name,renderQueue=(int)RenderQueue.Transparent+5};
            m.SetOverrideTag("RenderType","Transparent");m.SetFloat("_Surface",1);m.SetFloat("_ZWrite",0);
            m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);m.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Cull",(float)CullMode.Off);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }
        static MeshRenderer Make(Transform parent,string name,Mesh mesh,Material mat,float scale,float z)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localScale=new Vector3(scale,scale,1);go.transform.localPosition=Vector3.forward*z;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        public void PlacePath(Transform frame,LabCueToken[] tokens,int count,float height)
        {
            for(int i=0;i<3;i++)
            {
                glyphs[i].gameObject.SetActive(i<count);
                if(i>=count)continue;
                var token=tokens[i];
                glyphs[i].position=frame.TransformPoint(new Vector3(token.x,height,token.z));
                var direction=frame.TransformDirection(new Vector3(token.dx,0,token.dz));
                glyphs[i].rotation=Quaternion.LookRotation(direction,frame.up)*Quaternion.Euler(90,0,0);
                glyphs[i].localScale=new Vector3(LabRouteCueSampler.Width/.92f,LabRouteCueSampler.Depth/.575f,1);
            }
            VisibleCount=count;
        }
        public void PlaceBoard(Camera camera,Vector3 position,float bearing)
        {
            var rotation=camera.transform.rotation*Quaternion.Euler(0,0,-bearing);
            float depth=Vector3.Dot(position-camera.transform.position,camera.transform.forward);
            float height=2*Mathf.Max(.05f,depth)*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)*(128f/412)*camera.aspect;
            float unit=height/1.94f;
            for(int i=0;i<3;i++)
            {
                glyphs[i].gameObject.SetActive(true);glyphs[i].position=position+rotation*(Vector3.up*((i-1)*.64f*unit));
                glyphs[i].rotation=rotation;glyphs[i].localScale=Vector3.one*unit;
            }
            VisibleCount=3;
        }
        public void SetVisible(int index,bool value)
        {
            bool previous=glyphs[index].gameObject.activeSelf;glyphs[index].gameObject.SetActive(value);
            if(previous!=value)VisibleCount+=value?1:-1;
        }
        public void Paint(int index,float alpha,float light)
        {
            SetTint(renderers[index][0],new Color(.035f,.16f,.47f,alpha));
            SetTint(renderers[index][1],new Color(.92f*light,.98f*light,1,alpha));
            SetTint(renderers[index][2],new Color(.025f,.09f,.28f,alpha));
        }
        void SetTint(MeshRenderer renderer,Color color)
        {tint.SetColor("_BaseColor",color);tint.SetColor("_Color",color);renderer.SetPropertyBlock(tint);}
        public void Hide(){for(int i=0;i<3;i++)if(glyphs[i]!=null)glyphs[i].gameObject.SetActive(false);VisibleCount=0;}
        // Includes face, border and extrusion; never checks the centre alone.
        public bool ProjectedBounds(Camera camera,int index,out Rect bounds)
        {
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            bool inFront=true;
            foreach(var r in renderers[index])
            {
                foreach(var p in LabChevronShape.Outline)
                {
                    for(int side=0;side<2;side++)
                    {
                        Vector3 local=new Vector3(p.x,p.y,side*.04f);
                        var vp=camera.WorldToViewportPoint(r.transform.TransformPoint(local));
                        if(vp.z<=camera.nearClipPlane+.03f)inFront=false;
                        min=Vector2.Min(min,new Vector2(vp.x,vp.y));max=Vector2.Max(max,new Vector2(vp.x,vp.y));
                    }
                }
            }
            bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y);return inFront;
        }
        void OnDestroy()
        {
            Release(faceMaterial);Release(edgeMaterial);Release(sideMaterial);Release(faceMesh);Release(rimMesh);Release(sideMesh);
        }
        static void Release(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
    }
}

