# -*- coding: utf-8 -*-
"""Trim, posterize and verify the three certificate papers, then (write) copy them into
Assets/Resources/Items with bill_sheet's import settings. Like the bill, the papers are
NOT on the 55-palette — paper keeps its own warm whites, few tones, binary alpha.

  py -3 ship.py write         — out/ -> Assets/Resources/Items/*.png (+ .meta, sliced border)

The stocks come out of derive.py; each ships with a 12px spriteBorder so the sheet may
grow tall (sliced) without stretching its deckle. GUIDs are md5('lastcall/'+name), the
open_sign_gen scheme, so a fresh machine imports the same identity.
"""
import hashlib
import os
import shutil
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
ITEMS = os.path.join(HERE, "..", "..", "Assets", "Resources", "Items")
NAMES = ["cert_paper_worn", "cert_paper_clean", "cert_paper_ivory"]
BORDER = 12  # sliced border in texture px (the deckle band stays at its drawn scale)


META = """fileFormatVersion: 2
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
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {b}, y: {b}, z: {b}, w: {b}}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
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
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_for(name):
    return hashlib.md5(("lastcall/" + name).encode("utf-8")).hexdigest()


def write():
    for name in NAMES:
        src = os.path.join(OUT, name + ".png")
        im = Image.open(src)
        assert im.size[0] == 512, (name, im.size)
        dst = os.path.join(ITEMS, name + ".png")
        shutil.copyfile(src, dst)
        meta = dst + ".meta"
        if not os.path.exists(meta):
            with open(meta, "w", encoding="utf-8", newline="\n") as f:
                f.write(META.format(guid=guid_for(name), b=BORDER))
        print("shipped", dst, im.size)


if __name__ == "__main__":
    {"write": write}[sys.argv[1]]()
