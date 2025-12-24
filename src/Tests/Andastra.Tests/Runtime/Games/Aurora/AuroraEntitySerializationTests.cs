using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Aurora;
using Andastra.Runtime.Games.Aurora.Components;
using Andastra.Runtime.Games.Common.Components;
using Andastra.Tests.Runtime.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Andastra.Tests.Runtime.Games.Aurora
{
    /// <summary>
    /// Comprehensive tests for AuroraEntity serialization and deserialization.
    /// Tests all components, local variables, custom data, and edge cases.
    /// </summary>
    public class AuroraEntitySerializationTests
    {
        [Fact]
        public void Serialize_WithBasicProperties_ShouldSucceed()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");
            entity.AreaId = 456u;

            // Act
            byte[] data = entity.Serialize();

            // Assert
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Serialize_And_Deserialize_WithBasicProperties_ShouldRoundTrip()
        {
            // Arrange
            var originalEntity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");
            originalEntity.AreaId = 456u;

            // Act
            byte[] serialized = originalEntity.Serialize();
            var deserializedEntity = new AuroraEntity(123u, ObjectType.Creature, "");
            deserializedEntity.Deserialize(serialized);

            // Assert
            deserializedEntity.ObjectId.Should().Be(originalEntity.ObjectId);
            deserializedEntity.Tag.Should().Be(originalEntity.Tag);
            deserializedEntity.ObjectType.Should().Be(originalEntity.ObjectType);
            deserializedEntity.AreaId.Should().Be(originalEntity.AreaId);
            deserializedEntity.IsValid.Should().Be(originalEntity.IsValid);
        }

        [Fact]
        public void Serialize_And_Deserialize_WithTransformComponent_ShouldRoundTrip()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");
            var transformComponent = entity.GetComponent<ITransformComponent>();
            transformComponent.Position = new Vector3(10.5f, 20.75f, 30.25f);
            transformComponent.Facing = 1.57f;
            transformComponent.Scale = new Vector3(1.5f, 2.0f, 0.5f);

            // Act
            byte[] serialized = entity.Serialize();
            var deserializedEntity = new AuroraEntity(123u, ObjectType.Creature, "");
            deserializedEntity.Deserialize(serialized);

            // Assert
            var deserializedTransform = deserializedEntity.GetComponent<ITransformComponent>();
            deserializedTransform.Should().NotBeNull();
            deserializedTransform.Position.X.Should().BeApproximately(10.5f, 0.01f);
            deserializedTransform.Position.Y.Should().BeApproximately(20.75f, 0.01f);
            deserializedTransform.Position.Z.Should().BeApproximately(30.25f, 0.01f);
            deserializedTransform.Facing.Should().BeApproximately(1.57f, 0.01f);
            deserializedTransform.Scale.X.Should().BeApproximately(1.5f, 0.01f);
            deserializedTransform.Scale.Y.Should().BeApproximately(2.0f, 0.01f);
            deserializedTransform.Scale.Z.Should().BeApproximately(0.5f, 0.01f);
        }

        [Fact]
        public void Serialize_And_Deserialize_WithScriptHooks_ShouldRoundTrip()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");
            var scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            scriptHooks.SetScript(ScriptEvent.OnHeartbeat, "test_heartbeat");
            scriptHooks.SetScript(ScriptEvent.OnAttacked, "test_attacked");
            scriptHooks.SetScript(ScriptEvent.OnDeath, "test_death");

            // Act
            byte[] serialized = entity.Serialize();
            var deserializedEntity = new AuroraEntity(123u, ObjectType.Creature, "");
            deserializedEntity.Deserialize(serialized);

            // Assert
            var deserializedScriptHooks = deserializedEntity.GetComponent<IScriptHooksComponent>();
            deserializedScriptHooks.Should().NotBeNull();
            deserializedScriptHooks.GetScript(ScriptEvent.OnHeartbeat).Should().Be("test_heartbeat");
            deserializedScriptHooks.GetScript(ScriptEvent.OnAttacked).Should().Be("test_attacked");
            deserializedScriptHooks.GetScript(ScriptEvent.OnDeath).Should().Be("test_death");
        }

        [Fact]
        public void Serialize_And_Deserialize_WithLocalVariables_ShouldRoundTrip()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");
            var scriptHooks = entity.GetComponent<IScriptHooksComponent>();

            // Set local variables using reflection
            Type componentType = typeof(BaseScriptHooksComponent);
            FieldInfo localIntsField = componentType.GetField("_localInts", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo localFloatsField = componentType.GetField("_localFloats", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo localStringsField = componentType.GetField("_localStrings", BindingFlags.NonPublic | BindingFlags.Instance);

            var localInts = localIntsField?.GetValue(scriptHooks) as Dictionary<string, int>;
            var localFloats = localFloatsField?.GetValue(scriptHooks) as Dictionary<string, float>;
            var localStrings = localStringsField?.GetValue(scriptHooks) as Dictionary<string, string>;

            localInts["TestInt"] = 42;
            localInts["AnotherInt"] = 100;
            localFloats["TestFloat"] = 3.14f;
            localFloats["AnotherFloat"] = 2.71f;
            localStrings["TestString"] = "Hello World";
            localStrings["AnotherString"] = "Test Value";

            // Act
            byte[] serialized = entity.Serialize();
            var deserializedEntity = new AuroraEntity(123u, ObjectType.Creature, "");
            deserializedEntity.Deserialize(serialized);

            // Assert
            var deserializedScriptHooks = deserializedEntity.GetComponent<IScriptHooksComponent>();
            deserializedScriptHooks.Should().NotBeNull();
            deserializedScriptHooks.GetLocalInt("TestInt").Should().Be(42);
            deserializedScriptHooks.GetLocalInt("AnotherInt").Should().Be(100);
            deserializedScriptHooks.GetLocalFloat("TestFloat").Should().BeApproximately(3.14f, 0.01f);
            deserializedScriptHooks.GetLocalFloat("AnotherFloat").Should().BeApproximately(2.71f, 0.01f);
            deserializedScriptHooks.GetLocalString("TestString").Should().Be("Hello World");
            deserializedScriptHooks.GetLocalString("AnotherString").Should().Be("Test Value");
        }

        [Fact]
        public void Serialize_And_Deserialize_WithCustomData_ShouldRoundTrip()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");
            EntityTestHelper.SetCustomData(entity, "TestInt", 42);
            EntityTestHelper.SetCustomData(entity, "TestFloat", 3.14f);
            EntityTestHelper.SetCustomData(entity, "TestString", "Hello World");
            EntityTestHelper.SetCustomData(entity, "TestBool", true);
            EntityTestHelper.SetCustomData(entity, "TestUInt", 100u);

            // Act
            byte[] serialized = entity.Serialize();
            var deserializedEntity = new AuroraEntity(123u, ObjectType.Creature, "");
            deserializedEntity.Deserialize(serialized);

            // Assert
            EntityTestHelper.GetCustomData(deserializedEntity, "TestInt").Should().Be(42);
            EntityTestHelper.GetCustomData(deserializedEntity, "TestFloat").Should().Be(3.14f);
            EntityTestHelper.GetCustomData(deserializedEntity, "TestString").Should().Be("Hello World");
            EntityTestHelper.GetCustomData(deserializedEntity, "TestBool").Should().Be(true);
            EntityTestHelper.GetCustomData(deserializedEntity, "TestUInt").Should().Be(100u);
        }

        [Fact]
        public void Serialize_And_Deserialize_WithDoorComponent_ShouldRoundTrip()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Door, "test_door");
            var doorComponent = entity.GetComponent<IDoorComponent>();
            doorComponent.IsOpen = true;
            doorComponent.IsLocked = true;
            doorComponent.LockDC = 25;
            doorComponent.HitPoints = 40;
            doorComponent.MaxHitPoints = 50;
            doorComponent.Hardness = 10;
            doorComponent.KeyTag = "test_key";
            doorComponent.KeyRequired = true;
            doorComponent.LinkedTo = "linked_door";
            doorComponent.LinkedToModule = "test_module";

            // Act
            byte[] serialized = entity.Serialize();
            var deserializedEntity = new AuroraEntity(123u, ObjectType.Door, "");
            deserializedEntity.Deserialize(serialized);

            // Assert
            var deserializedDoor = deserializedEntity.GetComponent<IDoorComponent>();
            deserializedDoor.Should().NotBeNull();
            deserializedDoor.IsOpen.Should().BeTrue();
            deserializedDoor.IsLocked.Should().BeTrue();
            deserializedDoor.LockDC.Should().Be(25);
            deserializedDoor.HitPoints.Should().Be(40);
            deserializedDoor.MaxHitPoints.Should().Be(50);
            deserializedDoor.Hardness.Should().Be(10);
            deserializedDoor.KeyTag.Should().Be("test_key");
            deserializedDoor.KeyRequired.Should().BeTrue();
            deserializedDoor.LinkedTo.Should().Be("linked_door");
            deserializedDoor.LinkedToModule.Should().Be("test_module");
        }

        [Fact]
        public void Serialize_And_Deserialize_WithPlaceableComponent_ShouldRoundTrip()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Placeable, "test_placeable");
            var placeableComponent = entity.GetComponent<IPlaceableComponent>();
            placeableComponent.IsUseable = true;
            placeableComponent.HasInventory = true;
            placeableComponent.IsOpen = true;
            placeableComponent.IsLocked = true;
            placeableComponent.LockDC = 20;
            placeableComponent.HitPoints = 25;
            placeableComponent.MaxHitPoints = 30;
            placeableComponent.Hardness = 5;
            placeableComponent.AnimationState = 2;

            // Act
            byte[] serialized = entity.Serialize();
            var deserializedEntity = new AuroraEntity(123u, ObjectType.Placeable, "");
            deserializedEntity.Deserialize(serialized);

            // Assert
            var deserializedPlaceable = deserializedEntity.GetComponent<IPlaceableComponent>();
            deserializedPlaceable.Should().NotBeNull();
            deserializedPlaceable.IsUseable.Should().BeTrue();
            deserializedPlaceable.HasInventory.Should().BeTrue();
            deserializedPlaceable.IsOpen.Should().BeTrue();
            deserializedPlaceable.IsLocked.Should().BeTrue();
            deserializedPlaceable.LockDC.Should().Be(20);
            deserializedPlaceable.HitPoints.Should().Be(25);
            deserializedPlaceable.MaxHitPoints.Should().Be(30);
            deserializedPlaceable.Hardness.Should().Be(5);
            deserializedPlaceable.AnimationState.Should().Be(2);
        }

        [Fact]
        public void Deserialize_WithNullData_ShouldThrowArgumentException()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => entity.Deserialize(null));
        }

        [Fact]
        public void Deserialize_WithEmptyData_ShouldThrowArgumentException()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => entity.Deserialize(new byte[0]));
        }

        [Fact]
        public void Deserialize_WithInvalidGFFData_ShouldThrowInvalidDataException()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");
            byte[] invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

            // Act & Assert
            Assert.Throws<System.IO.InvalidDataException>(() => entity.Deserialize(invalidData));
        }

        [Fact]
        public void Serialize_And_Deserialize_WithAllComponents_ShouldRoundTrip()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");
            entity.AreaId = 456u;

            // Set transform
            var transform = entity.GetComponent<ITransformComponent>();
            transform.Position = new Vector3(1.0f, 2.0f, 3.0f);
            transform.Facing = 0.5f;
            transform.Scale = new Vector3(1.0f, 1.0f, 1.0f);

            // Set script hooks
            var scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            scriptHooks.SetScript(ScriptEvent.OnHeartbeat, "heartbeat_script");

            // Set local variables
            Type componentType = typeof(BaseScriptHooksComponent);
            FieldInfo localIntsField = componentType.GetField("_localInts", BindingFlags.NonPublic | BindingFlags.Instance);
            var localInts = localIntsField?.GetValue(scriptHooks) as Dictionary<string, int>;
            localInts["TestVar"] = 123;

            // Set custom data
            EntityTestHelper.SetCustomData(entity, "CustomValue", 999);

            // Act
            byte[] serialized = entity.Serialize();
            var deserializedEntity = new AuroraEntity(123u, ObjectType.Creature, "");
            deserializedEntity.Deserialize(serialized);

            // Assert
            deserializedEntity.ObjectId.Should().Be(123u);
            deserializedEntity.Tag.Should().Be("test_creature");
            deserializedEntity.AreaId.Should().Be(456u);

            var deserializedTransform = deserializedEntity.GetComponent<ITransformComponent>();
            deserializedTransform.Position.X.Should().BeApproximately(1.0f, 0.01f);

            var deserializedScriptHooks = deserializedEntity.GetComponent<IScriptHooksComponent>();
            deserializedScriptHooks.GetScript(ScriptEvent.OnHeartbeat).Should().Be("heartbeat_script");
            deserializedScriptHooks.GetLocalInt("TestVar").Should().Be(123);

            EntityTestHelper.GetCustomData(deserializedEntity, "CustomValue").Should().Be(999);
        }

        [Fact]
        public void Serialize_WithEmptyTag_ShouldSucceed()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "");

            // Act
            byte[] data = entity.Serialize();

            // Assert
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Serialize_WithNullTag_ShouldSucceed()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, null);

            // Act
            byte[] data = entity.Serialize();

            // Assert
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Serialize_And_Deserialize_WithMultipleScriptEvents_ShouldRoundTrip()
        {
            // Arrange
            var entity = new AuroraEntity(123u, ObjectType.Creature, "test_creature");
            var scriptHooks = entity.GetComponent<IScriptHooksComponent>();

            // Set multiple script events
            scriptHooks.SetScript(ScriptEvent.OnHeartbeat, "hb");
            scriptHooks.SetScript(ScriptEvent.OnNotice, "notice");
            scriptHooks.SetScript(ScriptEvent.OnSpellAt, "spell");
            scriptHooks.SetScript(ScriptEvent.OnAttacked, "attacked");
            scriptHooks.SetScript(ScriptEvent.OnDamaged, "damaged");
            scriptHooks.SetScript(ScriptEvent.OnDisturbed, "disturbed");
            scriptHooks.SetScript(ScriptEvent.OnEndRound, "endround");
            scriptHooks.SetScript(ScriptEvent.OnDialogue, "dialogue");
            scriptHooks.SetScript(ScriptEvent.OnSpawn, "spawn");
            scriptHooks.SetScript(ScriptEvent.OnRested, "rested");
            scriptHooks.SetScript(ScriptEvent.OnDeath, "death");
            scriptHooks.SetScript(ScriptEvent.OnUserDefined, "userdef");
            scriptHooks.SetScript(ScriptEvent.OnBlocked, "blocked");

            // Act
            byte[] serialized = entity.Serialize();
            var deserializedEntity = new AuroraEntity(123u, ObjectType.Creature, "");
            deserializedEntity.Deserialize(serialized);

            // Assert
            var deserializedScriptHooks = deserializedEntity.GetComponent<IScriptHooksComponent>();
            deserializedScriptHooks.GetScript(ScriptEvent.OnHeartbeat).Should().Be("hb");
            deserializedScriptHooks.GetScript(ScriptEvent.OnNotice).Should().Be("notice");
            deserializedScriptHooks.GetScript(ScriptEvent.OnSpellAt).Should().Be("spell");
            deserializedScriptHooks.GetScript(ScriptEvent.OnAttacked).Should().Be("attacked");
            deserializedScriptHooks.GetScript(ScriptEvent.OnDamaged).Should().Be("damaged");
            deserializedScriptHooks.GetScript(ScriptEvent.OnDisturbed).Should().Be("disturbed");
            deserializedScriptHooks.GetScript(ScriptEvent.OnEndRound).Should().Be("endround");
            deserializedScriptHooks.GetScript(ScriptEvent.OnDialogue).Should().Be("dialogue");
            deserializedScriptHooks.GetScript(ScriptEvent.OnSpawn).Should().Be("spawn");
            deserializedScriptHooks.GetScript(ScriptEvent.OnRested).Should().Be("rested");
            deserializedScriptHooks.GetScript(ScriptEvent.OnDeath).Should().Be("death");
            deserializedScriptHooks.GetScript(ScriptEvent.OnUserDefined).Should().Be("userdef");
            deserializedScriptHooks.GetScript(ScriptEvent.OnBlocked).Should().Be("blocked");
        }
    }
}

