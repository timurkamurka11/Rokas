using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Presentation
{
    public readonly struct VnCharacterVisualState
    {
        public string Id { get; }
        public string Character { get; }
        public Rect PortraitUv { get; }
        public Rect BodyUv { get; }
        public bool HasBlinkState { get; }
        public Rect BlinkUv { get; }

        public VnCharacterVisualState(string id, string character, Rect portraitUv, Rect bodyUv,
            bool hasBlinkState = false, Rect blinkUv = default)
        {
            Id = id ?? string.Empty;
            Character = character ?? string.Empty;
            PortraitUv = portraitUv;
            BodyUv = bodyUv;
            HasBlinkState = hasBlinkState;
            BlinkUv = blinkUv;
        }
    }

    public static class VnCharacterVisualCatalog
    {
        // These UVs are measured from the exact 1086x1448 authored character sheets inspected for
        // this feature. Closed-eye Mina is an authored happy expression, not a neutral blink frame.
        private static readonly Rect KeikoBodyUv = new Rect(.008287f, .011740f, .429098f, .986188f);
        private static readonly Rect MinaBodyUv = new Rect(.000921f, .008287f, .361878f, .986878f);

        private static readonly Dictionary<string, VnCharacterVisualState> States =
            new Dictionary<string, VnCharacterVisualState>(StringComparer.Ordinal)
            {
                { "keiko_neutral", State("keiko_neutral", "Keiko", .069061f, .792818f, .276243f, .207182f, KeikoBodyUv) },
                { "keiko_surprised", State("keiko_surprised", "Keiko", .432781f, .616713f, .285451f, .214088f, KeikoBodyUv) },
                { "keiko_serious", State("keiko_serious", "Keiko", .709024f, .616713f, .285451f, .214088f, KeikoBodyUv) },
                { "keiko_thoughtful", State("keiko_thoughtful", "Keiko", .414365f, .261050f, .294659f, .220994f, KeikoBodyUv) },
                { "keiko_uneasy", State("keiko_uneasy", "Keiko", .704420f, .254144f, .294659f, .220994f, KeikoBodyUv) },
                { "mina_neutral", State("mina_neutral", "Mina", .372928f, .730663f, .322284f, .241713f, MinaBodyUv) },
                { "mina_happy", State("mina_happy", "Mina", .676796f, .730663f, .322284f, .241713f, MinaBodyUv) },
                { "mina_serious", State("mina_serious", "Mina", .372928f, .437155f, .322284f, .241713f, MinaBodyUv) },
                { "mina_surprised", State("mina_surprised", "Mina", .676796f, .437155f, .322284f, .241713f, MinaBodyUv) },
                { "mina_reaching", State("mina_reaching", "Mina", .547882f, .088398f, .386740f, .290055f, MinaBodyUv) }
            };

        public static bool TryResolve(string id, out VnCharacterVisualState state)
        {
            return States.TryGetValue(id ?? string.Empty, out state);
        }

        public static VnCharacterVisualState ResolveOrNeutral(string id, string speaker)
        {
            if (TryResolve(id, out VnCharacterVisualState resolved))
            {
                return resolved;
            }

            string neutralId;
            if (IsCharacter(id, speaker, "Keiko", "keiko_"))
            {
                neutralId = "keiko_neutral";
            }
            else if (IsCharacter(id, speaker, "Mina", "mina_"))
            {
                neutralId = "mina_neutral";
            }
            else
            {
                throw new ArgumentException("Unknown VN character visual id: " + (id ?? string.Empty), nameof(id));
            }

            return States[neutralId];
        }

        private static bool IsCharacter(string id, string speaker, string character, string prefix)
        {
            return string.Equals(speaker, character, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(id) && id.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static VnCharacterVisualState State(string id, string character, float x, float y,
            float width, float height, Rect bodyUv)
        {
            return new VnCharacterVisualState(id, character, new Rect(x, y, width, height), bodyUv);
        }
    }
}
