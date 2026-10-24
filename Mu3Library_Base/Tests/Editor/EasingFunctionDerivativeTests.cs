using System;
using System.Collections.Generic;
using Mu3Library.Utility;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    public class EasingFunctionDerivativeTests
    {
        private const float Step = 1e-3f;
        private const float RelativeTolerance = 1e-2f;

        // Samples stay away from the ends, from t = 0.5 where EaseInOutCirc has an infinite slope,
        // and from the points where the Bounce derivatives jump (multiples of 1 / 2.75 and their mirrors).
        private static readonly float[] SampleTimes = { 0.05f, 0.15f, 0.25f, 0.35f, 0.45f, 0.55f, 0.65f, 0.75f, 0.85f, 0.95f };

        private static IEnumerable<EasingFunction.Ease> AllEases()
            => (EasingFunction.Ease[])Enum.GetValues(typeof(EasingFunction.Ease));

        [TestCaseSource(nameof(AllEases))]
        public void Derivative_MatchesCentralDifference(EasingFunction.Ease ease)
        {
            EasingFunction.Function function = EasingFunction.GetEasingFunction(ease);
            EasingFunction.Function derivative = EasingFunction.GetEasingFunctionDerivative(ease);
            Assert.That(function, Is.Not.Null, $"{ease} has no easing function.");
            Assert.That(derivative, Is.Not.Null, $"{ease} has no derivative function.");

            AssertMatchesCentralDifference(ease, function, derivative, 0f, 1f);
            AssertMatchesCentralDifference(ease, function, derivative, -2f, 3f);
        }

        [TestCaseSource(nameof(AllEases))]
        public void Derivative01_MatchesCentralDifference(EasingFunction.Ease ease)
        {
            EasingFunction.Function01 function = EasingFunction.GetEasingFunction01(ease);
            EasingFunction.Function01 derivative = EasingFunction.GetEasingFunction01Derivative(ease);
            Assert.That(function, Is.Not.Null, $"{ease} has no easing function.");
            Assert.That(derivative, Is.Not.Null, $"{ease} has no derivative function.");

            AssertMatchesCentralDifference(ease, (s, e, v) => function(v), (s, e, v) => derivative(v), 0f, 1f);
        }

        private static void AssertMatchesCentralDifference(
            EasingFunction.Ease ease,
            EasingFunction.Function function,
            EasingFunction.Function derivative,
            float start,
            float end)
        {
            foreach (float t in SampleTimes)
            {
                float before = t - Step;
                float after = t + Step;
                double expected = ((double)function(start, end, after) - function(start, end, before)) / ((double)after - before);
                double actual = derivative(start, end, t);
                double tolerance = RelativeTolerance * Math.Max(1d, Math.Abs(expected));

                Assert.That(
                    actual,
                    Is.EqualTo(expected).Within(tolerance),
                    $"{ease} derivative at t = {t} for {start} -> {end}");
            }
        }
    }
}
