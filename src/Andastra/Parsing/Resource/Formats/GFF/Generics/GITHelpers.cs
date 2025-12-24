using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Resource;

namespace Andastra.Parsing.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py
    // Original: construct_git and dismantle_git functions
    public static class GITHelpers
    {
        // Helper method to create a default triangle geometry at a position
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:1258-1259, 1333-1334
        // Original: encounter.geometry.create_triangle(origin=encounter.position)
        private static void CreateDefaultTriangle(List<Vector3> geometry, Vector3 origin)
        {
            // Create a simple triangle: origin, origin + (3, 0, 0), origin + (3, 3, 0)
            geometry.Add(new Vector3(origin.X, origin.Y, origin.Z));
            geometry.Add(new Vector3(origin.X + 3.0f, origin.Y, origin.Z));
            geometry.Add(new Vector3(origin.X + 3.0f, origin.Y + 3.0f, origin.Z));
        }
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:1184-1365
        // Original: def construct_git(gff: GFF) -> GIT:
        //
        // Default values verified against engine behavior:
        // - swkotor.exe: FUN_005c95f0 (LoadAudioProperties) @ 0x005c95f0, FUN_00507490 (LoadAreaProperties) @ 0x00507490
        // - swkotor2.exe: FUN_00574350 (LoadAudioProperties) @ 0x00574350, LoadAreaProperties @ 0x004e26d0
        public static GIT ConstructGit(GFF gff)
        {
            var git = new GIT();

            var root = gff.Root;
            var propertiesStruct = root.Acquire<GFFStruct>("AreaProperties", new GFFStruct());
            // Audio properties - all optional, engine uses existing values as defaults
            // swkotor.exe: 0x005c95f0, swkotor2.exe: 0x00574350
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x005c95f0 line 21-23, swkotor2.exe: 0x00574350 line 21-23)
            // For new GIT objects, default is 0
            git.AmbientVolume = propertiesStruct.Acquire<int>("AmbientSndDayVol", 0);
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x005c95f0 line 17-18, swkotor2.exe: 0x00574350 line 17-18)
            // For new GIT objects, default is 0
            git.AmbientSoundId = propertiesStruct.Acquire<int>("AmbientSndDay", 0);
            // Engine default: 0 (not explicitly loaded in audio properties, but AreaProperties struct defaults to 0)
            git.EnvAudio = propertiesStruct.Acquire<int>("EnvAudio", 0);
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x005c95f0 line 11-12, swkotor2.exe: 0x00574350 line 11-12)
            // For new GIT objects, default is 0
            git.MusicStandardId = propertiesStruct.Acquire<int>("MusicDay", 0);
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x005c95f0 line 15-16, swkotor2.exe: 0x00574350 line 15-16)
            // For new GIT objects, default is 0
            git.MusicBattleId = propertiesStruct.Acquire<int>("MusicBattle", 0);
            // Engine default: Uses existing value if field missing (swkotor.exe: 0x005c95f0 line 9-10, swkotor2.exe: 0x00574350 line 9-10)
            // For new GIT objects, default is 0
            git.MusicDelay = propertiesStruct.Acquire<int>("MusicDelay", 0);

            // Extract camera list - all fields optional
            // swkotor.exe: 0x005062a0, swkotor2.exe: 0x004e0ff0
            var cameraList = root.Acquire<GFFList>("CameraList", new GFFList());
            foreach (var cameraStruct in cameraList)
            {
                var camera = new GITCamera();
                // Engine default: -1 (swkotor2.exe: 0x004e0ff0 line 50)
                // NOTE: Engine uses -1 as default, not 0. This is important for camera identification.
                camera.CameraId = cameraStruct.Acquire<int>("CameraID", -1);
                // Engine default: 55.0 (swkotor2.exe: 0x004e0ff0 line 57)
                // NOTE: Engine uses 55.0 as default, not 0.0 or 45.0. This is the field of view angle.
                camera.Fov = cameraStruct.Acquire<float>("FieldOfView", 55.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x004e0ff0 line 55)
                camera.Height = cameraStruct.Acquire<float>("Height", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x004e0ff0 line 59)
                camera.MicRange = cameraStruct.Acquire<float>("MicRange", 0.0f);
                // Engine default: (0, 0, 0, 1) - quaternion identity (swkotor2.exe: 0x004e0ff0 line 52)
                // NOTE: Engine uses quaternion (0,0,0,1) as default orientation
                camera.Orientation = cameraStruct.Acquire<Vector4>("Orientation", new Vector4(0, 0, 0, 1));
                // Engine default: (0, 0, 0) (swkotor2.exe: 0x004e0ff0 line 51)
                camera.Position = cameraStruct.Acquire<Vector3>("Position", new Vector3());
                // Engine default: 0.0 (swkotor2.exe: 0x004e0ff0 line 53)
                camera.Pitch = cameraStruct.Acquire<float>("Pitch", 0.0f);
                git.Cameras.Add(camera);
            }

            // Extract creature list - all fields optional
            // swkotor.exe: 0x004c5bb0, swkotor2.exe: 0x004dfbb0
            var creatureList = root.Acquire<GFFList>("Creature List", new GFFList());
            foreach (var creatureStruct in creatureList)
            {
                var creature = new GITCreature();
                // Engine default: "" (swkotor2.exe: 0x004dfbb0 line 99)
                creature.ResRef = creatureStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 65, swkotor.exe: 0x004dfbb0 line 60)
                float x = creatureStruct.Acquire<float>("XPosition", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 67, swkotor.exe: 0x004dfbb0 line 58)
                float y = creatureStruct.Acquire<float>("YPosition", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 69, swkotor.exe: 0x004dfbb0 line 56)
                float z = creatureStruct.Acquire<float>("ZPosition", 0.0f);
                creature.Position = new Vector3(x, y, z);
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 80, swkotor.exe: 0x004dfbb0 line 80)
                float rotX = creatureStruct.Acquire<float>("XOrientation", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 79, swkotor.exe: 0x004dfbb0 line 79)
                float rotY = creatureStruct.Acquire<float>("YOrientation", 0.0f);
                // Calculate bearing from orientation
                var vec2 = new Vector2(rotX, rotY);
                creature.Bearing = (float)Math.Atan2(vec2.Y, vec2.X) - (float)(Math.PI / 2);
                git.Creatures.Add(creature);
            }

            // Extract door list - all fields optional
            // swkotor.exe: 0x0050a0e0, swkotor2.exe: 0x004e56b0
            var doorList = root.Acquire<GFFList>("Door List", new GFFList());
            foreach (var doorStruct in doorList)
            {
                var door = new GITDoor();
                // Engine default: 0.0 (not explicitly verified, but consistent with other bearing fields)
                door.Bearing = doorStruct.Acquire<float>("Bearing", 0.0f);
                // Engine default: "" (not explicitly verified, but consistent with other tag fields)
                door.Tag = doorStruct.Acquire<string>("Tag", "");
                // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                door.ResRef = doorStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                // Engine default: "" (not explicitly verified, but consistent with other string fields)
                door.LinkedTo = doorStruct.Acquire<string>("LinkedTo", "");
                // Engine default: 0 (NoLink) (not explicitly verified, but consistent with enum default)
                door.LinkedToFlags = (GITModuleLink)doorStruct.Acquire<int>("LinkedToFlags", 0);
                // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                door.LinkedToModule = doorStruct.Acquire<ResRef>("LinkedToModule", ResRef.FromBlank());
                // Engine default: Invalid LocalizedString (not explicitly verified, but consistent with other LocalizedString fields)
                door.TransitionDestination = doorStruct.Acquire<LocalizedString>("TransitionDestin", LocalizedString.FromInvalid());
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float x = doorStruct.Acquire<float>("X", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float y = doorStruct.Acquire<float>("Y", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float z = doorStruct.Acquire<float>("Z", 0.0f);
                door.Position = new Vector3(x, y, z);
                // Engine default: 0 (false) - K2 only field
                int tweakEnabled = doorStruct.Acquire<int>("UseTweakColor", 0);
                if (tweakEnabled != 0)
                {
                    // Engine default: 0 (not explicitly verified, but consistent with color defaults)
                    int tweakColorInt = doorStruct.Acquire<int>("TweakColor", 0);
                    door.TweakColor = new Color(ParsingColor.FromBgrInteger(tweakColorInt));
                }
                git.Doors.Add(door);
            }

            // Extract encounter list - all fields optional except geometry (which has fallback)
            // swkotor.exe: 0x0050a7b0, swkotor2.exe: 0x004e2b20
            var encounterList = root.Acquire<GFFList>("Encounter List", new GFFList());
            foreach (var encounterStruct in encounterList)
            {
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float x = encounterStruct.Acquire<float>("XPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float y = encounterStruct.Acquire<float>("YPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float z = encounterStruct.Acquire<float>("ZPosition", 0.0f);
                var encounter = new GITEncounter();
                encounter.Position = new Vector3(x, y, z);
                // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                encounter.ResRef = encounterStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());

                // Extract geometry if present - geometry points default to 0.0
                // NOTE: Geometry is required for encounters - if missing or empty, engine creates default triangle
                if (encounterStruct.Exists("Geometry"))
                {
                    var geometryList = encounterStruct.Acquire<GFFList>("Geometry", new GFFList());
                    if (geometryList != null && geometryList.Count > 0)
                    {
                        foreach (var geometryStruct in geometryList)
                        {
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            float gx = geometryStruct.Acquire<float>("X", 0.0f);
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            float gy = geometryStruct.Acquire<float>("Y", 0.0f);
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            float gz = geometryStruct.Acquire<float>("Z", 0.0f);
                            encounter.Geometry.Add(new Vector3(gx, gy, gz));
                        }
                    }
                    else
                    {
                        new Logger.RobustLogger().Warning("Encounter geometry list is empty! Creating a default triangle at its position.");
                        CreateDefaultTriangle(encounter.Geometry, encounter.Position);
                    }
                }
                else
                {
                    new Logger.RobustLogger().Warning("Encounter geometry list missing! Creating a default triangle at its position.");
                    CreateDefaultTriangle(encounter.Geometry, encounter.Position);
                }

                // Extract spawn points - all fields optional
                var spawnList = encounterStruct.Acquire<GFFList>("SpawnPointList", new GFFList());
                foreach (var spawnStruct in spawnList)
                {
                    var spawn = new GITEncounterSpawnPoint();
                    // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                    spawn.X = spawnStruct.Acquire<float>("X", 0.0f);
                    // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                    spawn.Y = spawnStruct.Acquire<float>("Y", 0.0f);
                    // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                    spawn.Z = spawnStruct.Acquire<float>("Z", 0.0f);
                    // Engine default: 0.0 (not explicitly verified, but consistent with other orientation fields)
                    spawn.Orientation = spawnStruct.Acquire<float>("Orientation", 0.0f);
                    encounter.SpawnPoints.Add(spawn);
                }

                git.Encounters.Add(encounter);
            }

            // Extract placeable list - all fields optional
            // swkotor.exe: 0x0050a7b0, swkotor2.exe: 0x004e5d80
            var placeableList = root.Acquire<GFFList>("Placeable List", new GFFList());
            foreach (var placeableStruct in placeableList)
            {
                var placeable = new GITPlaceable();
                // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                placeable.ResRef = placeableStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float x = placeableStruct.Acquire<float>("X", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float y = placeableStruct.Acquire<float>("Y", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float z = placeableStruct.Acquire<float>("Z", 0.0f);
                placeable.Position = new Vector3(x, y, z);
                // Engine default: 0.0 (not explicitly verified, but consistent with other bearing fields)
                placeable.Bearing = placeableStruct.Acquire<float>("Bearing", 0.0f);
                // Engine default: 0 (false) - K2 only field
                int tweakEnabled = placeableStruct.Acquire<int>("UseTweakColor", 0);
                if (tweakEnabled != 0)
                {
                    // Engine default: 0 (not explicitly verified, but consistent with color defaults)
                    int tweakColorInt = placeableStruct.Acquire<int>("TweakColor", 0);
                    placeable.TweakColor = new Color(ParsingColor.FromBgrInteger(tweakColorInt));
                }
                git.Placeables.Add(placeable);
            }

            // Extract sound list - all fields optional
            // swkotor.exe: 0x00507b10, swkotor2.exe: 0x004e06a0
            var soundList = root.Acquire<GFFList>("SoundList", new GFFList());
            foreach (var soundStruct in soundList)
            {
                var sound = new GITSound();
                // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                sound.ResRef = soundStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float x = soundStruct.Acquire<float>("XPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float y = soundStruct.Acquire<float>("YPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float z = soundStruct.Acquire<float>("ZPosition", 0.0f);
                sound.Position = new Vector3(x, y, z);
                git.Sounds.Add(sound);
            }

            // Extract store list - all fields optional
            // swkotor.exe: 0x00507ca0, swkotor2.exe: 0x004e08e0
            var storeList = root.Acquire<GFFList>("StoreList", new GFFList());
            foreach (var storeStruct in storeList)
            {
                var store = new GITStore();
                // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                store.ResRef = storeStruct.Acquire<ResRef>("ResRef", ResRef.FromBlank());
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float x = storeStruct.Acquire<float>("XPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float y = storeStruct.Acquire<float>("YPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float z = storeStruct.Acquire<float>("ZPosition", 0.0f);
                store.Position = new Vector3(x, y, z);
                // Engine default: 0.0 (not explicitly verified, but consistent with other orientation fields)
                float rotX = storeStruct.Acquire<float>("XOrientation", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other orientation fields)
                float rotY = storeStruct.Acquire<float>("YOrientation", 0.0f);
                // Bearing is calculated as arctangent of YOrientation over XOrientation, minus 90 degrees
                store.Bearing = (float)Math.Atan2(rotY, rotX) - (float)(Math.PI / 2);
                git.Stores.Add(store);
            }

            // Extract trigger list - all fields optional
            // swkotor.exe: 0x0050a350, swkotor2.exe: 0x004e5920
            var triggerList = root.Acquire<GFFList>("TriggerList", new GFFList());
            foreach (var triggerStruct in triggerList)
            {
                var trigger = new GITTrigger();
                // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                trigger.ResRef = triggerStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float x = triggerStruct.Acquire<float>("XPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float y = triggerStruct.Acquire<float>("YPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float z = triggerStruct.Acquire<float>("ZPosition", 0.0f);
                trigger.Position = new Vector3(x, y, z);
                // Engine default: "" (not explicitly verified, but consistent with other tag fields)
                trigger.Tag = triggerStruct.Acquire<string>("Tag", "");
                // Engine default: "" (not explicitly verified, but consistent with other string fields)
                trigger.LinkedTo = triggerStruct.Acquire<string>("LinkedTo", "");
                // Engine default: 0 (NoLink) (not explicitly verified, but consistent with enum default)
                trigger.LinkedToFlags = (GITModuleLink)triggerStruct.Acquire<int>("LinkedToFlags", 0);
                // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                trigger.LinkedToModule = triggerStruct.Acquire<ResRef>("LinkedToModule", ResRef.FromBlank());
                // Engine default: Invalid LocalizedString (not explicitly verified, but consistent with other LocalizedString fields)
                trigger.TransitionDestination = triggerStruct.Acquire<LocalizedString>("TransitionDestin", LocalizedString.FromInvalid());
                // Extract geometry if present - geometry points default to 0.0
                // NOTE: Geometry is required for triggers - if missing or empty, engine creates default triangle
                if (triggerStruct.Exists("Geometry"))
                {
                    var geometryList = triggerStruct.Acquire<GFFList>("Geometry", new GFFList());
                    if (geometryList != null && geometryList.Count > 0)
                    {
                        foreach (var geometryStruct in geometryList)
                        {
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            float px = geometryStruct.Acquire<float>("PointX", 0.0f);
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            float py = geometryStruct.Acquire<float>("PointY", 0.0f);
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            float pz = geometryStruct.Acquire<float>("PointZ", 0.0f);
                            trigger.Geometry.Add(new Vector3(px, py, pz));
                        }
                    }
                    else
                    {
                        new Logger.RobustLogger().Warning("Trigger geometry list is empty! Creating a default triangle at its position.");
                        CreateDefaultTriangle(trigger.Geometry, trigger.Position);
                    }
                }
                else
                {
                    new Logger.RobustLogger().Warning("Trigger geometry list missing! Creating a default triangle at its position.");
                    CreateDefaultTriangle(trigger.Geometry, trigger.Position);
                }
                git.Triggers.Add(trigger);
            }

            // Extract waypoint list - all fields optional
            // swkotor.exe: 0x00505360, swkotor2.exe: 0x004e04a0, FUN_0056f5a0 (LoadWaypoint) @ 0x0056f5a0
            var waypointList = root.Acquire<GFFList>("WaypointList", new GFFList());
            foreach (var waypointStruct in waypointList)
            {
                var waypoint = new GITWaypoint();
                // Engine default: Invalid LocalizedString (swkotor2.exe: 0x0056f5a0 line 52-54)
                waypoint.Name = waypointStruct.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
                // Engine default: "" (swkotor2.exe: 0x0056f5a0 line 43)
                waypoint.Tag = waypointStruct.Acquire<string>("Tag", "");
                // Engine default: "" (not explicitly loaded, but ResRef defaults to blank)
                waypoint.ResRef = waypointStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                // Engine default: 0.0 (swkotor2.exe: 0x0056f5a0 line 59, swkotor.exe: 0x00505360 line 60)
                float x = waypointStruct.Acquire<float>("XPosition", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x0056f5a0 line 61, swkotor.exe: 0x00505360 line 58)
                float y = waypointStruct.Acquire<float>("YPosition", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x0056f5a0 line 63, swkotor.exe: 0x00505360 line 56)
                float z = waypointStruct.Acquire<float>("ZPosition", 0.0f);
                waypoint.Position = new Vector3(x, y, z);
                // Engine default: 0 (false) (swkotor2.exe: 0x0056f5a0 line 77)
                waypoint.HasMapNote = waypointStruct.Acquire<int>("HasMapNote", 0) != 0;
                if (waypoint.HasMapNote)
                {
                    // Engine default: Invalid LocalizedString (swkotor2.exe: 0x0056f5a0 line 84)
                    waypoint.MapNote = waypointStruct.Acquire<LocalizedString>("MapNote", LocalizedString.FromInvalid());
                    // Engine default: 0 (false) (swkotor2.exe: 0x0056f5a0 line 80)
                    waypoint.MapNoteEnabled = waypointStruct.Acquire<int>("MapNoteEnabled", 0) != 0;
                }
                else
                {
                    waypoint.MapNote = null; // Explicitly set to null when HasMapNote is false, matching Python behavior
                }
                // Engine default: 0.0 (swkotor2.exe: 0x0056f5a0 line 65, swkotor.exe: 0x00505360 line 60)
                float rotX = waypointStruct.Acquire<float>("XOrientation", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x0056f5a0 line 67, swkotor.exe: 0x00505360 line 58)
                float rotY = waypointStruct.Acquire<float>("YOrientation", 0.0f);
                if (Math.Abs(rotX) < 1e-6f && Math.Abs(rotY) < 1e-6f)
                {
                    waypoint.Bearing = 0.0f;
                }
                else
                {
                    // Math.Atan2 calculates the angle in radians between the X axis and the point (rotX, rotY)
                    waypoint.Bearing = (float)Math.Atan2(rotY, rotX) - (float)(Math.PI / 2);
                }
                git.Waypoints.Add(waypoint);
            }

            return git;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:1368-1594
        // Original: def dismantle_git(git: GIT, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleGit(GIT git, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.GIT);
            var root = gff.Root;

            root.SetUInt8("UseTemplates", 1);

            var propertiesStruct = new GFFStruct(100);
            root.SetStruct("AreaProperties", propertiesStruct);
            propertiesStruct.SetInt32("AmbientSndDayVol", git.AmbientVolume);
            propertiesStruct.SetInt32("AmbientSndDay", git.AmbientSoundId);
            propertiesStruct.SetInt32("AmbientSndNitVol", git.AmbientVolume);
            propertiesStruct.SetInt32("AmbientSndNight", git.AmbientSoundId);
            propertiesStruct.SetInt32("EnvAudio", git.EnvAudio);
            propertiesStruct.SetInt32("MusicDay", git.MusicStandardId);
            propertiesStruct.SetInt32("MusicNight", git.MusicStandardId);
            propertiesStruct.SetInt32("MusicBattle", git.MusicBattleId);
            propertiesStruct.SetInt32("MusicDelay", git.MusicDelay);

            // Write camera list
            var cameraList = new GFFList();
            root.SetList("CameraList", cameraList);
            foreach (var camera in git.Cameras)
            {
                var cameraStruct = cameraList.Add(GITCamera.GffStructId);
                cameraStruct.SetInt32("CameraID", camera.CameraId);
                cameraStruct.SetSingle("FieldOfView", camera.Fov);
                cameraStruct.SetSingle("Height", camera.Height);
                cameraStruct.SetSingle("MicRange", camera.MicRange);
                cameraStruct.SetVector4("Orientation", camera.Orientation);
                cameraStruct.SetVector3("Position", camera.Position);
                cameraStruct.SetSingle("Pitch", camera.Pitch);
            }

            // Write creature list
            var creatureList = new GFFList();
            root.SetList("Creature List", creatureList);
            foreach (var creature in git.Creatures)
            {
                float angle = creature.Bearing + (float)(Math.PI / 2);
                var bearing = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
                var creatureStruct = creatureList.Add(GITCreature.GffStructId);
                if (creature.ResRef != null && !string.IsNullOrEmpty(creature.ResRef.ToString()))
                {
                    creatureStruct.SetResRef("TemplateResRef", creature.ResRef);
                }
                creatureStruct.SetSingle("XOrientation", bearing.X);
                creatureStruct.SetSingle("YOrientation", bearing.Y);
                creatureStruct.SetSingle("XPosition", creature.Position.X);
                creatureStruct.SetSingle("YPosition", creature.Position.Y);
                creatureStruct.SetSingle("ZPosition", creature.Position.Z);
            }

            // Write door list
            var doorList = new GFFList();
            root.SetList("Door List", doorList);
            foreach (var door in git.Doors)
            {
                var doorStruct = doorList.Add(GITDoor.GffStructId);
                doorStruct.SetSingle("Bearing", door.Bearing);
                doorStruct.SetString("Tag", door.Tag);
                if (door.ResRef != null && !string.IsNullOrEmpty(door.ResRef.ToString()))
                {
                    doorStruct.SetResRef("TemplateResRef", door.ResRef);
                }
                doorStruct.SetString("LinkedTo", door.LinkedTo);
                doorStruct.SetUInt8("LinkedToFlags", (byte)door.LinkedToFlags);
                doorStruct.SetResRef("LinkedToModule", door.LinkedToModule);
                doorStruct.SetLocString("TransitionDestin", door.TransitionDestination);
                doorStruct.SetSingle("X", door.Position.X);
                doorStruct.SetSingle("Y", door.Position.Y);
                doorStruct.SetSingle("Z", door.Position.Z);
                if (game.IsK2())
                {
                    int tweakColor = door.TweakColor != null ? door.TweakColor.ToBgrInteger() : 0;
                    doorStruct.SetUInt32("TweakColor", (uint)tweakColor);
                    doorStruct.SetUInt8("UseTweakColor", door.TweakColor != null ? (byte)1 : (byte)0);
                }
            }

            // Write encounter list
            var encounterList = new GFFList();
            root.SetList("Encounter List", encounterList);
            foreach (var encounter in git.Encounters)
            {
                var encounterStruct = encounterList.Add(GITEncounter.GffStructId);
                if (encounter.ResRef != null && !string.IsNullOrEmpty(encounter.ResRef.ToString()))
                {
                    encounterStruct.SetResRef("TemplateResRef", encounter.ResRef);
                }
                encounterStruct.SetSingle("XPosition", encounter.Position.X);
                encounterStruct.SetSingle("YPosition", encounter.Position.Y);
                encounterStruct.SetSingle("ZPosition", encounter.Position.Z);

                if (encounter.Geometry == null || encounter.Geometry.Count == 0)
                {
                    new Logger.RobustLogger().Warning($"Missing encounter geometry for '{encounter.ResRef}', creating a default triangle at its position...");
                    var tempGeometry = new List<Vector3>();
                    CreateDefaultTriangle(tempGeometry, encounter.Position);
                    encounter.Geometry = tempGeometry;
                }

                var geometryList = new GFFList();
                encounterStruct.SetList("Geometry", geometryList);
                foreach (var point in encounter.Geometry)
                {
                    var geometryStruct = geometryList.Add(GITEncounter.GffGeometryStructId);
                    geometryStruct.SetSingle("X", point.X);
                    geometryStruct.SetSingle("Y", point.Y);
                    geometryStruct.SetSingle("Z", point.Z);
                }

                var spawnList = new GFFList();
                encounterStruct.SetList("SpawnPointList", spawnList);
                foreach (var spawn in encounter.SpawnPoints)
                {
                    var spawnStruct = spawnList.Add(GITEncounter.GffSpawnStructId);
                    spawnStruct.SetSingle("Orientation", spawn.Orientation);
                    spawnStruct.SetSingle("X", spawn.X);
                    spawnStruct.SetSingle("Y", spawn.Y);
                    spawnStruct.SetSingle("Z", spawn.Z);
                }
            }

            // Write placeable list
            var placeableList = new GFFList();
            root.SetList("Placeable List", placeableList);
            foreach (var placeable in git.Placeables)
            {
                var placeableStruct = placeableList.Add(GITPlaceable.GffStructId);
                placeableStruct.SetSingle("Bearing", placeable.Bearing);
                if (placeable.ResRef != null && !string.IsNullOrEmpty(placeable.ResRef.ToString()))
                {
                    placeableStruct.SetResRef("TemplateResRef", placeable.ResRef);
                }
                placeableStruct.SetSingle("X", placeable.Position.X);
                placeableStruct.SetSingle("Y", placeable.Position.Y);
                placeableStruct.SetSingle("Z", placeable.Position.Z);
                if (game.IsK2())
                {
                    int tweakColor = placeable.TweakColor != null ? placeable.TweakColor.ToBgrInteger() : 0;
                    placeableStruct.SetUInt32("TweakColor", (uint)tweakColor);
                    placeableStruct.SetUInt8("UseTweakColor", placeable.TweakColor != null ? (byte)1 : (byte)0);
                }
            }

            // Write sound list
            var soundList = new GFFList();
            root.SetList("SoundList", soundList);
            foreach (var sound in git.Sounds)
            {
                var soundStruct = soundList.Add(GITSound.GffStructId);
                soundStruct.SetUInt32("GeneratedType", 0);
                if (sound.ResRef != null && !string.IsNullOrEmpty(sound.ResRef.ToString()))
                {
                    soundStruct.SetResRef("TemplateResRef", sound.ResRef);
                }
                soundStruct.SetSingle("XPosition", sound.Position.X);
                soundStruct.SetSingle("YPosition", sound.Position.Y);
                soundStruct.SetSingle("ZPosition", sound.Position.Z);
            }

            // Write store list
            var storeList = new GFFList();
            root.SetList("StoreList", storeList);
            foreach (var store in git.Stores)
            {
                float angle = store.Bearing + (float)(Math.PI / 2);
                var bearing = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
                var storeStruct = storeList.Add(GITStore.GffStructId);
                if (store.ResRef != null && !string.IsNullOrEmpty(store.ResRef.ToString()))
                {
                    storeStruct.SetResRef("ResRef", store.ResRef);
                }
                storeStruct.SetSingle("XOrientation", bearing.X);
                storeStruct.SetSingle("YOrientation", bearing.Y);
                storeStruct.SetSingle("XPosition", store.Position.X);
                storeStruct.SetSingle("YPosition", store.Position.Y);
                storeStruct.SetSingle("ZPosition", store.Position.Z);
            }

            // Write trigger list
            var triggerList = new GFFList();
            root.SetList("TriggerList", triggerList);
            foreach (var trigger in git.Triggers)
            {
                var triggerStruct = triggerList.Add(GITTrigger.GffStructId);
                if (trigger.ResRef != null && !string.IsNullOrEmpty(trigger.ResRef.ToString()))
                {
                    triggerStruct.SetResRef("TemplateResRef", trigger.ResRef);
                }
                triggerStruct.SetSingle("XPosition", trigger.Position.X);
                triggerStruct.SetSingle("YPosition", trigger.Position.Y);
                triggerStruct.SetSingle("ZPosition", trigger.Position.Z);
                triggerStruct.SetSingle("XOrientation", 0.0f);
                triggerStruct.SetSingle("YOrientation", 0.0f);
                triggerStruct.SetSingle("ZOrientation", 0.0f);
                triggerStruct.SetString("Tag", trigger.Tag);
                triggerStruct.SetString("LinkedTo", trigger.LinkedTo);
                triggerStruct.SetUInt8("LinkedToFlags", (byte)trigger.LinkedToFlags);
                triggerStruct.SetResRef("LinkedToModule", trigger.LinkedToModule);
                triggerStruct.SetLocString("TransitionDestin", trigger.TransitionDestination);

                if (trigger.Geometry == null || trigger.Geometry.Count == 0)
                {
                    new Logger.RobustLogger().Warning($"Missing trigger geometry for '{trigger.ResRef}', creating a default triangle at its position...");
                    var tempGeometry = new List<Vector3>();
                    CreateDefaultTriangle(tempGeometry, trigger.Position);
                    trigger.Geometry = tempGeometry;
                }

                var geometryList = new GFFList();
                triggerStruct.SetList("Geometry", geometryList);
                foreach (var point in trigger.Geometry)
                {
                    var geometryStruct = geometryList.Add(GITTrigger.GffGeometryStructId);
                    geometryStruct.SetSingle("PointX", point.X);
                    geometryStruct.SetSingle("PointY", point.Y);
                    geometryStruct.SetSingle("PointZ", point.Z);
                }
            }

            // Write waypoint list
            var waypointList = new GFFList();
            root.SetList("WaypointList", waypointList);
            foreach (var waypoint in git.Waypoints)
            {
                float angle = waypoint.Bearing + (float)(Math.PI / 2);
                var bearing = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
                var waypointStruct = waypointList.Add(GITWaypoint.GffStructId);
                waypointStruct.SetLocString("LocalizedName", waypoint.Name);
                waypointStruct.SetString("Tag", waypoint.Tag);
                waypointStruct.SetResRef("TemplateResRef", waypoint.ResRef);
                waypointStruct.SetSingle("XPosition", waypoint.Position.X);
                waypointStruct.SetSingle("YPosition", waypoint.Position.Y);
                waypointStruct.SetSingle("ZPosition", waypoint.Position.Z);
                waypointStruct.SetSingle("XOrientation", bearing.X);
                waypointStruct.SetSingle("YOrientation", bearing.Y);
                waypointStruct.SetUInt8("MapNoteEnabled", waypoint.MapNoteEnabled ? (byte)1 : (byte)0);
                waypointStruct.SetUInt8("HasMapNote", waypoint.HasMapNote ? (byte)1 : (byte)0);
                // Matching PyKotor: LocalizedString.from_invalid() if waypoint.map_note is None else waypoint.map_note
                waypointStruct.SetLocString("MapNote", waypoint.MapNote == null ? LocalizedString.FromInvalid() : waypoint.MapNote);

                if (useDeprecated)
                {
                    waypointStruct.SetUInt8("Appearance", 1);
                    waypointStruct.SetLocString("Description", LocalizedString.FromInvalid());
                    waypointStruct.SetString("LinkedTo", "");
                }
            }

            if (useDeprecated)
            {
                root.SetList("List", new GFFList());
            }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:1585-1594
        // Original: def bytes_git(git: GIT, game: Game = BioWareGame.K2, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesGit(GIT git, BioWareGame game = BioWareGame.K2, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.GIT;
            }
            GFF gff = DismantleGit(git, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}
