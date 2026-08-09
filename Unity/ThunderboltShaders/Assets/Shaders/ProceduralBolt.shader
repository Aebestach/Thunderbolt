Shader "Thunderbolt/ProceduralBolt"
{
	Properties
	{
		_Color ("Color", Color) = (1.4, 1.5, 2.0, 1)
		_Fade ("Fade", Range(0, 1)) = 1
		_Seed ("Seed", Float) = 0
		_CoreWidth ("Core Width", Float) = 0.014
		_GlowWidth ("Glow Width", Float) = 0.065
		_Intensity ("Intensity", Float) = 8
		_Bend ("Bend", Float) = 0.65
		_Branch ("Branch", Float) = 1.0
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
		}

		Pass
		{
			Blend SrcAlpha One
			Cull Off
			ZWrite Off
			ZTest LEqual

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"

			float4 _Color;
			float _Fade;
			float _Seed;
			float _CoreWidth;
			float _GlowWidth;
			float _Intensity;
			float _Bend;
			float _Branch;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			float Hash11(float p)
			{
				p = frac(p * 0.1031);
				p *= p + 33.33;
				p *= p + p;
				return frac(p);
			}

			float Noise1D(float x)
			{
				float i = floor(x);
				float f = frac(x);
				float a = Hash11(i + _Seed);
				float b = Hash11(i + 1.0 + _Seed);
				float u = f * f * (3.0 - 2.0 * f);
				return lerp(a, b, u) * 2.0 - 1.0;
			}

			float BoltProfile(float x, float halfWidth)
			{
				return saturate(1.0 - abs(x) / max(1e-4, halfWidth));
			}

			// One fork: denser along length, drifts away from the main channel.
			float BranchBolt(float2 uv, float bend, float side, float dens, float phase)
			{
				float cell = floor(uv.y * dens + phase);
				float mask = step(0.45, Hash11(cell + _Seed * (2.7 + phase)));
				float local = frac(uv.y * dens + phase);
				// Short forks that die out quickly.
				float lenFade = smoothstep(0.0, 0.12, local) * smoothstep(1.0, 0.35, local);
				float drift = side * (0.08 + 0.22 * local);
				float jag = Noise1D(uv.y * 14.0 + phase * 13.0) * 0.04;
				float bx = uv.x - 0.5 - bend * 0.85 - drift - jag;
				float body = BoltProfile(bx, _CoreWidth * 0.7) * 0.9
					+ BoltProfile(bx, _GlowWidth * 0.55) * 0.35;
				return body * mask * lenFade;
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;
				// Every quad represents one polyline edge. Pull the channel back to its
				// centreline at both ends so adjacent edges and vessel anchors meet exactly.
				float endpointPin =
					smoothstep(0.0, 0.14, uv.y)
					* smoothstep(0.0, 0.14, 1.0 - uv.y);

				// Stronger low/mid zigzags so the channel wanders like reference bolts.
				float bend =
					Noise1D(uv.y * 1.1) * 0.50 +
					Noise1D(uv.y * 2.8 + 7.0) * 0.28 +
					Noise1D(uv.y * 6.5 + 19.0) * 0.14 +
					Noise1D(uv.y * 16.0 + 37.0) * 0.08;
				bend = clamp(bend * _Bend, -0.28, 0.28) * endpointPin;

				float x = uv.x - 0.5 - bend;
				float core = BoltProfile(x, _CoreWidth);
				float glow = BoltProfile(x, _GlowWidth);
				float bolt = core * 1.4 + glow * 0.6;

				// Several forks on both sides.
				float branches = 0.0;
				branches += BranchBolt(uv, bend, 1.0, 5.0, 0.0);
				branches += BranchBolt(uv, bend, -1.0, 6.5, 1.7);
				branches += BranchBolt(uv, bend, 1.0, 9.0, 3.1) * 0.75;
				branches += BranchBolt(uv, bend, -1.0, 11.0, 4.4) * 0.55;
				bolt += branches * _Branch * endpointPin;

				// No end fade — pierce paths are many short pieces that must meet with no gaps.
				// Fade only at the side borders to prevent a clipped rectangular quad edge.
				float sideEnvelope =
					smoothstep(0.0, 0.06, uv.x)
					* smoothstep(0.0, 0.06, 1.0 - uv.x);
				bolt *= sideEnvelope;

				float flicker = 0.82 + 0.18 * Hash11(floor(_Time.y * 45.0) + _Seed);
				bolt *= flicker;

				float3 rgb = _Color.rgb * bolt * _Intensity;
				float alpha = saturate(bolt) * _Fade * _Color.a;
				return float4(rgb, alpha);
			}
			ENDCG
		}
	}

	FallBack Off
}
