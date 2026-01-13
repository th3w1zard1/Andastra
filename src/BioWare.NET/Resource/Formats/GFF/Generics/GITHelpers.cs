using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Common.Logger;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.GFF.Generics
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
        // Engine relevant functions:
        // - [LoadAudioProperties] @ (K1: 0x005c95f0, TSL: 0x00574350)
        // - [LoadAreaProperties] @ (K1: 0x00507490, TSL: 0x004e26d0)
        public static GIT ConstructGit(GFF gff)
        {
            var git = new GIT();

            var root = gff.Root;
            var propertiesStruct = root.Acquire<GFFStruct>("AreaProperties", new GFFStruct());
            // Audio properties - all optional, engine uses existing values as defaults
            // Engine default: Uses existing value if field missing.  For new GIT objects, default is 0
            git.AmbientVolume = propertiesStruct.Acquire<int>("AmbientSndDayVol", 0);
            // Engine default: Uses existing value if field missing.  For new GIT objects, default is 0
            git.AmbientSoundId = propertiesStruct.Acquire<int>("AmbientSndDay", 0);
            // Engine default: 0 (not explicitly loaded in audio properties, but AreaProperties struct defaults to 0)
            git.EnvAudio = propertiesStruct.Acquire<int>("EnvAudio", 0);
            // Engine default: Uses existing value if field missing.  For new GIT objects, default is 0
            git.MusicStandardId = propertiesStruct.Acquire<int>("MusicDay", 0);
            // Engine default: Uses existing value if field missing.  For new GIT objects, default is 0
            git.MusicBattleId = propertiesStruct.Acquire<int>("MusicBattle", 0);
            // Engine default: Uses existing value if field missing.  For new GIT objects, default is 0
            git.MusicDelay = propertiesStruct.Acquire<int>("MusicDelay", 0);

            // Extract camera list - all fields optional
            // [TODO: Name this data/function] @ (K1: 0x005062a0, TSL: 0x004e0ff0)
            var cameraList = root.Acquire("CameraList", new GFFList());
            foreach (var cameraStruct in cameraList)
            {
                var camera = new GITCamera();
                // Engine default: -1
                // NOTE: Engine uses -1 as default, not 0. This is important for camera identification.
                camera.CameraId = cameraStruct.Acquire<int>("CameraID", -1);
                // Engine default: 55.0
                // NOTE: Engine uses 55.0 as default, not 0.0 or 45.0. This is the field of view angle.
                camera.Fov = cameraStruct.Acquire<float>("FieldOfView", 55.0f);
                // Engine default: 0.0
                camera.Height = cameraStruct.Acquire<float>("Height", 0.0f);
                // Engine default: 0.0 ([TODO: Name this data/function] @ (K1: 0x004e0ff0, TSL: 0x004e0ff0))
                camera.MicRange = cameraStruct.Acquire<float>("MicRange", 0.0f);
                // Engine default: (0, 0, 0, 1) - quaternion identity
                // NOTE: Engine uses quaternion (0,0,0,1) as default orientation
                camera.Orientation = cameraStruct.Acquire<Vector4>("Orientation", new Vector4(0, 0, 0, 1));
                // Engine default: (0, 0, 0)
                camera.Position = cameraStruct.Acquire<Vector3>("Position", new Vector3());
                // Engine default: 0.0
                camera.Pitch = cameraStruct.Acquire<float>("Pitch", 0.0f);
                git.Cameras.Add(camera);
            }

            // Extract creature list - all fields optional
            // swkotor.exe: 0x004c5bb0, swkotor2.exe: 0x004dfbb0
            var creatureList = root.Acquire("Creature List", new GFFList());
            foreach (var creatureStruct in creatureList)
            {
                var creature = new GITCreature();
                // Engine default: "" (swkotor2.exe: 0x004dfbb0 line 99)
                creature.ResRef = creatureStruct.Acquire("TemplateResRef", ResRef.FromBlank());
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 65, swkotor.exe: 0x004dfbb0 line 60)
                float x = creatureStruct.Acquire("XPosition", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 67, swkotor.exe: 0x004dfbb0 line 58)
                float y = creatureStruct.Acquire("YPosition", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 69, swkotor.exe: 0x004dfbb0 line 56)
                float z = creatureStruct.Acquire("ZPosition", 0.0f);
                creature.Position = new Vector3(x, y, z);
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 80, swkotor.exe: 0x004dfbb0 line 80)
                float rotX = creatureStruct.Acquire("XOrientation", 0.0f);
                // Engine default: 0.0 (swkotor2.exe: 0x004dfbb0 line 79, swkotor.exe: 0x004dfbb0 line 79)
                float rotY = creatureStruct.Acquire("YOrientation", 0.0f);
                // Calculate bearing from orientation
                var vec2 = new Vector2(rotX, rotY);
                creature.Bearing = (float)Math.Atan2(vec2.Y, vec2.X) - (float)(Math.PI / 2);
                git.Creatures.Add(creature);
            }

            // Extract door list - all fields optional
            // swkotor.exe: 0x0050a0e0, swkotor2.exe: 0x004e56b0
            var doorList = root.Acquire("Door List", new GFFList());
            foreach (var doorStruct in doorList)
            {
                var door = new GITDoor
                {
                    // Engine default: 0.0 (not explicitly verified, but consistent with other bearing fields)
                    Bearing = doorStruct.Acquire("Bearing", 0.0f), // Engine default: "" (not explicitly verified, but consistent with other tag fields)
                    Tag = doorStruct.Acquire("Tag", ""), // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                    ResRef = doorStruct.Acquire("TemplateResRef", ResRef.FromBlank()),
                    // Engine default: "" (not explicitly verified, but consistent with other string fields)
                    LinkedTo = doorStruct.Acquire("LinkedTo", ""),
                    // Engine default: 0 (NoLink) (not explicitly verified, but consistent with enum default)
                    LinkedToFlags = (GITModuleLink)doorStruct.Acquire("LinkedToFlags", 0),
                    // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                    LinkedToModule = doorStruct.Acquire("LinkedToModule", ResRef.FromBlank()),
                    // Engine default: Invalid LocalizedString (not explicitly verified, but consistent with other LocalizedString fields)
                    TransitionDestination = doorStruct.Acquire("TransitionDestin", LocalizedString.FromInvalid())
                };
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float x = doorStruct.Acquire("X", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float y = doorStruct.Acquire("Y", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float z = doorStruct.Acquire("Z", 0.0f);
                door.Position = new Vector3(x, y, z);
                // Engine default: 0 (false) - K2 only field
                int tweakEnabled = doorStruct.Acquire("UseTweakColor", 0);
                if (tweakEnabled != 0)
                {
                    // Engine default: 0 (not explicitly verified, but consistent with color defaults)
                    int tweakColorInt = doorStruct.Acquire("TweakColor", 0);
                    door.TweakColor = new Color(Color.FromBgrInteger(tweakColorInt));
                }
                git.Doors.Add(door);
            }

            // Extract encounter list - all fields optional except geometry (which has fallback)
            // swkotor.exe: 0x0050a7b0, swkotor2.exe: 0x004e2b20
            var encounterList = root.Acquire("Encounter List", new GFFList());
            foreach (var encounterStruct in encounterList)
            {
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float x = encounterStruct.Acquire("XPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float y = encounterStruct.Acquire("YPosition", 0.0f);
                // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                float z = encounterStruct.Acquire("ZPosition", 0.0f);
                var encounter = new GITEncounter {
                    Position = new Vector3(x, y, z),
                    ResRef = encounterStruct.Acquire("TemplateResRef", ResRef.FromBlank()),
                };

                // Extract geometry if present - geometry points default to 0.0
                // NOTE: Geometry is required for encounters - if missing or empty, engine creates default triangle
                if (encounterStruct.Exists("Geometry"))
                {
                    var geometryList = encounterStruct.Acquire("Geometry", new GFFList());
                    if (geometryList.Count > 0)
                    {
                        foreach (var geometryStruct in geometryList)
                        {
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            float gx = geometryStruct.Acquire("X", 0.0f);
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            float gy = geometryStruct.Acquire("Y", 0.0f);
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            float gz = geometryStruct.Acquire("Z", 0.0f);
                            encounter.Geometry.Add(new Vector3(gx, gy, gz));
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("WARNING: Encounter geometry list is empty! Creating a default triangle at its position.");
                        CreateDefaultTriangle(encounter.Geometry, encounter.Position);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: Encounter geometry list missing! Creating a default triangle at its position.");
                    CreateDefaultTriangle(encounter.Geometry, encounter.Position);
                }

                // Extract spawn points - all fields optional
                var spawnList = encounterStruct.Acquire("SpawnPointList", new GFFList());
                foreach (var spawnStruct in spawnList)
                {
                    var spawn = new GITEncounterSpawnPoint
                    {
                        // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                        X = spawnStruct.Acquire("X", 0.0f), // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                        Y = spawnStruct.Acquire("Y", 0.0f), // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                        Z = spawnStruct.Acquire("Z", 0.0f),
                        // Engine default: 0.0 (not explicitly verified, but consistent with other orientation fields)
                        Orientation = spawnStruct.Acquire("Orientation", 0.0f)
                    };
                    encounter.SpawnPoints.Add(spawn);
                }

                git.Encounters.Add(encounter);
            }

            // Extract placeable list - all fields optional
            // swkotor.exe: 0x0050a7b0, swkotor2.exe: 0x004e5d80
            var placeableList = root.Acquire("Placeable List", new GFFList());
            foreach (var placeableStruct in placeableList)
            {
                var placeable = new GITPlaceable {
                    // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                    ResRef = placeableStruct.Acquire("TemplateResRef", ResRef.FromBlank()),
                    Position = new Vector3(
                        x: placeableStruct.Acquire("X", 0.0f),
                        y: placeableStruct.Acquire("Y", 0.0f),
                        z: placeableStruct.Acquire("Z", 0.0f)
                    ),
                    Bearing = placeableStruct.Acquire("Bearing", 0.0f),
                    TweakColor = placeableStruct.Acquire("UseTweakColor", 0) != 0 ? new Color(Color.FromBgrInteger(placeableStruct.Acquire("TweakColor", 0))) : null,
                };
                git.Placeables.Add(placeable);
            }

            // Extract sound list - all fields optional
            // swkotor.exe: 0x00507b10, swkotor2.exe: 0x004e06a0
            var soundList = root.Acquire("SoundList", new GFFList());
            foreach (var soundStruct in soundList)
            {
                var sound = new GITSound {
                    // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                    ResRef = soundStruct.Acquire("TemplateResRef", ResRef.FromBlank()),
                    Position = new Vector3(
                        x: soundStruct.Acquire("XPosition", 0.0f),
                        y: soundStruct.Acquire("YPosition", 0.0f),
                        z: soundStruct.Acquire("ZPosition", 0.0f)
                    ),
                };
                git.Sounds.Add(sound);
            }

            // Extract store list - all fields optional
            // swkotor.exe: 0x00507ca0, swkotor2.exe: 0x004e08e0
            var storeList = root.Acquire("StoreList", new GFFList());
            foreach (var storeStruct in storeList)
            {
                var store = new GITStore {
                    // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                    ResRef = storeStruct.Acquire("ResRef", ResRef.FromBlank()),
                    Position = new Vector3(
                        x: storeStruct.Acquire("XPosition", 0.0f),
                        y: storeStruct.Acquire("YPosition", 0.0f),
                        z: storeStruct.Acquire("ZPosition", 0.0f)
                    ),
                    Bearing = (float)Math.Atan2(storeStruct.Acquire("YOrientation", 0.0f), storeStruct.Acquire("XOrientation", 0.0f)) - (float)(Math.PI / 2),
                };
                git.Stores.Add(store);
            }

            // Extract trigger list - all fields optional
            // swkotor.exe: 0x0050a350, swkotor2.exe: 0x004e5920
            var triggerList = root.Acquire("TriggerList", new GFFList());
            foreach (var triggerStruct in triggerList)
            {
                var trigger = new GITTrigger {
                    // Engine default: "" (not explicitly verified, but consistent with other ResRef fields)
                    ResRef = triggerStruct.Acquire("TemplateResRef", ResRef.FromBlank()),
                    Position = new Vector3(
                        x: triggerStruct.Acquire("XPosition", 0.0f),
                        y: triggerStruct.Acquire("YPosition", 0.0f),
                        z: triggerStruct.Acquire("ZPosition", 0.0f)
                    ),
                    // Engine default: "" (not explicitly verified, but consistent with other tag fields)
                    Tag = triggerStruct.Acquire("Tag", ""),
                    LinkedTo = triggerStruct.Acquire("LinkedTo", ""),
                    LinkedToFlags = (GITModuleLink)triggerStruct.Acquire("LinkedToFlags", 0),
                    LinkedToModule = triggerStruct.Acquire("LinkedToModule", ResRef.FromBlank()),
                    TransitionDestination = triggerStruct.Acquire("TransitionDestin", LocalizedString.FromInvalid()),
                };
                // Extract geometry if present - geometry points default to 0.0
                // NOTE: Geometry is required for triggers - if missing or empty, engine creates default triangle
                if (triggerStruct.Exists("Geometry"))
                {
                    var geometryList = triggerStruct.Acquire("Geometry", new GFFList());
                    if (geometryList.Count > 0)
                    {
                        foreach (var geometryStruct in geometryList)
                        {
                            // Engine default: 0.0 (not explicitly verified, but consistent with other position fields)
                            trigger.Geometry.Add(new Vector3(
                                x: geometryStruct.Acquire("PointX", 0.0f),
                                y: geometryStruct.Acquire("PointY", 0.0f),
                                z: geometryStruct.Acquire("PointZ", 0.0f)
                            ));
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("WARNING: Trigger geometry list is empty! Creating a default triangle at its position.");
                        CreateDefaultTriangle(trigger.Geometry, trigger.Position);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: Trigger geometry list missing! Creating a default triangle at its position.");
                    CreateDefaultTriangle(trigger.Geometry, trigger.Position);
                }
                git.Triggers.Add(trigger);
            }

            // Extract waypoint list - all fields optional
            // [LoadWaypoint] @ (K1: 0x00505360, TSL: 0x004e04a0) @ FUN_0056f5a0 (K1: TODO: Find this address, TSL: 0x0056f5a0)
            var waypointList = root.Acquire("WaypointList", new GFFList());
            foreach (var waypointStruct in waypointList)
            {
                var waypoint = new GITWaypoint
                {
                    // Engine default: Invalid LocalizedString (swkotor2.exe: 0x0056f5a0 line 52-54)
                    Name = waypointStruct.Acquire("LocalizedName", LocalizedString.FromInvalid()), // Engine default: "" (swkotor2.exe: 0x0056f5a0 line 43)
                    Tag = waypointStruct.Acquire("Tag", ""),
                    // Engine default: "" (not explicitly loaded, but ResRef defaults to blank)
                    ResRef = waypointStruct.Acquire("TemplateResRef", ResRef.FromBlank()),
                    Position = new Vector3(
                        x: waypointStruct.Acquire("XPosition", 0.0f),
                        y: waypointStruct.Acquire("YPosition", 0.0f),
                        z: waypointStruct.Acquire("ZPosition", 0.0f)
                    ),
                    HasMapNote = waypointStruct.Acquire("HasMapNote", 0) != 0,
                };
                if (waypoint.HasMapNote)
                {
                    // Engine default: Invalid LocalizedString (swkotor2.exe: 0x0056f5a0 line 84)
                    waypoint.MapNote = waypointStruct.Acquire("MapNote", LocalizedString.FromInvalid());
                    // Engine default: 0 (false) [LoadWaypoint] @ (K1: TODO: Find this address, TSL: 0x0056f5a0) line 80
                    waypoint.MapNoteEnabled = waypointStruct.Acquire("MapNoteEnabled", 0) != 0;
                }
                else
                {
                    waypoint.MapNote = null; // Explicitly set to null when HasMapNote is false, matching Python behavior
                }
                // Engine default: 0.0 [LoadWaypoint] @ (K1: TODO: Find this address, TSL: 0x0056f5a0) line 65
                float rotX = waypointStruct.Acquire("XOrientation", 0.0f);
                // Engine default: 0.0 [LoadWaypoint] @ (K1: TODO: Find this address, TSL: 0x0056f5a0) line 67
                float rotY = waypointStruct.Acquire("YOrientation", 0.0f);
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
                var bearing = Vector2.Normalize(new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
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
                    int tweakColor = door.TweakColor?.ToBgrInteger() ?? 0;
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
                    System.Diagnostics.Debug.WriteLine($"WARNING: Missing encounter geometry for '{encounter.ResRef}', creating a default triangle at its position...");
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
                    int tweakColor = placeable.TweakColor?.ToBgrInteger() ?? 0;
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
                var bearing = Vector2.Normalize(new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
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
                    System.Diagnostics.Debug.WriteLine($"WARNING: Missing trigger geometry for '{trigger.ResRef}', creating a default triangle at its position...");
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
                var bearing = Vector2.Normalize(new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
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
