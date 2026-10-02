using System.Collections;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class WorkbenchUiPlayModeTests
    {
        private GameObject root;

        [UnityTest]
        public IEnumerator WorkbenchSwitchesWeaponPresentationAndKeepsUpgradeAuthoritative()
        {
            root = new GameObject("WorkbenchUiFixture");
            root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            var stageObject = new GameObject("Stage", typeof(RectTransform));
            var stage = (RectTransform)stageObject.transform;
            stage.SetParent(root.transform, false);
            stage.anchorMin = stage.anchorMax = new Vector2(0, 1);
            stage.pivot = new Vector2(0, 1);
            stage.sizeDelta = new Vector2(1744, 812);

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            var ui = new UiKit(assets, null);
            var state = new SaveData { phase = RunPhase.Home, yen = 2000, weaponLevel = 1 };
            var session = new GameSession(state, new ContractDefinition());
            bool closed = false;
            var view = new LaptopWorkbenchView(ui, session, (action, _) => action());

            view.Build(stage, () => closed = true);
            yield return new WaitForSecondsRealtime(.25f);

            Button twoHanded = FindButton("WorkbenchWeaponTwoHanded");
            Button dagger = FindButton("WorkbenchWeaponDagger");
            Button upgrade = FindButton("UpgradeWeapon");
            Assert.That(twoHanded, Is.Not.Null);
            Assert.That(dagger, Is.Not.Null);
            Assert.That(upgrade, Is.Not.Null);
            Assert.That(FindText("WorkbenchInfoCategory").text, Is.EqualTo("ДВУРУЧНИК"));
            Assert.That(upgrade.IsInteractable(), Is.True);

            dagger.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.32f);
            Assert.That(FindText("WorkbenchInfoCategory").text, Is.EqualTo("КИНЖАЛ"));
            Assert.That(Find("WorkbenchWeaponPreview").GetComponent<WorkbenchWeaponGraphic>().Kind,
                Is.EqualTo(WorkbenchWeaponKind.Dagger));
            Assert.That(upgrade.IsInteractable(), Is.False,
                "Dagger must not fabricate an upgrade path that the domain model does not own.");

            twoHanded.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.32f);
            Assert.That(FindText("WorkbenchInfoCategory").text, Is.EqualTo("ДВУРУЧНИК"));
            Assert.That(upgrade.IsInteractable(), Is.True);
            upgrade.onClick.Invoke();
            Assert.That(session.State.weaponLevel, Is.EqualTo(2),
                "The CTA must call the existing GameSession upgrade path instead of a local UI counter.");
            Assert.That(session.State.yen, Is.EqualTo(1700));

            FindButton("WorkbenchClose").onClick.Invoke();
            Assert.That(closed, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private Button FindButton(string name)
        {
            GameObject item = Find(name);
            return item ? item.GetComponent<Button>() : null;
        }

        private Text FindText(string name)
        {
            GameObject item = Find(name);
            return item ? item.GetComponent<Text>() : null;
        }

        private GameObject Find(string name)
        {
            if (!root) return null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
        }
    }
}
