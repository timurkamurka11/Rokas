import importlib.util
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
HELPER = TOOLS / 'asset_validation.py'


def load_helper():
    if not HELPER.exists():
        raise AssertionError('asset_validation.py missing: Messages UI classification is not implemented yet')
    spec = importlib.util.spec_from_file_location('asset_validation', HELPER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


VALID_SPRITE_META = '''fileFormatVersion: 2
guid: 0123456789abcdef0123456789abcdef
TextureImporter:
  mipmaps:
    enableMipMap: 0
  spriteMode: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
'''

PLAIN_TEXTURE_META = '''fileFormatVersion: 2
guid: fedcba9876543210fedcba9876543210
TextureImporter:
  mipmaps:
    enableMipMap: 1
  spriteMode: 0
  alphaUsage: 1
  alphaIsTransparency: 0
  textureType: 0
'''


class MessagesAssetValidationTests(unittest.TestCase):
    def test_valid_messages_portrait_is_valid(self):
        module = load_helper()
        errors = module.validate_png_asset(
            Path('Assets/Rokas/Resources/Messages/Portraits/TestPortrait.png'),
            96, 96, 6, VALID_SPRITE_META)
        self.assertEqual([], errors)

    def test_valid_messages_icon_is_valid(self):
        module = load_helper()
        errors = module.validate_png_asset(
            Path('Assets/Rokas/Resources/Messages/Icons/TestIcon.png'),
            64, 64, 6, VALID_SPRITE_META)
        self.assertEqual([], errors)

    def test_messages_plain_texture_is_invalid(self):
        module = load_helper()
        errors = module.validate_png_asset(
            Path('Assets/Rokas/Resources/Messages/Portraits/BadTexture.png'),
            96, 96, 6, PLAIN_TEXTURE_META)
        self.assertTrue(any('Messages UI sprite importer invalid' in error for error in errors), errors)
        self.assertFalse(any('Background below HD' in error for error in errors), errors)

    def test_small_background_outside_messages_remains_invalid(self):
        module = load_helper()
        errors = module.validate_png_asset(
            Path('Assets/Rokas/Resources/SomeBackground.png'),
            96, 96, 6, VALID_SPRITE_META)
        self.assertEqual(['Background below HD: SomeBackground.png'], errors)

    def test_ugui_declares_textmeshpro_assembly(self):
        module = load_helper()
        assemblies = module.declared_external_assemblies({'com.unity.ugui': '2.0.0'})
        self.assertIn('UnityEngine.UI', assemblies)
        self.assertIn('Unity.TextMeshPro', assemblies)

    def test_textmeshpro_is_not_accepted_without_ugui(self):
        module = load_helper()
        assemblies = module.declared_external_assemblies({})
        self.assertNotIn('Unity.TextMeshPro', assemblies)


if __name__ == '__main__':
    unittest.main()
