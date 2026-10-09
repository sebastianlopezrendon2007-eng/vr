Shader "Telemedicina/AnatomiaCristal" {Properties{_BaseColor("Color",Color)=(.4,.6,1,1)} SubShader{Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"} Pass{Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial) float4 _BaseColor; CBUFFER_END
struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 v:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.v=GetWorldSpaceViewDir(TransformObjectToWorld(a.p.xyz));return o;}
half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float rim=pow(1-abs(dot(normalize(i.n),normalize(i.v))),2.8);return half4(_BaseColor.rgb+rim*float3(.32,.36,.5),_BaseColor.a+rim*.5);}
ENDHLSL
}}}