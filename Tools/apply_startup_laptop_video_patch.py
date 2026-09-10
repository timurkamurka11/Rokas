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
    "        private readonly Action close;\n        private RectTransform frame;",
    "        private readonly Action close;\n        private readonly VideoSequencePresenter video;\n        private readonly Func<float> videoVolume;\n        private RectTransform frame;",
    "laptop fields",
)

laptop = replace_once(
    laptop,
    "        public bool IsClosing { get; private set; }\n        public bool MessagesOpen { get { return section == 6 && !IsClosing; } }",
    "        public bool IsClosing { get; private set; }\n        public bool Booting { get; private set; }\n        public bool MessagesOpen { get { return section == 6 && !IsClosing && !Booting; } }",
    "laptop state properties",
)

laptop = replace_once(
    laptop,
    "        public LaptopView(UiKit ui, RokasAssets assets, GameSession session, ContractPanels contracts,\n            Action<Func<bool>, string> act, Action click, Action<string> notify, Action close)\n        {\n            this.ui = ui;\n            this.assets = assets;\n            this.session = session;\n            this.contracts = contracts;\n            this.click = click;\n            this.close = close;\n            food = new LaptopFoodView(ui, session, act, click, notify);",
    "        public LaptopView(UiKit ui, RokasAssets assets, GameSession session, ContractPanels contracts,\n            Action<Func<bool>, string> act, Action click, Action<string> notify, Action close,\n            VideoSequencePresenter video, Func<float> videoVolume)\n        {\n            this.ui = ui;\n            this.assets = assets;\n            this.session = session;\n            this.contracts = contracts;\n            this.click = click;\n            this.close = close;\n            this.video = video ?? throw new ArgumentNullException(nameof(video));\n            this.videoVolume = videoVolume ?? throw new ArgumentNullException(nameof(videoVolume));\n            food = new LaptopFoodView(ui, session, act, click, notify);",
    "laptop constructor",
)

laptop = replace_once(
    laptop,
    "        public void Reset()\n        {\n            messages.Hide();\n            section = -1;\n            lastTile = 0;\n            IsClosing = false;\n            closed = null;\n        }",
    "        public void Reset()\n        {\n            video.Cancel();\n            messages.Hide();\n            section = -1;\n            lastTile = 0;\n            IsClosing = false;\n            Booting = false;\n            closed = null;\n            frame = null;\n            content = null;\n            windowGroup = null;\n            contentGroup = null;\n            clock = null;\n            date = null;\n        }",
    "laptop reset",
)

start_marker = "        public void Build(RectTransform parent)\n        {"
next_marker = "        private void BuildContent()"
start = laptop.index(start_marker)
end = laptop.index(next_marker, start)
old_build = laptop[start:end]
ready_start = old_build.index('            Surface(frame, "Camera", 892, 12, 8, 8, 4, new Color(.08f, .10f, .11f));')
ready_tail = old_build[ready_start:]
method_close = "        }\n\n"
if not ready_tail.endswith(method_close):
    raise SystemExit("laptop build extraction: unexpected method ending")
ready_body = ready_tail[:-len(method_close)]
new_build = '''        public void Build(RectTransform parent)
        {
            Surface(parent, "LaptopShadow", 66, 50, 1792, 1008, 22, new Color(0, 0, 0, .5f));
            frame = Surface(parent, "YomiLaptop", 64, 36, 1792, 1008, 18, new Color(.018f, .023f, .027f), true).rectTransform;
            windowGroup = frame.gameObject.AddComponent<CanvasGroup>();
            windowGroup.alpha = 0;
            openTime = 0;
            pageTime = 0;
            content = null;
            contentGroup = null;
            clock = null;
            date = null;
            clockMinute = -1;
            Booting = true;
            video.PlayInHost(frame, "LaptopBoot.mp4", "LaptopBootSurface", 1792, 1008, videoVolume(), FinishBoot);
        }

        private void FinishBoot()
        {
            if (!Booting || IsClosing || !frame) return;
            Booting = false;
            BuildReadyFrame();
        }

        private void BuildReadyFrame()
        {
''' + ready_body + method_close
laptop = laptop[:start] + new_build + laptop[end:]

laptop = replace_once(
    laptop,
    "        private void OpenSection(int index)\n        {\n            if (IsClosing) return;",
    "        private void OpenSection(int index)\n        {\n            if (IsClosing || Booting) return;",
    "open section boot guard",
)

laptop = replace_once(
    laptop,
    "        public bool BackToDesktop()\n        {\n            if (section < 0 || IsClosing) return false;",
    "        public bool BackToDesktop()\n        {\n            if (Booting || section < 0 || IsClosing) return false;",
    "back to desktop boot guard",
)

laptop = replace_once(
    laptop,
    "        public void BeginClose(Action complete)\n        {\n            if (IsClosing) return;\n            messages.Hide();",
    "        public void BeginClose(Action complete)\n        {\n            if (IsClosing) return;\n            if (Booting)\n            {\n                video.Cancel();\n                Booting = false;\n            }\n            messages.Hide();",
    "close cancels boot",
)

laptop_path.write_text(laptop, encoding="utf-8")

view_path = Path("Assets/Rokas/Scripts/Presentation/RokasView.cs")
view = view_path.read_text(encoding="utf-8")
view = replace_once(
    view,
    "            laptop = new LaptopView(ui, assets, session, contracts, Act, audio.Click, ToastShort, ClosePanel);",
    "            laptop = new LaptopView(ui, assets, session, contracts, Act, audio.Click, ToastShort, ClosePanel,\n                owner.VideoPresenter, () => audio.VideoVolume);",
    "RokasView laptop constructor",
)
view_path.write_text(view, encoding="utf-8")

print("Applied startup/laptop video production patch.")
