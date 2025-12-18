using System;
using System.Collections.Generic;
using FluentAssertions;
using HolocronToolset.Windows.DesignerControls;
using Xunit;

namespace HolocronToolset.Tests.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py
    // Original: Tests for the improved designer controls
    public class DesignerControlsTests
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:18-115
        // Original: class TestInputSmoother(unittest.TestCase)
        public class InputSmootherTests
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:27-31
            // Original: def test_initialization(self)
            [Fact]
            public void TestInitialization()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.3f);
                smoother.SmoothingFactor.Should().Be(0.3f);
                smoother.IsInitialized.Should().BeFalse();
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:33-41
            // Original: def test_first_input_passthrough(self)
            [Fact]
            public void TestFirstInputPassthrough()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.5f);

                var (x, y) = smoother.Smooth(10.0f, 20.0f);

                x.Should().Be(10.0f);
                y.Should().Be(20.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:43-56
            // Original: def test_smoothing_reduces_jitter(self)
            [Fact]
            public void TestSmoothingReducesJitter()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.5f);

                // Initial input
                smoother.Smooth(0.0f, 0.0f);

                // Sudden large change
                var (x, y) = smoother.Smooth(100.0f, 100.0f);

                // Output should be between 0 and 100 (smoothed)
                x.Should().BeGreaterThan(0);
                x.Should().BeLessThan(100);
                y.Should().BeGreaterThan(0);
                y.Should().BeLessThan(100);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:58-72
            // Original: def test_smoothing_converges(self)
            [Fact]
            public void TestSmoothingConverges()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.3f);

                // Initialize
                smoother.Smooth(0.0f, 0.0f);

                // Apply same input repeatedly
                float x = 0, y = 0;
                for (int i = 0; i < 50; i++)
                {
                    var result = smoother.Smooth(100.0f, 100.0f);
                    x = result.Item1;
                    y = result.Item2;
                }

                // Should be very close to target after many iterations
                Math.Abs(x - 100.0f).Should().BeLessThan(1.0f);
                Math.Abs(y - 100.0f).Should().BeLessThan(1.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:74-87
            // Original: def test_high_smoothing_is_slow(self)
            [Fact]
            public void TestHighSmoothingIsSlow()
            {
                var slowSmoother = new InputSmoother(smoothingFactor: 0.9f);
                var fastSmoother = new InputSmoother(smoothingFactor: 0.1f);

                // Initialize both
                slowSmoother.Smooth(0.0f, 0.0f);
                fastSmoother.Smooth(0.0f, 0.0f);

                // Apply same input
                var (slowX, _) = slowSmoother.Smooth(100.0f, 100.0f);
                var (fastX, __) = fastSmoother.Smooth(100.0f, 100.0f);

                // Slow smoother should have moved less
                slowX.Should().BeLessThan(fastX);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:89-103
            // Original: def test_reset(self)
            [Fact]
            public void TestReset()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.5f);

                // Use smoother
                smoother.Smooth(50.0f, 50.0f);
                smoother.Smooth(100.0f, 100.0f);

                // Reset
                smoother.Reset();

                // Next input should be passthrough
                var (x, y) = smoother.Smooth(200.0f, 200.0f);
                x.Should().Be(200.0f);
                y.Should().Be(200.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:105-115
            // Original: def test_negative_values(self)
            [Fact]
            public void TestNegativeValues()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.3f);

                smoother.Smooth(0.0f, 0.0f);
                var (x, y) = smoother.Smooth(-50.0f, -50.0f);

                // Should be negative, between 0 and -50
                x.Should().BeLessThan(0);
                x.Should().BeGreaterThan(-50);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:117-206
        // Original: class TestInputAccelerator(unittest.TestCase)
        public class InputAcceleratorTests
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:125-129
            // Original: def test_initialization(self)
            [Fact]
            public void TestInitialization()
            {
                var accelerator = new InputAccelerator(power: 1.5f, threshold: 2.0f);
                accelerator.Power.Should().Be(1.5f);
                accelerator.Threshold.Should().Be(2.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:131-139
            // Original: def test_below_threshold_linear(self)
            [Fact]
            public void TestBelowThresholdLinear()
            {
                var accelerator = new InputAccelerator(power: 2.0f, threshold: 10.0f);

                // Input below threshold
                float result = accelerator.Accelerate(5.0f);

                // Should be unchanged
                result.Should().Be(5.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:141-149
            // Original: def test_above_threshold_accelerated(self)
            [Fact]
            public void TestAboveThresholdAccelerated()
            {
                var accelerator = new InputAccelerator(power: 2.0f, threshold: 5.0f);

                // Input above threshold
                float result = accelerator.Accelerate(10.0f);

                // Should be greater than linear (10.0)
                result.Should().BeGreaterThan(10.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:151-162
            // Original: def test_negative_values_preserve_sign(self)
            [Fact]
            public void TestNegativeValuesPreserveSign()
            {
                var accelerator = new InputAccelerator(power: 2.0f, threshold: 5.0f);

                // Negative input above threshold
                float result = accelerator.Accelerate(-10.0f);

                // Should be negative
                result.Should().BeLessThan(0);

                // Magnitude should be accelerated
                result.Should().BeLessThan(-10.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:164-170
            // Original: def test_zero_unchanged(self)
            [Fact]
            public void TestZeroUnchanged()
            {
                var accelerator = new InputAccelerator(power: 2.0f, threshold: 5.0f);

                float result = accelerator.Accelerate(0.0f);

                result.Should().Be(0.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:172-183
            // Original: def test_power_affects_curve(self)
            [Fact]
            public void TestPowerAffectsCurve()
            {
                var lowPower = new InputAccelerator(power: 1.5f, threshold: 5.0f);
                var highPower = new InputAccelerator(power: 3.0f, threshold: 5.0f);

                float inputValue = 15.0f;  // Well above threshold

                float lowResult = lowPower.Accelerate(inputValue);
                float highResult = highPower.Accelerate(inputValue);

                // Higher power should give more acceleration
                lowResult.Should().BeLessThan(highResult);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:185-192
            // Original: def test_exact_threshold_linear(self)
            [Fact]
            public void TestExactThresholdLinear()
            {
                var accelerator = new InputAccelerator(power: 2.0f, threshold: 10.0f);

                float result = accelerator.Accelerate(10.0f);

                // At threshold, should be exactly linear
                result.Should().Be(10.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:194-205
            // Original: def test_acceleration_is_continuous(self)
            [Fact]
            public void TestAccelerationIsContinuous()
            {
                var accelerator = new InputAccelerator(power: 2.0f, threshold: 10.0f);

                // Just below threshold
                float below = accelerator.Accelerate(9.99f);

                // Just above threshold
                float above = accelerator.Accelerate(10.01f);

                // Should be very close (continuous)
                Math.Abs(below - above).Should().BeLessThan(0.1f);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:208-269
        // Original: class TestControlsIntegration(unittest.TestCase)
        public class ControlsIntegrationTests
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:211-243
            // Original: def test_smoother_and_accelerator_together(self)
            [Fact]
            public void TestSmootherAndAcceleratorTogether()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.2f);
                var accelerator = new InputAccelerator(power: 1.5f, threshold: 3.0f);

                // Initialize smoother
                smoother.Smooth(0.0f, 0.0f);

                // Simulate a sequence of mouse movements
                var results = new List<Tuple<float, float>>();
                for (int i = 0; i < 10; i++)
                {
                    // Raw input: gradually increasing
                    float rawX = i * 2.0f;
                    float rawY = i * 2.0f;

                    // Apply smoothing first
                    var (smoothX, smoothY) = smoother.Smooth(rawX, rawY);

                    // Then acceleration
                    float accelX = accelerator.Accelerate(smoothX);
                    float accelY = accelerator.Accelerate(smoothY);

                    results.Add(Tuple.Create(accelX, accelY));
                }

                // Verify monotonically increasing (mostly)
                for (int i = 1; i < results.Count; i++)
                {
                    // Allow small variance due to smoothing
                    (results[i].Item1).Should().BeGreaterThanOrEqualTo(results[i - 1].Item1 - 1.0f,
                        $"X should generally increase: {results[i].Item1} < {results[i - 1].Item1}");
                }
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:245-269
            // Original: def test_realistic_mouse_movement(self)
            [Fact]
            public void TestRealisticMouseMovement()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.3f);
                var accelerator = new InputAccelerator(power: 1.3f, threshold: 3.0f);

                // Simulate realistic mouse movement (small variations with occasional large moves)
                var movements = new (int, int)[]
                {
                    (1, 2), (2, 1), (3, 3), (2, 2), (1, 3),  // Small precise movements
                    (15, 10), (14, 11), (16, 9),  // Fast movement
                    (0, 0), (1, 0), (0, 1),  // Return to precise
                };

                smoother.Smooth(0.0f, 0.0f);  // Initialize

                foreach (var (rawX, rawY) in movements)
                {
                    var (smoothX, smoothY) = smoother.Smooth((float)rawX, (float)rawY);
                    float accelX = accelerator.Accelerate(smoothX);
                    float accelY = accelerator.Accelerate(smoothY);

                    // Results should be finite and reasonable
                    float.IsFinite(accelX).Should().BeTrue();
                    float.IsFinite(accelY).Should().BeTrue();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:271-338
        // Original: class TestEdgeCases(unittest.TestCase)
        public class EdgeCasesTests
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:280-289
            // Original: def test_smoother_with_zero_factor(self)
            [Fact]
            public void TestSmootherWithZeroFactor()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.0f);

                smoother.Smooth(0.0f, 0.0f);
                var (x, y) = smoother.Smooth(100.0f, 100.0f);

                // With zero smoothing, should immediately reach target
                x.Should().Be(100.0f);
                y.Should().Be(100.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:291-300
            // Original: def test_smoother_with_one_factor(self)
            [Fact]
            public void TestSmootherWithOneFactor()
            {
                var smoother = new InputSmoother(smoothingFactor: 1.0f);

                smoother.Smooth(0.0f, 0.0f);
                var (x, y) = smoother.Smooth(100.0f, 100.0f);

                // With maximum smoothing, should stay at initial value
                x.Should().Be(0.0f);
                y.Should().Be(0.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:302-308
            // Original: def test_accelerator_with_power_one(self)
            [Fact]
            public void TestAcceleratorWithPowerOne()
            {
                var accelerator = new InputAccelerator(power: 1.0f, threshold: 0.0f);

                // Should be linear
                float result = accelerator.Accelerate(10.0f);
                result.Should().Be(10.0f);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:310-322
            // Original: def test_very_large_values(self)
            [Fact]
            public void TestVeryLargeValues()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.3f);
                var accelerator = new InputAccelerator(power: 1.5f, threshold: 3.0f);

                smoother.Smooth(0.0f, 0.0f);

                // Large values
                var (smoothX, smoothY) = smoother.Smooth(1000000.0f, 1000000.0f);
                float accelX = accelerator.Accelerate(smoothX);

                // Should be finite
                float.IsFinite(accelX).Should().BeTrue();
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_designer_controls.py:324-337
            // Original: def test_very_small_values(self)
            [Fact]
            public void TestVerySmallValues()
            {
                var smoother = new InputSmoother(smoothingFactor: 0.3f);
                var accelerator = new InputAccelerator(power: 1.5f, threshold: 0.001f);

                smoother.Smooth(0.0f, 0.0f);

                // Very small values
                var (smoothX, smoothY) = smoother.Smooth(0.0001f, 0.0001f);
                float accelX = accelerator.Accelerate(smoothX);

                // Should be finite and close to input
                float.IsFinite(accelX).Should().BeTrue();
                Math.Abs(accelX - smoothX).Should().BeLessThan(0.001f);
            }
        }
    }
}

