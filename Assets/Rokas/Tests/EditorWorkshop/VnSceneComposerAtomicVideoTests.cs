using System.Collections.Generic;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerAtomicVideoTests
    {
        private sealed class Preview : VnSceneComposerVideoPreview
        {
            public bool Ready, Playing; public int Prepares, Resumes, Restarts;
            public Preview() : base(null,16,16,false) { warning=string.Empty; texture=new RenderTexture(32,32,0); texture.Create(); }
            public override bool IsPrepared { get { return Ready; } }
            public override bool IsPreparing { get { return !Ready; } }
            public override bool HasVisibleFrame { get { return Ready; } }
            public override bool IsPlaying { get { return Playing; } }
            public override void Prepare() { Prepares++; }
            public override void Play() { Playing=Ready; }
            public override void Pause() { Playing=false; }
            public override void ResumePresentation() { Resumes++; Playing=Ready; }
            public override void Restart() { Restarts++; }
        }
        private sealed class Factory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly List<Preview> Created=new List<Preview>();
            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference m,int w,int h) { var p=new Preview(); Created.Add(p); return p; }
        }
        [TestCase(false)] [TestCase(true)]
        public void RequestedVideoMustBeReadyBeforeAnyIncomingFrameIsExposed(bool curtain)
        {
            var factory=new Factory(); VnSceneComposerMediaEditing.VideoPreviewFactory=factory;
            try
            {
                var project=new VnSceneComposerProject();
                project.scenes.Add(new VnSceneComposerScene());
                project.scenes[0].characters.Add(new VnSceneComposerCharacter { characterId="Mina", stateId="mina_neutral" });
                project.scenes.Add(new VnSceneComposerScene { media=new VnSceneComposerMediaReference { kind=VnSceneComposerMediaKind.ExternalVideo,reference="requested.mp4",contentHash="requested" } });
                project.scenes[1].transition.sceneTransitionType=curtain?VnSceneComposerSceneTransitionType.DarkCurtain:VnSceneComposerSceneTransitionType.None;
                project.scenes[1].transition.sceneTransitionDuration=1;
                using(var c=new VnSceneComposerPlaybackController(project))
                {
                    c.PlayFromHere(0); c.Advance(3); var old=c.CurrentFrame;
                    c.Next(); c.Advance(.6f);
                    if(curtain) Assert.That(c.CurrentFrame.SceneTransitionOverlay.FullCover,Is.True);
                    else { Assert.That(c.CurrentSceneIndex,Is.EqualTo(0)); Assert.That(c.CurrentFrame.WorkshopFrame,Is.SameAs(old.WorkshopFrame)); Assert.That(c.CurrentFrame.ShowCharacters,Is.True); }
                    var requested=factory.Created[factory.Created.Count-1];
                    int prepares=requested.Prepares; requested.Ready=true; c.Advance(.01f);
                    Assert.That(c.CurrentSceneIndex,Is.EqualTo(1));
                    Assert.That(c.CurrentFrame.TargetBackground,Is.SameAs(requested.texture),"Reveal must bind the requested video frame, never the old texture.");
                    Assert.That(requested.Prepares,Is.EqualTo(prepares));
                }
            }
            finally { VnSceneComposerMediaEditing.ResetVideoPreviewFactory(); foreach(var p in factory.Created) if(p.texture!=null) p.Dispose(); }
        }
        [Test]
        public void MenuResumesPreparedVideoWithoutPrepareOrRestart()
        {
            var factory=new Factory(); VnSceneComposerMediaEditing.VideoPreviewFactory=factory;
            try
            {
                var project=new VnSceneComposerProject(); project.scenes.Add(new VnSceneComposerScene { media=new VnSceneComposerMediaReference { kind=VnSceneComposerMediaKind.ExternalVideo,reference="same.mp4",contentHash="same" } });
                using(var c=new VnSceneComposerPlaybackController(project))
                {
                    var p=factory.Created[0];p.Ready=true;c.PlayScene(0);c.Advance(.3f);
                    int prepares=p.Prepares; int restarts=p.Restarts;
                    c.SetMenuOpen(true); Assert.That(p.Playing,Is.False); c.Advance(5); c.SetMenuOpen(false);
                    Assert.That(p.Playing,Is.True);Assert.That(p.Prepares,Is.EqualTo(prepares));Assert.That(p.Restarts,Is.EqualTo(restarts));
                }
            }
            finally { VnSceneComposerMediaEditing.ResetVideoPreviewFactory(); foreach(var p in factory.Created) if(p.texture!=null) p.Dispose(); }
        }
    }
}
