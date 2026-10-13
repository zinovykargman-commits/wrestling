Shader "GripBreak/WrestlerOpaque" {
    Properties {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex("Albedo",2D)="white"{}
        _BumpMap("Anatomical normal",2D)="bump"{}
        _BumpScale("Normal strength",Range(0,1))=.18
        _Glossiness("Smoothness",Range(0,1))=.15
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        LOD 250
        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex,_BumpMap;fixed4 _Color;half _Glossiness,_BumpScale;
        struct Input {float2 uv_MainTex;float2 uv_BumpMap;};
        void surf(Input IN,inout SurfaceOutputStandardSpecular o){
            o.Albedo=tex2D(_MainTex,IN.uv_MainTex).rgb*_Color.rgb;
            half3 n=UnpackNormal(tex2D(_BumpMap,IN.uv_BumpMap));n.xy*=_BumpScale;o.Normal=normalize(n);
            o.Specular=.018;o.Smoothness=_Glossiness;o.Occlusion=1;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
