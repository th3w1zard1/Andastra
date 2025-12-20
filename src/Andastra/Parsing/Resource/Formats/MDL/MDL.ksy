meta:
  id: mdl
  file-extension: mdl
  endian: le
  encoding: ASCII
  ks-version: 0.11
  license: MIT
  title: BioWare MDL (Model) Binary Format
doc: |
  BioWare MDL Model Format

  The MDL file contains:
  - File header (12 0xC bytes)
  - Geometry header (80 bytes)
  - Model header (92 bytes)
  - Names header (28 bytes)
  - Node name arrays
  - Animation headers and nodes
  - Node hierarchy with geometry data

  Reference implementations:
  - vendor/PyKotor/vendor/MDLOps/MDLOpsM.pm
  - vendor/PyKotor/wiki/MDL-MDX-File-Format.md
doc-ref: https://github.com/th3w1zard1/PyKotor/wiki/MDL-MDX-File-Format.md

seq:
  - id: file_header
    type: file_header
  - id: geometry_header
    type: geometry_header
  - id: model_header
    type: model_header
  - id: names_header
    type: names_header
  - id: name_indexes
    type: u4
    repeat: expr
    repeat-expr: names_header.name_count
    if: names_header.name_count > 0
  - id: names_data
    type: name_strings
    if: names_header.name_count > 0
    size: _parent.model_header.animation_root_offset - (names_header.name_indexes_offset + (4 * names_header.name_count))
  - id: animations
    type: animation_header
    repeat: expr
    repeat-expr: model_header.animation_count
  - id: root_node
    type: node
    pos: geometry_header.root_node_offset
    if: geometry_header.root_node_offset > 0

types:
  file_header:
    doc: MDL file header (12 bytes)
    seq:
      - id: unused
        type: u4
        doc: Always 0
      - id: mdl_size
        type: u4
        doc: Size of MDL file in bytes
      - id: mdx_size
        type: u4
        doc: Size of MDX file in bytes

  geometry_header:
    doc: Geometry header (80 bytes) - Located at offset 12
    seq:
      - id: function_pointer_0
        type: u4
        doc: |
          Game engine version identifier:
          - KOTOR 1 PC: 4273776 (0x413750)
          - KOTOR 2 PC: 4285200 (0x416610)
          - KOTOR 1 Xbox: 4254992 (0x40EE90)
          - KOTOR 2 Xbox: 4285872 (0x416950)
      - id: function_pointer_1
        type: u4
        doc: Function pointer to ASCII model parser
      - id: model_name
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Model name (null-terminated string, max 32 bytes)
      - id: root_node_offset
        type: u4
        doc: Offset to root node structure (relative to MDL data start, offset 12)
      - id: node_count
        type: u4
        doc: Total number of nodes in model hierarchy
      - id: unknown_array_1
        type: array_definition
        doc: Unknown array definition (offset, count, count duplicate)
      - id: unknown_array_2
        type: array_definition
        doc: Unknown array definition (offset, count, count duplicate)
      - id: reference_count
        type: u4
        doc: Reference count (initialized to 0, incremented when model is referenced)
      - id: geometry_type
        type: u1
        doc: |
          Geometry type:
          - 0x01: Basic geometry header (not in models)
          - 0x02: Model geometry header
          - 0x05: Animation geometry header
          If bit 7 (0x80) is set, model is compiled binary with absolute addresses
      - id: padding
        type: u1
        size: 3
        doc: Padding bytes for alignment
    instances:
      is_kotor2:
        value: function_pointer_0 == 4285200 || function_pointer_0 == 4285872
        doc: True if this is a KOTOR 2 model

  array_definition:
    doc: Array definition structure (offset, count, count duplicate)
    seq:
      - id: offset
        type: s4
        doc: Offset to array (relative to MDL data start, offset 12)
      - id: count
        type: u4
        doc: Number of used entries
      - id: count_duplicate
        type: u4
        doc: Duplicate of count (allocated entries)

  model_header:
    doc: Model header (92 bytes) - Located at offset 92
    seq:
      - id: classification
        type: u1
        doc: |
          Model classification:
          - 0x00: Other
          - 0x01: Effect
          - 0x02: Tile
          - 0x04: Character
          - 0x08: Door
          - 0x10: Lightsaber
          - 0x20: Placeable
          - 0x40: Flyer
      - id: subclassification
        type: u1
        doc: Model subclassification value
      - id: unknown
        type: u1
        doc: Purpose unknown (possibly smoothing-related)
      - id: affected_by_fog
        type: u1
        doc: 0 = Not affected by fog, 1 = Affected by fog
      - id: child_model_count
        type: u4
        doc: Number of child models
      - id: animation_array_offset
        type: u4
        doc: Offset to animation array (relative to MDL data start, offset 12)
      - id: animation_count
        type: u4
        doc: Number of animations
      - id: animation_count_duplicate
        type: u4
        doc: Duplicate value of animation count
      - id: parent_model_pointer
        type: u4
        doc: Pointer to parent model (context-dependent)
      - id: bounding_box_min
        type: vec3f
        doc: Minimum coordinates of bounding box (X, Y, Z)
      - id: bounding_box_max
        type: vec3f
        doc: Maximum coordinates of bounding box (X, Y, Z)
      - id: radius
        type: f4
        doc: Radius of model's bounding sphere
      - id: animation_scale
        type: f4
        doc: Scale factor for animations (typically 1.0)
      - id: supermodel_name
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Name of supermodel (null-terminated string, "null" if empty)

  vec3f:
    doc: 3D vector (3 floats)
    seq:
      - id: x
        type: f4
      - id: y
        type: f4
      - id: z
        type: f4

  quaternion:
    doc: Quaternion rotation (4 floats W, X, Y, Z)
    seq:
      - id: w
        type: f4
      - id: x
        type: f4
      - id: y
        type: f4
      - id: z
        type: f4

  names_header:
    doc: Names header (28 bytes) - Located at offset 180
    seq:
      - id: root_node_offset
        type: u4
        doc: Offset to root node (often duplicate of geometry header value)
      - id: unknown_padding
        type: u4
        doc: Unknown field, typically unused or padding
      - id: mdx_data_size
        type: u4
        doc: Size of MDX file data in bytes
      - id: mdx_data_offset
        type: u4
        doc: Offset to MDX data within MDX file (typically 0)
      - id: names_array_offset
        type: u4
        doc: Offset to array of name string offsets
      - id: name_count
        type: u4
        doc: Number of node names in array
      - id: name_count_duplicate
        type: u4
        doc: Duplicate value of name count

  name_strings:
    doc: Array of null-terminated name strings
    seq:
      - id: strings
        type: str
        repeat: eos
        encoding: ASCII
        terminator: 0

  animation_header:
    doc: Animation header (136 bytes = 80 byte geometry header + 56 byte animation header)
    seq:
      - id: geo_header
        type: geometry_header
        doc: Standard 80-byte geometry header (geometry_type = 0x01 for animation)
      - id: animation_length
        type: f4
        doc: Duration of animation in seconds
      - id: transition_time
        type: f4
        doc: Transition/blend time to this animation in seconds
      - id: animation_root
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Root node name for animation (null-terminated string)
      - id: event_array_offset
        type: u4
        doc: Offset to animation events array
      - id: event_count
        type: u4
        doc: Number of animation events
      - id: event_count_duplicate
        type: u4
        doc: Duplicate value of event count
      - id: unknown
        type: u4
        doc: Purpose unknown

  animation_event:
    doc: Animation event (36 bytes)
    seq:
      - id: activation_time
        type: f4
        doc: Time in seconds when event triggers during animation playback
      - id: event_name
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Name of event (null-terminated string, e.g., "detonate")

  node:
    doc: Node structure - starts with 80-byte header, followed by type-specific sub-header
    seq:
      - id: header
        type: node_header
      - id: light_sub_header
        type: light_header
        if: header.node_type == 3
      - id: emitter_sub_header
        type: emitter_header
        if: header.node_type == 5
      - id: reference_sub_header
        type: reference_header
        if: header.node_type == 17
      - id: trimesh_sub_header
        type: trimesh_header
        if: header.node_type == 33
      - id: skinmesh_sub_header
        type: skinmesh_header
        if: header.node_type == 97
      - id: animmesh_sub_header
        type: animmesh_header
        if: header.node_type == 161
      - id: danglymesh_sub_header
        type: danglymesh_header
        if: header.node_type == 289
      - id: aabb_sub_header
        type: aabb_header
        if: header.node_type == 545
      - id: lightsaber_sub_header
        type: lightsaber_header
        if: header.node_type == 2081

  node_header:
    doc: Node header (80 bytes) - present in all node types
    seq:
      - id: node_type
        type: u2
        doc: |
          Bitmask indicating node features:
          - 0x0001: NODE_HAS_HEADER
          - 0x0002: NODE_HAS_LIGHT
          - 0x0004: NODE_HAS_EMITTER
          - 0x0008: NODE_HAS_CAMERA
          - 0x0010: NODE_HAS_REFERENCE
          - 0x0020: NODE_HAS_MESH
          - 0x0040: NODE_HAS_SKIN
          - 0x0080: NODE_HAS_ANIM
          - 0x0100: NODE_HAS_DANGLY
          - 0x0200: NODE_HAS_AABB
          - 0x0800: NODE_HAS_SABER
      - id: node_index
        type: u2
        doc: Sequential index of this node in the model
      - id: node_name_index
        type: u2
        doc: Index into names array for this node's name
      - id: padding
        type: u2
        doc: Padding for alignment
      - id: root_node_offset
        type: u4
        doc: Offset to model's root node
      - id: parent_node_offset
        type: u4
        doc: Offset to this node's parent node (0 if root)
      - id: position
        type: vec3f
        doc: Node position in local space (X, Y, Z)
      - id: orientation
        type: quaternion
        doc: Node orientation as quaternion (W, X, Y, Z)
      - id: child_array_offset
        type: u4
        doc: Offset to array of child node offsets
      - id: child_count
        type: u4
        doc: Number of child nodes
      - id: child_count_duplicate
        type: u4
        doc: Duplicate value of child count
      - id: controller_array_offset
        type: u4
        doc: Offset to array of controller structures
      - id: controller_count
        type: u4
        doc: Number of controllers attached to this node
      - id: controller_count_duplicate
        type: u4
        doc: Duplicate value of controller count
      - id: controller_data_offset
        type: u4
        doc: Offset to controller keyframe/data array
      - id: controller_data_count
        type: u4
        doc: Number of floats in controller data array
      - id: controller_data_count_duplicate
        type: u4
        doc: Duplicate value of controller data count
    instances:
      has_light:
        value: (node_type & 0x0002) != 0
      has_emitter:
        value: (node_type & 0x0004) != 0
      has_reference:
        value: (node_type & 0x0010) != 0
      has_mesh:
        value: (node_type & 0x0020) != 0
      has_skin:
        value: (node_type & 0x0040) != 0
      has_anim:
        value: (node_type & 0x0080) != 0
      has_dangly:
        value: (node_type & 0x0100) != 0
      has_aabb:
        value: (node_type & 0x0200) != 0
      has_saber:
        value: (node_type & 0x0800) != 0


  light_header:
    doc: Light header (92 bytes)
    seq:
      - id: unknown
        type: f4
        repeat: expr
        repeat-expr: 4
        doc: Purpose unknown, possibly padding or reserved values
      - id: flare_sizes_offset
        type: u4
        doc: Offset to flare sizes array (floats)
      - id: flare_sizes_count
        type: u4
        doc: Number of flare size entries
      - id: flare_sizes_count_duplicate
        type: u4
        doc: Duplicate of flare sizes count
      - id: flare_positions_offset
        type: u4
        doc: Offset to flare positions array (floats, 0.0-1.0 along light ray)
      - id: flare_positions_count
        type: u4
        doc: Number of flare position entries
      - id: flare_positions_count_duplicate
        type: u4
        doc: Duplicate of flare positions count
      - id: flare_color_shifts_offset
        type: u4
        doc: Offset to flare color shift array (RGB floats)
      - id: flare_color_shifts_count
        type: u4
        doc: Number of flare color shift entries
      - id: flare_color_shifts_count_duplicate
        type: u4
        doc: Duplicate of flare color shifts count
      - id: flare_texture_names_offset
        type: u4
        doc: Offset to flare texture name string offsets array
      - id: flare_texture_names_count
        type: u4
        doc: Number of flare texture names
      - id: flare_texture_names_count_duplicate
        type: u4
        doc: Duplicate of flare texture names count
      - id: flare_radius
        type: f4
        doc: Radius of flare effect
      - id: light_priority
        type: u4
        doc: Rendering priority for light culling/sorting
      - id: ambient_only
        type: u4
        doc: 1 if light only affects ambient, 0 for full lighting
      - id: dynamic_type
        type: u4
        doc: Type of dynamic lighting behavior
      - id: affect_dynamic
        type: u4
        doc: 1 if light affects dynamic objects, 0 otherwise
      - id: shadow
        type: u4
        doc: 1 if light casts shadows, 0 otherwise
      - id: flare
        type: u4
        doc: 1 if lens flare effect enabled, 0 otherwise
      - id: fading_light
        type: u4
        doc: 1 if light intensity fades with distance, 0 otherwise

  emitter_header:
    doc: Emitter header (224 bytes)
    seq:
      - id: dead_space
        type: f4
        doc: Minimum distance from emitter before particles become visible
      - id: blast_radius
        type: f4
        doc: Radius of explosive/blast particle effects
      - id: blast_length
        type: f4
        doc: Length/duration of blast effects
      - id: branch_count
        type: u4
        doc: Number of branching paths for particle trails
      - id: control_point_smoothing
        type: f4
        doc: Smoothing factor for particle path control points
      - id: x_grid
        type: u4
        doc: Grid subdivisions along X axis for particle spawning
      - id: y_grid
        type: u4
        doc: Grid subdivisions along Y axis for particle spawning
      - id: padding_unknown
        type: u4
        doc: Purpose unknown or padding
      - id: update_script
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Update behavior script name (e.g., "single", "fountain")
      - id: render_script
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Render mode script name (e.g., "normal", "billboard_to_local_z")
      - id: blend_script
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Blend mode script name (e.g., "normal", "lighten")
      - id: texture_name
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Particle texture name (null-terminated string)
      - id: chunk_name
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Associated model chunk name (null-terminated string)
      - id: two_sided_texture
        type: u4
        doc: 1 if texture should render two-sided, 0 for single-sided
      - id: loop
        type: u4
        doc: 1 if particle system loops, 0 for single playback
      - id: render_order
        type: u2
        doc: Rendering priority/order for particle sorting
      - id: frame_blending
        type: u1
        doc: 1 if frame blending enabled, 0 otherwise
      - id: depth_texture_name
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Depth/softparticle texture name (null-terminated string)
      - id: padding
        type: u1
        doc: Padding byte for alignment
      - id: flags
        type: u4
        doc: Emitter behavior flags bitmask (P2P, bounce, inherit, etc.)

  reference_header:
    doc: Reference header (36 bytes)
    seq:
      - id: model_resref
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Referenced model resource name without extension (null-terminated string)
      - id: reattachable
        type: u4
        doc: 1 if model can be detached and reattached dynamically, 0 if permanent

  trimesh_header:
    doc: Trimesh header (332 bytes KOTOR 1, 340 bytes KOTOR 2)
    seq:
      - id: function_pointer_0
        type: u4
        doc: Game engine function pointer (version-specific)
      - id: function_pointer_1
        type: u4
        doc: Secondary game engine function pointer
      - id: faces_array_offset
        type: u4
        doc: Offset to face definitions array
      - id: faces_count
        type: u4
        doc: Number of triangular faces in mesh
      - id: faces_count_duplicate
        type: u4
        doc: Duplicate of faces count
      - id: bounding_box_min
        type: vec3f
        doc: Minimum bounding box coordinates (X, Y, Z)
      - id: bounding_box_max
        type: vec3f
        doc: Maximum bounding box coordinates (X, Y, Z)
      - id: radius
        type: f4
        doc: Bounding sphere radius
      - id: average_point
        type: vec3f
        doc: Average vertex position (centroid) X, Y, Z
      - id: diffuse_color
        type: vec3f
        doc: Material diffuse color (R, G, B, range 0.0-1.0)
      - id: ambient_color
        type: vec3f
        doc: Material ambient color (R, G, B, range 0.0-1.0)
      - id: transparency_hint
        type: u4
        doc: Transparency rendering mode
      - id: texture_0_name
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Primary diffuse texture name (null-terminated string)
      - id: texture_1_name
        type: str
        size: 32
        encoding: ASCII
        pad-right: 0
        doc: Secondary texture name, often lightmap (null-terminated string)
      - id: texture_2_name
        type: str
        size: 12
        encoding: ASCII
        pad-right: 0
        doc: Tertiary texture name (null-terminated string)
      - id: texture_3_name
        type: str
        size: 12
        encoding: ASCII
        pad-right: 0
        doc: Quaternary texture name (null-terminated string)
      - id: indices_count_array_offset
        type: u4
        doc: Offset to vertex indices count array
      - id: indices_count_array_count
        type: u4
        doc: Number of entries in indices count array
      - id: indices_count_array_count_duplicate
        type: u4
        doc: Duplicate of indices count array count
      - id: indices_offset_array_offset
        type: u4
        doc: Offset to vertex indices offset array
      - id: indices_offset_array_count
        type: u4
        doc: Number of entries in indices offset array
      - id: indices_offset_array_count_duplicate
        type: u4
        doc: Duplicate of indices offset array count
      - id: inverted_counter_array_offset
        type: u4
        doc: Offset to inverted counter array
      - id: inverted_counter_array_count
        type: u4
        doc: Number of entries in inverted counter array
      - id: inverted_counter_array_count_duplicate
        type: u4
        doc: Duplicate of inverted counter array count
      - id: unknown_values
        type: s4
        repeat: expr
        repeat-expr: 3
        doc: Typically {-1, -1, 0}, purpose unknown
      - id: saber_unknown_data
        type: u1
        size: 8
        doc: Data specific to lightsaber meshes
      - id: unknown
        type: u4
        doc: Purpose unknown
      - id: uv_direction
        type: vec3f
        doc: UV animation direction X, Y components (Z = jitter speed)
      - id: uv_jitter
        type: f4
        doc: UV animation jitter amount
      - id: uv_jitter_speed
        type: f4
        doc: UV animation jitter speed
      - id: mdx_vertex_size
        type: u4
        doc: Size in bytes of each vertex in MDX data
      - id: mdx_data_flags
        type: u4
        doc: |
          Bitmask of present vertex attributes:
          - 0x00000001: MDX_VERTICES (vertex positions)
          - 0x00000002: MDX_TEX0_VERTICES (primary texture coordinates)
          - 0x00000004: MDX_TEX1_VERTICES (secondary texture coordinates)
          - 0x00000008: MDX_TEX2_VERTICES (tertiary texture coordinates)
          - 0x00000010: MDX_TEX3_VERTICES (quaternary texture coordinates)
          - 0x00000020: MDX_VERTEX_NORMALS (vertex normals)
          - 0x00000040: MDX_VERTEX_COLORS (vertex colors)
          - 0x00000080: MDX_TANGENT_SPACE (tangent space data)
      - id: mdx_vertices_offset
        type: s4
        doc: Relative offset to vertex positions in MDX (or -1 if none)
      - id: mdx_normals_offset
        type: s4
        doc: Relative offset to vertex normals in MDX (or -1 if none)
      - id: mdx_vertex_colors_offset
        type: s4
        doc: Relative offset to vertex colors in MDX (or -1 if none)
      - id: mdx_tex0_uvs_offset
        type: s4
        doc: Relative offset to primary texture UVs in MDX (or -1 if none)
      - id: mdx_tex1_uvs_offset
        type: s4
        doc: Relative offset to secondary texture UVs in MDX (or -1 if none)
      - id: mdx_tex2_uvs_offset
        type: s4
        doc: Relative offset to tertiary texture UVs in MDX (or -1 if none)
      - id: mdx_tex3_uvs_offset
        type: s4
        doc: Relative offset to quaternary texture UVs in MDX (or -1 if none)
      - id: mdx_tangent_space_offset
        type: s4
        doc: Relative offset to tangent space data in MDX (or -1 if none)
      - id: mdx_unknown_offset_1
        type: s4
        doc: Relative offset to unknown MDX data (or -1 if none)
      - id: mdx_unknown_offset_2
        type: s4
        doc: Relative offset to unknown MDX data (or -1 if none)
      - id: mdx_unknown_offset_3
        type: s4
        doc: Relative offset to unknown MDX data (or -1 if none)
      - id: vertex_count
        type: u2
        doc: Number of vertices in mesh
      - id: texture_count
        type: u2
        doc: Number of textures used by mesh
      - id: lightmapped
        type: u1
        doc: 1 if mesh uses lightmap, 0 otherwise
      - id: rotate_texture
        type: u1
        doc: 1 if texture should rotate, 0 otherwise
      - id: background_geometry
        type: u1
        doc: 1 if background geometry, 0 otherwise
      - id: shadow
        type: u1
        doc: 1 if mesh casts shadows, 0 otherwise
      - id: beaming
        type: u1
        doc: 1 if beaming effect enabled, 0 otherwise
      - id: render
        type: u1
        doc: 1 if mesh is renderable, 0 if hidden
      - id: unknown_flag
        type: u1
        doc: Purpose unknown (possibly UV animation enable)
      - id: padding
        type: u1
        doc: Padding byte
      - id: total_area
        type: f4
        doc: Total surface area of all faces
      - id: unknown2
        type: u4
        doc: Purpose unknown
      - id: k2_unknown_1
        type: u4
        doc: "KOTOR 2 only: Additional unknown field"
        if: _root.geometry_header.is_kotor2
      - id: k2_unknown_2
        type: u4
        doc: "KOTOR 2 only: Additional unknown field"
        if: _root.geometry_header.is_kotor2
      - id: mdx_data_offset
        type: u4
        doc: Absolute offset to this mesh's vertex data in MDX file
      - id: mdl_vertices_offset
        type: u4
        doc: Offset to vertex coordinate array in MDL file (for walkmesh/AABB nodes)

  skinmesh_header:
    doc: Skinmesh header (432 bytes KOTOR 1, 440 bytes KOTOR 2) - extends trimesh_header
    seq:
      - id: trimesh_base
        type: trimesh_header
        doc: Standard trimesh header
      - id: unknown_weights
        type: s4
        doc: Purpose unknown (possibly compilation weights)
      - id: padding1
        type: u1
        size: 8
        doc: Padding
      - id: mdx_bone_weights_offset
        type: u4
        doc: Offset to bone weight data in MDX file (4 floats per vertex)
      - id: mdx_bone_indices_offset
        type: u4
        doc: Offset to bone index data in MDX file (4 floats per vertex, cast to uint16)
      - id: bone_map_offset
        type: u4
        doc: Offset to bone map array (maps local bone indices to skeleton bone numbers)
      - id: bone_map_count
        type: u4
        doc: Number of bones referenced by this mesh (max 16)
      - id: qbones_offset
        type: u4
        doc: Offset to quaternion bind pose array (4 floats per bone)
      - id: qbones_count
        type: u4
        doc: Number of quaternion bind poses
      - id: qbones_count_duplicate
        type: u4
        doc: Duplicate of QBones count
      - id: tbones_offset
        type: u4
        doc: Offset to translation bind pose array (3 floats per bone)
      - id: tbones_count
        type: u4
        doc: Number of translation bind poses
      - id: tbones_count_duplicate
        type: u4
        doc: Duplicate of TBones count
      - id: unknown_array
        type: u4
        doc: Purpose unknown
      - id: bone_node_serial_numbers
        type: u2
        repeat: expr
        repeat-expr: 16
        doc: Serial indices of bone nodes (0xFFFF for unused slots)
      - id: padding2
        type: u2
        doc: Padding for alignment

  animmesh_header:
    doc: Animmesh header (388 bytes KOTOR 1, 396 bytes KOTOR 2) - extends trimesh_header
    seq:
      - id: trimesh_base
        type: trimesh_header
        doc: Standard trimesh header
      - id: unknown
        type: f4
        doc: Purpose unknown
      - id: unknown_array
        type: array_definition
        doc: Unknown array definition
      - id: unknown_floats
        type: f4
        repeat: expr
        repeat-expr: 9
        doc: Unknown float values

  danglymesh_header:
    doc: Danglymesh header (360 bytes KOTOR 1, 368 bytes KOTOR 2) - extends trimesh_header
    seq:
      - id: trimesh_base
        type: trimesh_header
        doc: Standard trimesh header
      - id: constraints_offset
        type: u4
        doc: Offset to vertex constraint values array
      - id: constraints_count
        type: u4
        doc: Number of vertex constraints (matches vertex count)
      - id: constraints_count_duplicate
        type: u4
        doc: Duplicate of constraints count
      - id: displacement
        type: f4
        doc: Maximum displacement distance for physics simulation
      - id: tightness
        type: f4
        doc: Tightness/stiffness of spring simulation (0.0-1.0)
      - id: period
        type: f4
        doc: Oscillation period in seconds
      - id: unknown
        type: u4
        doc: Purpose unknown

  aabb_header:
    doc: AABB (Axis-Aligned Bounding Box) header (336 bytes KOTOR 1, 344 bytes KOTOR 2) - extends trimesh_header
    seq:
      - id: trimesh_base
        type: trimesh_header
        doc: Standard trimesh header
      - id: unknown
        type: u4
        doc: Purpose unknown

  lightsaber_header:
    doc: Lightsaber header (352 bytes KOTOR 1, 360 bytes KOTOR 2) - extends trimesh_header
    seq:
      - id: trimesh_base
        type: trimesh_header
        doc: Standard trimesh header
      - id: vertices_offset
        type: u4
        doc: Offset to vertex position array in MDL file (3 floats × 8 vertices × 20 pieces)
      - id: texcoords_offset
        type: u4
        doc: Offset to texture coordinates array in MDL file (2 floats × 8 vertices × 20)
      - id: normals_offset
        type: u4
        doc: Offset to vertex normals array in MDL file (3 floats × 8 vertices × 20)
      - id: unknown1
        type: u4
        doc: Purpose unknown
      - id: unknown2
        type: u4
        doc: Purpose unknown

  controller:
    doc: Controller structure (16 bytes) - defines animation data for a node property over time
    seq:
      - id: type
        type: u4
        doc: |
          Controller type identifier. Controllers define animation data for node properties over time.
          
          Common Node Controllers (used by all node types):
          - 8: Position (3 floats: X, Y, Z translation)
          - 20: Orientation (4 floats: quaternion W, X, Y, Z rotation)
          - 36: Scale (3 floats: X, Y, Z scale factors)
          
          Light Controllers (specific to light nodes):
          - 76: Color (light color, 3 floats: R, G, B)
          - 88: Radius (light radius, 1 float)
          - 96: Shadow Radius (shadow casting radius, 1 float)
          - 100: Vertical Displacement (vertical offset, 1 float)
          - 140: Multiplier (light intensity multiplier, 1 float)
          
          Emitter Controllers (specific to emitter nodes):
          - 80: Alpha End (final alpha value, 1 float)
          - 84: Alpha Start (initial alpha value, 1 float)
          - 88: Birth Rate (particle spawn rate, 1 float)
          - 92: Bounce Coefficient (particle bounce factor, 1 float)
          - 96: Combine Time (particle combination timing, 1 float)
          - 100: Drag (particle drag/resistance, 1 float)
          - 104: FPS (frames per second, 1 float)
          - 108: Frame End (ending frame number, 1 float)
          - 112: Frame Start (starting frame number, 1 float)
          - 116: Gravity (gravity force, 1 float)
          - 120: Life Expectancy (particle lifetime, 1 float)
          - 124: Mass (particle mass, 1 float)
          - 128: P2P Bezier 2 (point-to-point bezier control point 2, varies)
          - 132: P2P Bezier 3 (point-to-point bezier control point 3, varies)
          - 136: Particle Rotation (particle rotation speed/angle, 1 float)
          - 140: Random Velocity (random velocity component, 3 floats: X, Y, Z)
          - 144: Size Start (initial particle size, 1 float)
          - 148: Size End (final particle size, 1 float)
          - 152: Size Start Y (initial particle size Y component, 1 float)
          - 156: Size End Y (final particle size Y component, 1 float)
          - 160: Spread (particle spread angle, 1 float)
          - 164: Threshold (threshold value, 1 float)
          - 168: Velocity (particle velocity, 3 floats: X, Y, Z)
          - 172: X Size (particle X dimension size, 1 float)
          - 176: Y Size (particle Y dimension size, 1 float)
          - 180: Blur Length (motion blur length, 1 float)
          - 184: Lightning Delay (lightning effect delay, 1 float)
          - 188: Lightning Radius (lightning effect radius, 1 float)
          - 192: Lightning Scale (lightning effect scale factor, 1 float)
          - 196: Lightning Subdivide (lightning subdivision count, 1 float)
          - 200: Lightning Zig Zag (lightning zigzag pattern, 1 float)
          - 216: Alpha Mid (mid-point alpha value, 1 float)
          - 220: Percent Start (starting percentage, 1 float)
          - 224: Percent Mid (mid-point percentage, 1 float)
          - 228: Percent End (ending percentage, 1 float)
          - 232: Size Mid (mid-point particle size, 1 float)
          - 236: Size Mid Y (mid-point particle size Y component, 1 float)
          - 240: Random Birth Rate (randomized particle spawn rate, 1 float)
          - 252: Target Size (target particle size, 1 float)
          - 256: Number of Control Points (control point count, 1 float)
          - 260: Control Point Radius (control point radius, 1 float)
          - 264: Control Point Delay (control point delay timing, 1 float)
          - 268: Tangent Spread (tangent spread angle, 1 float)
          - 272: Tangent Length (tangent vector length, 1 float)
          - 284: Color Mid (mid-point color, 3 floats: R, G, B)
          - 380: Color End (final color, 3 floats: R, G, B)
          - 392: Color Start (initial color, 3 floats: R, G, B)
          - 502: Emitter Detonate (detonation trigger, 1 float)
          
          Mesh Controllers (used by all mesh node types: trimesh, skinmesh, animmesh, danglymesh, AABB, lightsaber):
          - 100: SelfIllumColor (self-illumination color, 3 floats: R, G, B)
          - 128: Alpha (transparency/opacity, 1 float)
          
          Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Additional Controller Types section
          Reference: vendor/mdlops/MDLOpsM.pm:342-407 - Controller type definitions
          Reference: vendor/xoreos-docs/specs/torlack/binmdl.html - Comprehensive controller list
      - id: unknown
        type: u2
        doc: Purpose unknown, typically 0xFFFF
      - id: row_count
        type: u2
        doc: Number of keyframe rows (timepoints) for this controller
      - id: time_index
        type: u2
        doc: Index into controller data array where time values begin
      - id: data_index
        type: u2
        doc: Index into controller data array where property values begin
      - id: column_count
        type: u1
        doc: |
          Number of float values per keyframe (e.g., 3 for position XYZ, 4 for quaternion WXYZ)
          If bit 4 (0x10) is set, controller uses Bezier interpolation and stores 3× data per keyframe
      - id: padding
        type: u1
        size: 3
        doc: Padding bytes for 16-byte alignment
    instances:
      uses_bezier:
        value: (column_count & 0x10) != 0
        doc: True if controller uses Bezier interpolation

enums:
  model_classification:
    0x00: other
    0x01: effect
    0x02: tile
    0x04: character
    0x08: door
    0x10: lightsaber
    0x20: placeable
    0x40: flyer

  node_type_value:
    0x001: dummy
    0x003: light
    0x005: emitter
    0x011: reference
    0x021: trimesh
    0x061: skinmesh
    0x0a1: animmesh
    0x121: danglymesh
    0x221: aabb
    0x821: lightsaber

  controller_type:
    8: position
    20: orientation
    36: scale
    76: color
    88: radius
    96: shadow_radius
    100: vertical_displacement_or_drag_or_selfillumcolor
    132: alpha
    140: multiplier_or_randvel
