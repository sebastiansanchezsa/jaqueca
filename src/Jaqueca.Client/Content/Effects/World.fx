// Shader del mundo de Jaqueca (el de Inquisition pasado a primera persona). Todo se dibuja a 640x360 y
// se escala entero: la luz de la lámpara grande se cuantiza en bandas (cel), las sombras vienen de un
// shadow map duro, el ambiente tiñe las sombras y las luces puntuales suman en pasos. La textura de
// cada material es ruido en el espacio del mundo, cuantizado. Las figuras (los pensamientos, las
// manos de Ernesto) se animan acá: cada vértice va con la matriz de su hueso, y la luz elige un tono
// dentro de la rampa de su material, como los personajes de Inquisition.

#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#define MAX_LIGHTS 32
#define MAX_BONES 24

float4x4 World;
float4x4 ViewProj;
float4x4 LightViewProj;
float3 CamPos;
float3 CamRight;
float3 CamUp;
float3 CamFwd;
float CenterDepth;
float2 ScreenSize;

float3 SunDir;
float3 SunColor;
float3 SkyColor;
float3 GroundColor;
float3 FillColor;
float3 FillDir;
float ShadowBias;
float Time;
float SunVis;
// Luz de más para las figuras (así se leen en lo oscuro).
float3 FigureAmbient;
// Destello de la figura que se dibuja (rgb el color, a cuánto: el blanco del golpe).
float4 FigureFlash;
// Niebla: el color de lo lejos y desde/hasta dónde (lo de adentro de la cabeza se pierde en rosa).
float3 FogColor;
float2 FogRange;

float LightCount;
float4 LightPosR[MAX_LIGHTS];   // xyz posición, w radio
float4 LightCol[MAX_LIGHTS];    // rgb color * intensidad

float4x4 Bones[MAX_BONES];

texture ShadowTex;
sampler2D ShadowS = sampler_state { Texture = <ShadowTex>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture FigPalTex;
sampler2D FigPalS = sampler_state { Texture = <FigPalTex>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
float FigRows;

// ------------------------------------------------------------------ ruido

float Hash3(float3 p)
{
    return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float VNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = Hash3(i), b = Hash3(i + float3(1, 0, 0)), c = Hash3(i + float3(0, 1, 0)), d = Hash3(i + float3(1, 1, 0));
    float e = Hash3(i + float3(0, 0, 1)), g = Hash3(i + float3(1, 0, 1)), h = Hash3(i + float3(0, 1, 1)), k = Hash3(i + float3(1, 1, 1));
    float z0 = lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
    float z1 = lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y);
    return lerp(z0, z1, f.z);
}

// ------------------------------------------------------------------ luz

float Shadow(float3 p)
{
    float4 lp = mul(float4(p, 1), LightViewProj);
    float2 uv = lp.xy * float2(0.5, -0.5) + 0.5;
    if (uv.x <= 0 || uv.x >= 1 || uv.y <= 0 || uv.y >= 1) return 1.0;
    float d = tex2Dlod(ShadowS, float4(uv, 0, 0)).r;
    return lp.z - ShadowBias > d ? 0.0 : 1.0;
}

// Bandas de la luz grande: pocas y marcadas, como una rampa de pixel art.
float SunBand(float s)
{
    return s > 0.62 ? 1.0 : (s > 0.3 ? 0.72 : (s > 0.08 ? 0.42 : 0.0));
}

// Arriba el ambiente, abajo el rebote, y un relleno suave del lado contrario a la luz grande.
float3 Ambient(float3 n)
{
    float fill = saturate(dot(n, FillDir));
    return lerp(GroundColor, SkyColor, saturate(n.y * 0.5 + 0.5)) + FillColor * fill;
}

// Luces puntuales: caen suave (cuadrado de la distancia), sin escalones.
float3 PointLights(float3 p, float3 n)
{
    float3 acc = float3(0, 0, 0);
    [loop]
    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        if (i >= LightCount) break;
        float3 L = LightPosR[i].xyz - p;
        float dist = length(L);
        float r = LightPosR[i].w;
        if (dist < r)
        {
            L /= max(dist, 0.001);
            float att = 1.0 - dist / r;
            att *= att;
            float ndl = saturate(dot(n, L) * 0.7 + 0.3);
            acc += LightCol[i].rgb * (att * ndl);
        }
    }
    return acc;
}

float3 Lighting(float3 p, float3 n, float ao)
{
    float sh = Shadow(p + n * 0.35);
    float band = SunBand(saturate(dot(n, SunDir)) * sh);
    return Ambient(n) * ao + SunColor * band + PointLights(p, n);
}

// Lo lejano se va perdiendo en el color de adentro de la cabeza (en escalones: sin degradé liso).
float3 Fog(float3 c, float3 w)
{
    float d = distance(w, CamPos);
    float f = saturate((d - FogRange.x) / max(FogRange.y - FogRange.x, 1.0));
    f = floor(f * 6.0 + 0.5) / 6.0;
    return lerp(c, FogColor, f * 0.85);
}

// ------------------------------------------------------------------ materiales

// Coordenadas de pared: a lo largo (x o z, según hacia dónde mira) y la altura.
float2 WallUv(float3 w, float3 n)
{
    return float2(abs(n.x) > abs(n.z) ? w.z : w.x, w.y);
}

float3 Albedo(float3 w, float3 n, float3 c, float m)
{
    float3 a = c;
    if (m > 0.5 && m < 1.5)
    {
        // Piedra: manchas grandes y vetas chicas, en 4 escalones.
        float v = VNoise(w * 0.28) * 0.65 + VNoise(w * 0.9 + 17.0) * 0.35;
        a *= 0.86 + 0.28 * floor(v * 3.99) / 3.0;
    }
    else if (m > 9.5 && m < 10.5)
    {
        // Madera: tablas con juntas oscuras y vetas.
        float board = frac(w.y * 0.55 + floor((w.x + w.z) * 0.12) * 0.37);
        float v = VNoise(float3(w.x * 0.2, w.y * 2.0, w.z * 0.2));
        a *= (board < 0.06 ? 0.7 : 1.0) * (v > 0.6 ? 0.9 : (v < 0.25 ? 1.06 : 1.0));
    }
    else if (m > 15.5 && m < 16.5)
    {
        // Parquet en damero: cuadros de cuatro tablitas, uno a lo largo y el otro a lo ancho; las juntas
        // oscuras, cada tablita con su tono y la veta a lo largo (roble lustrado de los noventa).
        float S = 10.0;
        float2 q = w.xz / S;
        float2 cell = floor(q);
        float2 f = frac(q);
        float flip = fmod(abs(cell.x + cell.y), 2.0);
        float2 g = flip > 0.5 ? f.yx : f;
        float plank = floor(g.x * 4.0);
        float fx = frac(g.x * 4.0);
        float h = Hash3(float3(cell, plank + 3.0));
        float along = flip > 0.5 ? w.x : w.z;
        float grain = VNoise(float3(along * 0.9, plank * 7.0 + h * 13.0, cell.x * 3.0 + cell.y));
        if (fx < 0.07 || g.y < 0.012 || g.y > 0.988) a *= 0.55;
        else
        {
            a *= 0.84 + 0.3 * floor(h * 3.99) / 3.0;
            a *= grain > 0.62 ? 0.9 : (grain < 0.28 ? 1.07 : 1.0);
        }
    }
    else if (m > 16.5 && m < 17.5)
    {
        // Empapelado: franjas verticales de dos tonos con florcitas en rombo, y la humedad que sube del zócalo.
        float2 uv = WallUv(w, n);
        float stripe = frac(uv.x / 7.0);
        a *= stripe < 0.5 ? 1.0 : 0.88;
        if (stripe > 0.45 && stripe < 0.5) a *= 0.8;
        float2 fc = float2(uv.x / 7.0, uv.y / 5.0 + floor(uv.x / 7.0) * 0.5);
        float2 ff = frac(fc) - 0.5;
        float petal = abs(ff.x) * 1.3 + abs(ff.y);
        if (petal < 0.12) a = lerp(a, float3(0.62, 0.3, 0.28), 0.55);
        else if (petal < 0.17) a *= 0.86;
        float damp = saturate((6.0 - uv.y) * 0.18) * VNoise(w * 0.12 + 5.0);
        if (damp > 0.35) a *= 0.8;
        if (VNoise(float3(uv.x * 0.05, uv.y * 0.3, 1.0)) > 0.78) a *= 0.9;
    }
    else if (m > 17.5 && m < 18.5)
    {
        // Alfombra: la trama gruesa y un dibujo persa grande (dos escalones).
        float weave = Hash3(floor(w * 1.6));
        float orn = sin(w.x * 0.21) * sin(w.z * 0.21) + 0.5 * sin((w.x + w.z) * 0.37);
        a *= orn > 0.55 ? 0.72 : (orn < -0.6 ? 1.15 : 1.0);
        a *= weave > 0.8 ? 1.07 : (weave < 0.2 ? 0.93 : 1.0);
    }
    else if (m > 18.5 && m < 19.5)
    {
        // Tapizado: rombos del capitoné (costuras oscuras) con un botón en cada cruce.
        float2 uv = abs(n.y) > 0.7 ? w.xz : WallUv(w, n);
        float2 d = uv / 4.0;
        float2 r = float2(d.x + d.y, d.x - d.y);
        float2 fr = frac(r);
        float seam = min(min(fr.x, 1.0 - fr.x), min(fr.y, 1.0 - fr.y));
        if (seam < 0.05) a *= 0.72;
        float2 bt = frac(r + 0.5) - 0.5;
        if (length(bt) < 0.08) a *= 0.6;
        a *= 0.94 + 0.12 * VNoise(w * 0.8);
    }
    else if (m > 19.5 && m < 20.5)
    {
        // Azulejos: cuadrados blancos con junta gris; alguno amarillento.
        float2 uv = abs(n.y) > 0.7 ? w.xz : WallUv(w, n);
        float2 f = frac(uv / 4.5);
        float h = Hash3(float3(floor(uv / 4.5), 20.0));
        if (f.x < 0.05 || f.y < 0.05) a *= 0.62;
        else a *= h > 0.85 ? float3(1.0, 0.95, 0.82) : 1.0;
    }
    else if (m > 20.5 && m < 21.5)
    {
        // Carne: venas que laten (crestas del ruido que se oscurecen con el pulso).
        float v = abs(VNoise(w * 0.06 + float3(0, Time * 0.05, 0)) - 0.5) * 2.0;
        float v2 = abs(VNoise(w * 0.16 + 7.0) - 0.5) * 2.0;
        float pulse = 0.5 + 0.5 * sin(Time * 5.2) * sin(Time * 2.6);
        if (v < 0.07 + 0.03 * pulse) a = lerp(a, float3(0.35, 0.05, 0.1), 0.7);
        else if (v2 < 0.05) a *= 0.8;
        a *= 0.9 + 0.2 * floor(VNoise(w * 0.3) * 2.99) / 2.0;
    }
    else if (m > 21.5 && m < 22.5)
    {
        // Mantel de hule: cuadros rojos y blancos.
        float2 uv = abs(n.y) > 0.7 ? w.xz : WallUv(w, n);
        float2 cc = floor(uv / 3.0);
        float white = fmod(abs(cc.x + cc.y), 2.0);
        a = white > 0.5 ? float3(0.92, 0.9, 0.84) : a;
        if (VNoise(w * 0.5) > 0.75) a *= 0.9;
    }
    else if (m > 22.5 && m < 23.5)
    {
        // El televisor sin señal: la lluvia (granos que cambian 20 veces por segundo) y una franja que baja.
        float2 uv = WallUv(w, n);
        float g = Hash3(float3(floor(uv * 2.5), floor(Time * 20.0)));
        float band = frac(uv.y * 0.08 - Time * 0.35);
        a = lerp(float3(0.12, 0.13, 0.15), float3(0.8, 0.84, 0.88), g) * (band < 0.12 ? 1.3 : 1.0);
        if (frac(uv.y * 1.25) < 0.3) a *= 0.7;
    }
    return a;
}

// ------------------------------------------------------------------ geometría iluminada

struct VIn
{
    float4 Pos : POSITION0;
    float3 N : NORMAL0;
    float4 C : COLOR0;
    float4 D : TEXCOORD0;
};

struct VOut
{
    float4 Pos : POSITION0;
    float4 C : COLOR0;
    float3 W : TEXCOORD0;
    float3 N : TEXCOORD1;
    float4 D : TEXCOORD2;
};

VOut LitVS(VIn i)
{
    VOut o;
    float4 w = mul(i.Pos, World);
    o.Pos = mul(w, ViewProj);
    o.W = w.xyz;
    o.N = normalize(mul(float4(i.N, 0), World).xyz);
    o.C = i.C;
    o.D = i.D;
    return o;
}

float4 LitPS(VOut i) : COLOR0
{
    float3 n = normalize(i.N);
    float3 a = Albedo(i.W, n, i.C.rgb, i.D.x);
    float3 c = a * Lighting(i.W, n, i.D.w);
    c += a * (1.0 - i.C.a) * 2.0;   // emisión
    return float4(Fog(c, i.W), 1);
}

// Normal (en espacio de cámara) y profundidad para los contornos: x, y de la normal,
// z = distancia a lo largo de la vista, w = cuánto contorno lleva.
float4 NormalDepthPS(VOut i) : COLOR0
{
    float3 n = normalize(i.N);
    return float4(dot(n, CamRight), dot(n, CamUp), dot(i.W, CamFwd) - CenterDepth, i.D.y);
}

// ------------------------------------------------------------------ sombra

struct SOut
{
    float4 Pos : POSITION0;
    float4 P : TEXCOORD0;
};

// La pasada de sombra usa ViewProj (el código le pone la matriz de la luz).
SOut ShadowVS(VIn i)
{
    SOut o;
    float4 w = mul(i.Pos, World);
    o.Pos = mul(w, ViewProj);
    o.P = o.Pos;
    return o;
}

float4 ShadowPS(SOut i) : COLOR0
{
    return float4(i.P.z / i.P.w, 0, 0, 1);
}

// ------------------------------------------------------------------ figuras (animadas por hueso)

// Vértice de figura: la posición y la normal en el espacio de su hueso; C.r el hueso, C.g 1 si el tono
// es fijo (los ojos); D.x la fila de la rampa, D.y el contorno, D.z cuántos tonos se corre (o el tono,
// si es fijo), D.w cuánto brilla sola.
float4x4 BoneOf(float4 c)
{
    return Bones[(int)(c.r * 255.0 + 0.5)];
}

VOut FigureVS(VIn i)
{
    VOut o;
    float4x4 m = BoneOf(i.C);
    float4 w = mul(i.Pos, m);
    o.Pos = mul(w, ViewProj);
    o.W = w.xyz;
    o.N = normalize(mul(float4(i.N, 0), m).xyz);
    o.C = i.C;
    o.D = i.D;
    return o;
}

SOut FigureShadowVS(VIn i)
{
    SOut o;
    float4 w = mul(i.Pos, BoneOf(i.C));
    o.Pos = mul(w, ViewProj);
    o.P = o.Pos;
    return o;
}

float3 FPal(float row, float tone)
{
    return tex2Dlod(FigPalS, float4((tone + 0.5) / 8.0, (row + 0.5) / FigRows, 0, 0)).rgb;
}

// Misma regla de tonos que Shading.cs (el generador de Inquisition): 4 / 3 / 2 / 1.
float ToneOf(float l, float shiny)
{
    return l > (shiny > 0.5 ? 0.8 : 0.93) ? 4.0 : (l > 0.56 ? 3.0 : (l > 0.1 ? 2.0 : 1.0));
}

float4 FigurePS(VOut i) : COLOR0
{
    float3 n = normalize(i.N);
    float3 toCam = normalize(CamPos - i.W);
    float row = i.D.x;
    float4 prm = tex2Dlod(FigPalS, float4(6.5 / 8.0, (row + 0.5) / FigRows, 0, 0));
    float lift = prm.r - 0.5;
    float flat = prm.g;
    float shiny = prm.b;

    float sh = Shadow(i.W + n * 0.6);
    float lk = dot(n, SunDir);
    // A la sombra: el ambiente de arriba y un relleno desde donde se mira marcan la forma, más bajos.
    float ls = n.y * 0.3 + dot(n, toCam) * 0.25 - 0.32;
    float l = lerp(ls, lk, sh * SunVis);
    l = l * (1.0 - flat) + 0.55 * flat + lift;

    float3 ptc = float3(0, 0, 0);
    float ptl = 0;
    [loop]
    for (int k = 0; k < MAX_LIGHTS; k++)
    {
        if (k >= LightCount) break;
        float3 L = LightPosR[k].xyz - i.W;
        float dist = length(L);
        float r = LightPosR[k].w;
        if (dist < r)
        {
            L /= max(dist, 0.001);
            float att = 1.0 - dist / r;
            att *= att;
            float kk = att * saturate(dot(n, L) * 1.1 - 0.05);
            ptl += kk * dot(LightCol[k].rgb, float3(0.3, 0.5, 0.2));
            ptc += LightCol[k].rgb * kk;
        }
    }
    l += ptl * 1.3;

    float fixedTone = step(0.5, i.C.g * 255.0);
    float tone = lerp(clamp(ToneOf(l, shiny) + floor(i.D.z + 0.5), 0.0, 4.0), i.D.z, fixedTone);
    float3 c = FPal(row, tone);
    float3 tint = (SkyColor * 0.9 + FillColor * 0.5 + SunColor) / float3(1.07, 1.1, 1.09);
    tint *= lerp(float3(0.86, 0.9, 1.0), float3(1, 1, 1), sh);
    c = c * (tint + FigureAmbient + ptc * 1.25);
    c += FPal(row, 4.0) * i.D.w * 1.6;
    c = lerp(c, FigureFlash.rgb, FigureFlash.a);
    return float4(Fog(c, i.W), 1);
}

// ------------------------------------------------------------------ cielo

// Lo que hay arriba y detrás de las paredes que se terminan: el adentro de la cabeza. Una bóveda de
// carne rosada y violeta con venas que laten y, lejos, pliegues como de cerebro.
struct KIn
{
    float4 Pos : POSITION0;
    float3 N : NORMAL0;
    float4 C : COLOR0;
    float4 D : TEXCOORD0;
};

struct KOut
{
    float4 Pos : POSITION0;
    float3 Dir : TEXCOORD0;
};

KOut SkyVS(KIn i)
{
    KOut o;
    o.Pos = float4(i.Pos.xy, 0.99999, 1);
    o.Dir = i.N;
    return o;
}

float4 SkyPS(KOut i) : COLOR0
{
    float3 d = normalize(i.Dir);
    float up = d.y;
    // De la bruma rosada del horizonte al violeta hondo de arriba, en bandas.
    float band = floor(saturate(up * 1.2 + 0.2) * 7.0) / 7.0;
    float3 low = FogColor;
    float3 high = float3(0.2, 0.05, 0.12);
    float3 c = lerp(low, high, band);
    // Los pliegues: crestas del ruido sobre la esfera de la dirección.
    float3 p = d * 6.0;
    float fold = abs(VNoise(p + float3(0, Time * 0.03, 0)) - 0.5) * 2.0;
    float fold2 = abs(VNoise(p * 2.3 + 11.0) - 0.5) * 2.0;
    float pulse = 0.5 + 0.5 * sin(Time * 5.2) * sin(Time * 2.6);
    if (fold < 0.06 + 0.02 * pulse) c = lerp(c, float3(0.5, 0.08, 0.16), 0.6);
    else if (fold2 < 0.05) c *= 0.82;
    // Un resplandor que late donde está la luz grande.
    float glow = saturate(dot(d, SunDir));
    c += SunColor * (glow > 0.985 ? 0.9 : (glow > 0.94 ? 0.25 : 0.0));
    return float4(c, 0);
}

technique Sky { pass P0 { VertexShader = compile VS_SHADERMODEL SkyVS(); PixelShader = compile PS_SHADERMODEL SkyPS(); } }

// ------------------------------------------------------------------ capas que suman y que oscurecen

// Luz que se suma (fogonazos, chispas, el brillo de la sangre que cura): el color por su alfa.
float4 GlowPS(VOut i) : COLOR0
{
    return float4(i.C.rgb * i.C.a * 1.6, 0);
}

// Lo que oscurece (la sombra de los pies, el humo).
float4 ShadePS(VOut i) : COLOR0
{
    return float4(i.C.rgb, i.C.a);
}

// Sin luz: el color tal cual (con la niebla). Las gotas de sangre en el aire, la tiza.
float4 FlatPS(VOut i) : COLOR0
{
    return float4(Fog(i.C.rgb, i.W), 1);
}

technique Lit { pass P0 { VertexShader = compile VS_SHADERMODEL LitVS(); PixelShader = compile PS_SHADERMODEL LitPS(); } }
technique Glow { pass P0 { VertexShader = compile VS_SHADERMODEL LitVS(); PixelShader = compile PS_SHADERMODEL GlowPS(); } }
technique Shade { pass P0 { VertexShader = compile VS_SHADERMODEL LitVS(); PixelShader = compile PS_SHADERMODEL ShadePS(); } }
technique Flat { pass P0 { VertexShader = compile VS_SHADERMODEL LitVS(); PixelShader = compile PS_SHADERMODEL FlatPS(); } }
technique NormalDepth { pass P0 { VertexShader = compile VS_SHADERMODEL LitVS(); PixelShader = compile PS_SHADERMODEL NormalDepthPS(); } }
technique Shadow { pass P0 { VertexShader = compile VS_SHADERMODEL ShadowVS(); PixelShader = compile PS_SHADERMODEL ShadowPS(); } }
technique Figure { pass P0 { VertexShader = compile VS_SHADERMODEL FigureVS(); PixelShader = compile PS_SHADERMODEL FigurePS(); } }
technique FigureDepth { pass P0 { VertexShader = compile VS_SHADERMODEL FigureVS(); PixelShader = compile PS_SHADERMODEL NormalDepthPS(); } }
technique FigureShadow { pass P0 { VertexShader = compile VS_SHADERMODEL FigureShadowVS(); PixelShader = compile PS_SHADERMODEL ShadowPS(); } }
