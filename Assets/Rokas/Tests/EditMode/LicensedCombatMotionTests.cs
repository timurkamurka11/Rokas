using System;using System.Reflection;using NUnit.Framework;using Rokas.Presentation;using UnityEngine;
namespace Rokas.Tests {
 public sealed class LicensedCombatMotionTests {
  // Catches contact fired by clip duration, wrong action IDs, or duplicate domain events.
  [Test] public void ContactWaitsForMatchingCoreActionAndCannotRestartRecovery(){
   var asm=typeof(ReactiveCombatActorVisual).Assembly;var pt=asm.GetType("Rokas.Presentation.LicensedCombatMotionProfile");var mt=asm.GetType("Rokas.Presentation.LicensedCombatMotionPlayer");
   Assert.That(pt,Is.Not.Null,"Licensed motion profile is missing");Assert.That(mt,Is.Not.Null,"Licensed presentation player is missing");
   var root=new GameObject("MotionFixture");var child=new GameObject("Animated");child.transform.SetParent(root.transform,false);var anim=root.AddComponent<Animation>();var profile=ScriptableObject.CreateInstance(pt);var anticipation=Clip(0,1);var action=Clip(10,10);var recovery=Clip(20,22);object player=null;
   try {Set(profile,"anticipation",anticipation);var st=asm.GetType("Rokas.Presentation.LicensedMotionSegment");var segments=Array.CreateInstance(st,2);object a=Activator.CreateInstance(st),b=Activator.CreateInstance(st);Set(a,"clip",action);Set(a,"start",0f);Set(a,"duration",1f);Set(b,"clip",recovery);Set(b,"start",1f);Set(b,"duration",1f);segments.SetValue(a,0);segments.SetValue(b,1);Set(profile,"segments",segments);
    player=Activator.CreateInstance(mt,new object[]{anim,null});Call(player,"Begin",profile,"action-1","Attack");Call(player,"Tick",.4f);Assert.That(child.transform.localPosition.x,Is.EqualTo(.4f).Within(.002f));
    Assert.That(Call(player,"ConfirmContact","other-action"),Is.False);Call(player,"Tick",.8f);Assert.That(child.transform.localPosition.x,Is.EqualTo(1f).Within(.002f),"Anticipation must wait at its end for Core, never auto-contact");
    Assert.That(Call(player,"ConfirmContact","action-1"),Is.True);Assert.That(child.transform.localPosition.x,Is.EqualTo(10f).Within(.002f));Call(player,"Tick",1.5f);Assert.That(child.transform.localPosition.x,Is.EqualTo(21f).Within(.002f));
    Assert.That(Call(player,"ConfirmContact","action-1"),Is.False);Call(player,"Tick",.1f);Assert.That(child.transform.localPosition.x,Is.EqualTo(21.2f).Within(.002f),"Duplicate contact must not rewind recovery");
   } finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(profile);foreach(var c in new[]{anticipation,action,recovery})UnityEngine.Object.DestroyImmediate(c);}
  }
  // Catches the equal-weight plateau when a source Timeline overlaps its two phases.
  [Test] public void OverlapUsesAuthoredMixCurvesInsteadOfEqualWeightPlateau(){
   var asm=typeof(ReactiveCombatActorVisual).Assembly;var pt=asm.GetType("Rokas.Presentation.LicensedCombatMotionProfile");var mt=asm.GetType("Rokas.Presentation.LicensedCombatMotionPlayer");var st=asm.GetType("Rokas.Presentation.LicensedMotionSegment");var root=new GameObject("OverlapFixture");var child=new GameObject("Animated");child.transform.SetParent(root.transform,false);var animation=root.AddComponent<Animation>();var p=ScriptableObject.CreateInstance(pt);var antic=Clip(0,0);var action=Clip(10,10);var recover=Clip(20,20);
   try{Set(p,"anticipation",antic);var segments=Array.CreateInstance(st,2);var a=Activator.CreateInstance(st);var b=Activator.CreateInstance(st);Set(a,"clip",action);Set(a,"duration",1.05f);Set(b,"clip",recover);Set(b,"start",1f);Set(b,"duration",1f);segments.SetValue(a,0);segments.SetValue(b,1);Set(p,"segments",segments);var player=Activator.CreateInstance(mt,new object[]{animation,null});Call(player,"Begin",p,"overlap-action","Attack");Call(player,"ConfirmContact","overlap-action");Call(player,"Tick",1.0125f);Assert.That(child.transform.localPosition.x,Is.EqualTo(12.5f).Within(.005f),"Quarter way through a 50ms overlap must use outgoing75%/incoming25%");}
   finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(p);foreach(var c in new[]{antic,action,recover})UnityEngine.Object.DestroyImmediate(c);}
  }
  static void Set(object o,string key,object v)=>o.GetType().GetField(key).SetValue(o,v);
  static object Call(object o,string key,params object[] values)=>o.GetType().GetMethod(key).Invoke(o,values);
  static AnimationClip Clip(float a,float b){var c=new AnimationClip{legacy=true};c.SetCurve("Animated",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,a,1,b));return c;}
 }
}