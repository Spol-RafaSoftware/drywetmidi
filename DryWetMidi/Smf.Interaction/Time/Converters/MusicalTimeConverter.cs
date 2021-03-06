﻿using Melanchall.DryWetMidi.Common;
using System;
using System.Linq;

namespace Melanchall.DryWetMidi.Smf.Interaction
{
    internal sealed class MusicalTimeConverter : ITimeConverter
    {
        #region ITimeConverter

        public ITime ConvertTo(long time, TempoMap tempoMap)
        {
            ThrowIfTimeArgument.IsNegative(nameof(time), time);
            ThrowIfArgument.IsNull(nameof(tempoMap), tempoMap);

            var ticksPerQuarterNoteTimeDivision = tempoMap.TimeDivision as TicksPerQuarterNoteTimeDivision;
            if (ticksPerQuarterNoteTimeDivision != null)
                return ConvertToByTicksPerQuarterNote(time, ticksPerQuarterNoteTimeDivision.TicksPerQuarterNote, tempoMap);

            ThrowIfTimeDivision.IsNotSupportedForTimeConversion(tempoMap.TimeDivision);
            return null;
        }

        public long ConvertFrom(ITime time, TempoMap tempoMap)
        {
            ThrowIfArgument.IsNull(nameof(time), time);
            ThrowIfArgument.IsNull(nameof(tempoMap), tempoMap);

            var musicalTime = time as MusicalTime;
            if (musicalTime == null)
                throw new ArgumentException($"Time is not an instance of the {nameof(MusicalTime)}.", nameof(time));

            var ticksPerQuarterNoteTimeDivision = tempoMap.TimeDivision as TicksPerQuarterNoteTimeDivision;
            if (ticksPerQuarterNoteTimeDivision != null)
                return ConvertFromByTicksPerQuarterNote(musicalTime, ticksPerQuarterNoteTimeDivision.TicksPerQuarterNote, tempoMap);

            ThrowIfTimeDivision.IsNotSupportedForTimeConversion(tempoMap.TimeDivision);
            return 0;
        }

        #endregion

        #region Methods

        private static MusicalTime ConvertToByTicksPerQuarterNote(long time, short ticksPerQuarterNote, TempoMap tempoMap)
        {
            ThrowIfTimeArgument.IsNegative(nameof(time), time);
            ThrowIfArgument.IsNull(nameof(tempoMap), tempoMap);

            //

            var bars = 0;
            var lastTime = 0L;
            var lastTimeSignature = TimeSignature.Default;

            foreach (var timeSignatureChange in tempoMap.TimeSignature.Values.Where(v => v.Time <= time))
            {
                var timeSignatureChangeTime = timeSignatureChange.Time;

                bars += GetBarsCount(timeSignatureChangeTime - lastTime, lastTimeSignature, ticksPerQuarterNote);
                lastTimeSignature = timeSignatureChange.Value;
                lastTime = timeSignatureChangeTime;
            }

            //

            var deltaTime = time - lastTime;
            var lastBars = GetBarsCount(deltaTime, lastTimeSignature, ticksPerQuarterNote);
            bars += lastBars;

            //

            deltaTime = deltaTime % GetBarLength(lastTimeSignature, ticksPerQuarterNote);
            var beatLength = GetBeatLength(lastTimeSignature, ticksPerQuarterNote);
            var beats = deltaTime / beatLength;

            //

            var fraction = FractionUtilities.FromTicks(deltaTime % beatLength, ticksPerQuarterNote);

            //

            return new MusicalTime(bars, (int)beats, fraction);
        }

        private static long ConvertFromByTicksPerQuarterNote(MusicalTime time, short ticksPerQuarterNote, TempoMap tempoMap)
        {
            ThrowIfArgument.IsNull(nameof(time), time);
            ThrowIfArgument.IsNull(nameof(tempoMap), tempoMap);

            //

            var timeBars = time.Bars;
            var accumulatedBars = 0;
            var lastTime = 0L;
            var lastTimeSignature = TimeSignature.Default;

            foreach (var timeSignatureChange in tempoMap.TimeSignature.Values)
            {
                var timeSignatureChangeTime = timeSignatureChange.Time;

                var bars = GetBarsCount(timeSignatureChangeTime - lastTime, lastTimeSignature, ticksPerQuarterNote);
                if (accumulatedBars + bars > timeBars)
                    break;

                accumulatedBars += bars;
                lastTimeSignature = timeSignatureChange.Value;
                lastTime = timeSignatureChangeTime;
            }

            var beatLength = GetBeatLength(lastTimeSignature, ticksPerQuarterNote);
            return lastTime + (timeBars - accumulatedBars) * GetBarLength(lastTimeSignature, ticksPerQuarterNote) +
                   time.Beats * beatLength +
                   time.Fraction.ToTicks(ticksPerQuarterNote);
        }

        private static int GetBarsCount(long time, TimeSignature timeSignature, short ticksPerQuarterNote)
        {
            if (time == 0)
                return 0;

            var barLength = GetBarLength(timeSignature, ticksPerQuarterNote);
            return (int)(time / barLength);
        }

        private static int GetBarLength(TimeSignature timeSignature, short ticksPerQuarterNote)
        {
            var beatLength = GetBeatLength(timeSignature, ticksPerQuarterNote);
            return timeSignature.Numerator * beatLength;
        }

        private static int GetBeatLength(TimeSignature timeSignature, short ticksPerQuarterNote)
        {
            return 4 * ticksPerQuarterNote / timeSignature.Denominator;
        }

        #endregion
    }
}
