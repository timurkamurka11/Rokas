using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Rokas.Presentation
{
    public enum ReactivePressKind { Dodge, Parry }

    public readonly struct ReactiveDevicePress
    {
        public readonly long InputId;
        public readonly long Epoch;
        public readonly long DeviceTimeUs;
        public readonly ReactivePressKind Kind;

        public ReactiveDevicePress(long inputId, long epoch, long deviceTimeUs, ReactivePressKind kind)
        {
            InputId = inputId;
            Epoch = epoch;
            DeviceTimeUs = deviceTimeUs;
            Kind = kind;
        }
    }

    public readonly struct ReactiveOffensePress
    {
        public readonly long InputId;
        public readonly long Epoch;
        public readonly long DeviceTimeUs;

        public ReactiveOffensePress(long inputId, long epoch, long deviceTimeUs)
        {
            InputId = inputId;
            Epoch = epoch;
            DeviceTimeUs = deviceTimeUs;
        }
    }

    // InputAction callback time comes from the device event. The queue is flushed before Core time advances.
    public sealed class ReactiveInputAdapter : IDisposable
    {
        private readonly InputAction dodge;
        private readonly InputAction parry;
        private readonly InputAction offense;
        private readonly InputAction uiActivation;
        private readonly List<ReactiveDevicePress> pending = new List<ReactiveDevicePress>();
        private readonly List<ReactiveOffensePress> pendingOffense = new List<ReactiveOffensePress>();
        private bool active;
        private bool armed;
        private bool defenseEnabled;
        private bool offenseEnabled;
        private long? uiActivationTimeUs;
        private long epoch = 1;
        private long nextInputId = 1;

        public long Epoch { get { return epoch; } }
        public long DeviceNowUs { get { return ToMicroseconds(InputState.currentTime); } }

        public ReactiveInputAdapter()
        {
            dodge = new InputAction("ReactiveDodge", InputActionType.Button);
            dodge.AddBinding("<Mouse>/rightButton");
            dodge.AddBinding("<Keyboard>/q");
            parry = new InputAction("ReactiveParry", InputActionType.Button);
            parry.AddBinding("<Keyboard>/e");
            offense = new InputAction("ReactiveOffenseTiming", InputActionType.Button);
            offense.AddBinding("<Keyboard>/space");
            offense.AddBinding("<Mouse>/leftButton");
            uiActivation = new InputAction("ReactiveUiActivation", InputActionType.Button);
            uiActivation.AddBinding("<Mouse>/leftButton");
            uiActivation.AddBinding("<Keyboard>/enter");
            uiActivation.AddBinding("<Keyboard>/numpadEnter");
            dodge.performed += OnDodge;
            parry.performed += OnParry;
            offense.performed += OnOffense;
            uiActivation.performed += OnUiActivation;
        }

        public void SetContext(bool enabled)
        {
            if (active == enabled) return;
            active = enabled;
            epoch++;
            pending.Clear();
            pendingOffense.Clear();
            uiActivationTimeUs = null;
            defenseEnabled = enabled;
            offenseEnabled = false;
            armed = enabled && AreControlsReleased();
            if (enabled)
            {
                dodge.Enable();
                parry.Enable();
                offense.Enable();
                uiActivation.Enable();
            }
            else
            {
                dodge.Disable();
                parry.Disable();
                offense.Disable();
                uiActivation.Disable();
            }
        }

        public void SetDefenseEnabled(bool enabled)
        {
            enabled &= active;
            if (defenseEnabled == enabled) return;
            defenseEnabled = enabled;
            epoch++;
            pending.Clear();
            armed = (defenseEnabled || offenseEnabled) && AreControlsReleased();
        }

        public void SetOffenseEnabled(bool enabled)
        {
            enabled &= active;
            if (offenseEnabled == enabled) return;
            offenseEnabled = enabled;
            epoch++;
            pendingOffense.Clear();
            armed = (defenseEnabled || offenseEnabled) && AreControlsReleased();
        }

        public void ForceRearm()
        {
            epoch++;
            pending.Clear();
            pendingOffense.Clear();
            uiActivationTimeUs = null;
            armed = false;
        }

        public bool TryConsumeUiActivation(out long deviceTimeUs)
        {
            deviceTimeUs = 0;
            if (!active || !uiActivationTimeUs.HasValue) return false;
            long captured = uiActivationTimeUs.Value;
            uiActivationTimeUs = null;
            long age = DeviceNowUs - captured;
            if (age < 0 || age > 100000) return false;
            deviceTimeUs = captured;
            return true;
        }

        public void Tick()
        {
            if (active && !armed && AreControlsReleased())
            {
                armed = true;
                epoch++;
            }
        }

        public bool IsReleased(ReactivePressKind kind)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kind == ReactivePressKind.Dodge)
                return (keyboard == null || !keyboard.qKey.isPressed) &&
                       (mouse == null || !mouse.rightButton.isPressed);
            return keyboard == null || !keyboard.eKey.isPressed;
        }

        public void Flush(Action<ReactiveDevicePress> accept)
        {
            if (accept == null) throw new ArgumentNullException(nameof(accept));
            pending.Sort((left, right) =>
            {
                int order = left.DeviceTimeUs.CompareTo(right.DeviceTimeUs);
                if (order != 0) return order;
                order = left.Kind.CompareTo(right.Kind); // Dodge wins exact simultaneous presses.
                return order != 0 ? order : left.InputId.CompareTo(right.InputId);
            });
            for (int index = 0; index < pending.Count; index++) accept(pending[index]);
            pending.Clear();
        }

        public void FlushOffense(Action<ReactiveOffensePress> accept)
        {
            if (accept == null) throw new ArgumentNullException(nameof(accept));
            pendingOffense.Sort((left, right) => left.DeviceTimeUs.CompareTo(right.DeviceTimeUs));
            for (int index = 0; index < pendingOffense.Count; index++) accept(pendingOffense[index]);
            pendingOffense.Clear();
        }

        private void OnDodge(InputAction.CallbackContext context)
        {
            Capture(context, ReactivePressKind.Dodge);
        }

        private void OnParry(InputAction.CallbackContext context)
        {
            Capture(context, ReactivePressKind.Parry);
        }

        private void OnOffense(InputAction.CallbackContext context)
        {
            if (!active || !offenseEnabled || !armed) return;
            pendingOffense.Add(new ReactiveOffensePress(nextInputId++, epoch,
                ToMicroseconds(context.time)));
        }

        private void OnUiActivation(InputAction.CallbackContext context)
        {
            if (active) uiActivationTimeUs = ToMicroseconds(context.time);
        }

        private void Capture(InputAction.CallbackContext context, ReactivePressKind kind)
        {
            if (!active || !defenseEnabled || !armed) return;
            pending.Add(new ReactiveDevicePress(nextInputId++, epoch, ToMicroseconds(context.time), kind));
        }

        private bool AreControlsReleased()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return (keyboard == null || (!keyboard.spaceKey.isPressed && !keyboard.eKey.isPressed && !keyboard.qKey.isPressed)) &&
                   (mouse == null || (!mouse.rightButton.isPressed &&
                       (!offenseEnabled || !mouse.leftButton.isPressed)));
        }

        private static long ToMicroseconds(double seconds)
        {
            return checked((long)Math.Round(seconds * 1000000d, MidpointRounding.AwayFromZero));
        }

        public void Dispose()
        {
            SetContext(false);
            dodge.performed -= OnDodge;
            parry.performed -= OnParry;
            offense.performed -= OnOffense;
            uiActivation.performed -= OnUiActivation;
            dodge.Dispose();
            parry.Dispose();
            offense.Dispose();
            uiActivation.Dispose();
        }
    }
}
