"""Validate genuine combat flipbooks without relaxing background or UI gates."""
from pathlib import Path
import unittest
from asset_validation import validate_png_asset

ATLAS = Path('Assets/Rokas/Resources/Combat/ReactiveTurns/Vfx/EmberGen/HeavyBurst.png')
META = '''TextureImporter:
  textureType: 0
  enableMipMap: 0
  alphaUsage: 1
  alphaIsTransparency: 0
  textureSettings:
    filterMode: 1
    wrapU: 1
    wrapV: 1
'''

class EmberAssetValidationTests(unittest.TestCase):
    def test_game_ready_atlas_is_not_misclassified_as_background(self):
        for size in (1024, 2048):
            self.assertEqual([], validate_png_asset(ATLAS, size, size, 6, META))

    def test_ordinary_small_background_still_fails(self):
        errors = validate_png_asset(Path('Assets/Rokas/Resources/SomeBackground.png'), 1024, 1024, 6, META)
        self.assertEqual(['Background below HD: SomeBackground.png'], errors)

    def test_missing_alpha_and_invalid_frame_layout_fail(self):
        errors = validate_png_asset(ATLAS, 1000, 512, 2, META)
        self.assertEqual(2, len(errors))
        self.assertTrue(any('RGBA' in error for error in errors))
        self.assertTrue(any('8x8' in error for error in errors))

    def test_missing_meta_fails(self):
        self.assertEqual(['EmberGen flipbook missing meta: HeavyBurst.png'],
                         validate_png_asset(ATLAS, 1024, 1024, 6, None))

    def test_mip_bleeding_and_premult_colour_dilation_fail(self):
        bad = META.replace('enableMipMap: 0', 'enableMipMap: 1').replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1')
        errors = validate_png_asset(ATLAS, 1024, 1024, 6, bad)
        self.assertEqual(2, len(errors))

    def test_repeating_carrier_and_nearest_filter_fail(self):
        bad = META.replace('wrapU: 1', 'wrapU: 0').replace('filterMode: 1', 'filterMode: 0')
        errors = validate_png_asset(ATLAS, 1024, 1024, 6, bad)
        self.assertEqual(2, len(errors))

if __name__ == '__main__':
    unittest.main()
