// Generates the maps of a hotspot atlas. Pass 0 writes a data texture (height, wear, edge band, patch index) that
// the other passes read. Atlas coordinates: normalized, origin at the top-left (same as the layout rects).
Shader "Hidden/HarryTools/HotspotGenerator" {
    Properties {
        _MainTex ("", 2D) = "black" {}
    }
    SubShader {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        #define MAX_PATCHES 256

        sampler2D _DataTex;
        float4 _DataTex_TexelSize;

        float4 _AtlasWorld; // world width, world height (metres), metres per pixel, unused
        int _PatchCount;
        float4 _Patches[MAX_PATCHES];    // xMin, yMin, xMax, yMax (normalized, top-left origin)
        float4 _PatchFlags[MAX_PATCHES]; // tiling axis (0 none, 1 horizontal, 2 vertical), rotate layers, random, random

        float _Intensity, _BevelWidth, _EdgeTone, _ChipSize, _ChipDensity, _GapDepth, _Seed;
        int _Channel;

        sampler2D _TopAlbedo, _TopNormal, _TopMS;
        float4 _TopTint;
        float4 _TopParams; // metallic, smoothness, normal strength, lod
        float4 _TopTiling; // repetitions across the atlas (x, y), has normal, has metallic/smoothness
        sampler2D _BottomAlbedo, _BottomNormal, _BottomMS;
        float4 _BottomTint;
        float4 _BottomParams;
        float4 _BottomTiling;

        struct PatchInfo {
            int index;
            float dist;   // metres to the nearest border that does not repeat
            float tiling;
            float rotate;
            float2 rnd;
        };

        PatchInfo FindPatch(float2 uv) {
            PatchInfo p;
            p.index = -1; p.dist = 0; p.tiling = 0; p.rotate = 0; p.rnd = 0;
            float2 n = float2(uv.x, 1 - uv.y);
            [loop]
            for (int i = 0; i < _PatchCount; i++) {
                float4 r = _Patches[i];
                if (n.x >= r.x && n.x < r.z && n.y >= r.y && n.y < r.w) {
                    float4 f = _PatchFlags[i];
                    float2 a = (n - r.xy) * _AtlasWorld.xy;
                    float2 b = (r.zw - n) * _AtlasWorld.xy;
                    float dx = f.x == 1 ? 1e5 : min(a.x, b.x);
                    float dy = f.x == 2 ? 1e5 : min(a.y, b.y);
                    p.index = i; p.dist = min(dx, dy); p.tiling = f.x; p.rotate = f.y; p.rnd = f.zw;
                    return p;
                }
            }
            return p;
        }

        float Hash(int2 c, float seed) {
            uint h = (uint)c.x * 73856093u ^ (uint)c.y * 19349663u ^ (uint)(seed * 1000.0) * 83492791u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return (h & 0xffffffu) / 16777216.0;
        }

        int2 Wrap(int2 c, int2 period) { return ((c % period) + period) % period; }

        // Value noise that repeats every `period` cells, so it wraps seamlessly across the atlas.
        float ValueNoise(float2 x, int2 period, float seed) {
            int2 i = (int2)floor(x);
            float2 f = frac(x);
            f = f * f * (3 - 2 * f);
            float a = Hash(Wrap(i, period), seed);
            float b = Hash(Wrap(i + int2(1, 0), period), seed);
            float c = Hash(Wrap(i + int2(0, 1), period), seed);
            float d = Hash(Wrap(i + int2(1, 1), period), seed);
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float Fbm(float2 uv, float seed) {
            float2 cells = max(1, round(_AtlasWorld.xy / _ChipSize));
            float2 x = float2(uv.x, 1 - uv.y) * cells;
            float sum = 0, amp = 0.5, norm = 0;
            [unroll]
            for (int o = 0; o < 3; o++) {
                sum += amp * ValueNoise(x, (int2)cells, seed + o * 0.017);
                norm += amp;
                x *= 2; cells *= 2; amp *= 0.5;
            }
            return sum / norm;
        }

        float BottomLevel() { return 1 - saturate(_GapDepth); }

        float4 ComputeData(float2 uv) {
            PatchInfo p = FindPatch(uv);
            float bottom = BottomLevel();
            if (p.index < 0) return float4(bottom, 0, 0, 0);

            float t = saturate(p.dist / _BevelWidth);
            float profile = sqrt(1 - (1 - t) * (1 - t)); // rounded bevel
            float height = lerp(bottom, 1, profile);

            float wear = 0;
            if (_Intensity > 0) {
                // Contrasted noise so chips are blobs with irregular outlines rather than a uniform band.
                float n = saturate((Fbm(uv, _Seed * 0.001 + p.index * 0.0731) - 0.5) * 2.2 + 0.5);
                float reach = saturate(1 - p.dist / (_ChipSize * (1 + 3 * _ChipDensity)));
                float threshold = 1 - _Intensity * 0.8 * reach;
                wear = smoothstep(threshold, threshold + 0.04, n);
            }
            height = lerp(height, bottom, wear);
            float edge = 1 - saturate(p.dist / (_BevelWidth * 2));
            return float4(height, wear, edge, p.index + 1);
        }

        // Layer coordinates in repetitions, with a per-patch offset (and a 90° turn when allowed) for variety.
        float2 LayerUV(PatchInfo p, float2 uv, float4 tiling, out float rotated) {
            float2 t = uv * tiling.xy;
            rotated = 0;
            if (p.index >= 0) {
                t += floor(p.rnd * 16) + p.rnd;
                if (p.rotate > 0.5 && p.tiling == 0 && p.rnd.x > 0.5) {
                    t = float2(t.y, -t.x);
                    rotated = 1;
                }
            }
            return t;
        }

        float4 SampleLayer(sampler2D tex, float2 t, float lod) { return tex2Dlod(tex, float4(frac(t), 0, lod)); }

        float3 LayerNormal(sampler2D tex, float2 t, float4 params, float4 tiling, float rotated) {
            if (tiling.z < 0.5) return float3(0, 0, 1);
            float3 n = UnpackNormal(SampleLayer(tex, t, params.w));
            n.xy *= params.z;
            if (rotated > 0.5) n.xy = float2(-n.y, n.x);
            return normalize(n);
        }

        void LayerSurface(sampler2D ms, float2 t, float4 params, float4 tiling, out float metallic, out float smoothness) {
            metallic = params.x;
            smoothness = params.y;
            if (tiling.w > 0.5) {
                float4 s = SampleLayer(ms, t, params.w);
                metallic = s.r;
                smoothness = s.a;
            }
        }

        struct Layers {
            PatchInfo patch;
            float4 data;
            float2 topUV, bottomUV;
            float topRotated, bottomRotated;
        };

        Layers GetLayers(float2 uv) {
            Layers l;
            l.patch = FindPatch(uv);
            l.data = tex2D(_DataTex, uv);
            l.topUV = LayerUV(l.patch, uv, _TopTiling, l.topRotated);
            l.bottomUV = LayerUV(l.patch, uv, _BottomTiling, l.bottomRotated);
            return l;
        }

        // Amount of the bottom layer: all of it in the gaps, the wear mask inside patches.
        float BottomAmount(Layers l) { return l.patch.index < 0 ? 1 : l.data.g; }

        float4 FragData(v2f_img i) : SV_Target { return ComputeData(i.uv); }

        float4 FragBaseColor(v2f_img i) : SV_Target {
            Layers l = GetLayers(i.uv);
            float3 top = SampleLayer(_TopAlbedo, l.topUV, _TopParams.w).rgb * _TopTint.rgb;
            float3 bottom = SampleLayer(_BottomAlbedo, l.bottomUV, _BottomParams.w).rgb * _BottomTint.rgb;
            float amount = BottomAmount(l);
            float3 worn = lerp(top, saturate(top + 0.25), _EdgeTone * l.data.b * (1 - amount));
            return float4(lerp(worn, bottom, amount), 1);
        }

        float Height(float2 uv) { return tex2Dlod(_DataTex, float4(uv, 0, 0)).r; }

        float4 FragNormal(v2f_img i) : SV_Target {
            Layers l = GetLayers(i.uv);
            float2 px = _DataTex_TexelSize.xy;
            float scale = _BevelWidth * 2 / (2 * _AtlasWorld.z); // height units → metres, over two pixels
            float dx = (Height(i.uv + float2(px.x, 0)) - Height(i.uv - float2(px.x, 0))) * scale;
            float dy = (Height(i.uv + float2(0, px.y)) - Height(i.uv - float2(0, px.y))) * scale;
            float3 heightNormal = normalize(float3(-dx, -dy, 1));

            float3 topN = LayerNormal(_TopNormal, l.topUV, _TopParams, _TopTiling, l.topRotated);
            float3 bottomN = LayerNormal(_BottomNormal, l.bottomUV, _BottomParams, _BottomTiling, l.bottomRotated);
            float3 layerNormal = normalize(lerp(topN, bottomN, BottomAmount(l)));

            float3 n = normalize(float3(heightNormal.xy + layerNormal.xy, heightNormal.z * layerNormal.z));
            return float4(n * 0.5 + 0.5, 1);
        }

        float4 FragMask(v2f_img i) : SV_Target {
            Layers l = GetLayers(i.uv);
            float tm, ts, bm, bs;
            LayerSurface(_TopMS, l.topUV, _TopParams, _TopTiling, tm, ts);
            LayerSurface(_BottomMS, l.bottomUV, _BottomParams, _BottomTiling, bm, bs);
            float amount = BottomAmount(l);
            ts = saturate(ts - _EdgeTone * 0.5 * l.data.b * (1 - amount));
            float metallic = lerp(tm, bm, amount);
            return float4(metallic, metallic, metallic, lerp(ts, bs, amount));
        }

        float4 FragOcclusion(v2f_img i) : SV_Target {
            float h = Height(i.uv);
            float radius = max(1, _BevelWidth * 1.5 / _AtlasWorld.z);
            float occlusion = 0;
            [unroll]
            for (int k = 0; k < 8; k++) {
                float angle = k * 0.785398;
                float2 offset = float2(cos(angle), sin(angle)) * radius * _DataTex_TexelSize.xy;
                occlusion += saturate(Height(i.uv + offset) - h);
            }
            float ao = 1 - saturate(occlusion / 8 * 2.5);
            return float4(ao, ao, ao, 1);
        }

        float4 FragChannel(v2f_img i) : SV_Target {
            float v = tex2D(_DataTex, i.uv)[_Channel];
            return float4(v, v, v, 1);
        }
        ENDCG

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragData
            #pragma target 3.5
        ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragBaseColor
            #pragma target 3.5
        ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragNormal
            #pragma target 3.5
        ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragMask
            #pragma target 3.5
        ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragOcclusion
            #pragma target 3.5
        ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragChannel
            #pragma target 3.5
        ENDCG }
    }
}
