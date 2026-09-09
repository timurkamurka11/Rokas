"""Focused PNG classification rules shared by the ROKAS static asset validator."""
from pathlib import Path
import re

_MESSAGES_PREFIX = ('Assets', 'Rokas', 'Resources', 'Messages')


def _contains_path_prefix(path, prefix):
    parts = tuple(Path(path).parts)
    width = len(prefix)
    return any(parts[index:index + width] == prefix for index in range(len(parts) - width + 1))


def _meta_int(meta_text, field):
    if not meta_text:
        return None
    match = re.search(r'^\s*' + re.escape(field) + r':\s*(-?\d+)\s*$', meta_text, re.M)
    return int(match.group(1)) if match else None


def declared_external_assemblies(dependencies):
    """Return package assemblies that are justified by the declared Unity dependencies."""
    assemblies = set()
    if 'com.unity.ugui' in dependencies:
        assemblies.update(('UnityEngine.UI', 'Unity.TextMeshPro'))
    if 'dev.yarnspinner.unity' in dependencies:
        assemblies.add('YarnSpinner.Unity')
    return assemblies


def validate_png_asset(path, width, height, color_type, meta_text):
    """Return validation errors for one already-signature-checked PNG."""
    path = Path(path)
    errors = []

    if _contains_path_prefix(path, _MESSAGES_PREFIX):
        if meta_text is None:
            return ['Messages UI sprite missing meta: ' + path.name]

        if _meta_int(meta_text, 'textureType') != 8 or _meta_int(meta_text, 'spriteMode') != 1:
            errors.append('Messages UI sprite importer invalid: ' + path.name)
        if _meta_int(meta_text, 'enableMipMap') != 0:
            errors.append('Messages UI sprite mipmaps must be OFF: ' + path.name)
        if color_type in (4, 6):
            if _meta_int(meta_text, 'alphaUsage') != 1 or _meta_int(meta_text, 'alphaIsTransparency') != 1:
                errors.append('Messages UI sprite transparency import invalid: ' + path.name)
        return errors

    if 'Yokai' in path.parts or 'Familiars' in path.parts:
        if color_type != 6:
            errors.append('Character PNG must carry real RGBA: ' + path.name)
        return errors

    if width < 1600 or height < 900:
        errors.append('Background below HD: ' + path.name)
    return errors
