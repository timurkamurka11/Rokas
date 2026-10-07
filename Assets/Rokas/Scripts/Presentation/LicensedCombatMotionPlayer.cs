using System;using System.Collections.Generic;using UnityEngine;
namespace Rokas.Presentation {
 // Samples licensed pose data. Core alone authorizes contact; this class never deals damage.
 public sealed class LicensedCombatMotionPlayer {
  private readonly Animation animation;private readonly AnimationClip fallbackIdle;
  private readonly List<string> aliases=new List<string>();private readonly HashSet<string> consumedActions=new HashSet<string>(StringComparer.Ordinal);
  private float[] blendIn,blendOut;
  private string actionId,anticipationAlias;private float clock;private bool contacted;
  public LicensedCombatMotionProfile Profile {get;private set;}
  public bool Active=>Profile!=null;
  public bool Contacted=>contacted&&Active;
  public float Clock=>clock;
  public LicensedCombatMotionPlayer(Animation animation,AnimationClip fallbackIdle){this.animation=animation??throw new ArgumentNullException(nameof(animation));this.fallbackIdle=fallbackIdle;}
  public void Begin(LicensedCombatMotionProfile profile,string id,string alias){
   Cancel();if(profile==null)return;Profile=profile;actionId=id;clock=0;contacted=false;anticipationAlias=alias;
   blendIn=new float[profile.segments.Length];blendOut=new float[profile.segments.Length];for(int i=0;i<profile.segments.Length;i++){var s=profile.segments[i];blendIn[i]=s.blendIn>0?s.blendIn:s.easeIn;blendOut[i]=s.blendOut>0?s.blendOut:s.easeOut;if(i>0&&s.blendIn<0){var prev=profile.segments[i-1];float overlap=prev.start+prev.duration-s.start;if(overlap>0)blendIn[i]=Mathf.Min(s.duration,overlap);}if(i+1<profile.segments.Length&&s.blendOut<0){float overlap=s.start+s.duration-profile.segments[i+1].start;if(overlap>0)blendOut[i]=Mathf.Min(s.duration,overlap);}}
   Register(profile.anticipation,alias);Register(profile.baseIdle??fallbackIdle,"Licensed_BaseIdle");for(int i=0;i<profile.segments.Length;i++)Register(profile.segments[i].clip,"Licensed_Segment_"+i);
   Sample();
  }
  private void Register(AnimationClip clip,string alias){if(clip==null)return;if(!clip.legacy)throw new InvalidOperationException("Licensed combat clip must be Legacy: "+clip.name);animation.AddClip(clip,alias);animation[alias].wrapMode=WrapMode.ClampForever;aliases.Add(alias);}
  public bool ConfirmContact(string id){if(!Active||contacted||string.IsNullOrEmpty(id)||!string.Equals(id,actionId,StringComparison.Ordinal)||!consumedActions.Add(id))return false;contacted=true;clock=0;Sample();return true;}
  public void Tick(float delta){if(!Active)return;clock+=Mathf.Max(0,delta);if(contacted&&clock>=Profile.Duration){Cancel();return;}Sample();}
  public void Sample(){if(!Active)return;foreach(AnimationState state in animation){state.enabled=false;state.weight=0;}animation.enabled=false;
   if(!contacted){Enable(anticipationAlias,Mathf.Min(clock*Profile.anticipationSpeed,Profile.anticipation==null?0:Profile.anticipation.length),1);}
   else{float sum=0;for(int i=0;i<Profile.segments.Length;i++)sum+=Profile.segments[i].Weight(clock,blendIn[i],blendOut[i]);float norm=Mathf.Max(1,sum);for(int i=0;i<Profile.segments.Length;i++){var s=Profile.segments[i];Enable("Licensed_Segment_"+i,s.SampleTime(clock),s.Weight(clock,blendIn[i],blendOut[i])/norm);}var idle=Profile.baseIdle??fallbackIdle;Enable("Licensed_BaseIdle",idle==null?0:Mathf.Repeat(clock*Profile.idleSpeed,Mathf.Max(.001f,idle.length)),Mathf.Max(0,1-sum));}
   animation.Sample();
  }
  private void Enable(string alias,float time,float weight){if(weight<=0||string.IsNullOrEmpty(alias))return;var state=animation[alias];if(state==null)return;state.enabled=true;state.speed=0;state.weight=weight;state.time=time;}
  public void Cancel(){foreach(var alias in aliases){var state=animation[alias];if(state!=null){state.enabled=false;state.weight=0;}}aliases.Clear();Profile=null;actionId=null;clock=0;contacted=false;}
 }
}
