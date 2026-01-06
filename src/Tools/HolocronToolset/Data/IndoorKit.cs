using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.BWM;
using Andastra.Parsing.Resource.Formats.LYT;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Vector3 = System.Numerics.Vector3;

namespace HolocronToolset.Data
{
    /// <summary>
    /// Represents a "kit" - a collection of reusable building blocks for creating indoor modules.
    /// Kits contain components (rooms), doors, textures, lightmaps, and other assets that can be
    /// assembled to build complete game areas.
    /// </summary>
    /// <remarks>
    /// WHAT IS A KIT?
    ///
    /// A kit is like a box of LEGO pieces for building game levels. Instead of building rooms from
    /// scratch every time, you can use pre-made room pieces (components) from a kit. Each kit
    /// contains:
    ///
    /// 1. COMPONENTS: Pre-made room pieces that can be placed in the indoor map builder.
    ///    - Each component has a 3D model (MDL/MDX files), a walkmesh (BWM), and a preview image
    ///    - Components can be rotated, flipped, and positioned anywhere in the map
    ///    - Components have "hooks" (connection points) where doors can be placed
    ///
    /// 2. DOORS: Door templates that can be placed at component hooks.
    ///    - Each door has a width, height, and door definition (UTD)
    ///    - Doors connect components together, allowing characters to move between rooms
    ///
    /// 3. TEXTURES: Image files used to texture the 3D models.
    ///    - Stored as TPC (texture) files
    ///    - Applied to component models during rendering
    ///
    /// 4. LIGHTMAPS: Pre-computed lighting data for components.
    ///    - Lightmaps make rooms look realistic by adding shadows and lighting
    ///    - Stored as TPC files
    ///
    /// 5. TXIS: Texture information files that describe how textures should be applied.
    ///
    /// 6. ALWAYS: Resources that are always included in modules built from this kit.
    ///
    /// 7. SIDE/TOP PADDING: Additional model pieces used to fill gaps between components.
    ///
    /// 8. SKYBOXES: Background images that appear outside the module.
    ///
    /// HOW KITS ARE USED:
    ///
    /// When building an indoor module:
    /// 1. Load a kit (or create one from an existing module)
    /// 2. Select components from the kit
    /// 3. Place components in the map builder, rotating/flipping as needed
    /// 4. Connect components using doors at hook points
    /// 5. The builder combines all component walkmeshes into one navigation mesh
    /// 6. The builder combines all component models into one 3D scene
    ///
    /// KITS FROM MODULES:
    ///
    /// Kits can be extracted from existing game modules. The ModuleKit class loads a module file
    /// (.rim or .mod), extracts all rooms as components, and creates a kit from them. This allows
    /// you to reuse rooms from existing game areas in your own modules.
    ///
    /// ORIGINAL IMPLEMENTATION:
    ///
    /// Based on PyKotor's Kit class. Kits are a standard feature of the Aurora engine's module
    /// building system, allowing level designers to create modular, reusable room pieces.
    /// </remarks>
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:24
    // Original: class Kit:
    public class Kit
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:25-35
        // Original: def __init__(self, name: str):
        public Kit(string name)
        {
            Name = name;
            Components = new List<KitComponent>();
            Doors = new List<KitDoor>();
            Textures = new Dictionary<string, byte[]>();
            Lightmaps = new Dictionary<string, byte[]>();
            Txis = new Dictionary<string, byte[]>();
            Always = new Dictionary<string, byte[]>();
            SidePadding = new Dictionary<int, Dictionary<int, MDLMDXTuple>>();
            TopPadding = new Dictionary<int, Dictionary<int, MDLMDXTuple>>();
            Skyboxes = new Dictionary<string, MDLMDXTuple>();
        }

        public string Name { get; set; }
        public List<KitComponent> Components { get; set; }
        public List<KitDoor> Doors { get; set; }
        public Dictionary<string, byte[]> Textures { get; set; }
        public Dictionary<string, byte[]> Lightmaps { get; set; }
        public Dictionary<string, byte[]> Txis { get; set; }
        public Dictionary<string, byte[]> Always { get; set; }
        public Dictionary<int, Dictionary<int, MDLMDXTuple>> SidePadding { get; set; }
        public Dictionary<int, Dictionary<int, MDLMDXTuple>> TopPadding { get; set; }
        public Dictionary<string, MDLMDXTuple> Skyboxes { get; set; }
    }

    /// <summary>
    /// Represents a single reusable room component within a kit.
    /// Each component is a building block that can be placed multiple times in an indoor map.
    /// </summary>
    /// <remarks>
    /// WHAT IS A KIT COMPONENT?
    ///
    /// A KitComponent is a single room piece that can be reused in multiple places. Think of it
    /// like a stamp - you can stamp the same room design multiple times in different locations.
    ///
    /// Each component contains:
    ///
    /// 1. BWM (Walkmesh): The collision/navigation data for the room.
    ///    - Defines where characters can walk
    ///    - Contains triangles with surface materials
    ///    - Has edges with transitions (door connection points)
    ///    - MUST be centered at origin (0, 0, 0) for proper placement
    ///
    /// 2. MDL/MDX (Model): The 3D visual representation of the room.
    ///    - MDL contains the model geometry
    ///    - MDX contains model extensions (animations, etc.)
    ///    - These are the files that make the room visible in-game
    ///
    /// 3. IMAGE: A preview image shown in the indoor map builder.
    ///    - Generated from the walkmesh (top-down view)
    ///    - Shows walkable areas in white, non-walkable in gray
    ///    - Used for visual selection in the builder UI
    ///
    /// 4. HOOKS: Connection points where doors can be placed.
    ///    - Each hook has a position, rotation, and edge index
    ///    - Hooks are extracted from walkmesh edges with transitions
    ///    - When two components are connected, their hooks link together
    ///
    /// WHY COMPONENTS ARE CENTERED:
    ///
    /// Components are stored with their walkmesh centered at (0, 0, 0). This is critical because:
    ///
    /// 1. The preview image is drawn centered at the component's position
    /// 2. The walkmesh is translated by the component's position when placed
    /// 3. If the walkmesh isn't centered, the image and walkmesh won't align
    ///
    /// For example, if a walkmesh has vertices at (100, 200, 0) to (200, 300, 0), and the component
    /// is placed at position (50, 50, 0):
    /// - Without centering: Walkmesh ends up at (150, 250, 0) to (250, 350, 0) - WRONG!
    /// - With centering: Walkmesh is first centered to (-50, -50, 0) to (50, 50, 0), then
    ///   translated to (0, 0, 0) to (100, 100, 0) - CORRECT!
    ///
    /// The _RecenterBwm() method in ModuleKit ensures components are properly centered when
    /// extracted from game modules.
    ///
    /// DEEP COPYING:
    ///
    /// When a component is placed in a map, it's deep copied so each placement can have its own
    /// transformations (rotation, flip, position). The DeepCopy() method creates a new BWM,
    /// new MDL/MDX byte arrays, and new hooks, ensuring no shared state between placements.
    ///
    /// ORIGINAL IMPLEMENTATION:
    ///
    /// Based on PyKotor's KitComponent class. Components are the fundamental building blocks
    /// of the indoor map builder system.
    /// </remarks>
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:38
    // Original: class KitComponent:
    public class KitComponent
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:39-47
        // Original: def __init__(self, kit: Kit, name: str, image: QImage, bwm: BWM, mdl: bytes, mdx: bytes):
        public KitComponent(Kit kit, string name, object image, BWM bwm, byte[] mdl, byte[] mdx)
        {
            Kit = kit;
            Name = name;
            Image = image;
            Bwm = bwm;
            Mdl = mdl;
            Mdx = mdx;
            Hooks = new List<KitComponentHook>();
        }

        public Kit Kit { get; set; }
        public string Name { get; set; }
        public object Image { get; set; }
        public BWM Bwm { get; set; }
        public byte[] Mdl { get; set; }
        public byte[] Mdx { get; set; }
        public List<KitComponentHook> Hooks { get; set; }

        /// <summary>
        /// Creates a deep copy of this KitComponent so it can be transformed independently.
        /// </summary>
        /// <remarks>
        /// WHAT IS DEEP COPYING?
        ///
        /// Deep copying creates a completely independent copy of an object. Unlike a shallow copy
        /// (which just copies references), a deep copy creates new objects for all nested data.
        ///
        /// WHY DO WE NEED DEEP COPYING FOR COMPONENTS?
        ///
        /// When placing a component in an indoor map, each placement needs to be independent. If
        /// multiple rooms use the same component, they each need their own copy so they can be
        /// transformed (flipped, rotated, translated) independently. Without deep copying, transforming
        /// one room would affect all other rooms that share that component.
        ///
        /// WHAT GETS COPIED?
        ///
        /// The DeepCopy method creates a new KitComponent and copies:
        /// 1. BWM (Walkmesh): Creates a completely new BWM with all faces copied
        ///    - CRITICAL: Material is explicitly copied to preserve walkability
        ///    - All vertices, materials, and transitions are copied
        /// 2. MDL/MDX (Model Data): Creates new byte arrays with copied model data
        ///    - Models are copied so each placement can be transformed independently
        /// 3. Hooks: Creates new KitComponentHook objects for each hook
        ///    - Hook positions are copied (Vector3 is a struct, so copied by value)
        ///    - Door references stay the same (doors are shared within a kit)
        ///
        /// WHAT DOESN'T GET COPIED?
        ///
        /// These are shared references (not copied):
        /// - Kit: The component still belongs to the same kit
        /// - Image: Preview images are typically immutable or shared
        /// - Name: Strings are immutable in C#, so safe to share
        /// - Door (in hooks): Doors are shared within a kit, so references stay the same
        ///
        /// CRITICAL: MATERIAL PRESERVATION:
        ///
        /// The DeepCopyBwm method MUST explicitly copy Material: newFace.Material = face.Material
        ///
        /// If materials are not preserved during deep copying, faces that should be walkable will
        /// become non-walkable, causing the bug where "levels/modules are NOT walkable despite having
        /// the right surface material."
        ///
        /// This bug was fixed by ensuring Material is explicitly copied in DeepCopyBwm(). The original
        /// implementation might have relied on default copying behavior, which could fail if the
        /// BWMFace constructor doesn't initialize Material properly.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Deep Copy BWM
        /// - Calls DeepCopyBwm() to create an independent walkmesh copy
        /// - This ensures each component placement has its own walkmesh
        ///
        /// STEP 2: Deep Copy Model Data
        /// - Creates new byte arrays for MDL and MDX
        /// - Copies all bytes from original to new arrays
        /// - This ensures each placement can transform models independently
        ///
        /// STEP 3: Create New Component
        /// - Creates new KitComponent with copied BWM and models
        /// - Shares Kit, Name, and Image (these are safe to share)
        ///
        /// STEP 4: Deep Copy Hooks
        /// - Creates new KitComponentHook for each hook
        /// - Copies position (Vector3 is a struct, so copied by value)
        /// - Shares Door reference (doors are shared within a kit)
        ///
        /// STEP 5: Return Independent Copy
        /// - Returns a completely independent KitComponent that can be transformed without affecting the original
        ///
        /// WHEN IS IT USED?
        ///
        /// DeepCopy is called when:
        /// 1. Placing a component in an indoor map (each placement needs its own copy)
        /// 2. Editing hooks on a component (hooks can be edited independently)
        /// 3. Applying transformations (flip, rotate, translate) to a component
        ///
        /// ORIGINAL IMPLEMENTATION:
        ///
        /// Based on PyKotor's deepcopy(component) behavior. The original code also explicitly copies
        /// materials to ensure walkability is preserved during transformations.
        ///
        /// Matching PyKotor implementation: deepcopy(component) behavior
        /// This matches the behavior in indoor_builder.py:338 and indoor_builder.py:1721
        /// </remarks>
        /// <returns>A new, independent KitComponent copy that can be transformed without affecting the original</returns>
        public KitComponent DeepCopy()
        {
            // Deep copy the BWM (walkmesh) - each component needs its own instance
            // CRITICAL: This ensures materials are preserved and each placement can be transformed independently
            BWM bwmCopy = DeepCopyBwm(Bwm);

            // Deep copy the byte arrays (MDL and MDX model data)
            // This ensures each placement can transform models independently
            byte[] mdlCopy = null;
            if (Mdl != null)
            {
                mdlCopy = new byte[Mdl.Length];
                System.Array.Copy(Mdl, mdlCopy, Mdl.Length);
            }

            byte[] mdxCopy = null;
            if (Mdx != null)
            {
                mdxCopy = new byte[Mdx.Length];
                System.Array.Copy(Mdx, mdxCopy, Mdx.Length);
            }

            // Create new component with copied data
            // Kit reference stays the same (component belongs to the same kit)
            // Image reference stays the same (images are typically immutable or shared)
            // Name is a string (immutable in C#)
            var componentCopy = new KitComponent(Kit, Name, Image, bwmCopy, mdlCopy, mdxCopy);

            // Deep copy all hooks - each hook needs to be a new instance
            // This allows hooks to be edited independently for each component placement
            foreach (var hook in Hooks)
            {
                if (hook != null)
                {
                    // Create new hook with copied values
                    // Position is a struct (Vector3), so it's copied by value
                    // Door reference stays the same (doors are shared within a kit)
                    var hookCopy = new KitComponentHook(
                        new Vector3(hook.Position.X, hook.Position.Y, hook.Position.Z),
                        hook.Rotation,
                        hook.Edge,
                        hook.Door
                    );
                    componentCopy.Hooks.Add(hookCopy);
                }
            }

            return componentCopy;
        }

        /// <summary>
        /// Helper method to deep copy a BWM walkmesh, ensuring all data is independently copied.
        /// </summary>
        /// <remarks>
        /// CRITICAL: MATERIAL PRESERVATION:
        ///
        /// The Material property MUST be explicitly copied: newFace.Material = face.Material
        ///
        /// If materials are not preserved during deep copying, faces that should be walkable will
        /// become non-walkable, causing the bug where "levels/modules are NOT walkable despite having
        /// the right surface material."
        ///
        /// This bug was fixed by ensuring Material is explicitly copied. The original implementation
        /// might have relied on default copying behavior, which could fail if the BWMFace constructor
        /// doesn't initialize Material properly.
        /// </remarks>
        /// <param name="original">The original BWM to copy</param>
        /// <returns>A new, independent BWM copy that can be transformed without affecting the original</returns>
        private BWM DeepCopyBwm(BWM original)
        {
            if (original == null)
            {
                return null;
            }

            var copy = new BWM();
            copy.WalkmeshType = original.WalkmeshType;
            copy.Position = original.Position;
            copy.RelativeHook1 = original.RelativeHook1;
            copy.RelativeHook2 = original.RelativeHook2;
            copy.AbsoluteHook1 = original.AbsoluteHook1;
            copy.AbsoluteHook2 = original.AbsoluteHook2;

            // Deep copy all faces
            // CRITICAL: Material MUST be explicitly copied to preserve walkability!
            foreach (var face in original.Faces)
            {
                var newFace = new BWMFace(face.V1, face.V2, face.V3);
                newFace.Material = face.Material;  // CRITICAL: Explicitly copy material
                newFace.Trans1 = face.Trans1;
                newFace.Trans2 = face.Trans2;
                newFace.Trans3 = face.Trans3;
                copy.Faces.Add(newFace);
            }

            return copy;
        }
    }

    /// <summary>
    /// Represents a connection point (hook) on a KitComponent where a door can be placed.
    /// Hooks are extracted from the walkmesh's perimeter edges that have transitions.
    /// </summary>
    /// <remarks>
    /// WHAT IS A KITCOMPONENTHOOK?
    ///
    /// A KitComponentHook is a connection point on a room component (KitComponent) where a door
    /// can be placed. When you place a door in the indoor map builder, it goes on one of these
    /// hooks. Hooks tell the game where doors should be positioned and how they should be rotated.
    ///
    /// WHERE DO HOOKS COME FROM?
    ///
    /// Hooks are extracted from the walkmesh's perimeter edges. A perimeter edge is an edge of
    /// a walkable triangle that doesn't have a neighboring triangle on one side - it forms the
    /// outer boundary of the walkable area. When a perimeter edge has a transition value (not -1),
    /// it means that edge is meant to connect to another room, so it becomes a hook.
    ///
    /// HOW ARE HOOKS EXTRACTED?
    ///
    /// The extraction process works like this:
    /// 1. Get all walkable faces from the component's BWM
    /// 2. For each walkable face, check its three edges
    /// 3. For each edge, check if it's a perimeter edge (no adjacent neighbor)
    /// 4. If it's a perimeter edge AND has a transition (not -1), create a hook
    /// 5. Calculate the hook's position (middle of the edge)
    /// 6. Calculate the hook's rotation (perpendicular to the edge, facing outward)
    /// 7. Store which edge this is (0, 1, or 2) and which door should be used
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// A KitComponentHook stores:
    /// 1. Position: The 3D position where the door should be placed (middle of the edge)
    ///    - This is calculated as: (vertex1 + vertex2) / 2
    ///    - The position is in the component's local coordinate system (centered at origin)
    ///
    /// 2. Rotation: The rotation angle (in degrees) for the door
    ///    - This is calculated as the angle perpendicular to the edge, facing outward
    ///    - The rotation tells which direction the door should face
    ///    - 0 degrees = facing positive X, 90 degrees = facing positive Y, etc.
    ///
    /// 3. Edge: The edge index (0, 1, or 2) of the triangle that contains this hook
    ///    - Edge 0: V1 -> V2
    ///    - Edge 1: V2 -> V3
    ///    - Edge 2: V3 -> V1
    ///    - This is used to identify which edge of the triangle the hook is on
    ///
    /// 4. Door: The KitDoor object that should be placed at this hook
    ///    - This tells what kind of door to use (width, height, UTD resources)
    ///    - Can be null if no specific door is assigned (uses default door)
    ///
    /// HOW ARE HOOKS USED?
    ///
    /// When you place a door in the indoor map builder:
    /// 1. The builder finds all hooks on the selected component
    /// 2. It displays them as connection points (usually shown as small markers)
    /// 3. When you click on a hook, it places a door at that hook's position and rotation
    /// 4. The door is positioned so it connects the current room to the room specified by the transition
    ///
    /// TRANSITIONS AND ROOM CONNECTIONS:
    ///
    /// The transition value on a perimeter edge tells which room this edge connects to. When
    /// the indoor map builder processes a room's walkmesh, it remaps transitions from dummy
    /// indices (from the kit component) to actual room indices (in the built module).
    ///
    /// For example:
    /// - Component has a hook with transition = 1 (dummy index meaning "next room")
    /// - When placed in a module, transition = 1 gets remapped to the actual room index (e.g., 5)
    /// - The door is placed at the hook's position, connecting room 0 to room 5
    ///
    /// WHY ARE HOOKS CENTERED AT THE ORIGIN?
    ///
    /// KitComponents are centered at the origin (0, 0, 0) so they can be easily positioned,
    /// rotated, and flipped. When a component is placed in a module, its hooks are transformed
    /// along with the component (translated, rotated, flipped) to their final positions.
    ///
    /// CRITICAL: HOOK EXTRACTION DEPENDS ON WALKMESH TYPE:
    ///
    /// Hooks are only extracted from walkable faces. If the walkmesh type is not set to AreaModel
    /// (WOK), or if materials are not preserved, hooks may not be extracted correctly. This is
    /// because:
    /// 1. Only walkable faces have perimeter edges that can become hooks
    /// 2. Non-walkable faces (walls, obstacles) don't have hooks
    /// 3. If materials are lost, faces become non-walkable, and hooks disappear
    ///
    /// ORIGINAL IMPLEMENTATION:
    ///
    /// Based on PyKotor's KitComponentHook class. Hooks are extracted from BWM perimeter edges
    /// with transitions. The original implementation calculates hook positions and rotations
    /// from the edge's vertex positions and the triangle's normal vector.
    ///
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:50-55
    /// Original: class KitComponentHook
    /// </remarks>
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:50
    // Original: class KitComponentHook:
    public class KitComponentHook
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:51-55
        // Original: def __init__(self, position: Vector3, rotation: float, edge: int, door: KitDoor):
        public KitComponentHook(Vector3 position, float rotation, int edge, KitDoor door)
        {
            Position = position;
            Rotation = rotation;
            Edge = edge;
            Door = door;
        }

        /// <summary>
        /// The 3D position where the door should be placed (middle of the perimeter edge).
        /// Position is in the component's local coordinate system (centered at origin).
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// The rotation angle (in degrees) for the door, perpendicular to the edge, facing outward.
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// The edge index (0, 1, or 2) of the triangle that contains this hook.
        /// Edge 0: V1 -> V2, Edge 1: V2 -> V3, Edge 2: V3 -> V1
        /// </summary>
        public int Edge { get; set; }

        /// <summary>
        /// The KitDoor object that should be placed at this hook (can be null for default door).
        /// </summary>
        public KitDoor Door { get; set; }
    }

    /// <summary>
    /// Represents a door template that can be placed at hooks in the indoor map builder.
    /// Doors connect rooms together and allow characters to move between them.
    /// </summary>
    /// <remarks>
    /// WHAT IS A KITDOOR?
    ///
    /// A KitDoor is a template for a door that can be placed in an indoor module. It defines what
    /// the door looks like (its 3D model), how big it is (width and height), and what resources
    /// it uses (UTD files). When you place a door at a hook, the builder uses the KitDoor's
    /// information to create the actual door in the module.
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// A KitDoor stores:
    /// 1. UtdK1: The first UTD (door template) resource for this door
    ///    - UTD files define the door's appearance, behavior, and properties
    ///    - This is the primary door resource
    ///
    /// 2. UtdK2: The second UTD resource for this door (optional, can be null)
    ///    - Some doors have two UTD files (one for each side)
    ///    - If null, only UtdK1 is used
    ///
    /// 3. Width: The width of the door in game units
    ///    - This determines how wide the door opening is
    ///    - Used to position the door correctly at the hook
    ///
    /// 4. Height: The height of the door in game units
    ///    - This determines how tall the door opening is
    ///    - Used to position the door correctly at the hook
    ///
    /// HOW ARE DOORS PLACED?
    ///
    /// When you place a door at a hook:
    /// 1. The builder gets the hook's position and rotation
    /// 2. It creates a door using the KitDoor's UTD resources
    /// 3. It positions the door at the hook's position
    /// 4. It rotates the door to match the hook's rotation
    /// 5. It sets the door's width and height from the KitDoor
    /// 6. It connects the door to the rooms specified by the hook's transition
    ///
    /// DOOR CONNECTIONS:
    ///
    /// Doors connect two rooms together. The hook's transition value tells which room the door
    /// connects to. When the door is placed:
    /// - The door is added to both rooms' door lists
    /// - The door's position is set to the hook's position (transformed to world coordinates)
    /// - The door's rotation is set to the hook's rotation (with component rotation applied)
    /// - The door's UTD resources are loaded from the KitDoor
    ///
    /// DEFAULT DOORS:
    ///
    /// If a hook doesn't have a specific KitDoor assigned (Door = null), the builder uses a
    /// default door. The default door is created when loading a module kit and has standard
    /// width and height values.
    ///
    /// ORIGINAL IMPLEMENTATION:
    ///
    /// Based on PyKotor's KitDoor class. Doors are defined in kit files and can be placed at
    /// hooks to connect rooms together.
    ///
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:58-63
    /// Original: class KitDoor
    /// </remarks>
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:58
    // Original: class KitDoor:
    public class KitDoor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:59-63
        // Original: def __init__(self, utdK1: UTD, utdK2: UTD, width: float, height: float):
        public KitDoor(UTD utdK1, UTD utdK2, float width, float height)
        {
            UtdK1 = utdK1;
            UtdK2 = utdK2;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// The first UTD (door template) resource for this door (primary door resource).
        /// </summary>
        public UTD UtdK1 { get; set; }

        /// <summary>
        /// The second UTD resource for this door (optional, can be null for single-sided doors).
        /// </summary>
        public UTD UtdK2 { get; set; }

        /// <summary>
        /// The width of the door in game units (determines door opening width).
        /// </summary>
        public float Width { get; set; }

        /// <summary>
        /// The height of the door in game units (determines door opening height).
        /// </summary>
        public float Height { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:66
    // Original: class MDLMDXTuple(NamedTuple):
    public class MDLMDXTuple
    {
        public MDLMDXTuple(byte[] mdl, byte[] mdx)
        {
            Mdl = mdl;
            Mdx = mdx;
        }

        public byte[] Mdl { get; set; }
        public byte[] Mdx { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:33-57
    // Original: class ModuleKit(Kit):
    public class ModuleKit : Kit
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:43-57
        // Original: def __init__(self, name: str, module_root: str, installation: HTInstallation):
        public ModuleKit(string name, string moduleRoot, HTInstallation installation) : base(name)
        {
            ModuleRoot = moduleRoot;
            _installation = installation;
            _loaded = false;
            IsModuleKit = true;
            SourceModule = moduleRoot;
        }

        public string ModuleRoot { get; set; }
        public bool IsModuleKit { get; set; }
        public string SourceModule { get; set; }
        private HTInstallation _installation;
        private bool _loaded;
        private Andastra.Parsing.Common.Module _module;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:59-74
        // Original: def ensure_loaded(self) -> bool:
        public bool EnsureLoaded()
        {
            if (_loaded)
            {
                return Components.Count > 0;
            }

            _loaded = true;
            try
            {
                _LoadModuleComponents();
                return Components.Count > 0;
            }
            catch (Exception ex)
            {
                new RobustLogger().Warning($"Failed to load module kit for '{ModuleRoot}': {ex.Message}");
                return false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:76-107
        // Original: def _load_module_components(self):
        private void _LoadModuleComponents()
        {
            // Load the module
            try
            {
                _module = new Andastra.Parsing.Common.Module(ModuleRoot, _installation.Installation, useDotMod: true);
            }
            catch (Exception ex)
            {
                new RobustLogger().Warning($"Module '{ModuleRoot}' failed to load: {ex.Message}");
                return;
            }

            // Get the LYT resource
            var lytResource = _module.Layout();
            if (lytResource == null)
            {
                new RobustLogger().Warning($"Module '{ModuleRoot}' has no LYT resource");
                return;
            }

            var lytData = lytResource.Resource() as LYT;
            if (lytData == null)
            {
                new RobustLogger().Warning($"Failed to load LYT data for module '{ModuleRoot}'");
                return;
            }

            // Create a default door for hooks
            var defaultDoor = _CreateDefaultDoor();
            Doors.Add(defaultDoor);

            // Extract rooms from LYT
            for (int roomIdx = 0; roomIdx < lytData.Rooms.Count; roomIdx++)
            {
                var lytRoom = lytData.Rooms[roomIdx];
                var component = _CreateComponentFromLytRoom(lytRoom, roomIdx, defaultDoor);
                if (component != null)
                {
                    Components.Add(component);
                }
            }

            // Also load any doorhooks from LYT as potential hook points
            _ProcessLytDoorhooks(lytData);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:109-114
        // Original: def _create_default_door(self) -> KitDoor:
        private KitDoor _CreateDefaultDoor()
        {
            var utd = new UTD();
            utd.ResRef.SetData("sw_door");
            utd.Tag = "module_door";
            return new KitDoor(utd, utd, 2.0f, 3.0f);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:116-201
        // Original: def _create_component_from_lyt_room(...) -> KitComponent | None:
        private KitComponent _CreateComponentFromLytRoom(LYTRoom lytRoom, int roomIdx, KitDoor defaultDoor)
        {
            string model = lytRoom.Model ?? "";
            string modelName = !string.IsNullOrEmpty(model) ? model.ToLowerInvariant() : $"room{roomIdx}";

            // Try to get the walkmesh (WOK) for this room
            BWM bwm = _GetRoomWalkmesh(modelName);
            // Ensure we always have a usable walkmesh with at least one face for
            // collision / snapping logic. Some modules ship with empty or missing
            // WOK data; in that case we fall back to a simple placeholder quad.
            if (bwm == null || bwm.Faces.Count == 0)
            {
                bwm = _CreatePlaceholderBwm(lytRoom.Position);
            }
            else
            {
                // Make a deep copy of the BWM so each component has its own instance
                // This prevents issues if multiple rooms share the same model name
                bwm = _DeepCopyBwm(bwm);
            }

            // CRITICAL FIX: Re-center the BWM around (0, 0)
            // Game WOKs are stored in world coordinates, but the Indoor Map Builder
            // expects BWMs centered at origin because:
            // - The preview image is drawn CENTERED at room.position
            // - The walkmesh is TRANSLATED by room.position from its original coords
            // Without re-centering, the image and walkmesh end up at different locations!
            bwm = _RecenterBwm(bwm);

            // Try to get the model data
            byte[] mdlData = _GetRoomModel(modelName);
            byte[] mdxData = _GetRoomModelExt(modelName);

            if (mdlData == null)
            {
                mdlData = new byte[0];
            }
            if (mdxData == null)
            {
                mdxData = new byte[0];
            }

            // Create a preview image from the walkmesh (now re-centered)
            // Each component gets its own image generated from its own BWM copy
            object image = _CreatePreviewImageFromBwm(bwm);

            // Create display name - include room index to ensure uniqueness
            // This helps distinguish rooms that share the same model name
            string displayName;
            if (!string.IsNullOrEmpty(lytRoom.Model))
            {
                displayName = $"{modelName.ToUpperInvariant()}_{roomIdx}";
            }
            else
            {
                displayName = $"ROOM{roomIdx}";
            }

            var component = new KitComponent(this, displayName, image, bwm, mdlData, mdxData);

            // Extract hooks from BWM edges with transitions (after recentering!)
            // This matches how kit.py extracts hooks in extract_kit()
            var doorhooks = _ExtractDoorhooksFromBwm(bwm, Doors.Count);

            // Create KitComponentHook objects from extracted doorhooks
            // Hook positions are in the recentered coordinate space (centered at 0,0)
            foreach (var doorhook in doorhooks)
            {
                var position = new Vector3(
                    Convert.ToSingle(doorhook["x"]),
                    Convert.ToSingle(doorhook["y"]),
                    Convert.ToSingle(doorhook["z"])
                );
                float rotation = (float)doorhook["rotation"];
                int doorIndex = (int)doorhook["door"];
                int edge = (int)doorhook["edge"];

                // Get the door for this hook (use default door if index is invalid)
                KitDoor door;
                if (doorIndex >= 0 && doorIndex < Doors.Count)
                {
                    door = Doors[doorIndex];
                }
                else
                {
                    door = defaultDoor;
                }

                var hook = new KitComponentHook(position, rotation, edge, door);
                component.Hooks.Add(hook);
            }

            return component;
        }

        /// <summary>
        /// Re-centers a walkmesh around the origin (0, 0, 0).
        /// This is CRITICAL for proper component placement in the indoor map builder.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method moves all vertices in the walkmesh so that the walkmesh's center point
        /// is at (0, 0, 0). The center is calculated as the midpoint between the minimum and
        /// maximum X, Y, and Z coordinates of all vertices.
        ///
        /// WHY THIS IS CRITICAL:
        ///
        /// Game walkmeshes (WOK files) are stored in "world coordinates" - they have absolute
        /// positions in the game world. For example, a room might have vertices at (100, 200, 0)
        /// to (200, 300, 0) in world space.
        ///
        /// However, the Indoor Map Builder expects components to be centered at (0, 0, 0) because:
        ///
        /// 1. PREVIEW IMAGE ALIGNMENT:
        ///    - The preview image is generated from the walkmesh and drawn CENTERED at the
        ///      component's position in the builder UI
        ///    - If the walkmesh isn't centered, the image and walkmesh won't align visually
        ///    - Users will see the image in one place but the walkmesh hitbox in another
        ///
        /// 2. POSITIONING LOGIC:
        ///    - When a component is placed at position (50, 50, 0), the walkmesh is translated
        ///      by that amount from its ORIGINAL coordinates
        ///    - If the walkmesh starts at (100, 200, 0), translating by (50, 50, 0) gives
        ///      (150, 250, 0) - which is NOT where the user expects it
        ///    - If the walkmesh is centered at (0, 0, 0), translating by (50, 50, 0) gives
        ///      (50, 50, 0) - which IS where the user expects it
        ///
        /// 3. TRANSFORMATION CONSISTENCY:
        ///    - Components can be rotated and flipped
        ///    - Rotations and flips are applied around the origin (0, 0, 0)
        ///    - If the walkmesh isn't centered, rotations/flips will move it unexpectedly
        ///
        /// HOW IT WORKS:
        ///
        /// 1. Get all vertices from the walkmesh
        /// 2. Find the minimum and maximum X, Y, Z values across all vertices
        /// 3. Calculate the center: center = (min + max) / 2
        /// 4. Translate all vertices by -center (move walkmesh so center is at origin)
        ///
        /// Example:
        /// - Vertices range from (100, 200, 0) to (200, 300, 0)
        /// - Center = ((100+200)/2, (200+300)/2, (0+0)/2) = (150, 250, 0)
        /// - Translate by (-150, -250, 0)
        /// - New vertices range from (-50, -50, 0) to (50, 50, 0)
        /// - Walkmesh is now centered at (0, 0, 0)
        ///
        /// BUG PREVENTION:
        ///
        /// Without this fix, the following bugs occur:
        /// - Preview images don't match walkmesh positions in the builder
        /// - Components appear in wrong locations when placed
        /// - Rotations/flips move components unexpectedly
        /// - Walkmesh hitboxes don't align with visual representation
        ///
        /// This method is called in _CreateComponentFromLytRoom() after loading the walkmesh
        /// from the game module, ensuring all components are properly centered before use.
        ///
        /// ORIGINAL IMPLEMENTATION:
        ///
        /// Based on PyKotor's _recenter_bwm() method. This fix addresses a critical alignment
        /// issue where game walkmeshes (in world coordinates) don't match the builder's expectations
        /// (centered coordinates).
        /// </remarks>
        /// <param name="bwm">The walkmesh to re-center</param>
        /// <returns>The re-centered walkmesh (same instance, modified in place)</returns>
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:203-243
        // Original: def _recenter_bwm(self, bwm: BWM) -> BWM:
        /// <summary>
        /// Re-centers a walkmesh so its center is at the origin (0, 0, 0).
        ///
        /// WHAT THIS FUNCTION DOES:
        ///
        /// This function takes a walkmesh and moves all its vertices so that the walkmesh's center
        /// is at the origin (0, 0, 0). This is important because when components are placed in the
        /// indoor map builder, they are positioned relative to their center point. If the walkmesh
        /// is not centered, the component will appear in the wrong position.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Find the Bounding Box
        /// - Get all vertices from the walkmesh
        /// - Find the smallest and largest X, Y, Z coordinates
        /// - This creates a box that contains all the vertices
        ///
        /// STEP 2: Calculate the Center
        /// - The center is the midpoint of the bounding box
        /// - centerX = (minX + maxX) / 2
        /// - centerY = (minY + maxY) / 2
        /// - centerZ = (minZ + maxZ) / 2
        ///
        /// STEP 3: Move the Walkmesh
        /// - Translate all vertices by (-centerX, -centerY, -centerZ)
        /// - This moves the walkmesh so its center is at (0, 0, 0)
        ///
        /// WHY THIS IS NEEDED:
        ///
        /// When a walkmesh is loaded from a game module, its vertices are in world coordinates.
        /// The walkmesh might be positioned anywhere in the world (e.g., at position 100, 200, 50).
        /// When we create a kit component from this walkmesh, we want it to be centered at the origin
        /// so that when we place it in the map builder, we can position it correctly by just moving
        /// it to the desired location.
        ///
        /// EXAMPLE:
        ///
        /// Before: Walkmesh vertices range from (95, 195, 45) to (105, 205, 55)
        /// - Center is at (100, 200, 50)
        /// - After translation: Vertices range from (-5, -5, -5) to (5, 5, 5)
        /// - Center is now at (0, 0, 0)
        ///
        /// EDGE CASES HANDLED:
        ///
        /// - Empty walkmesh: Returns unchanged (no vertices to process)
        /// </summary>
        /// <param name="bwm">The walkmesh to re-center</param>
        /// <returns>The re-centered walkmesh (same object, vertices modified)</returns>
        private BWM _RecenterBwm(BWM bwm)
        {
            var vertices = bwm.Vertices();
            if (vertices.Count == 0)
            {
                return bwm;
            }

            // Calculate current center
            // Find the bounding box of all vertices
            float minX = vertices.Min(v => v.X);
            float maxX = vertices.Max(v => v.X);
            float minY = vertices.Min(v => v.Y);
            float maxY = vertices.Max(v => v.Y);
            float minZ = vertices.Min(v => v.Z);
            float maxZ = vertices.Max(v => v.Z);

            // Calculate center point (midpoint of bounding box)
            float centerX = (minX + maxX) / 2.0f;
            float centerY = (minY + maxY) / 2.0f;
            float centerZ = (minZ + maxZ) / 2.0f;

            // Translate all vertices to center around origin
            // Use BWM.translate() which handles all vertices in faces
            // This moves the walkmesh so its center is at (0, 0, 0)
            bwm.Translate(-centerX, -centerY, -centerZ);

            return bwm;
        }

        /// <summary>
        /// Loads a room's walkmesh (WOK file) from the module.
        ///
        /// WHAT THIS FUNCTION DOES:
        ///
        /// This function loads a walkmesh file (WOK) from the module. Each room in a module has a
        /// corresponding WOK file that defines where characters can walk in that room.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Find the WOK Resource
        /// - Looks for a resource with the given model name and type WOK
        /// - WOK files are stored in the module's resource archive (RIM or MOD file)
        ///
        /// STEP 2: Read the Resource Data
        /// - Gets the raw bytes from the resource
        /// - If the resource doesn't exist or has no data, returns null
        ///
        /// STEP 3: Parse the BWM
        /// - Uses BWMAuto.ReadBwm to parse the raw bytes into a BWM object
        /// - If parsing fails (invalid data), logs a warning and returns null
        ///
        /// FALLBACK BEHAVIOR:
        ///
        /// If a room's walkmesh is missing or cannot be loaded, the caller should create a placeholder
        /// walkmesh using _CreatePlaceholderBwm. This ensures that every component has a walkmesh,
        /// even if the original is missing.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:245-270
        /// Original: def _get_room_walkmesh(self, model_name: str) -> BWM | None:
        /// </summary>
        /// <param name="modelName">The name of the room model (without extension)</param>
        /// <returns>The loaded BWM object, or null if the walkmesh could not be loaded</returns>
        private BWM _GetRoomWalkmesh(string modelName)
        {
            if (_module == null)
            {
                return null;
            }

            // Try to find the WOK resource
            var wokResource = _module.Resource(modelName, ResourceType.WOK);
            if (wokResource == null)
            {
                return null;
            }

            // Use reflection to call Data() method on ModuleResource<T>
            byte[] data = null;
            var dataMethod = wokResource.GetType().GetMethod("Data");
            if (dataMethod != null)
            {
                data = dataMethod.Invoke(wokResource, null) as byte[];
            }
            if (data == null)
            {
                return null;
            }

            try
            {
                var bwm = BWMAuto.ReadBwm(data);
                return bwm;
            }
            catch (Exception ex)
            {
                new RobustLogger().Warning($"Failed to read WOK for '{modelName}': {ex.Message}");
                return null;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:272-281
        // Original: def _get_room_model(self, model_name: str) -> bytes | None:
        private byte[] _GetRoomModel(string modelName)
        {
            if (_module == null)
            {
                return null;
            }

            var mdlResource = _module.Resource(modelName, ResourceType.MDL);
            if (mdlResource == null)
            {
                return null;
            }

            // Use reflection to call Data() method on ModuleResource<T>
            var dataMethod = mdlResource.GetType().GetMethod("Data");
            if (dataMethod != null)
            {
                return dataMethod.Invoke(mdlResource, null) as byte[];
            }
            return null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:283-292
        // Original: def _get_room_model_ext(self, model_name: str) -> bytes | None:
        private byte[] _GetRoomModelExt(string modelName)
        {
            if (_module == null)
            {
                return null;
            }

            var mdxResource = _module.Resource(modelName, ResourceType.MDX);
            if (mdxResource == null)
            {
                return null;
            }

            // Use reflection to call Data() method on ModuleResource<T>
            var dataMethod = mdxResource.GetType().GetMethod("Data");
            if (dataMethod != null)
            {
                return dataMethod.Invoke(mdxResource, null) as byte[];
            }
            return null;
        }

        /// <summary>
        /// Create a placeholder BWM with a single quad.
        ///
        /// This method is used as a fallback when a room's walkmesh (WOK) is missing or empty.
        /// It creates a simple 10x10 unit square walkmesh at the origin with Stone material,
        /// providing a minimal walkable surface for collision and snapping logic.
        ///
        /// The position parameter is provided for consistency with the PyKotor interface but
        /// is not used - the BWM is always created at the origin and positioning is handled
        /// by the caller via _RecenterBwm().
        /// </summary>
        /// <param name="position">Room position (unused - BWM is created at origin)</param>
        /// <returns>A new BWM with a single quad face</returns>
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:294-313
        // Original: def _create_placeholder_bwm(self, position: Vector3) -> BWM:
        private BWM _CreatePlaceholderBwm(Vector3 position)
        {
            var bwm = new BWM();

            // Create a 10x10 unit square walkmesh at origin
            float size = 5.0f;
            var v1 = new Vector3(-size, -size, 0);
            var v2 = new Vector3(size, -size, 0);
            var v3 = new Vector3(size, size, 0);
            var v4 = new Vector3(-size, size, 0);

            var face1 = new BWMFace(v1, v2, v3);
            face1.Material = SurfaceMaterial.Stone;
            var face2 = new BWMFace(v1, v3, v4);
            face2.Material = SurfaceMaterial.Stone;

            bwm.Faces.Add(face1);
            bwm.Faces.Add(face2);

            return bwm;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:315-405
        // Original: def _create_preview_image_from_bwm(self, bwm: BWM) -> QImage:
        private object _CreatePreviewImageFromBwm(BWM bwm)
        {
            // Collect all vertices to calculate bounding box
            var vertices = bwm.Vertices();
            if (vertices.Count == 0)
            {
                // Empty walkmesh - return blank image matching kit.py minimum size
                return new WriteableBitmap(new PixelSize(256, 256), new Vector2(96, 96), PixelFormat.Rgba8888);
            }

            // Calculate bounding box (same as kit.py)
            float minX = vertices.Min(v => v.X);
            float minY = vertices.Min(v => v.Y);
            float maxX = vertices.Max(v => v.X);
            float maxY = vertices.Max(v => v.Y);

            // Add padding (same as kit.py: 5.0 units)
            float padding = 5.0f;
            minX -= padding;
            minY -= padding;
            maxX += padding;
            maxY += padding;

            // Calculate image dimensions at 10 pixels per unit (matching kit.py exactly)
            const int PIXELS_PER_UNIT = 10;
            int width = (int)((maxX - minX) * PIXELS_PER_UNIT);
            int height = (int)((maxY - minY) * PIXELS_PER_UNIT);

            // Ensure minimum size (kit.py uses 256, not 100)
            width = Math.Max(width, 256);
            height = Math.Max(height, 256);

            // Create a WriteableBitmap with the calculated dimensions
            // For now, create a black image. Full walkmesh rendering will be implemented later.
            var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector2(96, 96), PixelFormat.Rgba8888);

            // TODO: STUB - Basic image creation implemented, but actual walkmesh rendering
            // (drawing faces with white/gray colors and mirroring) will be implemented
            // when full rendering infrastructure is available
            return bitmap;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py:1467-1535
        // Original: def _extract_doorhooks_from_bwm(bwm: BWM, num_doors: int) -> list[dict[str, float | int]]:
        private List<Dictionary<string, object>> _ExtractDoorhooksFromBwm(BWM bwm, int numDoors)
        {
            var doorhooks = new List<Dictionary<string, object>>();

            // Get all perimeter edges (these are the edges with transitions)
            var edges = bwm.Edges();

            // Process edges with valid transitions
            foreach (var edge in edges)
            {
                if (edge.Transition < 0) // Skip edges without transitions
                {
                    continue;
                }

                var face = edge.Face;
                // Get edge vertices based on local edge index (0, 1, or 2)
                // edge.index is the global edge index (face_index * 3 + local_edge_index)
                int faceIndex = edge.Index / 3;
                int localEdgeIndex = edge.Index % 3;

                // Get vertices for this edge
                Vector3 v1, v2;
                if (localEdgeIndex == 0)
                {
                    v1 = face.V1;
                    v2 = face.V2;
                }
                else if (localEdgeIndex == 1)
                {
                    v1 = face.V2;
                    v2 = face.V3;
                }
                else // localEdgeIndex == 2
                {
                    v1 = face.V3;
                    v2 = face.V1;
                }

                // Calculate midpoint of edge
                float midX = (v1.X + v2.X) / 2.0f;
                float midY = (v1.Y + v2.Y) / 2.0f;
                float midZ = (v1.Z + v2.Z) / 2.0f;

                // Calculate rotation (angle of edge in XY plane, in degrees)
                float dx = v2.X - v1.X;
                float dy = v2.Y - v1.Y;
                float rotation = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                // Normalize to 0-360
                rotation = rotation % 360.0f;
                if (rotation < 0)
                {
                    rotation += 360.0f;
                }

                // Map transition index to door index
                // Transition indices typically map directly to door indices, but clamp to valid range
                int doorIndex = numDoors > 0 ? Math.Min(edge.Transition, numDoors - 1) : 0;

                doorhooks.Add(new Dictionary<string, object>
                {
                    { "x", midX },
                    { "y", midY },
                    { "z", midZ },
                    { "rotation", rotation },
                    { "door", doorIndex },
                    { "edge", edge.Index }
                });
            }

            return doorhooks;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:407-417
        // Original: def _process_lyt_doorhooks(self, lyt_data: LYT):
        private void _ProcessLytDoorhooks(LYT lytData)
        {
            // LYT doorhooks contain information about where doors connect rooms
            // This implementation matches doorhooks from LYT to their corresponding components
            // and creates KitComponentHook objects for proper door placement

            if (lytData == null || lytData.Doorhooks == null || lytData.Doorhooks.Count == 0)
            {
                return;
            }

            if (lytData.Rooms == null || lytData.Rooms.Count == 0)
            {
                return;
            }

            // Create a mapping from room model names (case-insensitive) to components and their positions
            // This allows us to quickly find the component that matches each doorhook's room name
            var roomToComponentMap = new Dictionary<string, Tuple<KitComponent, Vector3>>();
            for (int i = 0; i < lytData.Rooms.Count && i < Components.Count; i++)
            {
                var lytRoom = lytData.Rooms[i];
                var component = Components[i];

                // Use the room model name as the key (case-insensitive)
                string modelName = lytRoom.Model ?? "";
                string roomKey = !string.IsNullOrEmpty(modelName)
                    ? modelName.ToLowerInvariant()
                    : $"room{i}";

                // Store component and its world position
                roomToComponentMap[roomKey] = new Tuple<KitComponent, Vector3>(component, lytRoom.Position);
            }

            // Get the default door for hooks that don't specify a door
            KitDoor defaultDoor = Doors.Count > 0 ? Doors[0] : _CreateDefaultDoor();
            if (Doors.Count == 0)
            {
                Doors.Add(defaultDoor);
            }

            // Process each doorhook from the LYT
            foreach (var doorhook in lytData.Doorhooks)
            {
                if (doorhook == null)
                {
                    continue;
                }

                // Find the component that matches this doorhook's room name (case-insensitive)
                string hookRoomName = doorhook.Room ?? "";
                string hookRoomKey = !string.IsNullOrEmpty(hookRoomName)
                    ? hookRoomName.ToLowerInvariant()
                    : null;

                if (hookRoomKey == null || !roomToComponentMap.ContainsKey(hookRoomKey))
                {
                    // Room not found - skip this doorhook
                    continue;
                }

                var componentData = roomToComponentMap[hookRoomKey];
                var component = componentData.Item1;
                var roomPosition = componentData.Item2;

                // Convert doorhook world position to component-local position
                // Components are centered at origin, so we subtract the room's world position
                Vector3 localPosition = new Vector3(
                    doorhook.Position.X - roomPosition.X,
                    doorhook.Position.Y - roomPosition.Y,
                    doorhook.Position.Z - roomPosition.Z
                );

                // Convert quaternion orientation to rotation angle (yaw in degrees)
                // Doors rotate around the Y-axis, so we extract the yaw from the quaternion
                // Convert Vector4 to Quaternion
                Quaternion quaternion = new Quaternion(
                    doorhook.Orientation.X,
                    doorhook.Orientation.Y,
                    doorhook.Orientation.Z,
                    doorhook.Orientation.W);
                float rotation = _QuaternionToYawRotation(quaternion);

                // Find the closest edge in the component's BWM to the hook position
                // This determines which edge the hook should be associated with
                int edgeIndex = _FindClosestEdge(component.Bwm, localPosition);

                // Create the KitComponentHook and add it to the component
                // Note: We use the default door since LYT doorhooks don't specify which door to use
                var hook = new KitComponentHook(localPosition, rotation, edgeIndex, defaultDoor);
                component.Hooks.Add(hook);
            }
        }

        /// <summary>
        /// Converts a quaternion to a yaw rotation angle in degrees.
        /// Doors rotate around the Y-axis, so we extract the yaw component from the Euler angles.
        /// </summary>
        /// <param name="quaternion">The quaternion orientation (x, y, z, w)</param>
        /// <returns>Rotation angle in degrees (0-360)</returns>
        private float _QuaternionToYawRotation(Quaternion quaternion)
        {
            // Convert quaternion to Euler angles (roll, pitch, yaw)
            // Based on PyKotor's Vector4.to_euler() implementation
            // Reference: vendor/PyKotor/Libraries/PyKotor/src/utility/common/geometry.py:956-989

            float qx = quaternion.X;
            float qy = quaternion.Y;
            float qz = quaternion.Z;
            float qw = quaternion.W;

            // Calculate yaw (rotation around Y-axis)
            // Formula from PyKotor: yaw = atan2(2*(w*z + x*y), 1 - 2*(y² + z²))
            float t3 = 2.0f * (qw * qz + qx * qy);
            float t4 = 1.0f - 2.0f * (qy * qy + qz * qz);
            float yawRadians = (float)Math.Atan2(t3, t4);

            // Convert to degrees
            float yawDegrees = yawRadians * 180.0f / (float)Math.PI;

            // Normalize to 0-360 range
            yawDegrees = yawDegrees % 360.0f;
            if (yawDegrees < 0)
            {
                yawDegrees += 360.0f;
            }

            return yawDegrees;
        }

        /// <summary>
        /// Finds the closest edge in a BWM to a given position.
        /// Returns the edge index of the closest edge, or -1 if no edges are found.
        /// </summary>
        /// <param name="bwm">The walkmesh to search</param>
        /// <param name="position">The position to find the closest edge to (in component-local coordinates)</param>
        /// <returns>The index of the closest edge, or -1 if no edges found</returns>
        private int _FindClosestEdge(BWM bwm, Vector3 position)
        {
            if (bwm == null || bwm.Faces.Count == 0)
            {
                return -1;
            }

            // Get all perimeter edges from the BWM
            var edges = bwm.Edges();
            if (edges == null || edges.Count == 0)
            {
                return -1;
            }

            float minDistance = float.MaxValue;
            int closestEdgeIndex = -1;

            // Search through all edges to find the one closest to the position
            foreach (var edge in edges)
            {
                if (edge == null || edge.Face == null)
                {
                    continue;
                }

                // Get the edge vertices
                int faceIndex = edge.Index / 3;
                int localEdgeIndex = edge.Index % 3;

                if (faceIndex < 0 || faceIndex >= bwm.Faces.Count)
                {
                    continue;
                }

                var face = bwm.Faces[faceIndex];
                Vector3 v1, v2;

                // Get vertices for this edge based on local edge index
                if (localEdgeIndex == 0)
                {
                    v1 = face.V1;
                    v2 = face.V2;
                }
                else if (localEdgeIndex == 1)
                {
                    v1 = face.V2;
                    v2 = face.V3;
                }
                else // localEdgeIndex == 2
                {
                    v1 = face.V3;
                    v2 = face.V1;
                }

                // Calculate the midpoint of the edge
                Vector3 edgeMidpoint = new Vector3(
                    (v1.X + v2.X) / 2.0f,
                    (v1.Y + v2.Y) / 2.0f,
                    (v1.Z + v2.Z) / 2.0f
                );

                // Calculate distance from position to edge midpoint (ignoring Z for 2D distance)
                // Door hooks are primarily concerned with X/Y positioning
                float dx = position.X - edgeMidpoint.X;
                float dy = position.Y - edgeMidpoint.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                // Update closest edge if this one is closer
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestEdgeIndex = edge.Index;
                }
            }

            return closestEdgeIndex;
        }

        // Helper method to deep copy a BWM
        private BWM _DeepCopyBwm(BWM original)
        {
            var copy = new BWM();
            copy.WalkmeshType = original.WalkmeshType;
            copy.Position = original.Position;
            copy.RelativeHook1 = original.RelativeHook1;
            copy.RelativeHook2 = original.RelativeHook2;
            copy.AbsoluteHook1 = original.AbsoluteHook1;
            copy.AbsoluteHook2 = original.AbsoluteHook2;

            foreach (var face in original.Faces)
            {
                var newFace = new BWMFace(face.V1, face.V2, face.V3);
                newFace.Material = face.Material;
                newFace.Trans1 = face.Trans1;
                newFace.Trans2 = face.Trans2;
                newFace.Trans3 = face.Trans3;
                copy.Faces.Add(newFace);
            }

            return copy;
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:419-503
    // Original: class ModuleKitManager:
    public class ModuleKitManager
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:426-429
        // Original: def __init__(self, installation: HTInstallation):
        public ModuleKitManager(HTInstallation installation)
        {
            _installation = installation;
            _cache = new Dictionary<string, ModuleKit>();
            _moduleNames = null;
        }

        private HTInstallation _installation;
        private Dictionary<string, ModuleKit> _cache;
        private Dictionary<string, string> _moduleNames;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:431-441
        // Original: def get_module_names(self) -> dict[str, str | None]:
        public Dictionary<string, string> GetModuleNames()
        {
            if (_moduleNames == null)
            {
                _moduleNames = _installation.ModuleNames();
            }
            return _moduleNames;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:443-458
        // Original: def get_module_roots(self) -> list[str]:
        public List<string> GetModuleRoots()
        {
            var seenRoots = new HashSet<string>();
            var roots = new List<string>();

            var moduleNames = GetModuleNames();
            foreach (var moduleFilename in moduleNames.Keys)
            {
                string root = Andastra.Parsing.Installation.Installation.GetModuleRoot(moduleFilename);
                if (!seenRoots.Contains(root))
                {
                    seenRoots.Add(root);
                    roots.Add(root);
                }
            }

            roots.Sort();
            return roots;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:460-479
        // Original: def get_module_display_name(self, module_root: str) -> str:
        public string GetModuleDisplayName(string moduleRoot)
        {
            var moduleNames = GetModuleNames();

            // Try to find the display name from various extensions
            string[] extensions = { ".rim", ".mod", "_s.rim" };
            foreach (var ext in extensions)
            {
                string filename = moduleRoot + ext;
                if (moduleNames.ContainsKey(filename))
                {
                    string areaName = moduleNames[filename];
                    if (!string.IsNullOrEmpty(areaName) && areaName != "<Unknown Area>")
                    {
                        return $"{moduleRoot.ToUpperInvariant()} - {areaName}";
                    }
                }
            }

            return moduleRoot.ToUpperInvariant();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:481-498
        // Original: def get_module_kit(self, module_root: str) -> ModuleKit:
        public ModuleKit GetModuleKit(string moduleRoot)
        {
            if (!_cache.ContainsKey(moduleRoot))
            {
                string displayName = GetModuleDisplayName(moduleRoot);
                var kit = new ModuleKit(displayName, moduleRoot, _installation);
                _cache[moduleRoot] = kit;
            }

            return _cache[moduleRoot];
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:500-503
        // Original: def clear_cache(self):
        public void ClearCache()
        {
            _cache.Clear();
            _moduleNames = null;
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:71-180
    // Original: def load_kits(path: os.PathLike | str) -> list[Kit]:
    public static class KitLoader
    {
        public static List<Kit> LoadKits(string path)
        {
            var kits = new List<Kit>();

            var kitsPath = new DirectoryInfo(path);
            if (!kitsPath.Exists)
            {
                kitsPath.Create();
            }

            foreach (var file in kitsPath.GetFiles("*.json"))
            {
                try
                {
                    string jsonContent = File.ReadAllText(file.FullName);
                    var kitJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonContent);
                    string kitName = kitJson["name"].ToString();
                    var kit = new Kit(kitName);
                    string kitIdentifier = kitJson["id"].ToString();

                    // Load always resources
                    var alwaysPath = Path.Combine(kitsPath.FullName, kitIdentifier, "always");
                    if (Directory.Exists(alwaysPath))
                    {
                        foreach (var alwaysFile in new DirectoryInfo(alwaysPath).GetFiles())
                        {
                            kit.Always[alwaysFile.FullName] = File.ReadAllBytes(alwaysFile.FullName);
                        }
                    }

                    // Load textures
                    var texturesPath = Path.Combine(kitsPath.FullName, kitIdentifier, "textures");
                    if (Directory.Exists(texturesPath))
                    {
                        foreach (var textureFile in new DirectoryInfo(texturesPath).GetFiles("*.tga"))
                        {
                            string texture = textureFile.Name.Replace(".tga", "").ToUpperInvariant();
                            kit.Textures[texture] = File.ReadAllBytes(textureFile.FullName);
                            var txiPath = Path.Combine(texturesPath, $"{texture}.txi");
                            kit.Txis[texture] = File.Exists(txiPath) ? File.ReadAllBytes(txiPath) : new byte[0];
                        }
                    }

                    // Load lightmaps
                    var lightmapsPath = Path.Combine(kitsPath.FullName, kitIdentifier, "lightmaps");
                    if (Directory.Exists(lightmapsPath))
                    {
                        foreach (var lightmapFile in new DirectoryInfo(lightmapsPath).GetFiles("*.tga"))
                        {
                            string lightmap = lightmapFile.Name.Replace(".tga", "").ToUpperInvariant();
                            kit.Lightmaps[lightmap] = File.ReadAllBytes(lightmapFile.FullName);
                            var txiPath = Path.Combine(lightmapsPath, $"{lightmap}.txi");
                            kit.Txis[lightmap] = File.Exists(txiPath) ? File.ReadAllBytes(txiPath) : new byte[0];
                        }
                    }

                    // Load skyboxes
                    var skyboxesPath = Path.Combine(kitsPath.FullName, kitIdentifier, "skyboxes");
                    if (Directory.Exists(skyboxesPath))
                    {
                        foreach (var skyboxFile in new DirectoryInfo(skyboxesPath).GetFiles("*.mdl"))
                        {
                            string skyboxResref = skyboxFile.Name.Replace(".mdl", "").ToUpperInvariant();
                            var mdxPath = Path.Combine(skyboxesPath, $"{skyboxResref}.mdx");
                            byte[] mdl = File.ReadAllBytes(skyboxFile.FullName);
                            byte[] mdx = File.Exists(mdxPath) ? File.ReadAllBytes(mdxPath) : new byte[0];
                            kit.Skyboxes[skyboxResref] = new MDLMDXTuple(mdl, mdx);
                        }
                    }

                    // Load doorway padding
                    var doorwayPath = Path.Combine(kitsPath.FullName, kitIdentifier, "doorway");
                    if (Directory.Exists(doorwayPath))
                    {
                        foreach (var paddingFile in new DirectoryInfo(doorwayPath).GetFiles("*.mdl"))
                        {
                            string paddingId = paddingFile.Name.Replace(".mdl", "");
                            var mdxPath = Path.Combine(doorwayPath, $"{paddingId}.mdx");
                            byte[] mdl = File.ReadAllBytes(paddingFile.FullName);
                            byte[] mdx = File.Exists(mdxPath) ? File.ReadAllBytes(mdxPath) : new byte[0];

                            // Extract door ID and padding size from filename (e.g., "side0_100.mdl" -> doorId=0, paddingSize=100)
                            var nums = GetNums(paddingId);
                            if (nums.Count >= 2)
                            {
                                int doorId = nums[0];
                                int paddingSize = nums[1];

                                if (paddingId.ToLowerInvariant().StartsWith("side"))
                                {
                                    if (!kit.SidePadding.ContainsKey(doorId))
                                    {
                                        kit.SidePadding[doorId] = new Dictionary<int, MDLMDXTuple>();
                                    }
                                    kit.SidePadding[doorId][paddingSize] = new MDLMDXTuple(mdl, mdx);
                                }
                                if (paddingId.ToLowerInvariant().StartsWith("top"))
                                {
                                    if (!kit.TopPadding.ContainsKey(doorId))
                                    {
                                        kit.TopPadding[doorId] = new Dictionary<int, MDLMDXTuple>();
                                    }
                                    kit.TopPadding[doorId][paddingSize] = new MDLMDXTuple(mdl, mdx);
                                }
                            }
                        }
                    }

                    // Load doors
                    var doorsArray = (JArray)kitJson["doors"];
                    foreach (var doorJson in doorsArray)
                    {
                        var doorDict = (JObject)doorJson;
                        string utdK1Path = Path.Combine(kitsPath.FullName, kitIdentifier, $"{doorDict["utd_k1"]}.utd");
                        string utdK2Path = Path.Combine(kitsPath.FullName, kitIdentifier, $"{doorDict["utd_k2"]}.utd");
                        var utdK1 = ResourceAutoHelpers.ReadUtd(File.ReadAllBytes(utdK1Path));
                        var utdK2 = ResourceAutoHelpers.ReadUtd(File.ReadAllBytes(utdK2Path));
                        float width = Convert.ToSingle(doorDict["width"]);
                        float height = Convert.ToSingle(doorDict["height"]);
                        var door = new KitDoor(utdK1, utdK2, width, height);
                        kit.Doors.Add(door);
                    }

                    // Load components
                    var componentsArray = (JArray)kitJson["components"];
                    foreach (var componentJson in componentsArray)
                    {
                        var componentDict = (JObject)componentJson;
                        string name = componentDict["name"].ToString();
                        string componentIdentifier = componentDict["id"].ToString();

                        // Load component image (PNG)
                        string imagePath = Path.Combine(kitsPath.FullName, kitIdentifier, $"{componentIdentifier}.png");
                        object image = null;
                        if (File.Exists(imagePath))
                        {
                            try
                            {
                                // Load PNG image using Avalonia Bitmap (matching PyKotor: QImage(str(png_path)))
                                Bitmap originalBitmap;
                                using (var fileStream = File.OpenRead(imagePath))
                                {
                                    originalBitmap = new Bitmap(fileStream);
                                }

                                // Mirror the image horizontally (matching PyKotor: .mirrored())
                                // PyKotor's QImage.mirrored() flips horizontally by default
                                // We use RenderTargetBitmap with a transformation matrix to mirror
                                var pixelSize = originalBitmap.PixelSize;
                                var mirroredBitmap = new RenderTargetBitmap(
                                    new PixelSize(pixelSize.Width, pixelSize.Height),
                                    new Avalonia.Vector(96, 96));

                                using (var context = mirroredBitmap.CreateDrawingContext())
                                {
                                    // Create transformation matrix to mirror horizontally
                                    // Matching PyKotor: QImage.mirrored() flips horizontally (mirrored(True, False))
                                    // For horizontal mirroring: scale X by -1 (flips around origin), then translate to correct position
                                    // Matrix multiplication applies right-to-left, so we want: translate * scale
                                    // This means: first scale (flip), then translate (move back)
                                    var transform = Matrix.CreateTranslation(pixelSize.Width, 0.0) *
                                                   Matrix.CreateScale(-1.0, 1.0);
                                    using (context.PushTransform(transform))
                                    {
                                        // Draw the original image with the transformation applied
                                        context.DrawImage(originalBitmap, new Rect(0, 0, pixelSize.Width, pixelSize.Height));
                                    }
                                }

                                // Convert RenderTargetBitmap to Bitmap for storage
                                // We need to save and reload to get a proper Bitmap instance
                                using (var memoryStream = new MemoryStream())
                                {
                                    mirroredBitmap.Save(memoryStream);
                                    memoryStream.Position = 0;
                                    image = new Bitmap(memoryStream);
                                }

                                // Dispose the intermediate bitmaps
                                originalBitmap.Dispose();
                                mirroredBitmap.Dispose();
                            }
                            catch (Exception ex)
                            {
                                // If image loading fails, log warning and continue without image
                                // This matches PyKotor behavior which adds to missing_files on exception
                                new RobustLogger().Warning($"Failed to load component image '{imagePath}': {ex.Message}");
                                image = null;
                            }
                        }

                        // Load BWM
                        string bwmPath = Path.Combine(kitsPath.FullName, kitIdentifier, $"{componentIdentifier}.wok");
                        var bwm = BWMAuto.ReadBwm(File.ReadAllBytes(bwmPath));

                        // Load MDL/MDX
                        string mdlPath = Path.Combine(kitsPath.FullName, kitIdentifier, $"{componentIdentifier}.mdl");
                        string mdxPath = Path.Combine(kitsPath.FullName, kitIdentifier, $"{componentIdentifier}.mdx");
                        byte[] mdl = File.ReadAllBytes(mdlPath);
                        byte[] mdx = File.Exists(mdxPath) ? File.ReadAllBytes(mdxPath) : new byte[0];

                        var component = new KitComponent(kit, name, image, bwm, mdl, mdx);

                        // Load doorhooks
                        var doorhooksArray = (JArray)componentDict["doorhooks"];
                        foreach (var hookJson in doorhooksArray)
                        {
                            var hookDict = (JObject)hookJson;
                            var position = new Vector3(
                                Convert.ToSingle(hookDict["x"]),
                                Convert.ToSingle(hookDict["y"]),
                                Convert.ToSingle(hookDict["z"])
                            );
                            float rotation = Convert.ToSingle(hookDict["rotation"]);
                            int doorIndex = Convert.ToInt32(hookDict["door"]);
                            var door = kit.Doors[doorIndex];
                            int edge = Convert.ToInt32(hookDict["edge"]);
                            var hook = new KitComponentHook(position, rotation, edge, door);
                            component.Hooks.Add(hook);
                        }

                        kit.Components.Add(component);
                    }

                    kits.Add(kit);
                }
                catch (Exception ex)
                {
                    new RobustLogger().Warning($"Failed to load kit from '{file.FullName}': {ex.Message}");
                }
            }

            return kits;
        }

        // Helper method to extract numbers from a string (matching PyKotor get_nums utility)
        private static List<int> GetNums(string text)
        {
            var nums = new List<int>();
            var currentNum = new System.Text.StringBuilder();
            foreach (char c in text)
            {
                if (char.IsDigit(c))
                {
                    currentNum.Append(c);
                }
                else
                {
                    if (currentNum.Length > 0)
                    {
                        if (int.TryParse(currentNum.ToString(), out int num))
                        {
                            nums.Add(num);
                        }
                        currentNum.Clear();
                    }
                }
            }
            if (currentNum.Length > 0)
            {
                if (int.TryParse(currentNum.ToString(), out int num))
                {
                    nums.Add(num);
                }
            }
            return nums;
        }
    }
}

