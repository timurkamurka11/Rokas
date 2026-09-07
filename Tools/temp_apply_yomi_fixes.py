from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def patch(rel, old, new, count=1):
    path = ROOT / rel
    text = path.read_text(encoding="utf-8")
    actual = text.count(old)
    if actual != count:
        raise RuntimeError(f"{rel}: expected {count} matches, found {actual} for {old[:80]!r}")
    path.write_text(text.replace(old, new, count), encoding="utf-8")


# Core: expose a typed reason without changing the existing restriction.
patch(
    "Assets/Rokas/Scripts/Core/FoodService.cs",
    "namespace Rokas.Core\n{\n    public sealed class FoodService",
    "namespace Rokas.Core\n{\n    public enum FoodConsumeBlockReason\n    {\n        None,\n        ContractPaymentPending,\n        InvalidPhase,\n        UnknownFood,\n        EffectAlreadyActive\n    }\n\n    public sealed class FoodService")

patch(
    "Assets/Rokas/Scripts/Core/FoodService.cs",
    "        // The laptop Food screen uses the already persisted prepared-food slot. Non-tea\n"
    "        // effects are intentionally data-only for now; gameplay can extend by food id\n"
    "        // without introducing a parallel inventory/save architecture.\n"
    "        public bool Consume(SaveData state, string foodId)\n"
    "        {\n"
    "            RequireState(state);\n"
    "            if ((state.phase != RunPhase.Home && state.phase != RunPhase.Accepted) ||\n"
    "                !IsFoodScreenItem(foodId) ||\n"
    "                !string.IsNullOrEmpty(state.preparedFoodId))\n"
    "            {\n"
    "                return false;\n"
    "            }\n\n"
    "            state.preparedFoodId = foodId;\n"
    "            return true;\n"
    "        }",
    "        // The laptop Food screen uses the already persisted prepared-food slot. Non-tea\n"
    "        // effects are intentionally data-only for now; gameplay can extend by food id\n"
    "        // without introducing a parallel inventory/save architecture.\n"
    "        public FoodConsumeBlockReason GetConsumeBlockReason(SaveData state, string foodId)\n"
    "        {\n"
    "            RequireState(state);\n"
    "            if (state.phase == RunPhase.Payment) return FoodConsumeBlockReason.ContractPaymentPending;\n"
    "            if (state.phase != RunPhase.Home && state.phase != RunPhase.Accepted)\n"
    "                return FoodConsumeBlockReason.InvalidPhase;\n"
    "            if (!IsFoodScreenItem(foodId)) return FoodConsumeBlockReason.UnknownFood;\n"
    "            if (!string.IsNullOrEmpty(state.preparedFoodId)) return FoodConsumeBlockReason.EffectAlreadyActive;\n"
    "            return FoodConsumeBlockReason.None;\n"
    "        }\n\n"
    "        public bool Consume(SaveData state, string foodId)\n"
    "        {\n"
    "            if (GetConsumeBlockReason(state, foodId) != FoodConsumeBlockReason.None) return false;\n"
    "            state.preparedFoodId = foodId;\n"
    "            return true;\n"
    "        }")

patch(
    "Assets/Rokas/Scripts/Core/GameSession.cs",
    "        public bool ConsumeFood(string foodId)\n        {\n            return NotifyIf(food.Consume(State, foodId));\n        }",
    "        public FoodConsumeBlockReason GetFoodConsumeBlockReason(string foodId)\n"
    "        {\n"
    "            return food.GetConsumeBlockReason(State, foodId);\n"
    "        }\n\n"
    "        public bool ConsumeFood(string foodId)\n"
    "        {\n"
    "            return NotifyIf(food.Consume(State, foodId));\n"
    "        }")

# Food UI: keep atlas thumbnails, use dedicated quality-pass textures for large detail art.
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs",
    "            public readonly Rect DetailUv;\n\n"
    "            public FoodItem(string id, string name, string summary, string symbol, string description,\n"
    "                string effectOne, string effectTwo, string duration, Rect thumbUv, Rect detailUv)\n",
    "            public readonly Rect DetailUv;\n"
    "            public readonly string DetailResource;\n\n"
    "            public FoodItem(string id, string name, string summary, string symbol, string description,\n"
    "                string effectOne, string effectTwo, string duration, Rect thumbUv, Rect detailUv, string detailResource)\n")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs",
    "                DetailUv = detailUv;\n            }",
    "                DetailUv = detailUv;\n                DetailResource = detailResource;\n            }")

replacements = [
    ("new Rect(0f, .5f, 1f, .5f)),", "new Rect(0f, .5f, 1f, .5f), null),"),
    ("new Rect(.333125f, .25f, .333125f, .25f)),", "new Rect(.333125f, .25f, .333125f, .25f), \"Food/Details/TravelerOnigiri\"),"),
    ("new Rect(.66625f, .25f, .33375f, .25f)),", "new Rect(.66625f, .25f, .33375f, .25f), \"Food/Details/SpicyMiso\"),"),
    ("new Rect(0f, 0f, .333125f, .25f)),", "new Rect(0f, 0f, .333125f, .25f), \"Food/Details/HunterTempura\"),"),
    ("new Rect(.333125f, 0f, .333125f, .25f)),", "new Rect(.333125f, 0f, .333125f, .25f), \"Food/Details/MoonMochi\"),"),
    ("new Rect(.66625f, 0f, .33375f, .25f))\n", "new Rect(.66625f, 0f, .33375f, .25f), \"Food/Details/GreenTea\")\n"),
]
for old, new in replacements:
    patch("Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs", old, new)

patch(
    "Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs",
    "        private readonly Action<Func<bool>, string> act;\n        private readonly Action click;\n\n        private Texture2D atlas;",
    "        private readonly Action<Func<bool>, string> act;\n        private readonly Action click;\n        private readonly Action<string> notify;\n\n        private Texture2D atlas;\n        private Texture2D[] detailTextures;")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs",
    "        public LaptopFoodView(UiKit ui, GameSession session, Action<Func<bool>, string> act, Action click)\n"
    "        {\n"
    "            this.ui = ui;\n"
    "            this.session = session;\n"
    "            this.act = act;\n"
    "            this.click = click;\n"
    "        }",
    "        public LaptopFoodView(UiKit ui, GameSession session, Action<Func<bool>, string> act, Action click, Action<string> notify)\n"
    "        {\n"
    "            this.ui = ui;\n"
    "            this.session = session;\n"
    "            this.act = act;\n"
    "            this.click = click;\n"
    "            this.notify = notify;\n"
    "        }")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs",
    "            if (!atlas)\n                throw new InvalidOperationException(\"ROKAS Food atlas is missing from Resources/Food.\");\n\n"
    "            selected = Mathf.Clamp(selected, 0, Items.Length - 1);",
    "            if (!atlas)\n                throw new InvalidOperationException(\"ROKAS Food atlas is missing from Resources/Food.\");\n\n"
    "            detailTextures = new Texture2D[Items.Length];\n"
    "            for (int i = 0; i < Items.Length; i++)\n"
    "            {\n"
    "                if (string.IsNullOrEmpty(Items[i].DetailResource)) continue;\n"
    "                detailTextures[i] = Resources.Load<Texture2D>(Items[i].DetailResource);\n"
    "                if (!detailTextures[i])\n"
    "                    throw new InvalidOperationException(\"ROKAS Food detail image is missing: \" + Items[i].DetailResource);\n"
    "            }\n\n"
    "            selected = Mathf.Clamp(selected, 0, Items.Length - 1);")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs",
    "            AddTrigger(trigger, EventTriggerType.PointerClick, () => UpdateSelection(index));",
    "            AddTrigger(trigger, EventTriggerType.PointerClick, () =>\n"
    "            {\n"
    "                click?.Invoke();\n"
    "                UpdateSelection(index);\n"
    "            });")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs",
    "            detailTitle.text = item.Name;\n            detailArt.uvRect = item.DetailUv;\n            description.text = item.Description;",
    "            detailTitle.text = item.Name;\n"
    "            Texture2D detailTexture = detailTextures != null ? detailTextures[selected] : null;\n"
    "            detailArt.texture = detailTexture ? detailTexture : atlas;\n"
    "            detailArt.uvRect = detailTexture ? new Rect(0, 0, 1, 1) : item.DetailUv;\n"
    "            description.text = item.Description;")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs",
    "            var item = Items[selected];\n            if (act != null)\n                act(() => session.ConsumeFood(item.Id), item.Name + \" — эффект активен.\");",
    "            var item = Items[selected];\n"
    "            if (session.GetFoodConsumeBlockReason(item.Id) == FoodConsumeBlockReason.ContractPaymentPending)\n"
    "            {\n"
    "                notify?.Invoke(\"Сначала сдайте контракт.\");\n"
    "                UpdateActionState();\n"
    "                return;\n"
    "            }\n"
    "            if (act != null)\n"
    "                act(() => session.ConsumeFood(item.Id), item.Name + \" — эффект активен.\");")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopFoodView.cs",
    "            bool active = session.State.preparedFoodId == Items[selected].Id;\n"
    "            eatButton.interactable = validPhase && empty;\n"
    "            eatTitle.text = active ? \"Эффект активен\" : empty ? \"Съесть\" : \"Другой эффект активен\";",
    "            bool active = session.State.preparedFoodId == Items[selected].Id;\n"
    "            bool paymentPending = session.State.phase == RunPhase.Payment;\n"
    "            eatButton.interactable = paymentPending || (validPhase && empty);\n"
    "            eatTitle.text = active ? \"Эффект активен\" : empty ? \"Съесть\" : \"Другой эффект активен\";")

# Pass the existing toast path into the specialized food view.
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopView.cs",
    "            Action<Func<bool>, string> act, Action click, Action close)",
    "            Action<Func<bool>, string> act, Action click, Action<string> notify, Action close)")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopView.cs",
    "            food = new LaptopFoodView(ui, session, act, click);",
    "            food = new LaptopFoodView(ui, session, act, click, notify);")
patch(
    "Assets/Rokas/Scripts/Presentation/RokasView.cs",
    "            laptop = new LaptopView(ui, assets, session, contracts, Act, audio.Click, ClosePanel);",
    "            laptop = new LaptopView(ui, assets, session, contracts, Act, audio.Click, ToastShort, ClosePanel);")

# Reuse the existing RokasAudio click path from dynamically-bound News controls.
patch(
    "Assets/Rokas/Scripts/Presentation/RokasBootstrap.cs",
    "        public void ApplyDisplaySettings()",
    "        public void PlayUiClick()\n"
    "        {\n"
    "            if (sound != null) sound.Click();\n"
    "        }\n\n"
    "        public void ApplyDisplaySettings()")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopNewsRuntime.cs",
    "        private bool muted;",
    "        private bool muted;\n        private Action click;")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopNewsRuntime.cs",
    "            ui = new UiKit(assets, null);\n            Build((RectTransform)transform);",
    "            var bootstrap = GetComponentInParent<RokasBootstrap>();\n"
    "            click = bootstrap != null ? bootstrap.PlayUiClick : null;\n"
    "            ui = new UiKit(assets, click);\n"
    "            Build((RectTransform)transform);")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopNewsRuntime.cs",
    "            playHotspot.onClick.AddListener(TogglePlay);",
    "            playHotspot.onClick.AddListener(() => { click?.Invoke(); TogglePlay(); });")
patch(
    "Assets/Rokas/Scripts/Presentation/LaptopNewsRuntime.cs",
    "            button.onClick.AddListener(() => action?.Invoke());",
    "            button.onClick.AddListener(() => { click?.Invoke(); action?.Invoke(); });")

print("Applied bounded YOMI food quality, contract feedback and laptop click fixes.")
