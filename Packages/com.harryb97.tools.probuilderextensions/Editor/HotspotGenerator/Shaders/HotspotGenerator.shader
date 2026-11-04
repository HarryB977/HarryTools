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

        float _Intensity, _BevelWidth, _EdgeTone, _Style, _WearSize, _EdgeWidth, _GapDepth, _Cavity, _Seed, _Variation;
        float _TopAlign, _BottomAlign;
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
            float2 edgeUV; // that border's closest point (UV space), where the wear width is sampled
            float2 cornerUV; // bottom-left corner of the patch (UV space)
            int side;      // that border: 0 xMin, 1 xMax, 2 yMin, 3 yMax (normalized, top-left origin)
            float along;   // absolute atlas position (metres) along that border; period AlongPeriod(side)
            float tiling;
            float rotate;
            float2 rnd;
        };

        PatchInfo FindPatch(float2 uv) {
            PatchInfo p;
            p.index = -1; p.dist = 0; p.edgeUV = uv; p.cornerUV = 0; p.side = 0; p.along = 0; p.tiling = 0; p.rotate = 0; p.rnd = 0;
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
                    float2 edge = dx < dy ? float2(a.x < b.x ? r.x : r.z, n.y) : float2(n.x, a.y < b.y ? r.y : r.w);
                    p.index = i; p.dist = min(dx, dy); p.edgeUV = float2(edge.x, 1 - edge.y); p.cornerUV = float2(r.x, 1 - r.w);
                    p.side = dx < dy ? (a.x < b.x ? 0 : 1) : (a.y < b.y ? 2 : 3);
                    p.along = p.side < 2 ? n.y * _AtlasWorld.y : n.x * _AtlasWorld.x;
                    p.tiling = f.x; p.rotate = f.y; p.rnd = f.zw;
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
            float2 cells = max(1, round(_AtlasWorld.xy / _WearSize));
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

        // Smooth two-octave noise for the wear width along an edge: rounded bites instead of needles.
        float WearNoise(float2 uv, float seed) {
            float2 cells = max(1, round(_AtlasWorld.xy / (_WearSize * 1.5)));
            float2 x = float2(uv.x, 1 - uv.y) * cells;
            float v = 0.7 * ValueNoise(x, (int2)cells, seed) + 0.3 * ValueNoise(x * 2, (int2)(cells * 2), seed + 0.017);
            return saturate((v - 0.5) * 1.8 + 0.5);
        }

        float AlongPeriod(int side) { return side < 2 ? _AtlasWorld.y : _AtlasWorld.x; }

        float ValueNoise1(float x, int period, float seed) {
            int i = (int)floor(x);
            float f = frac(x);
            f = f * f * (3 - 2 * f);
            float a = Hash(Wrap(int2(i, 0), int2(period, 1)), seed);
            float b = Hash(Wrap(int2(i + 1, 0), int2(period, 1)), seed);
            return lerp(a, b, f);
        }

        // Smooth two-octave noise along a border, in [0, 1], repeating every `period` metres.
        float EdgeNoise(float along, float period, float cellSize, float seed) {
            int cells = (int)max(1, round(period / cellSize));
            float x = along / period * cells;
            return 0.7 * ValueNoise1(x, cells, seed) + 0.3 * ValueNoise1(x * 2, cells * 2, seed + 0.017);
        }

        float BottomLevel() { return 1 - saturate(_GapDepth); }

        // Distance from p to segment ab; t = position along it (0..1).
        float SegmentDistance(float2 p, float2 a, float2 b, out float t) {
            float2 ab = b - a;
            t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-12));
            return length(p - a - ab * t);
        }

        // Coverage of one crack segment, tapering from w0 to w1 half-width (never below 0.75 px, so it stays connected).
        float CrackSegment(float2 q, float2 a, float2 b, float w0, float w1, float pixel) {
            float t;
            float d = SegmentDistance(q, a, b, t);
            float hw = max(0.75 * pixel, lerp(w0, w1, t));
            return 1 - smoothstep(hw - pixel * 0.5, hw + pixel * 0.5, d);
        }

        // Fine cracks growing from every non-tiling border of the patch under uv. Each crack is a polyline rooted just
        // outside the border, with one branch, so it is connected to the edge by construction. Roots sit far enough
        // from the border's ends that a crack can never leave the patch through a neighbouring side and come back.
        float CrackMask(float2 uv) {
            if (_Style != 0 || _Intensity <= 0) return 0;
            PatchInfo p = FindPatch(uv);
            if (p.index < 0) return 0;
            float4 r = _Patches[p.index];
            float2 world = _AtlasWorld.xy;
            float2 pos = float2(uv.x, 1 - uv.y) * world;
            float2 lo = r.xy * world, hi = r.zw * world;
            float pixel = _AtlasWorld.z;
            float baseSeed = _Seed * 0.001 + 0.37;
            float rootWidth = max(0.75 * pixel, 0.02 * _WearSize);
            float mask = 0;

            [loop]
            for (int side = 0; side < 4; side++) {
                if ((p.tiling == 1 && side < 2) || (p.tiling == 2 && side >= 2)) continue;
                bool vertical = side < 2;
                float s = vertical ? pos.y : pos.x;
                float d = side == 0 ? pos.x - lo.x : side == 1 ? hi.x - pos.x : side == 2 ? pos.y - lo.y : hi.y - pos.y;
                float period = vertical ? world.y : world.x;
                bool wraps = (vertical && p.tiling == 2) || (!vertical && p.tiling == 1);
                float2 extent = vertical ? float2(lo.y, hi.y) : float2(lo.x, hi.x);
                int cells = (int)max(1, round(period / _WearSize));
                float cellLength = period / cells;
                int k = (int)floor(s / cellLength);
                int id = p.index * 4 + side;

                [loop]
                for (int j = -2; j <= 2; j++) {
                    int c = k + j;
                    int2 cell = int2(((c % cells) + cells) % cells, id);
                    if (Hash(cell, baseSeed) >= _Intensity) continue;
                    float length = _Intensity * _WearSize * 2 * (0.5 + 0.5 * Hash(cell, baseSeed + 0.1));
                    float s0 = (c + 0.5 + 0.8 * (Hash(cell, baseSeed + 0.2) - 0.5)) * cellLength;
                    if (!wraps && (s0 < extent.x + 0.65 * length || s0 > extent.y - 0.65 * length)) continue;

                    float2 q = float2(s - s0, d); // crack space: x along the border, y inwards
                    float step = (length + pixel) / 4;
                    float2 a = float2(0, -pixel);
                    float2 branchStart = a;
                    int branchAt = Hash(cell, baseSeed + 0.3) < 0.5 ? 1 : 2;
                    [unroll]
                    for (int seg = 0; seg < 4; seg++) {
                        float angle = (Hash(cell, baseSeed + 0.4 + seg * 0.05) - 0.5) * 1.22; // ±35°
                        float2 b = a + float2(sin(angle), cos(angle)) * step;
                        mask = max(mask, CrackSegment(q, a, b, rootWidth * (1 - 0.15 * seg), rootWidth * (0.85 - 0.15 * seg), pixel));
                        if (seg + 1 == branchAt) branchStart = b;
                        a = b;
                    }

                    float side01 = Hash(cell, baseSeed + 0.7) < 0.5 ? -1 : 1;
                    float branchAngle = side01 * (0.52 + 0.52 * Hash(cell, baseSeed + 0.75)); // 30°..60°
                    float branchStep = (length * (1 - branchAt / 4.0)) * 0.25;
                    float2 b0 = branchStart, b1 = b0 + float2(sin(branchAngle), cos(branchAngle)) * branchStep;
                    float bendAngle = branchAngle * 0.6;
                    float2 b2 = b1 + float2(sin(bendAngle), cos(bendAngle)) * branchStep;
                    float bw = rootWidth * (0.85 - 0.15 * branchAt);
                    mask = max(mask, CrackSegment(q, b0, b1, bw, bw * 0.7, pixel));
                    mask = max(mask, CrackSegment(q, b1, b2, bw * 0.7, bw * 0.4, pixel));
                }
            }
            return mask;
        }

        float4 ComputeData(float2 uv) {
            PatchInfo p = FindPatch(uv);
            float bottom = BottomLevel();
            if (p.index < 0) return float4(bottom, 0, 0, 0);

            float t = saturate(p.dist / _BevelWidth);
            float profile = sqrt(1 - (1 - t) * (1 - t)); // rounded bevel
            float height = lerp(bottom, 1, profile);

            // All wear grows inwards from the border, so every worn pixel is connected to the edge.
            float pixel = _AtlasWorld.z;
            float seed = _Seed * 0.001 + p.index * 0.0731 + p.side * 0.013;

            // The rim: damage along the whole border, never narrower than Edge Width (or one pixel), with an
            // irregular inner outline up to 1.5x wider.
            float wear = 0;
            if (_EdgeWidth > 0) {
                float minWidth = max(_EdgeWidth, pixel);
                float n = EdgeNoise(p.along, AlongPeriod(p.side), max(_EdgeWidth, pixel) * 4, seed + 0.5);
                float rim = minWidth * (1 + 0.5 * n);
                wear = 1 - smoothstep(rim, rim + pixel, p.dist);
            }
            // Bites: a band whose width varies along the border, sampled at the closest border point.
            if (_Style == 1 && _Intensity > 0) {
                float n = WearNoise(p.edgeUV, _Seed * 0.001 + p.index * 0.0731);
                // Wear Size is the length of a bite along the edge; its depth stays in proportion.
                float width = _Intensity * _WearSize * 1.25 * n;
                wear = max(wear, width > pixel * 0.5 ? 1 - smoothstep(width - pixel, width, p.dist) : 0);
            }
            height = lerp(height, bottom, wear);
            height = lerp(height, bottom, 0.6 * CrackMask(uv)); // cracks: grooves in the top layer, no bottom layer
            float edge = 1 - saturate(p.dist / (_BevelWidth * 2));
            return float4(height, wear, edge, p.index + 1);
        }

        // Layer coordinates in repetitions. Aligned layers start at each patch's bottom-left corner; the others get a
        // per-patch offset (and a 90° turn when allowed) scaled by _Variation.
        float2 LayerUV(PatchInfo p, float2 uv, float4 tiling, float align, out float rotated) {
            rotated = 0;
            if (p.index < 0) return uv * tiling.xy;
            if (align > 0.5) return (uv - p.cornerUV) * tiling.xy;

            float2 t = uv * tiling.xy + (floor(p.rnd * 16) + p.rnd) * _Variation;
            if (p.rotate > 0.5 && p.tiling == 0 && p.rnd.x > 1 - 0.5 * _Variation) {
                t = float2(t.y, -t.x);
                rotated = 1;
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
            l.topUV = LayerUV(l.patch, uv, _TopTiling, _TopAlign, l.topRotated);
            l.bottomUV = LayerUV(l.patch, uv, _BottomTiling, _BottomAlign, l.bottomRotated);
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
            float3 color = lerp(worn, bottom, amount);
            float depth = saturate((1 - l.data.r) / max(1e-3, 1 - BottomLevel())); // 0 on top, 1 in gaps and chips
            return float4(color * (1 - _Cavity * depth), 1);
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

        float4 FragCracks(v2f_img i) : SV_Target {
            float v = CrackMask(i.uv);
            return float4(v, v, v, 1);
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
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragCracks
            #pragma target 3.5
        ENDCG }
    }
}
