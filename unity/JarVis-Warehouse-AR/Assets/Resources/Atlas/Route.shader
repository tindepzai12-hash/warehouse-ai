Shader "Atlas/Route Fade"
{
    Properties { _Color("Route",Color)=(.1,.45,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct V {float4 vertex:POSITION;};
            float4 vert(V v):SV_POSITION{return UnityObjectToClipPos(v.vertex);}
            fixed4 frag():SV_Target{return _Color;}
            ENDCG
        }
    }
}
