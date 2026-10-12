Shader "JarVis/Uniform UI Surface" {
Properties { [PerRendererData] _MainTex("Texture",2D)="white"{} _Fill("Fill",Color)=(1,1,1,1) _Stroke("Border",Color)=(.64,.71,.81,1) _Size("Size",Vector)=(100,50,12,1) _RectCenter("Rect center",Vector)=(0,0,0,0) _FillEnd("Gradient end",Color)=(1,1,1,1) _Gradient("Gradient",Float)=0 _Glass("Glass",Float)=0 _StencilComp("Stencil Comparison",Float)=8 _Stencil("Stencil ID",Float)=0 _StencilOp("Stencil Operation",Float)=0 _StencilWriteMask("Stencil Write Mask",Float)=255 _StencilReadMask("Stencil Read Mask",Float)=255 _ColorMask("Color Mask",Float)=15 }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"} Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]} Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
#include "UnityCG.cginc"
#include "UnityUI.cginc"
struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;float4 screen:TEXCOORD2;};
float4 _Fill,_FillEnd,_Stroke,_Size,_ClipRect,_RectCenter;float _Glass,_Gradient;sampler2D _JarVisBackdrop;float4 _JarVisBackdrop_TexelSize;float _JarVisHasBackdrop;
v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.local=v.vertex;o.color=v.color;o.uv=v.uv;o.screen=ComputeScreenPos(o.vertex);return o;}
fixed4 frag(v2f i):SV_Target {float2 p=(i.uv-.5)*_Size.xy;float r=min(_Size.z,min(_Size.x,_Size.y)*.5);float2 q=abs(p)-(_Size.xy*.5-r);float d=length(max(q,0))+min(max(q.x,q.y),0)-r;float aa=max(fwidth(d),.45);float coverage=1-smoothstep(-aa,aa,d);float edge=_Size.w>0?smoothstep(-_Size.w-aa,-_Size.w+aa,d):0;float4 baseFill=lerp(_Fill,_FillEnd,_Gradient*saturate((i.uv.x+1-i.uv.y)*.5));float4 c=lerp(baseFill,_Stroke,edge);c*=i.color;
if(_Glass>.5){float2 uv=i.screen.xy/i.screen.w;float2 delta=_JarVisBackdrop_TexelSize.xy*3;float2 refract=normalize(p+float2(.001,.001))*edge*.003;float3 b=tex2D(_JarVisBackdrop,uv+refract).rgb*.25;b+=(tex2D(_JarVisBackdrop,uv+delta).rgb+tex2D(_JarVisBackdrop,uv-delta).rgb+tex2D(_JarVisBackdrop,uv+float2(delta.x,-delta.y)).rgb+tex2D(_JarVisBackdrop,uv+float2(-delta.x,delta.y)).rgb)*.1875; c.rgb=lerp(c.rgb,b,_JarVisHasBackdrop*.3);float light=saturate(i.uv.y-.4)*edge;c.rgb+=light*.35+pow(saturate(i.uv.y),5)*.09;}
c.rgb=saturate(c.rgb); // UI colors stay inside display range; no HDR bloom halo.
c.a*=coverage;
#ifdef UNITY_UI_CLIP_RECT
c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
#endif
return c; }
ENDCG } } }


