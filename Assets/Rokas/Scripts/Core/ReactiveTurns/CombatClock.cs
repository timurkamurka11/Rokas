using System;
using System.Collections.Generic;

namespace Rokas.Core.ReactiveTurns
{
    // Initiative ticks are deliberately absent here: this maps only monotonic device time to authored combat time.
    public sealed class CombatClock
    {
        private sealed class Segment
        {
            public long DeviceStartUs;
            public long CombatStartUs;
            public double Speed;
            public bool Paused;
            public long Epoch;
        }

        private readonly List<Segment> _segments = new List<Segment>();
        public long CurrentEpoch { get { return _segments[_segments.Count - 1].Epoch; } }
        public bool IsPaused { get { return _segments[_segments.Count - 1].Paused; } }

        public CombatClock(long deviceStartUs, long combatStartUs = 0, double speed = 1.0)
        {
            CheckSpeed(speed);
            _segments.Add(new Segment { DeviceStartUs = deviceStartUs, CombatStartUs = combatStartUs,
                Speed = speed, Epoch = 1 });
        }

        public long? MapInput(long deviceUs, long epoch)
        {
            Segment segment = FindSegment(deviceUs);
            if (segment == null || segment.Paused || segment.Epoch != epoch) return null;
            return Map(segment, deviceUs);
        }

        public long CombatTimeAt(long deviceUs)
        {
            Segment segment = FindSegment(deviceUs);
            if (segment == null) throw new ArgumentOutOfRangeException("deviceUs");
            return Map(segment, deviceUs);
        }

        public void Pause(long deviceUs)
        {
            Segment current = LastAt(deviceUs);
            if (current.Paused) return;
            _segments.Add(new Segment { DeviceStartUs = deviceUs, CombatStartUs = Map(current, deviceUs),
                Speed = current.Speed, Paused = true, Epoch = checked(current.Epoch + 1) });
        }

        public void Resume(long deviceUs)
        {
            Segment current = LastAt(deviceUs);
            if (!current.Paused) throw new InvalidOperationException("Clock is not paused.");
            _segments.Add(new Segment { DeviceStartUs = deviceUs, CombatStartUs = current.CombatStartUs,
                Speed = current.Speed, Epoch = checked(current.Epoch + 1) });
        }

        public void SetSpeed(long deviceUs, double speed)
        {
            CheckSpeed(speed);
            Segment current = LastAt(deviceUs);
            if (current.Paused) throw new InvalidOperationException("Change speed at an active stable boundary.");
            _segments.Add(new Segment { DeviceStartUs = deviceUs, CombatStartUs = Map(current, deviceUs),
                Speed = speed, Epoch = current.Epoch });
        }

        private Segment LastAt(long deviceUs)
        {
            Segment current = _segments[_segments.Count - 1];
            if (deviceUs < current.DeviceStartUs) throw new ArgumentOutOfRangeException("deviceUs", "Device time must be monotonic.");
            return current;
        }

        private Segment FindSegment(long deviceUs)
        {
            for (int i = _segments.Count - 1; i >= 0; i--)
                if (deviceUs >= _segments[i].DeviceStartUs) return _segments[i];
            return null;
        }

        private static long Map(Segment segment, long deviceUs)
        {
            if (segment.Paused) return segment.CombatStartUs;
            double elapsed = (double)(deviceUs - segment.DeviceStartUs) * segment.Speed;
            if (elapsed > long.MaxValue - segment.CombatStartUs) throw new OverflowException("Combat clock exceeded long range.");
            return checked(segment.CombatStartUs + (long)Math.Round(elapsed, MidpointRounding.AwayFromZero));
        }

        private static void CheckSpeed(double speed)
        {
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed <= 0)
                throw new ArgumentOutOfRangeException("speed");
        }
    }
}
