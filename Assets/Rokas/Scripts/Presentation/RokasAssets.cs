using UnityEngine;

namespace Rokas.Presentation
{
    [CreateAssetMenu(menuName = "Rokas/Presentation Assets")]
    public sealed class RokasAssets : ScriptableObject
    {
        public Texture2D home;
        public Texture2D laptopWallpaper;
        public Texture2D portal;
        public Texture2D subway;
        public Texture2D enemy;
        public Texture2D familiar;
        public Texture2D vnKeikoCharacterSheet;
        public Texture2D vnMinaCharacterSheet;
        public Texture2D vnBusStopRainNight;
        public Texture2D vnNightSkyRain;
        public Texture2D vnBusStopPhoneMessageMina;
        public Texture2D vnDialoguePanelKeikoDark;
        public Texture2D vnDialoguePanelMinaLight;
        public Texture2D vnIconMute;
        public Texture2D vnIconPause;
        public Texture2D vnIconSkip;
        public Font sans;
        public Font serif;
        public TextAsset contract;
        public AudioClip homeAmbience;
        public AudioClip subwayAmbience;
        public AudioClip homeMusic;
        public AudioClip missionMusic;
        public AudioClip click;
        public AudioClip laptopMouseClick;
        public AudioClip enterGame;
        public AudioClip lampOn;
        public AudioClip lampOff;
        public AudioClip hit;
        public AudioClip critical;
        public AudioClip portalSound;
        public AudioClip seal;
        public AudioClip mame;

        public bool IsComplete()
        {
            return home && laptopWallpaper && portal && subway && enemy && familiar
                && vnKeikoCharacterSheet && vnMinaCharacterSheet
                && vnBusStopRainNight && vnNightSkyRain && vnBusStopPhoneMessageMina
                && vnDialoguePanelKeikoDark && vnDialoguePanelMinaLight
                && vnIconMute && vnIconPause && vnIconSkip
                && sans && serif && contract
                && homeAmbience && subwayAmbience && homeMusic && missionMusic
                && click && laptopMouseClick && enterGame && lampOn && lampOff
                && hit && critical && portalSound && seal && mame;
        }
    }
}