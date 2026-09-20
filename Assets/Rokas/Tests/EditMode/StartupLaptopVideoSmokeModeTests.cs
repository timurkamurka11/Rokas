using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;

namespace Rokas.Core.Tests
{
    public sealed class StartupLaptopVideoSmokeModeTests
    {
        [Test]
        public void SmokeModeIsStrictlyOptInByExactCommandLineArgument()
        {
            Type type = typeof(RokasBootstrap).Assembly.GetType("Rokas.Presentation.RokasVideoSmokeRunner");
            Assert.That(type, Is.Not.Null, "Production presentation assembly must expose the CI smoke runner type.");

            MethodInfo method = type.GetMethod("IsRequested", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Smoke runner must expose a pure command-line gate for deterministic verification.");

            Assert.That((bool)method.Invoke(null, new object[] { Array.Empty<string>() }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[] { new[] { "Rokas.exe", "-other" } }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[] { new[] { "Rokas.exe", "-rokasVideoSmoke" } }), Is.True);
            Assert.That((bool)method.Invoke(null, new object[] { new[] { "Rokas.exe", "-ROKASVIDEOSMOKE" } }), Is.True);
        }
    }
}
