// Inverted-hull outline. The Fizzy Moo mascot is drawn with a heavy black
// keyline, so flat-shaded primitives alone never look like the brand. Adding
// this as a SECOND material on each renderer draws the mesh twice: once
// front-faces-culled and pushed out along its normals in solid black, then
// normally on top. Cheap, needs no post-processing, works in built-in RP.
Shader "FizzyMoo/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Colour", Color) = (0.07, 0.07, 0.08, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.15)) = 0.035
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" }
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                // Extrude in view space so the outline keeps a stable thickness
                // regardless of how the part is scaled in the rig.
                float4 viewPos = mul(UNITY_MATRIX_MV, v.vertex);
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                viewPos.xyz += viewNormal * _OutlineWidth;
                o.pos = mul(UNITY_MATRIX_P, viewPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return _OutlineColor; }
            ENDCG
        }
    }
    Fallback Off
}
