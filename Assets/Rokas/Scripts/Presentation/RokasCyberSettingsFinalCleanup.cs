using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// Final visual cleanup of the existing V2 Cyber Settings overlay.
    /// No buttons, listeners, settings or save data are modified.
    /// </summary>
    public static class RokasCyberSettingsFinalCleanup
    {
        public static void Apply(RectTransform panels)
        {
            if (panels == null) return;
            Transform panel = panels.Find("SettingsPanel");
            if (panel == null) return;

            Transform title = panel.Find("SettingsTitle");
            if (title != null)
            {
                Text label = title.GetComponent<Text>();
                if (label != null) label.text = "Меню";
            }

            // The V2 PNG contains its own full beveled neon outline.
            // Drawing RokasCyberPolygon on top added visible double-corner wedges.
            // Only hide the duplicate frame if the actual artwork is present.
            if (panel.Find("CyberBackdropTexture") != null)
            {
                Transform duplicateFrame = panel.Find("CyberGlassSurface");
                if (duplicateFrame != null)
                {
                    Graphic extraOutline = duplicateFrame.GetComponent<Graphic>();
                    if (extraOutline != null) extraOutline.enabled = false;
                }
            }
        }
    }
}
