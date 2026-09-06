COMPLETED:
HOME scene-action pill layout remains unchanged and approved. The seven circular medallion glyphs use reference-matched artwork extracted from the supplied HOME mockup: Свет/lamp, За окном/torii, Снаряжение/crossed swords, YOMI / Ноутбук/laptop, Заварить чай/cup, Мамэ/cat, Выйти из дома/door. The right chevrons, pill sizes/positions, text, hover/press feedback and all existing actions are unchanged.

FIX:
The reference icon atlas no longer depends on Unity importing the PNG as a Texture2D. The exact same PNG blob is also stored as Resources/HomeActionIconsBytes.bytes, loaded as TextAsset, and decoded at runtime with ImageConversion.LoadImage. This avoids the prior Resources.Load<Texture2D>/AssetDatabase null-load warning.

UNITY:
NOT RUN — user will validate manually.

NEXT:
User pulls development, lets Unity reimport, and checks that no HOME icon atlas load warning appears and the seven medallion icons render correctly.
