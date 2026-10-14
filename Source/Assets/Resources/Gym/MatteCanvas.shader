Shader "GripBreak/MatteCanvas" {
    Properties {
        _Color("Canvas tint",Color)=(1,1,1,1)
        _MainTex("Printed weave",2D)="white"{}
        [Normal] _BumpMap("Weave normal",2D)="bump"{}
        _BumpScale("Normal strength",Range(0,1))=.18
        // Preserve the authoring map reference; Lambert never samples gloss.
        [HideInInspector] _MetallicGlossMap("Authoring smoothness (unused)",2D)="black"{}
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex,_BumpMap;
        fixed4 _Color;
        half _BumpScale;
        struct Input {float2 uv_MainTex;float2 uv_BumpMap;};
        void surf(Input IN,inout SurfaceOutput o){
            o.Albedo=tex2D(_MainTex,IN.uv_MainTex).rgb*_Color.rgb;
            half3 normal=UnpackNormal(tex2D(_BumpMap,IN.uv_BumpMap));
            normal.xy*=_BumpScale;o.Normal=normalize(normal);
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
