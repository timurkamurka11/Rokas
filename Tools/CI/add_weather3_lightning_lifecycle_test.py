from pathlib import Path

path = Path("Assets/Rokas/Tests/PlayMode/HomeWeather3PlayModeTests.cs")
text = path.read_text()
name = "AutomaticLightningSchedulingRecoversAfterLeavingHomeDuringActiveFlash"
if name in text:
    raise SystemExit(f"{name} already exists")

marker = "        [UnityTest]\n        public IEnumerator Weather3PreservesBoundedRigAndStationaryLaptopHotspot()"
test = r'''        [UnityTest]
        public IEnumerator AutomaticLightningSchedulingRecoversAfterLeavingHomeDuringActiveFlash()
        {
            Initialize("rokas-weather3-lightning-reentry-");
            yield return null;

            RokasView view = Bootstrap().View;
            RawImage storm = FindRawImage("HomeStormFlash");
            Assert.That(storm, Is.Not.Null);

            ForceLightning(view);
            view.Tick(.03f);
            yield return null;
            Assert.That(storm.color.a, Is.GreaterThan(.001f),
                "precondition: forced lightning must be active before leaving Home");

            object effects = typeof(RokasView)
                .GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(view);
            Assert.That(effects, Is.Not.Null);
            MethodInfo setLocation = effects.GetType().GetMethod("SetLocation", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(setLocation, Is.Not.Null);

            setLocation.Invoke(effects, new object[] { false, false });
            setLocation.Invoke(effects, new object[] { true, false });

            bool automaticLightningObserved = false;
            for (int index = 0; index < 700 && !automaticLightningObserved; index++)
            {
                view.Tick(.05f);
                automaticLightningObserved = storm.color.a > .001f;
            }

            Assert.That(automaticLightningObserved, Is.True,
                "automatic lightning must be scheduled again after Home re-entry when an active flash was interrupted");
            LogAssert.NoUnexpectedReceived();
        }

'''

if marker not in text:
    raise SystemExit("insertion marker not found")
path.write_text(text.replace(marker, test + marker, 1))
print(f"inserted {name} into {path}")
