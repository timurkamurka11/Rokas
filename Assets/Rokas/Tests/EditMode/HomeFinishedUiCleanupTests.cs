using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;

namespace Rokas.Tests
{
    public sealed class HomeFinishedUiCleanupTests
    {
        [Test]
        public void FinishedOverlayRegistersNoSwordsWorkbenchIcon()
        {
            FieldInfo iconsField = typeof(HomeFinalUiPresenter).GetField(
                "Icons", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(iconsField, Is.Not.Null);

            Array icons = iconsField.GetValue(null) as Array;
            Assert.That(icons, Is.Not.Null);
            Assert.That(icons.Length, Is.GreaterThan(0));

            foreach (object icon in icons)
            {
                Type type = icon.GetType();
                string id = type.GetField("id")?.GetValue(icon) as string;
                string resourceName = type.GetField("resourceName")?.GetValue(icon) as string;
                Assert.That(id, Is.Not.EqualTo("Hotspot_Swords"),
                    "The finished Hub overlay must not register a swords/Workbench icon.");
                Assert.That(resourceName, Is.Not.EqualTo("Swords"),
                    "The removed swords icon resource must not remain in the runtime icon registry.");
            }
        }
    }
}
