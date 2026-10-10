from pathlib import Path


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one match, found {count}")
    return text.replace(old, new, 1)


laptop_path = Path("Assets/Rokas/Scripts/Presentation/LaptopView.cs")
laptop = laptop_path.read_text(encoding="utf-8")
laptop = replace_once(
    laptop,
    "        private readonly Func<float> videoVolume;\n        private RectTransform frame;",
    "        private readonly Func<float> videoVolume;\n        private readonly Func<bool> videoTransitions;\n        private RectTransform frame;",
    "video transitions field",
)
laptop = replace_once(
    laptop,
    "            VideoSequencePresenter video, Func<float> videoVolume)\n        {",
    "            VideoSequencePresenter video, Func<float> videoVolume, Func<bool> videoTransitions)\n        {",
    "constructor signature",
)
laptop = replace_once(
    laptop,
    "            this.video = video ?? throw new ArgumentNullException(nameof(video));\n            this.videoVolume = videoVolume ?? throw new ArgumentNullException(nameof(videoVolume));\n            food =",
    "            this.video = video ?? throw new ArgumentNullException(nameof(video));\n            this.videoVolume = videoVolume ?? throw new ArgumentNullException(nameof(videoVolume));\n            this.videoTransitions = videoTransitions ?? throw new ArgumentNullException(nameof(videoTransitions));\n            food =",
    "constructor assignment",
)
laptop = replace_once(
    laptop,
    "            clockMinute = -1;\n            Booting = true;\n            video.PlayInHost(frame, \"LaptopBoot.mp4\", \"LaptopBootSurface\", 1792, 1008, videoVolume(), FinishBoot);",
    "            clockMinute = -1;\n            if (!videoTransitions())\n            {\n                Booting = false;\n                BuildReadyFrame();\n                return;\n            }\n            Booting = true;\n            video.PlayInHost(frame, \"LaptopBoot.mp4\", \"LaptopBootSurface\", 1792, 1008, videoVolume(), FinishBoot);",
    "fixture bypass",
)
laptop_path.write_text(laptop, encoding="utf-8")

view_path = Path("Assets/Rokas/Scripts/Presentation/RokasView.cs")
view = view_path.read_text(encoding="utf-8")
view = replace_once(
    view,
    "                owner.VideoPresenter, () => audio.VideoVolume);",
    "                owner.VideoPresenter, () => audio.VideoVolume, () => owner.VideoTransitionsEnabled);",
    "view constructor args",
)
view_path.write_text(view, encoding="utf-8")

print("Applied fixture video-transition seam.")
