from pathlib import Path
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Rokas/Resources/Food/ROKAS_FoodAtlas.png"
OUT = ROOT / "Assets/Rokas/Resources/Food/Details"

CROPS = {
    "TravelerOnigiri": (0.333125, 0.25, 0.333125, 0.25),
    "SpicyMiso": (0.66625, 0.25, 0.33375, 0.25),
    "HunterTempura": (0.0, 0.0, 0.333125, 0.25),
    "MoonMochi": (0.333125, 0.0, 0.333125, 0.25),
    "GreenTea": (0.66625, 0.0, 0.33375, 0.25),
}

META_GUIDS = {
    "TravelerOnigiri": "d150b21586b14d6a97a80118fc190001",
    "SpicyMiso": "d150b21586b14d6a97a80118fc190002",
    "HunterTempura": "d150b21586b14d6a97a80118fc190003",
    "MoonMochi": "d150b21586b14d6a97a80118fc190004",
    "GreenTea": "d150b21586b14d6a97a80118fc190005",
}

FOLDER_META = """fileFormatVersion: 2\nguid: d150b21586b14d6a97a80118fc190000\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: High-resolution YOMI food detail images derived from the approved source atlas\n  assetBundleName:\n  assetBundleVariant:\n"""

TEXTURE_META = """fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {{}}\n  serializedVersion: 13\n  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n    sRGBTexture: 1\n    linearTexture: 0\n    fadeOut: 0\n    borderMipMap: 0\n    mipMapsPreserveCoverage: 0\n    alphaTestReferenceValue: 0.5\n    mipMapFadeDistanceStart: 1\n    mipMapFadeDistanceEnd: 3\n  isReadable: 0\n  streamingMipmaps: 0\n  grayScaleToAlpha: 0\n  generateCubemap: 6\n  cubemapConvolution: 0\n  seamlessCubemap: 0\n  textureFormat: 1\n  maxTextureSize: 2048\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  lightmap: 0\n  compressionQuality: 100\n  spriteMode: 0\n  spriteExtrude: 1\n  spriteMeshType: 1\n  alignment: 0\n  spritePivot: {{x: 0.5, y: 0.5}}\n  spritePixelsToUnits: 100\n  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}\n  alphaUsage: 1\n  alphaIsTransparency: 0\n  spriteTessellationDetail: -1\n  textureType: 0\n  textureShape: 1\n  singleChannelComponent: 0\n  flipbookRows: 1\n  flipbookColumns: 1\n  platformSettings:\n  - serializedVersion: 3\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 2048\n    resizeAlgorithm: 0\n    textureFormat: -1\n    textureCompression: 0\n    compressionQuality: 100\n    crunchedCompression: 0\n    allowsAlphaSplitting: 0\n    overridden: 0\n    ignorePlatformSupport: 0\n    androidETC2FallbackOverride: 0\n    forceMaximumCompressionQuality_BC6H_BC7: 0\n  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n    physicsShape: []\n    bones: []\n    spriteID:\n    internalID: 0\n    vertices: []\n    indices:\n    edges: []\n    weights: []\n    secondaryTextures: []\n    nameFileIdTable: {{}}\n  mipmapLimitGroupName:\n  pSDRemoveMatte: 0\n  userData: YOMI food detail quality pass; lossless PNG, no Unity texture compression\n  assetBundleName:\n  assetBundleVariant:\n"""


def crop_from_unity_uv(image, uv):
    x, y, w, h = uv
    left = round(x * image.width)
    right = round((x + w) * image.width)
    top = round((1.0 - (y + h)) * image.height)
    bottom = round((1.0 - y) * image.height)
    return image.crop((left, top, right, bottom))


def main():
    image = Image.open(SOURCE).convert("RGB")
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT.parent / "Details.meta").write_text(FOLDER_META, encoding="utf-8")
    print(f"source atlas: {image.width}x{image.height}")

    for name, uv in CROPS.items():
        crop = crop_from_unity_uv(image, uv)
        scale = 4
        enlarged = crop.resize((crop.width * scale, crop.height * scale), Image.Resampling.LANCZOS)
        enlarged = enlarged.filter(ImageFilter.UnsharpMask(radius=1.2, percent=115, threshold=3))
        path = OUT / f"{name}.png"
        enlarged.save(path, format="PNG", optimize=False, compress_level=6)
        path.with_suffix(path.suffix + ".meta").write_text(
            TEXTURE_META.format(guid=META_GUIDS[name]), encoding="utf-8")
        print(f"{name}: {crop.width}x{crop.height} -> {enlarged.width}x{enlarged.height}")


if __name__ == "__main__":
    main()
