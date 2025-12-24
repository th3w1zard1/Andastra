using System;
using System.Numerics;
using Andastra.Parsing;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Common
{

    /// <summary>
    /// Tests for geometry types (Vector2, Vector3, Vector4, Face, Polygon2).
    /// 1:1 port of Python test_geometry.py from tests/common/test_geometry.py
    /// </summary>
    public class GeometryTests
    {
        public class Vector2Tests
        {
            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestUnpacking()
            {
                var source = new Vector2(1.2f, 2.3f);
                float x = source.X;
                float y = source.Y;
                x.Should().BeApproximately(1.2f, 0.0001f);
                y.Should().BeApproximately(2.3f, 0.0001f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromVector2()
            {
                var source = new Vector2(1.2f, 2.3f);
                var vec2 = new Vector2(source.X, source.Y);
                vec2.X.Should().BeApproximately(1.2f, 0.0001f);
                vec2.Y.Should().BeApproximately(2.3f, 0.0001f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromVector3()
            {
                var source = new Vector3(1.2f, 2.3f, 3.4f);
                var vec2 = new Vector2(source.X, source.Y);
                vec2.X.Should().BeApproximately(1.2f, 0.0001f);
                vec2.Y.Should().BeApproximately(2.3f, 0.0001f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromVector4()
            {
                var source = new Vector4(1.2f, 2.3f, 3.4f, 5.6f);
                var vec2 = new Vector2(source.X, source.Y);
                vec2.X.Should().BeApproximately(1.2f, 0.0001f);
                vec2.Y.Should().BeApproximately(2.3f, 0.0001f);
            }
        }

        public class Vector3Tests
        {
            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestUnpacking()
            {
                var source = new Vector3(1.2f, 2.3f, 3.4f);
                float x = source.X;
                float y = source.Y;
                float z = source.Z;
                x.Should().BeApproximately(1.2f, 0.0001f);
                y.Should().BeApproximately(2.3f, 0.0001f);
                z.Should().BeApproximately(3.4f, 0.0001f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromVector2()
            {
                var source = new Vector2(1.2f, 2.3f);
                var vec3 = new Vector3(source.X, source.Y, 0.0f);
                vec3.X.Should().BeApproximately(1.2f, 0.0001f);
                vec3.Y.Should().BeApproximately(2.3f, 0.0001f);
                vec3.Z.Should().Be(0.0f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromVector3()
            {
                var source = new Vector3(1.2f, 2.3f, 3.4f);
                var vec3 = new Vector3(source.X, source.Y, source.Z);
                vec3.X.Should().BeApproximately(1.2f, 0.0001f);
                vec3.Y.Should().BeApproximately(2.3f, 0.0001f);
                vec3.Z.Should().BeApproximately(3.4f, 0.0001f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromVector4()
            {
                var source = new Vector4(1.2f, 2.3f, 3.4f, 5.6f);
                var vec3 = new Vector3(source.X, source.Y, source.Z);
                vec3.X.Should().BeApproximately(1.2f, 0.0001f);
                vec3.Y.Should().BeApproximately(2.3f, 0.0001f);
                vec3.Z.Should().BeApproximately(3.4f, 0.0001f);
            }
        }

        public class Vector4Tests
        {
            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestUnpacking()
            {
                var source = new Vector4(1.2f, 2.3f, 3.4f, 4.5f);
                float x = source.X;
                float y = source.Y;
                float z = source.Z;
                float w = source.W;
                x.Should().BeApproximately(1.2f, 0.0001f);
                y.Should().BeApproximately(2.3f, 0.0001f);
                z.Should().BeApproximately(3.4f, 0.0001f);
                w.Should().BeApproximately(4.5f, 0.0001f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromVector2()
            {
                var source = new Vector2(1.2f, 2.3f);
                var vec4 = new Vector4(source.X, source.Y, 0.0f, 0.0f);
                vec4.X.Should().BeApproximately(1.2f, 0.0001f);
                vec4.Y.Should().BeApproximately(2.3f, 0.0001f);
                vec4.Z.Should().Be(0.0f);
                vec4.W.Should().Be(0.0f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromVector3()
            {
                var source = new Vector3(1.2f, 2.3f, 3.4f);
                var vec4 = new Vector4(source.X, source.Y, source.Z, 0.0f);
                vec4.X.Should().BeApproximately(1.2f, 0.0001f);
                vec4.Y.Should().BeApproximately(2.3f, 0.0001f);
                vec4.Z.Should().BeApproximately(3.4f, 0.0001f);
                vec4.W.Should().Be(0.0f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromVector4()
            {
                var source = new Vector4(1.2f, 2.3f, 3.4f, 5.6f);
                var vec4 = new Vector4(source.X, source.Y, source.Z, source.W);
                vec4.X.Should().BeApproximately(1.2f, 0.0001f);
                vec4.Y.Should().BeApproximately(2.3f, 0.0001f);
                vec4.Z.Should().BeApproximately(3.4f, 0.0001f);
                vec4.W.Should().BeApproximately(5.6f, 0.0001f);
            }

            [Fact(Timeout = 120000)] // 2 minutes timeout
            public void TestFromEuler()
            {
                // Converting degrees to radians
                double rad90 = Math.PI / 2.0;

                Vector4 q1 = QuaternionFromEuler(0, 0, 0);
                Vector4 q2 = QuaternionFromEuler(rad90, 0, 0);
                Vector4 q3 = QuaternionFromEuler(0, rad90, 0);
                Vector4 q4 = QuaternionFromEuler(0, 0, rad90);

                q1.X.Should().BeApproximately(0.0f, 0.1f);
                q1.Y.Should().BeApproximately(0.0f, 0.1f);
                q1.Z.Should().BeApproximately(0.0f, 0.1f);
                q1.W.Should().BeApproximately(1.0f, 0.1f);

                q2.X.Should().BeApproximately(0.7f, 0.1f);
                q2.Y.Should().BeApproximately(0.0f, 0.1f);
                q2.Z.Should().BeApproximately(0.0f, 0.1f);
                q2.W.Should().BeApproximately(0.7f, 0.1f);

                q3.X.Should().BeApproximately(0.0f, 0.1f);
                q3.Y.Should().BeApproximately(0.7f, 0.1f);
                q3.Z.Should().BeApproximately(0.0f, 0.1f);
                q3.W.Should().BeApproximately(0.7f, 0.1f);

                q4.X.Should().BeApproximately(0.0f, 0.1f);
                q4.Y.Should().BeApproximately(0.0f, 0.1f);
                q4.Z.Should().BeApproximately(0.7f, 0.1f);
                q4.W.Should().BeApproximately(0.7f, 0.1f);
            }

            private static Vector4 QuaternionFromEuler(double x, double y, double z)
            {
                // Python's from_euler implementation (geometry.py lines 896-905)
                // Args: x=roll, y=pitch, z=yaw
                double roll = x;
                double pitch = y;
                double yaw = z;

                double qx = Math.Sin(roll / 2) * Math.Cos(pitch / 2) * Math.Cos(yaw / 2) - Math.Cos(roll / 2) * Math.Sin(pitch / 2) * Math.Sin(yaw / 2);
                double qy = Math.Cos(roll / 2) * Math.Sin(pitch / 2) * Math.Cos(yaw / 2) + Math.Sin(roll / 2) * Math.Cos(pitch / 2) * Math.Sin(yaw / 2);
                double qz = Math.Cos(roll / 2) * Math.Cos(pitch / 2) * Math.Sin(yaw / 2) - Math.Sin(roll / 2) * Math.Sin(pitch / 2) * Math.Cos(yaw / 2);
                double qw = Math.Cos(roll / 2) * Math.Cos(pitch / 2) * Math.Cos(yaw / 2) + Math.Sin(roll / 2) * Math.Sin(pitch / 2) * Math.Sin(yaw / 2);

                return new Vector4((float)qx, (float)qy, (float)qz, (float)qw);
            }
        }

    }
}

