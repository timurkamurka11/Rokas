COMPLETED:
HOME scene-action pill layout remains unchanged and approved. The seven circular medallion glyphs still use the reference-matched silhouettes: Свет/lamp, За окном/torii, Снаряжение/crossed swords, YOMI / Ноутбук/laptop, Заварить чай/cup, Мамэ/cat, Выйти из дома/door. The right chevrons, pill sizes/positions, text, hover/press feedback and all existing actions are unchanged.

FIX:
Removed the runtime dependency on PNG/TextureImporter decoding for HOME reference icons. HomeReferenceIconBinder now embeds the icon alpha atlas as gzip-compressed data, reconstructs a standard RGBA32 Texture2D directly with SetPixels32, and applies the gold reference color in memory. This bypasses both the failed Resources Texture2D import path and the failed ImageConversion.LoadImage PNG path.

STATIC VERIFICATION:
The embedded source alpha data was generated from the approved 896x128 atlas, decompresses to exactly 896*128 alpha bytes, and the generated C# source has balanced braces. Unity/PlayMode was not run from this chat.

UNITY:
NOT RUN — user will validate manually.

NEXT:
User pulls development and runs Play. Expected regression check: no HOME icon atlas loading/PNG decoding warning and all seven medallion icons render in the approved HOME pills.
