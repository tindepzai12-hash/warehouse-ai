Shader "JarVis/Prototype Shadow" {
Properties { [PerRendererData] _MainTex("Texture",2D)="white"{} _Size("Card size and radius",Vector)=(100,50,18,0) _StencilComp("Stencil Comparison",Float)=8 _Stencil("Stencil ID",Float)=0 _StencilOp("Stencil Operation",Float)=0 _StencilWriteMask("Stencil Write Mask",Float)=255 _StencilReadMask("Stencil Read Mask",Float)=255 _ColorMask("Color Mask",Float)=15 }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]} Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
#include "UnityCG.cginc"
#include "UnityUI.cginc"
struct vertexInput {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
struct vertexOutput {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;};
float4 _Size,_ClipRect;
vertexOutput vert(vertexInput v){vertexOutput o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;o.local=v.vertex;return o;}
fixed4 frag(vertexOutput i):SV_Target {float2 p=(i.uv-.5)*(_Size.xy+24);float r=min(_Size.z,min(_Size.x,_Size.y)*.5);float2 q=abs(p)-(_Size.xy*.5-r);float d=length(max(q,0))+min(max(q.x,q.y),0)-r;float alpha=exp(-max(d,0)*max(d,0)/50)*.07*smoothstep(-1,1,d);fixed4 c=fixed4(.125,.212,.341,alpha)*i.color;
#ifdef UNITY_UI_CLIP_RECT
c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
#endif
return c;}
ENDCG } } }
