from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
path = ROOT / "Assets/Rokas/Tests/PlayMode/HomeAtmospherePlayModeTests.cs"
text = path.read_text(encoding="utf-8")

if "HomeBuildsLogical25DPlanesAndReadableDepth" in text:
    raise SystemExit("2.5D RED tests already applied")

profile_anchor = '            AssertField(profile, "wetGlassIntensity");\n'
if profile_anchor not in text:
    raise SystemExit("missing Home atmosphere profile assertion anchor")
text = text.replace(
    profile_anchor,
    profile_anchor
    + '            AssertField(profile, "parallaxIntensity");\n'
    + '            AssertField(profile, "outsideMotionIntensity");\n'
    + '            AssertField(profile, "weatherReadability");\n',
    1,
)

flash_anchor = '''            Graphic flash = FindGraphic("HomeStormFlash");
            Assert.That(flash, Is.Not.Null);
            Assert.That(flash.color.a, Is.GreaterThan(.01f), "forced lightning must visibly affect the home window immediately");
'''
if flash_anchor not in text:
    raise SystemExit("missing lightning assertion anchor")
text = text.replace(
    flash_anchor,
    '''            Graphic flash = FindGraphic("HomeStormFlash");
            Assert.That(flash, Is.Not.Null);
            Assert.That(flash, Is.TypeOf<RawImage>(),
                "lightning must use a soft textured environmental surface rather than a flat rectangular Image box");
            Assert.That(((RawImage)flash).texture, Is.Not.Null,
                "soft lightning surface must have a falloff texture");
            Assert.That(flash.color.a, Is.GreaterThan(.01f), "forced lightning must visibly affect the home window immediately");
''',
    1,
)

insert_anchor = '''        [UnityTest]
        public IEnumerator RepeatedTicksReuseTheBoundedWeatherRig()
'''
if insert_anchor not in text:
    raise SystemExit("missing bounded weather test anchor")
new_tests = r'''        [UnityTest]
        public IEnumerator HomeBuildsLogical25DPlanesAndReadableDepth()
        {
            Initialize("rokas-home-25d-layers-");
            yield return null;

            RawImage world = FindRawImage("WorldIllustration");
            RawImage outside = FindRawImage("HomeOutsideParallax");
            RawImage foreground = FindRawImage("HomeForegroundDepth");
            RawImage mistNear = FindRawImage("HomeExteriorMistNear");
            RawImage wetStreaks = FindRawImage("HomeWetGlassStreaks");

            Assert.That(world, Is.Not.Null);
            Assert.That(outside, Is.Not.Null,
                "Home must isolate the existing outside/window art into its own 2.5D plane");
            Assert.That(outside.texture, Is.SameAs(world.texture),
                "outside plane must reuse the existing Home illustration instead of replacing the art style");
            Assert.That(foreground, Is.Not.Null,
                "Home must include a restrained near-depth visual plane without moving UI");
            Assert.That(mistNear, Is.Not.Null,
                "outside atmosphere must contain a second independently drifting depth plane");
            Assert.That(wetStreaks, Is.Not.Null,
                "wet glass must include a second sparse moving streak layer for readable rain-on-glass motion");
            Assert.That(FindRect("HomeStormRoomLift"), Is.Not.Null,
                "storm lightning must have a room-wide environmental response layer");

            ParticleSystem far = FindParticle("HomeRainFar");
            ParticleSystem mid = FindParticle("HomeRainMid");
            ParticleSystem near = FindParticle("HomeRainNear");
            Assert.That(far.main.startSize.constant, Is.LessThan(mid.main.startSize.constant));
            Assert.That(mid.main.startSize.constant, Is.LessThan(near.main.startSize.constant));
            Assert.That(near.main.startSize.constant, Is.GreaterThanOrEqualTo(far.main.startSize.constant * 3f),
                "near rain must be materially larger than far rain so depth reads at a glance");

            ParticleSystemRenderer farRenderer = far.GetComponent<ParticleSystemRenderer>();
            ParticleSystemRenderer midRenderer = mid.GetComponent<ParticleSystemRenderer>();
            ParticleSystemRenderer nearRenderer = near.GetComponent<ParticleSystemRenderer>();
            Assert.That(farRenderer.lengthScale, Is.LessThan(midRenderer.lengthScale));
            Assert.That(midRenderer.lengthScale, Is.LessThan(nearRenderer.lengthScale));
            Assert.That(nearRenderer.lengthScale, Is.GreaterThanOrEqualTo(farRenderer.lengthScale * 1.8f),
                "near rain streak length must separate clearly from far rain");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HybridParallaxMovesOnlyVisualPlanesAndStaysBounded()
        {
            Initialize("rokas-home-25d-parallax-");
            yield return null;

            RectTransform world = FindRect("WorldIllustration");
            RectTransform outside = FindRect("HomeOutsideParallax");
            RectTransform foreground = FindRect("HomeForegroundDepth");
            RectTransform wet = FindRect("HomeWetGlass");
            RectTransform haze = FindRect("HomeExteriorHaze");
            RectTransform laptop = FindRect("LaptopHotspot");
            Assert.That(world, Is.Not.Null);
            Assert.That(outside, Is.Not.Null);
            Assert.That(foreground, Is.Not.Null);
            Assert.That(wet, Is.Not.Null);
            Assert.That(haze, Is.Not.Null);
            Assert.That(laptop, Is.Not.Null);

            Vector2 worldStart = world.anchoredPosition;
            Vector2 outsideStart = outside.anchoredPosition;
            Vector2 foregroundStart = foreground.anchoredPosition;
            Vector2 wetStart = wet.anchoredPosition;
            Vector2 hazeStart = haze.anchoredPosition;
            Vector2 laptopStart = laptop.anchoredPosition;
            Rect wetUvStart = FindRawImage("HomeWetGlass").uvRect;

            for (int index = 0; index < 300; index++)
                Bootstrap().View.Tick(1f / 60f);
            yield return null;

            Vector2 worldDelta = world.anchoredPosition - worldStart;
            Vector2 outsideDelta = outside.anchoredPosition - outsideStart;
            Vector2 foregroundDelta = foreground.anchoredPosition - foregroundStart;
            Vector2 wetDelta = wet.anchoredPosition - wetStart;
            Vector2 hazeDelta = haze.anchoredPosition - hazeStart;

            Assert.That(worldDelta.magnitude, Is.GreaterThan(.03f),
                "room/base plane must have a barely perceptible ambient drift");
            Assert.That(worldDelta.magnitude, Is.LessThanOrEqualTo(2.25f),
                "room/base parallax must stay subtle");
            Assert.That(outsideDelta.magnitude, Is.LessThanOrEqualTo(2.25f),
                "outside/far parallax must remain almost imperceptible");
            Assert.That(foregroundDelta.magnitude, Is.GreaterThan(worldDelta.magnitude),
                "near depth plane should move a little more than the room base");
            Assert.That(foregroundDelta.magnitude, Is.LessThanOrEqualTo(4.25f),
                "near parallax must remain controlled, never wallpaper wobble");
            Assert.That(Vector2.Distance(outsideDelta, foregroundDelta), Is.GreaterThan(.05f),
                "far and near planes must not share one flat motion vector");
            Assert.That(wetDelta.magnitude, Is.LessThanOrEqualTo(4.5f));
            Assert.That(hazeDelta.magnitude, Is.LessThanOrEqualTo(18f));
            Assert.That(Mathf.Abs(FindRawImage("HomeWetGlass").uvRect.y - wetUvStart.y), Is.GreaterThan(.01f),
                "wet glass must visibly slide over time");
            Assert.That(laptop.anchoredPosition, Is.EqualTo(laptopStart),
                "Home interaction/UI roots must remain stationary while visual planes move");

            LogAssert.NoUnexpectedReceived();
        }

'''
text = text.replace(insert_anchor, new_tests + insert_anchor, 1)

helper_anchor = '''        private Graphic FindGraphic(string name)
        {
            RectTransform rect = FindRect(name);
            return rect != null ? rect.GetComponent<Graphic>() : null;
        }
'''
if helper_anchor not in text:
    raise SystemExit("missing graphic helper anchor")
text = text.replace(
    helper_anchor,
    helper_anchor
    + '''\n        private RawImage FindRawImage(string name)\n        {\n            RectTransform rect = FindRect(name);\n            return rect != null ? rect.GetComponent<RawImage>() : null;\n        }\n'''.replace('\\n', '\n'),
    1,
)

path.write_text(text, encoding="utf-8")
print("Applied Home Hub 2.5D focused RED tests")
