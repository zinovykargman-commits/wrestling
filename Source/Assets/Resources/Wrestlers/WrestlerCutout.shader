Shader "GripBreak/WrestlerCutout" {
    Properties {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex("Strands and eye surface",2D)="white"{}
        _Glossiness("Smoothness",Range(0,1))=.08
        _Cutoff("Alpha cutoff",Range(0,1))=.15
    }
    SubShader {
        Tags {"RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull Off
        LOD 250
        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows alphatest:_Cutoff addshadow
        #pragma target 3.0
        sampler2D _MainTex;fixed4 _Color;half _Glossiness;
        struct Input {float2 uv_MainTex;};
        void surf(Input IN,inout SurfaceOutputStandardSpecular o){
            fixed4 c=tex2D(_MainTex,IN.uv_MainTex);o.Albedo=c.rgb*_Color.rgb;o.Alpha=c.a;
            o.Specular=.01;o.Smoothness=_Glossiness;o.Occlusion=1;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
}
