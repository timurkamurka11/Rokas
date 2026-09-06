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
        public Font sans;
        public Font serif;
        public TextAsset contract;
        public AudioClip homeAmbience;
        public AudioClip subwayAmbience;
        public AudioClip homeMusic;
        public AudioClip missionMusic;
        public AudioClip click;
        public AudioClip hit;
        public AudioClip critical;
        public AudioClip portalSound;
        public AudioClip seal;
        public AudioClip mame;

        public bool IsComplete()
        {
            return home && laptopWallpaper && portal && subway && enemy && familiar && sans && serif && contract
                && homeAmbience && subwayAmbience && homeMusic && missionMusic
                && click && hit && critical && portalSound && seal && mame;
        }
    }
}
