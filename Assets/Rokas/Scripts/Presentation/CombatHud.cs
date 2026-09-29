using System;
using Rokas.Core;
using UnityEngine;
using UnityEngine.UI;
namespace Rokas.Presentation
{
    public sealed class CombatHud
    {
        private readonly GameSession session;
        private readonly RectTransform hpBar, sealBar, playerBar, resonanceBar, enemyBeat, comboBeat;
        private readonly Text hp, seal, playerHp, telegraph, combo, charge, resonance, feedback, ritualCaption;
        private readonly CombatTimingRing chargeRing;
        private readonly RectTransform ritual;
        private readonly Image[] ritualPoints = new Image[3];
        private readonly RectTransform[] pointRects = new RectTransform[3];
        private readonly CombatInputSurface input;
        private readonly Button resonanceButton;
        private float feedbackTime;
        public CombatHud(UiKit ui, RectTransform parent, GameSession session, Func<bool> paused)
        {
            this.session = session;
            var target = ui.Box(parent,"EnemyAttack",600,295,720,690,Color.clear,true);
            input = target.gameObject.AddComponent<CombatInputSurface>(); input.targetGraphic = target;
            ui.Box(parent,"EnemyBarBack",656,237,650,8,UiKit.Ink);
            hpBar=ui.Box(parent,"EnemyBar",656,237,650,8,UiKit.Red).rectTransform;
            hp=ui.Label(parent,"EnemyHp","",1360,211,420,45,22);
            ui.Label(parent,"SealLabel","ПЕЧАТЬ",656,256,190,28,15,UiKit.Jade);
            ui.Box(parent,"SealBarBack",656,291,650,6,UiKit.Ink);
            sealBar=ui.Box(parent,"EnemySealBar",656,291,650,6,UiKit.Jade).rectTransform;
            seal=ui.Label(parent,"EnemySeal","",1360,258,420,40,20,UiKit.Jade);
            ui.Label(parent,"CombatControls","ЛКМ  /  УДАР ПО ЁКАЮ\nУдержать ЛКМ  /  ЗАРЯД\nПКМ  /  УКЛОНЕНИЕ\nПРОБЕЛ  /  ОТРАЖЕНИЕ\nR  /  РЕЗОНАНС",67,321,510,200,22);
            chargeRing=ui.Rect(parent,"ChargeTimingRing",245,544,144,144).gameObject.AddComponent<CombatTimingRing>(); chargeRing.raycastTarget=false;
            charge=ui.Label(parent,"ChargeHint","",80,688,490,40,19,UiKit.Gold,false,TextAnchor.MiddleCenter);
            ui.Label(parent,"ChargeRingHint","Отпустите на светлом секторе",67,523,500,28,17,UiKit.Muted,false,TextAnchor.MiddleCenter);
            var vitals=ui.Panel(parent,"HunterVitals",67,762,500,210);
            playerHp=ui.Label(vitals,"HunterHp","",25,15,450,40,23);
            ui.Box(vitals,"HunterBarBack",25,64,447,7,UiKit.Ink);
            playerBar=ui.Box(vitals,"HunterBar",25,64,447,7,UiKit.Jade).rectTransform;
            resonance=ui.Label(vitals,"ResonanceLabel","",25,85,450,37,18,UiKit.Gold);
            ui.Box(vitals,"ResonanceBack",25,132,447,7,UiKit.Ink);
            resonanceBar=ui.Box(vitals,"ResonanceBar",25,132,447,7,UiKit.Gold).rectTransform;
            resonanceButton=ui.Button(vitals,"ActivateResonance","R  /  РЕЗОНАНС ОХОТНИКА",25,154,447,42,()=>{if(!paused())session.ActivateResonance();},true);
            feedback=ui.Label(parent,"CombatFeedback","",1315,345,545,115,33,UiKit.Gold,true,TextAnchor.MiddleCenter);
            combo=ui.Label(parent,"ComboTiming","",1330,488,495,76,22,UiKit.Paper,false,TextAnchor.MiddleCenter);
            ui.Box(parent,"ComboBeatBack",1350,574,455,5,UiKit.Ink);
            comboBeat=ui.Box(parent,"ComboBeat",1350,574,455,5,UiKit.Jade).rectTransform;
            ui.Box(parent,"ComboPerfectWindow",1350+455*(CombatTuning.ComboBeat-CombatTuning.ComboPerfectHalfWindow)/CombatTuning.ComboExpiry,570,455*2*CombatTuning.ComboPerfectHalfWindow/CombatTuning.ComboExpiry,13,new Color(.9f,.8f,.5f,.4f));
            telegraph=ui.Label(parent,"EnemyTelegraph","",1325,643,530,100,27,UiKit.Red,true,TextAnchor.MiddleCenter);
            ui.Box(parent,"EnemyBeatBack",1350,756,455,9,UiKit.Ink);
            enemyBeat=ui.Box(parent,"EnemyBeat",1350,756,455,9,UiKit.Red).rectTransform;
            ui.Label(parent,"CombatStrategy","Ритм комбо → тяжёлый удар\nОтражение → урон печати\nСломайте печать и начертите ритуал",1325,810,530,143,21,UiKit.Muted,false,TextAnchor.MiddleCenter);
            ritual=ui.Rect(parent,"RitualTrace",620,330,700,590);
            ui.Box(ritual,"RitualShade",0,0,700,590,new Color(.02f,.045f,.06f,.85f));
            ritualCaption=ui.Label(ritual,"RitualCaption","",25,10,650,75,26,UiKit.Gold,true,TextAnchor.MiddleCenter);
            Vector2[] centers={new Vector2(155,180),new Vector2(550,275),new Vector2(255,460)};
            for(int i=0;i<2;i++)
            {
                Vector2 delta=centers[i+1]-centers[i]; var line=ui.Box(ritual,"TraceGuide"+i,centers[i].x,centers[i].y,delta.magnitude,3,new Color(.6f,.8f,.7f,.4f)).rectTransform;
                line.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            }
            for(int i=0;i<3;i++)
            {
                ritualPoints[i]=ui.Box(ritual,"RitualPoint"+(i+1),centers[i].x-44,centers[i].y-44,88,88,UiKit.Ink);
                pointRects[i]=ritualPoints[i].rectTransform;
                var ring=ui.Rect(pointRects[i],"PointRing",0,0,88,88).gameObject.AddComponent<CombatTimingRing>();ring.raycastTarget=false;ring.Set(1,0,0,UiKit.Jade);
                ui.Label(pointRects[i],"Number",(i+1).ToString(),0,0,88,88,32,UiKit.Paper,true,TextAnchor.MiddleCenter);
            }
            input.Configure(session,paused,pointRects);
            Refresh();
        }
        public void CancelInput() { input.CancelGesture(); }
        public void Refresh()
        {
            var s=session.State;var c=session.Combat;
            hp.text=Mathf.CeilToInt(s.enemyHp)+" / "+Mathf.CeilToInt(session.Contract.enemyHealth);
            seal.text="ПЕЧАТЬ  "+Mathf.CeilToInt(c.Seal)+" / 100";
            hpBar.sizeDelta=new Vector2(650*Mathf.Clamp01(s.enemyHp/session.Contract.enemyHealth),8);
            sealBar.sizeDelta=new Vector2(650*c.Seal/CombatTuning.MaxSeal,6);
            playerHp.text="ЗДОРОВЬЕ  "+Mathf.CeilToInt(s.playerHp)+" / 100";
            playerBar.sizeDelta=new Vector2(447*s.playerHp/100,7);
            resonance.text=c.ResonanceActive ? "РЕЗОНАНС  /  "+c.ResonanceRemaining.ToString("0.0")+" с" : "РЕЗОНАНС  "+Mathf.FloorToInt(c.Resonance)+" / 100";
            resonanceBar.sizeDelta=new Vector2(447*(c.ResonanceActive ? c.ResonanceRemaining/CombatTuning.ResonanceDuration : c.Resonance/100),7);
            resonanceButton.interactable=c.Resonance>=100&&!c.ResonanceActive&&c.Stage==CombatStage.Fighting;
            bool holding=c.IsHolding&&c.Stage==CombatStage.Fighting;
            float bonus=c.ResonanceActive?CombatTuning.ResonanceWindowBonus:0;
            chargeRing.Set(holding?c.ChargeSeconds/CombatTuning.ChargeEnd:0,(CombatTuning.PerfectCutStart-bonus)/CombatTuning.ChargeEnd,(CombatTuning.PerfectCutEnd+bonus)/CombatTuning.ChargeEnd,c.ResonanceActive?UiKit.Jade:UiKit.Gold);
            charge.text=!holding?"Удерживайте ЛКМ на ёкае":c.ChargeSeconds>CombatTuning.ChargeEnd?"ПЕРЕДЕРЖАНО  /  СЛАБЫЙ УДАР":c.ChargeSeconds>=CombatTuning.PerfectCutStart-bonus&&c.ChargeSeconds<=CombatTuning.PerfectCutEnd+bonus?"PERFECT CUT — ОТПУСТИТЕ!":c.ChargeSeconds>=CombatTuning.ChargeStart?"ЗАРЯЖЕН — ОТПУСТИТЕ":"ЗАРЯД…";
            combo.text=c.ComboStep==0||c.ComboAge>CombatTuning.ComboExpiry?"КОМБО  1 → 2 → 3\nЛовите светлое окно":(c.LastPerfect?"PERFECT  /  ":"КОМБО  /  ")+c.ComboStep+" из 3";
            comboBeat.sizeDelta=new Vector2(455*(c.ComboStep>0?Mathf.Clamp01(c.ComboAge/CombatTuning.ComboExpiry):0),5);
            telegraph.text=c.Stage!=CombatStage.Fighting?"ДАВЛЕНИЕ ПРЕРВАНО":c.EnemyTelegraph?(c.DelayedAttack?"ЗАДЕРЖКА… ":"УДАР ЧЕРЕЗ ")+s.enemyTimer.ToString("0.00")+" с\nПКМ  /  ПРОБЕЛ":"ФАЗА "+c.EnemyPhase+"  /  НАБЛЮДАЙТЕ";
            telegraph.color=c.EnemyTelegraph?UiKit.Red:UiKit.Muted;
            enemyBeat.sizeDelta=new Vector2(c.EnemyTelegraph?455*(1-Mathf.Clamp01(s.enemyTimer/CombatTuning.TelegraphDuration)):0,9);
            ritual.gameObject.SetActive(c.Stage!=CombatStage.Fighting);
            ritualCaption.text=c.Stage==CombatStage.SealBreak?"ПЕЧАТЬ СЛОМАНА":"ДЕРЖИТЕ ЛКМ: 1 → 2 → 3  /  "+c.RitualRemaining.ToString("0.0")+" с";
            for(int i=0;i<3;i++)ritualPoints[i].color=i<c.RitualPoint?UiKit.Jade:i==c.RitualPoint?new Color(.3f,.25f,.14f):UiKit.Ink;
        }
        public void Tick(float dt) { feedbackTime=Mathf.Max(0,feedbackTime-dt);if(feedbackTime<=0)feedback.text="";Refresh(); }
        public void ShowFeedback(CombatAction action)
        {
            feedbackTime=1.1f;
            switch(action)
            {
                case CombatAction.PerfectCombo: feedback.text="PERFECT";break;
                case CombatAction.Finisher: feedback.text="КОМБО  /  РАССЕЧЕНИЕ";break;
                case CombatAction.PerfectCut: feedback.text="PERFECT CUT";break;
                case CombatAction.Charged: feedback.text="ЗАРЯЖЕННЫЙ УДАР";break;
                case CombatAction.WeakCut: feedback.text="СЛАБЫЙ УДАР";break;
                case CombatAction.Dodge: feedback.text="УКЛОНЕНИЕ";break;
                case CombatAction.Deflect: feedback.text="ОТРАЖЕНО!\nПЕЧАТЬ ТРЕСНУЛА";break;
                case CombatAction.MissedDefense: feedback.text="СЛИШКОМ РАНО";break;
                case CombatAction.SealBreak: feedback.text="SEAL BREAK";break;
                case CombatAction.RitualSuccess: feedback.text="РИТУАЛ ЗАВЕРШЁН";break;
                case CombatAction.RitualFailed: feedback.text="РИТУАЛ СОРВАН\nГОТОВЬТЕСЬ К УДАРУ";break;
                case CombatAction.Resonance: feedback.text="РЕЗОНАНС ОХОТНИКА";break;
                case CombatAction.Damaged: feedback.text="ПРОПУЩЕН УДАР";break;
                case CombatAction.Basic: feedback.text="УДАР "+session.Combat.ComboStep;break;
            }
            feedback.color=action==CombatAction.Damaged||action==CombatAction.RitualFailed||action==CombatAction.MissedDefense?UiKit.Red:UiKit.Gold;
        }
    }
}
