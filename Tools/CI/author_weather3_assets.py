from __future__ import annotations

import hashlib
import os
import random
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path.cwd()
SRC = ROOT / "Weather3Sources"
OUT = ROOT / "Assets/Rokas/Resources/Weather3"
OUT.mkdir(parents=True, exist_ok=True)

EXPECTED_HOME_SIZE = (1672, 941)
assert Image.open(ROOT / "Assets/Rokas/Art/Home/ApartmentNight.png").size == EXPECTED_HOME_SIZE, (
    "Home source art dimensions changed; re-author Weather 3 spatial masks against the new illustration"
)


def save_rgba(name: str, rgb: tuple[int, int, int], alpha) -> None:
    if isinstance(alpha, Image.Image):
        a = np.array(alpha.convert("L"))
    else:
        a = np.asarray(alpha)
    arr = np.zeros((a.shape[0], a.shape[1], 4), dtype=np.uint8)
    arr[:, :, :3] = np.array(rgb, dtype=np.uint8)
    arr[:, :, 3] = np.clip(a, 0, 255).astype(np.uint8)
    Image.fromarray(arr, "RGBA").save(OUT / name, optimize=True)


def irregular_mask(size, shapes, blur=22, noise_strength=0.12, seed=1):
    width, height = size
    base = Image.new("L", size, 0)
    draw = ImageDraw.Draw(base)
    for shape in shapes:
        if shape[0] == "ellipse":
            draw.ellipse(shape[1], fill=shape[2])
        elif shape[0] == "polygon":
            draw.polygon(shape[1], fill=shape[2])
    base = base.filter(ImageFilter.GaussianBlur(blur))
    arr = np.array(base).astype(np.float32)
    if noise_strength > 0:
        rng = np.random.default_rng(seed)
        small = rng.random((height // 8 + 1, width // 8 + 1)).astype(np.float32)
        noise = cv2.resize(small, (width, height), interpolation=cv2.INTER_CUBIC)
        noise = cv2.GaussianBlur(noise, (0, 0), 12)
        arr *= (1 - noise_strength) + noise * noise_strength * 2
    return np.clip(arr, 0, 255).astype(np.uint8)


# Authored rain sprite: isolate one natural streak from gmason's CC0 rain_overlay.png.
rain = np.array(Image.open(SRC / "rain_overlay.png").convert("RGBA"))
rain_alpha = rain[:, :, 3]
connected = (rain_alpha > 24).astype(np.uint8)
count, labels, stats, _ = cv2.connectedComponentsWithStats(connected, 8)
target = None
for index in range(1, count):
    x, y, width, height, area = stats[index]
    if abs(int(x) - 969) < 3 and abs(int(y) - 578) < 3:
        target = index
        break
if target is None:
    candidates = []
    for index in range(1, count):
        x, y, width, height, area = stats[index]
        if height >= 18 and height / (width + 1e-6) > 2.3:
            candidates.append((height / (width + 1e-6), area, index))
    target = sorted(candidates, reverse=True)[0][2]
x, y, width, height, _ = stats[target]
pad = 12
x0, y0 = max(0, x - pad), max(0, y - pad)
x1, y1 = min(rain.shape[1], x + width + pad), min(rain.shape[0], y + height + pad)
component = (labels[y0:y1, x0:x1] == target).astype(np.uint8)
component = cv2.dilate(component, np.ones((7, 7), np.uint8), iterations=1)
a = np.clip(rain_alpha[y0:y1, x0:x1].astype(np.float32) * component * 3.0, 0, 255).astype(np.uint8)
rgba = np.zeros((a.shape[0], a.shape[1], 4), dtype=np.uint8)
rgba[:, :, :3] = [210, 238, 250]
rgba[:, :, 3] = a
Image.fromarray(rgba, "RGBA").resize((32, 96), Image.Resampling.LANCZOS).save(
    OUT / "RainStreakAuthored.png", optimize=True
)

# Distant haze: compact CC0 fog derivative, feathered before the Home window mask boundary.
fog = Image.open(SRC / "fog01.png").convert("RGBA").resize((512, 256), Image.Resampling.LANCZOS)
fa = np.array(fog)[:, :, 3].astype(np.float32)
yy = np.linspace(0, 1, 256)[:, None]
xx = np.linspace(0, 1, 512)[None, :]
fa *= np.clip(xx / 0.05, 0, 1) * np.clip((1 - xx) / 0.05, 0, 1)
fa *= np.clip((yy - 0.02) / 0.18, 0, 1) * np.clip((1.02 - yy) / 0.18, 0, 1)
fa *= 0.72
save_rgba("DistantHaze.png", (210, 236, 244), fa)

# Near mist: independent cloud-alpha source with fully transparent outer falloff.
cloud5 = Image.open(SRC / "fx_cloudalpha05.png").convert("RGBA")
cloud5 = cloud5.crop((120, 430, 1930, 1500)).resize((512, 256), Image.Resampling.LANCZOS)
ca = np.array(cloud5)[:, :, 3].astype(np.float32)
ca = np.array(
    Image.fromarray(np.clip(ca * 1.35, 0, 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2.0))
).astype(np.float32)
yy = np.linspace(0, 1, 256)[:, None]
xx = np.linspace(0, 1, 512)[None, :]
ca *= (
    np.clip(xx / 0.08, 0, 1)
    * np.clip((1 - xx) / 0.08, 0, 1)
    * np.clip(yy / 0.12, 0, 1)
    * np.clip((1 - yy) / 0.12, 0, 1)
    * 0.9
)
save_rgba("NearMist.png", (220, 242, 248), ca)

# Outside lightning source: separate irregular CC0 cloud-alpha derivative.
cloud2 = Image.open(SRC / "fx_cloudalpha02.png").convert("RGBA")
cloud2 = cloud2.crop((150, 160, 1880, 1880)).resize((512, 512), Image.Resampling.LANCZOS)
sa = np.array(cloud2)[:, :, 3].astype(np.float32)
sa = np.array(
    Image.fromarray(np.clip(sa * 2.4, 0, 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2.4))
).astype(np.float32)
yy = np.linspace(0, 1, 512)[:, None]
xx = np.linspace(0, 1, 512)[None, :]
sa *= (
    np.clip(xx / 0.09, 0, 1)
    * np.clip((1 - xx) / 0.09, 0, 1)
    * np.clip(yy / 0.09, 0, 1)
    * np.clip((1 - yy) / 0.09, 0, 1)
)
save_rgba("StormCloudMask.png", (235, 247, 255), sa)

# Natural wet-glass droplets: local-detail extraction from the CC0 window photograph.
photo = cv2.imread(str(SRC / "RaindropsOnWindow.jpg"), cv2.IMREAD_COLOR)
ph, pw = photo.shape[:2]
photo = photo[int(ph * 0.06):int(ph * 0.94), int(pw * 0.05):int(pw * 0.95)]
photo = cv2.resize(photo, (768, 512), interpolation=cv2.INTER_AREA)
gray = cv2.cvtColor(photo, cv2.COLOR_BGR2GRAY)
blur = cv2.GaussianBlur(gray, (0, 0), 6)
high_pass = cv2.absdiff(gray, blur).astype(np.float32)
gx = cv2.Sobel(gray, cv2.CV_32F, 1, 0, ksize=3)
gy = cv2.Sobel(gray, cv2.CV_32F, 0, 1, ksize=3)
gradient = cv2.magnitude(gx, gy)
score = cv2.GaussianBlur(high_pass * 1.6 + gradient * 0.32, (0, 0), 0.8)
p85, p995 = np.percentile(score, [85, 99.5])
norm = np.clip((score - p85) / max(1e-6, p995 - p85), 0, 1)
binary = (norm > 0.13).astype(np.uint8)
count, drop_labels, drop_stats, _ = cv2.connectedComponentsWithStats(binary, 8)
keep = np.zeros_like(binary)
for index in range(1, count):
    x, y, width, height, area = drop_stats[index]
    if 3 <= area <= 650 and width <= 52 and height <= 62:
        keep[drop_labels == index] = 1
keep = cv2.dilate(keep, np.ones((2, 2), np.uint8), iterations=1)
droplets = (norm * keep * 210).astype(np.uint8)
droplets = cv2.GaussianBlur(droplets, (0, 0), 0.55)
droplets = droplets[:, 128:640]
save_rgba("WetGlassDroplets.png", (220, 244, 250), droplets)

# Wet-glass streaks: sparse separate derivative, intentionally not the same texture as rainfall/droplets.
connected = (rain_alpha > 36).astype(np.uint8)
count, streak_labels, streak_stats, _ = cv2.connectedComponentsWithStats(connected, 8)
candidates = []
for index in range(1, count):
    x, y, width, height, area = streak_stats[index]
    ratio = height / (width + 1e-6)
    if area >= 12 and height >= 13 and ratio >= 1.7 and width <= 18:
        candidates.append((height * ratio + area * 0.05, index))
candidates = sorted(candidates, reverse=True)[:36]
canvas = np.zeros((512, 512), dtype=np.uint8)
rng = random.Random(20260909)
for _, index in candidates[:18]:
    x, y, width, height, _ = streak_stats[index]
    pad = 6
    x0, y0 = max(0, x - pad), max(0, y - pad)
    x1, y1 = min(rain_alpha.shape[1], x + width + pad), min(rain_alpha.shape[0], y + height + pad)
    component = (streak_labels[y0:y1, x0:x1] == index).astype(np.uint8)
    component = cv2.dilate(component, np.ones((5, 5), np.uint8), iterations=1)
    aa = np.clip(rain_alpha[y0:y1, x0:x1].astype(np.float32) * component * 2.8, 0, 170).astype(np.uint8)
    patch = Image.fromarray(aa, "L")
    scale = 1.25 + rng.random() * 0.95
    patch = patch.resize(
        (max(8, int(patch.width * (0.8 + rng.random() * 0.45))), max(24, int(patch.height * scale * 2.0))),
        Image.Resampling.LANCZOS,
    )
    patch = patch.rotate(18 + rng.uniform(-5, 5), resample=Image.Resampling.BICUBIC, expand=True)
    px = rng.randint(12, max(12, 512 - patch.width - 12))
    py = rng.randint(4, max(4, 512 - patch.height - 4))
    layer = np.zeros((512, 512), dtype=np.uint8)
    patch_arr = np.array(patch)
    y2, x2 = min(512, py + patch.height), min(512, px + patch.width)
    layer[py:y2, px:x2] = patch_arr[: y2 - py, : x2 - px]
    canvas = np.maximum(canvas, layer)
canvas = np.array(Image.fromarray(canvas, "L").filter(ImageFilter.GaussianBlur(0.6)))
save_rgba("WetGlassStreaks.png", (225, 246, 252), canvas)

# User-approved glazing semantics mapped onto the real Home art/window root.
# The two dark gaps match the actual vertical mullions in ApartmentNight.
glazing = Image.new("L", (605, 396), 0)
draw = ImageDraw.Draw(glazing)
for panel in [(3, 3, 255, 392), (282, 3, 489, 392), (520, 3, 602, 392)]:
    draw.rounded_rectangle(panel, radius=3, fill=255)
save_rgba("WindowGlazingMask.png", (255, 255, 255), glazing)

# Project-owned spatial masks derived from the approved lighting-zone reference.
warm = irregular_mask(
    (960, 540),
    [
        ("ellipse", (0, 155, 150, 430), 245),
        ("ellipse", (455, 75, 690, 330), 220),
        ("ellipse", (760, 70, 945, 330), 220),
        ("ellipse", (810, 245, 955, 460), 105),
    ],
    blur=30,
    noise_strength=0.08,
    seed=10,
)
save_rgba("WarmPracticalMask.png", (255, 235, 205), warm)

cold = irregular_mask(
    (960, 540),
    [
        ("polygon", [(170, 210), (500, 205), (700, 335), (620, 420), (300, 370), (135, 295)], 210),
        ("polygon", [(45, 245), (360, 270), (430, 325), (320, 365), (80, 330)], 135),
        ("polygon", [(265, 265), (690, 292), (710, 335), (300, 330)], 190),
        ("polygon", [(400, 410), (680, 415), (720, 520), (445, 530)], 125),
    ],
    blur=26,
    noise_strength=0.13,
    seed=22,
)
save_rgba("ColdSpillMask.png", (220, 240, 250), cold)

room = irregular_mask(
    (960, 540),
    [
        ("ellipse", (70, 45, 760, 470), 160),
        ("ellipse", (240, 130, 820, 535), 95),
        ("polygon", [(90, 100), (500, 70), (760, 270), (620, 480), (170, 430)], 145),
    ],
    blur=50,
    noise_strength=0.18,
    seed=33,
)
xx = np.linspace(0, 1, 960)[None, :]
room = (room.astype(np.float32) * np.clip((1.08 - xx) / 0.45, 0, 1)).astype(np.uint8)
save_rgba("StormRoomLiftMask.png", (225, 242, 252), room)

# Unity metadata: ordinary non-readable Texture2D assets with alpha; runtime sets Repeat only where needed.
def guid_for(path: Path) -> str:
    return hashlib.md5(("ROKAS-WEATHER3:" + path.as_posix()).encode("utf-8")).hexdigest()

folder_meta = OUT.parent / "Weather3.meta"
folder_meta.write_text(
    "fileFormatVersion: 2\n"
    f"guid: {guid_for(OUT)}\n"
    "folderAsset: yes\n"
    "DefaultImporter:\n"
    "  externalObjects: {}\n"
    "  userData: Weather 3.0 authored runtime resources\n"
    "  assetBundleName:\n"
    "  assetBundleVariant:\n",
    encoding="utf-8",
)

texture_meta = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  isReadable: 0
  streamingMipmaps: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 80
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 80
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData: Weather 3.0 compact authored alpha resource
  assetBundleName:
  assetBundleVariant:
"""

for png in sorted(OUT.glob("*.png")):
    Path(str(png) + ".meta").write_text(texture_meta.format(guid=guid_for(png)), encoding="utf-8")

print("Weather 3 authored assets:")
for png in sorted(OUT.glob("*.png")):
    digest = hashlib.sha256(png.read_bytes()).hexdigest()
    print(f"{png.name} {png.stat().st_size} bytes sha256={digest}")
