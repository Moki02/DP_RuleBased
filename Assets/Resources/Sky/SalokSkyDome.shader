// ============================================================================
// SalokSkyDome.shader
// The sky dome's day/night look: the dome's own gradient texture times a tint colour,
// plus stars made in the shader (no texture needed) that fade in at night.
// DayNightCycle.cs swaps the dome's material for one using this shader at Play and sets
// _Tint and _StarAlpha every frame; the artists' Sky_Box_01 material is never changed.
//
// It lives in a Resources folder so it is always included in builds.
//
// HOW TO EDIT: star look is Star Density / Star Size / Star Twinkle (on DayNightCycle, which
// copies them here). The gradient is the dome's original texture.
// ============================================================================
Shader "Salok/Sky Dome"
{
    Properties
    {
        _MainTex ("Gradient (from the dome's material)", 2D) = "white" {}
        _Tint ("Tint (set by DayNightCycle)", Color) = (1, 1, 1, 1)
        _StarColor ("Star Colour", Color) = (1, 1, 1, 1)
        _StarAlpha ("Stars (0 = day, 1 = night)", Range(0, 1)) = 0
        _StarDensity ("Star Density (0-1)", Range(0, 1)) = 0.06
        _StarSize ("Star Size", Range(0.02, 0.5)) = 0.16
        _StarGrid ("Star Grid (more = smaller, more stars)", Float) = 140
        _StarTwinkle ("Star Twinkle", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "SkyDome"
            Cull Off      // the dome is seen from the inside
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half4 _StarColor;
                half _StarAlpha;
                half _StarDensity;
                half _StarSize;
                float _StarGrid;
                half _StarTwinkle;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS = ws;
                o.positionHCS = TransformWorldToHClip(ws);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half Stars(float3 dir)
            {
                // Direction to longitude / latitude, then a grid of cells; some cells hold a star.
                float lon = atan2(dir.x, dir.z) / (2.0 * PI) + 0.5;
                float lat = asin(clamp(dir.y, -1.0, 1.0)) / PI + 0.5;
                float2 g = float2(lon * _StarGrid * 2.0, lat * _StarGrid);
                float2 cell = floor(g);
                float h = Hash(cell);
                if (h > _StarDensity) return 0;
                float2 centre = cell + 0.5 + (float2(Hash(cell + 7.1), Hash(cell + 3.7)) - 0.5) * 0.6;
                float d = length(g - centre);
                half star = saturate(1.0 - d / _StarSize);
                half twinkle = 1.0 - _StarTwinkle * (0.5 + 0.5 * sin(_Time.y * (2.0 + h * 40.0) + h * 100.0));
                return star * star * twinkle;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Tint;
                if (_StarAlpha > 0.001)
                {
                    float3 dir = normalize(i.positionWS - _WorldSpaceCameraPos);
                    half aboveHorizon = saturate(dir.y * 5.0);
                    col.rgb += _StarColor.rgb * Stars(dir) * _StarAlpha * aboveHorizon;
                }
                col.a = 1;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
