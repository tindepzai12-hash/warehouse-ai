using System.Collections.Generic;
using System;
using Cdm.Figma.UI;
using TMPro;
using UnityEngine;

namespace JarVis.Warehouse.UI
{
    // Shared presentation fixes; never changes screen geometry or warehouse data.
    public static class ImportedUiQuality
    {
        static readonly Dictionary<int,Sprite> cards=new Dictionary<int,Sprite>();
        [Serializable] sealed class Shape{public string id;public float r,width;public Color fill,stroke;}
        [Serializable] sealed class Shapes{public Shape[] shapes;}
        static Dictionary<string,Shape> shapes;
        static readonly Dictionary<string,Sprite> backgrounds=new Dictionary<string,Sprite>();
        static readonly Dictionary<Sprite,Sprite> tabMasks=new Dictionary<Sprite,Sprite>();
        public static void PrepareTabIcons(GameObject root)
        {
            foreach(var node in root.GetComponentsInChildren<FigmaNode>(true))
                if((node.nodeName??"").StartsWith("tab-"))
                    foreach(var image in node.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    {
                        if(image.gameObject==node.gameObject || image.sprite==null)continue;
                        var original=image.sprite;
                        if(!tabMasks.TryGetValue(original,out var white))
                        {
                            var source=original.texture;
                            var buffer=RenderTexture.GetTemporary(source.width,source.height,0);
                            var previous=RenderTexture.active;
                            Graphics.Blit(source,buffer);RenderTexture.active=buffer;
                            var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
                            texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);
                            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(buffer);
                            var pixels=texture.GetPixels32();
                            for(int i=0;i<pixels.Length;i++){pixels[i].r=pixels[i].g=pixels[i].b=255;}
                            texture.SetPixels32(pixels);texture.Apply(false,true);texture.filterMode=FilterMode.Bilinear;
                            white=Sprite.Create(texture,original.rect,original.pivot/original.rect.size,original.pixelsPerUnit,0,SpriteMeshType.FullRect,original.border);
                            tabMasks[original]=white;
                        }
                        image.sprite=white;
                    }
        }
        public static void Apply(GameObject root)
        {
            if(shapes==null)
            {
                shapes=new Dictionary<string,Shape>();
                var source=Resources.Load<TextAsset>("JarVisImportedVisuals");
                if(source!=null)foreach(var shape in JsonUtility.FromJson<Shapes>(source.text).shapes)shapes[shape.id]=shape;
            }
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                // Be Vietnam Pro has no waving-hand emoji; remove the unsupported decoration.
                if(text.text.Contains("Xin chào"))text.text=text.text.Replace("\U0001F44B","").Replace("\uFE0F","").Trim();
            }
            foreach(var node in root.GetComponentsInChildren<FigmaNode>(true))
            {
                var name=node.nodeName??"";
                var image=node.GetComponent<UnityEngine.UI.Image>();
                if(image!=null && image.enabled && shapes.TryGetValue(node.nodeId,out var shape))
                {
                    image.sprite=Background(shape);image.color=Color.white;
                    image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;image.useSpriteMesh=false;
                    UniformUiSurface.Use(image,shape.r,shape.fill,shape.stroke,shape.width);
                }
                bool card=name=="action-card" || name=="next-task-card" || name=="item-card" || name=="profile-card" || name=="list-card" || name=="stat-card" || name=="structured-card" || name=="preview-card" || name=="location-guide-card" || name=="product-item-card" || name.StartsWith("task-card-");
                if(!card)continue;
                if(image==null || !image.enabled)continue;
                int radius=(name=="list-card" || name=="stat-card" || name=="structured-card" || name=="preview-card" || name=="product-item-card")?12:16;
                image.sprite=Card(radius);image.type=UnityEngine.UI.Image.Type.Sliced;
                image.color=Color.white;image.useSpriteMesh=false;image.pixelsPerUnitMultiplier=1;
                UniformUiSurface.Use(image,radius,Color.white,JarVisDesignTokens.Border);
            }
        }
        static Sprite Background(Shape shape)
        {
            string key=shape.r+":"+ColorUtility.ToHtmlStringRGBA(shape.fill)+":"+ColorUtility.ToHtmlStringRGBA(shape.stroke)+":"+shape.width;
            if(backgrounds.TryGetValue(key,out var sprite) && sprite!=null)return sprite;
            const int scale=3;float r=shape.r*scale;
            int size=Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(r*2+16)),32,1024);
            r=Mathf.Min(r,size*.5f-1);
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Figma rounded shape",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[size*size];
            var stroke=shape.stroke;
            if(shape.width>0 && shape.fill.r>.85f && shape.fill.g>.85f && shape.fill.b>.85f)stroke=new Color(.64f,.71f,.81f,stroke.a);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var q=new Vector2(Mathf.Abs(x+.5f-size*.5f),Mathf.Abs(y+.5f-size*.5f))-Vector2.one*(size*.5f-r);
                float distance=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-r;
                var color=shape.width>0?Color.Lerp(shape.fill,stroke,Mathf.Clamp01(shape.width*scale+.5f+distance)):shape.fill;
                color.a*=Mathf.Clamp01(.5f-distance);pixels[y*size+x]=color;
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),300,0,SpriteMeshType.FullRect,new Vector4(r,r,r,r));
            sprite.name=texture.name;
            backgrounds[key]=sprite;return sprite;
        }
        public static Sprite RoundedCard(int radius) => Card(radius);
        public static Sprite ThemedCard(int radius,Color fill) => Background(new Shape{r=radius,fill=fill,stroke=new Color(.28f,.37f,.5f),width=1});
        static Sprite Card(int radius)
        {
            if(cards.TryGetValue(radius,out var cached) && cached!=null)return cached;
            const int size=192,scale=3;float r=radius*scale;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="JarVis crisp card "+radius,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var data=new Color32[size*size];var border=new Color(.64f,.71f,.81f);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var q=new Vector2(Mathf.Abs(x+.5f-size*.5f),Mathf.Abs(y+.5f-size*.5f))-Vector2.one*(size*.5f-r);
                float distance=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-r;
                var color=Color.Lerp(Color.white,border,Mathf.Clamp01(scale+.5f+distance));
                color.a=Mathf.Clamp01(.5f-distance);data[y*size+x]=color;
            }
            texture.SetPixels32(data);texture.Apply(false,true);
            var sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),300,0,SpriteMeshType.FullRect,new Vector4(r,r,r,r));
            sprite.name=texture.name;cards[radius]=sprite;return sprite;
        }
    }
}
