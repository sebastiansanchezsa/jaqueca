// Post-proceso de Jaqueca (el de Inquisition, que viene del de Kill Kill Again, pasado a perspectiva):
// - Edges: contornos de pixel art sobre el render de baja resolución, a partir de la normal y
//   la profundidad (silueta oscura donde algo tapa a lo de atrás, brillo en aristas convexas).
// - Threshold / Down / Blur: bloom en baja resolución.
// - Composite: la imagen nítida (muestreo por punto, escala entera) + el bloom suave encima,
//   exposición y gradación. Es el contraste HD-2D: píxeles duros con luz blanda.

#define PS_SHADERMODEL ps_3_0

texture SceneTex;
sampler2D SceneS = sampler_state { Texture = <SceneTex>; MinFilter = Linear; MagFilter = Linear; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
// La misma imagen por punto: para contornos y composición (cada píxel entero, sin mezclar).
texture PixelTex;
sampler2D PixelS = sampler_state { Texture = <PixelTex>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture NormalDepthTex;
sampler2D NdS = sampler_state { Texture = <NormalDepthTex>; MinFilter = Point; MagFilter = Point; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture BloomTex;
sampler2D BloomS = sampler_state { Texture = <BloomTex>; MinFilter = Linear; MagFilter = Linear; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
texture BloomTex2;
sampler2D Bloom2S = sampler_state { Texture = <BloomTex2>; MinFilter = Linear; MagFilter = Linear; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };

float2 TexelSize;
float2 Direction;
float Threshold;
float Time;
float Exposure;
float BloomStrength;
float Vignette;
float Grain;
float2 OutSize;
float3 Lift;
float3 Gain;
float Saturation;
float4 FlashColor;
float Tonemap;
float KeepHue;     // 1: las luces se comprimen sin perder el tono (la pantalla de título)
float Wobble;      // 0..1: la imagen ondula (mareado por las esporas del bejín)

float EdgeDepth;
float EdgeSlope;   // cuánto más salto hace falta para ser silueta, por unidad de distancia (en perspectiva)
float OutlineDark;
float HighlightGain;
float PixelSize;   // unidades de mundo por píxel a una unidad de distancia (se multiplica por la distancia)
float4 HurtColor;  // el borde que se tiñe al recibir un golpe (rgb, cuánto)
float Aberration;  // corrimiento de los canales en los bordes (píxeles), el mareo de la jaqueca

float Lum(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

// ------------------------------------------------------------------ contornos

float3 Nrm(float4 nd)
{
    return float3(nd.x, nd.y, sqrt(saturate(1.0 - nd.x * nd.x - nd.y * nd.y)));
}

float4 EdgesPS(float4 pos : POSITION0, float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 c = tex2D(PixelS, uv);
    // Alfa 0 = sprite (ya trae su propio contorno) o fondo.
    if (c.a < 0.5) return float4(c.rgb, 1);
    float4 nd = tex2D(NdS, uv);
    float3 n = Nrm(nd);
    float d = nd.z;

    float2 o[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };
    float sil = 0.0;
    float crease = 0.0;
    [unroll]
    for (int k = 0; k < 4; k++)
    {
        float4 q = tex2D(NdS, uv + o[k] * TexelSize);
        float dd = q.z - d;
        // El vecino está bastante más lejos: este píxel es el borde del objeto de adelante (lo lejos
        // necesita más salto: en perspectiva un piso inclinado también cambia mucho de un píxel al otro).
        if (dd > EdgeDepth + d * EdgeSlope) sil = 1.0;
        else
        {
            // Posición del vecino respecto de este píxel (espacio de cámara: derecha, arriba,
            // hacia la cámara). Si queda por debajo del plano tangente, la arista es convexa.
            float px = PixelSize * max(d, 0.5);
            float3 delta = float3(o[k].x * px, -o[k].y * px, -dd);
            float convex = -dot(delta, n);
            float3 nq = Nrm(q);
            float diff = 1.0 - dot(n, nq);
            // De los dos lados de la arista se aclara el que mira más hacia la luz.
            if (convex > 0.06 && diff > 0.1 && dot(n - nq, float3(-0.35, 0.8, 0.5)) > 0.0)
                crease = max(crease, saturate(diff * 2.5));
        }
    }
    float3 rgb = c.rgb;
    if (sil > 0.5 && nd.w > 0.75) rgb *= OutlineDark;
    else if (crease > 0.0 && nd.w > 0.25) rgb = rgb * (1.0 + HighlightGain * crease) + 0.02 * crease;
    return float4(rgb, 1);
}

// ------------------------------------------------------------------ bloom

float4 ThresholdPS(float4 pos : POSITION0, float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float3 c = tex2D(SceneS, uv + TexelSize * float2(-0.5, -0.5)).rgb
             + tex2D(SceneS, uv + TexelSize * float2(0.5, -0.5)).rgb
             + tex2D(SceneS, uv + TexelSize * float2(-0.5, 0.5)).rgb
             + tex2D(SceneS, uv + TexelSize * float2(0.5, 0.5)).rgb;
    c *= 0.25;
    float l = Lum(c);
    float k = saturate((l - Threshold) / max(l, 0.0001));
    return float4(c * k, 1);
}

float4 DownPS(float4 pos : POSITION0, float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float3 c = tex2D(SceneS, uv + TexelSize * float2(-0.5, -0.5)).rgb
             + tex2D(SceneS, uv + TexelSize * float2(0.5, -0.5)).rgb
             + tex2D(SceneS, uv + TexelSize * float2(-0.5, 0.5)).rgb
             + tex2D(SceneS, uv + TexelSize * float2(0.5, 0.5)).rgb;
    return float4(c * 0.25, 1);
}

float4 BlurPS(float4 pos : POSITION0, float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 o = Direction * TexelSize;
    float3 c = tex2D(SceneS, uv).rgb * 0.227027;
    c += tex2D(SceneS, uv + o * 1.3846153).rgb * 0.3162162;
    c += tex2D(SceneS, uv - o * 1.3846153).rgb * 0.3162162;
    c += tex2D(SceneS, uv + o * 3.2307692).rgb * 0.0702702;
    c += tex2D(SceneS, uv - o * 3.2307692).rgb * 0.0702702;
    return float4(c, 1);
}

// ------------------------------------------------------------------ composición

float3 Aces(float3 x)
{
    return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
}

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float4 CompositePS(float4 pos : POSITION0, float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // Mareado: la imagen ondula despacio, en dos direcciones (sin saltos: las ondas son largas).
    uv += float2(sin(uv.y * 19.0 + Time * 2.3), cos(uv.x * 13.0 + Time * 1.7)) * (0.0045 * Wobble);
    // La escena llega por punto (el SpriteBatch la dibuja con PointClamp): píxeles enteros.
    float3 c = tex2D(PixelS, uv).rgb;
    // Los canales se separan hacia los bordes (la puntada de la jaqueca): rojo afuera, azul adentro.
    if (Aberration > 0.0)
    {
        float2 dc = (uv - 0.5) * Aberration / OutSize * 3.0;
        c.r = tex2D(PixelS, uv + dc).r;
        c.b = tex2D(PixelS, uv - dc).b;
    }
    c += (tex2D(BloomS, uv).rgb * 0.6 + tex2D(Bloom2S, uv).rgb * 0.8) * BloomStrength;
    c *= Exposure;
    // Tonemap suave sólo arriba: los colores de la paleta quedan como están.
    if (Tonemap > 0.5) c = Aces(c);
    else if (KeepHue > 0.5)
    {
        // Por el canal más alto (el color no se lava hacia el gris), con un hombro que llega a 1:
        // lo que brilla mucho (el corazón del fuego) queda blanco amarillento, no beige.
        float m = max(c.r, max(c.g, c.b));
        float to = m < 0.8 ? m : 0.8 + 0.2 * (1.0 - exp(-(m - 0.8) / 0.2));
        c *= to / max(m, 0.0001);
    }
    else c = c / (1.0 + max(c - 0.85, 0.0) * 1.3);
    float l = Lum(c);
    c = lerp(float3(l, l, l), c, Saturation);
    c = c * Gain + Lift * (1.0 - c);
    c = lerp(c, FlashColor.rgb, FlashColor.a);
    float2 cc = uv - 0.5;
    c *= 1.0 - dot(cc, cc) * Vignette;
    // El golpe: el borde se tiñe (más cuanto más afuera), en dos escalones.
    float edge = saturate(dot(cc, cc) * 3.2);
    edge = edge > 0.55 ? 1.0 : (edge > 0.3 ? 0.55 : 0.0);
    c = lerp(c, HurtColor.rgb, edge * HurtColor.a);
    c += (Hash(uv * OutSize + Time * 61.0) - 0.5) * Grain;
    return float4(saturate(c), 1);
}

technique Edges { pass P0 { PixelShader = compile PS_SHADERMODEL EdgesPS(); } }
technique Threshold { pass P0 { PixelShader = compile PS_SHADERMODEL ThresholdPS(); } }
technique Down { pass P0 { PixelShader = compile PS_SHADERMODEL DownPS(); } }
technique Blur { pass P0 { PixelShader = compile PS_SHADERMODEL BlurPS(); } }
technique Composite { pass P0 { PixelShader = compile PS_SHADERMODEL CompositePS(); } }
