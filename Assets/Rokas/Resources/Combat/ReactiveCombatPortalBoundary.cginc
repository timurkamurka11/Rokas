#ifndef ROKAS_REACTIVE_PORTAL_BOUNDARY_INCLUDED
#define ROKAS_REACTIVE_PORTAL_BOUNDARY_INCLUDED

// Shared by the visible aperture and the emerging actor's world-space mask.
// Keep the coefficients identical to ReactiveCombatPortalEffect.AngularBoundary.
float portalAngularBoundary(float angle, float phase)
{
    return 1.0 + 0.08 * sin(angle * 3.0 + phase * 0.73)
        + 0.06 * sin(angle * 7.0 - phase * 1.19)
        + 0.028 * sin(angle * 11.0 + phase * 1.61)
        + 0.018 * sin(angle * 19.0 - phase * 2.13);
}

float portalHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float portalNoise(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(portalHash(cell), portalHash(cell + float2(1, 0)), f.x),
        lerp(portalHash(cell + float2(0, 1)), portalHash(cell + 1), f.x), f.y);
}

float portalFbm(float2 p)
{
    return portalNoise(p) * 0.57 + portalNoise(p * 2.07 + 13.2) * 0.28
        + portalNoise(p * 4.13 - 7.8) * 0.15;
}
#endif
