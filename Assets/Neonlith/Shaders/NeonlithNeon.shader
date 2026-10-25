// Neonlith/Neon: unbeleuchtete Leuchtlinien und Leuchtflächen mit Fernberuhigung (far_calm.gdshaderinc, KLARBILD.md):
// in der Ferne blendet die Linie zur Tönung (Nebelfarbe) aus, statt als dünne helle Linie zu flimmern. Der gemeinsame
// Puls _NeonPulse (ShaderGlobals) hebt die Helligkeit bei großen Momenten sanft an. Keine Texturen, ein Durchgang,
// SRP-Batcher-fähig (CBUFFER UnityPerMaterial), Vulkan/Quest-tauglich, kein Lesen von Bild oder Tiefe.
Shader "Neonlith/Neon"
{
    Properties
    {
        _BaseColor ("Farbe", Color) = (1, 1, 1, 1)
        _Intensity ("Helligkeit", Range(0, 4)) = 1
        _FarStart ("Fernberuhigung ab (m)", Float) = 40
        _FarEnd ("Fernberuhigung bis (m)", Float) = 120
        _FarTint ("Tönung in der Ferne (0 = Nebelfarbe)", Color) = (0, 0, 0, 0)
        [Toggle] _UsePulse ("NeonPulse mitgehen", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                float _FarStart;
                float _FarEnd;
                half4 _FarTint;
                half _UsePulse;
            CBUFFER_END
            // globale Werte (ShaderGlobals.cs): außerhalb des Material-CBUFFER
            half _NeonPulse;
            half _BtKlar;

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float dist : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.dist = distance(positionWS, GetCameraPositionWS());
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half pulse = 1.0h + 0.35h * _NeonPulse * _UsePulse;
                half3 col = _BaseColor.rgb * _Intensity * pulse;
                // Fernberuhigung: zur Tönung (Nebelfarbe, oder _FarTint wenn gesetzt) ausblenden
                half3 tint = _FarTint.a > 0.0h ? _FarTint.rgb : unity_FogColor.rgb;
                half far = saturate((IN.dist - _FarStart) / max(_FarEnd - _FarStart, 0.01));
                col = lerp(col, tint, far * far);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
