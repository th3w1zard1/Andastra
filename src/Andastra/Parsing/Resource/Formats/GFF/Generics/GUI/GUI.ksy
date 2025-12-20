meta:
  id: gui
  title: BioWare GUI (Graphical User Interface) File Format
  license: MIT
  endian: le
  file-extension: gui
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui/
    reone: vendor/reone/src/libs/resource/parser/gff/gui.cpp
    wiki: vendor/PyKotor/wiki/GFF-GUI.md
doc: |
  GUI (Graphical User Interface) files are GFF-based format files that store user interface
  definitions with controls, layouts, and styling. GUI files use the GFF (Generic File Format)
  binary structure with file type signature "GUI ".

  GUI files contain:
  - Root struct with Tag field (string identifier for the GUI)
  - CONTROLS: List of GUIControl structs (nested hierarchical control tree)

  Each GUIControl contains:
  - Common properties: ID, TAG, CONTROLTYPE, EXTENT (position/size), COLOR, ALPHA
  - State properties: BORDER, HILIGHT, SELECTED, HILIGHTSELECTED
  - Text properties: TEXT struct with font, alignment, color, string reference
  - Navigation: MOVETO struct for keyboard navigation (UP/DOWN/LEFT/RIGHT)
  - Control-specific properties based on CONTROLTYPE (Button, Label, Panel, ListBox, etc.)
  - Nested CONTROLS list for child controls

  Control Types:
  - 0: Control (base/container)
  - 2: Panel
  - 4: ProtoItem
  - 5: Label
  - 6: Button
  - 7: CheckBox
  - 8: Slider
  - 9: ScrollBar
  - 10: Progress
  - 11: ListBox

  References:
  - vendor/PyKotor/wiki/GFF-GUI.md
  - vendor/PyKotor/wiki/GFF-File-Format.md
  - GUIReader.cs implementation in Andastra

seq:
  - id: gff_header
    type: gff_header
    doc: GFF file header (56 bytes)

  - id: label_array
    type: label_array
    if: gff_header.label_count > 0
    pos: gff_header.label_array_offset
    doc: Array of field name labels (16-byte null-terminated strings)

  - id: struct_array
    type: struct_array
    pos: gff_header.struct_array_offset
    doc: Array of struct entries (12 bytes each)

  - id: field_array
    type: field_array
    pos: gff_header.field_array_offset
    doc: Array of field entries (12 bytes each)

  - id: field_data
    type: field_data_section
    if: gff_header.field_data_count > 0
    pos: gff_header.field_data_offset
    doc: Field data section for complex types (strings, ResRefs, LocalizedStrings, etc.)

  - id: field_indices
    type: field_indices_array
    if: gff_header.field_indices_count > 0
    pos: gff_header.field_indices_offset
    doc: Field indices array (MultiMap) for structs with multiple fields

  - id: list_indices
    type: list_indices_array
    if: gff_header.list_indices_count > 0
    pos: gff_header.list_indices_offset
    doc: List indices array for LIST type fields

types:
  # GFF Header (56 bytes)
  gff_header:
    seq:
      - id: file_type
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File type signature. Must be "GUI " for GUI files.
          Other GFF types: "GFF ", "DLG ", "ARE ", "UTC ", "UTI ", etc.
        valid: "GUI "

      - id: file_version
        type: str
        encoding: ASCII
        size: 4
        doc: |
          File format version. Typically "V3.2" for KotOR.
          Other versions: "V3.3", "V4.0", "V4.1" for other BioWare games.
        valid: ["V3.2", "V3.3", "V4.0", "V4.1"]

      - id: struct_array_offset
        type: u4
        doc: Byte offset to struct array from the beginning of the file

      - id: struct_count
        type: u4
        doc: Number of structs in the struct array

      - id: field_array_offset
        type: u4
        doc: Byte offset to field array from the beginning of the file

      - id: field_count
        type: u4
        doc: Number of fields in the field array

      - id: label_array_offset
        type: u4
        doc: Byte offset to label array from the beginning of the file

      - id: label_count
        type: u4
        doc: Number of labels in the label array

      - id: field_data_offset
        type: u4
        doc: Byte offset to field data section from the beginning of the file

      - id: field_data_count
        type: u4
        doc: Size of field data section in bytes

      - id: field_indices_offset
        type: u4
        doc: Byte offset to field indices array from the beginning of the file

      - id: field_indices_count
        type: u4
        doc: Number of field indices (uint32 values) in the field indices array

      - id: list_indices_offset
        type: u4
        doc: Byte offset to list indices array from the beginning of the file

      - id: list_indices_count
        type: u4
        doc: Number of list indices (uint32 values) in the list indices array

  # Label Array
  label_array:
    seq:
      - id: labels
        type: str
        encoding: ASCII
        size: 16
        repeat: expr
        repeat-expr: _root.gff_header.label_count
        doc: Array of 16-byte null-terminated field name labels

  # Struct Array
  struct_array:
    seq:
      - id: entries
        type: struct_entry
        repeat: expr
        repeat-expr: _root.gff_header.struct_count
        doc: Array of struct entries

  struct_entry:
    seq:
      - id: struct_id
        type: s4
        doc: |
          Structure type identifier.
          Root struct always has struct_id = 0xFFFFFFFF (-1).
          Other structs have programmer-defined IDs.

      - id: data_or_offset
        type: u4
        doc: |
          If field_count = 1: Direct field index into field_array.
          If field_count > 1: Byte offset into field_indices array.
          If field_count = 0: Unused (empty struct).

      - id: field_count
        type: u4
        doc: Number of fields in this struct (0, 1, or >1)

  # Field Array
  field_array:
    seq:
      - id: entries
        type: field_entry
        repeat: expr
        repeat-expr: _root.gff_header.field_count
        doc: Array of field entries

  field_entry:
    seq:
      - id: field_type
        type: u4
        doc: |
          Field data type (see GFFFieldType enum):
          0 = Byte (UInt8)
          1 = Char (Int8)
          2 = UInt16
          3 = Int16
          4 = UInt32
          5 = Int32
          6 = UInt64
          7 = Int64
          8 = Single (Float32)
          9 = Double (Float64)
          10 = CExoString (String)
          11 = ResRef
          12 = CExoLocString (LocalizedString)
          13 = Void (Binary)
          14 = Struct
          15 = List
          16 = Vector3
          17 = Vector4

      - id: label_index
        type: u4
        doc: Index into label_array for field name

      - id: data_or_offset
        type: u4
        doc: |
          For simple types (Byte, Char, UInt16, Int16, UInt32, Int32, UInt64, Int64, Single, Double):
            Inline data value (stored directly in this field)
          For complex types (String, ResRef, LocalizedString, Binary, Vector3, Vector4):
            Byte offset into field_data section
          For Struct type:
            Struct index into struct_array
          For List type:
            Byte offset into list_indices array

  # Field Data Section
  field_data_section:
    seq:
      - id: data
        type: str
        size: _root.gff_header.field_data_count
        doc: Raw field data bytes for complex types

  # Field Indices Array (MultiMap)
  field_indices_array:
    seq:
      - id: indices
        type: u4
        repeat: expr
        repeat-expr: _root.gff_header.field_indices_count
        doc: Array of field indices (uint32 values) for structs with multiple fields

  # List Indices Array
  list_indices_array:
    seq:
      - id: indices
        type: u4
        repeat: expr
        repeat-expr: _root.gff_header.list_indices_count
        doc: Array of list indices (uint32 values) for LIST type fields

  # GUI-Specific Types
  # Note: These represent the logical structure of GUI data as stored in GFF.
  # Actual parsing requires traversing the GFF structure (struct_array, field_array, etc.)
  # and resolving field references based on label names.

  gui_root:
    doc: |
      Root GUI struct. Contains:
      - Tag (string): GUI identifier/tag name
      - CONTROLS (list): Array of GUIControl structs (top-level controls)
    seq:
      - id: tag
        type: str
        encoding: UTF-8
        doc: GUI tag/identifier string (from "Tag" field)

  gui_control:
    doc: |
      GUI Control struct. Represents a single UI control element.
      Can be nested recursively via CONTROLS list.

      Common fields:
      - CONTROLTYPE (int32): Control type enum (0=Control, 2=Panel, 4=ProtoItem, 5=Label, 6=Button, 7=CheckBox, 8=Slider, 9=ScrollBar, 10=Progress, 11=ListBox)
      - ID (int32, optional): Control identifier
      - TAG (string): Control tag name
      - Obj_Parent (string, optional): Parent control tag reference
      - Obj_ParentID (int32, optional): Parent control ID reference
      - Obj_Locked (uint8, optional): Locked state (0=unlocked, 1=locked)
      - EXTENT (struct): Position and size (LEFT, TOP, WIDTH, HEIGHT)
      - COLOR (vector3, optional): RGB color (0.0-1.0 range)
      - ALPHA (float, optional): Alpha transparency (0.0-1.0)
      - BORDER (struct, optional): Border styling
      - HILIGHT (struct, optional): Highlight state styling
      - SELECTED (struct, optional): Selected state styling
      - HILIGHTSELECTED (struct, optional): Highlight+selected state styling
      - TEXT (struct, optional): Text properties
      - MOVETO (struct, optional): Keyboard navigation (UP/DOWN/LEFT/RIGHT)
      - CONTROLS (list, optional): Child controls (nested)

      Control-specific fields vary by CONTROLTYPE.
    seq:
      - id: control_type
        type: s4
        doc: Control type enum (see gui_control_type enum)
      - id: id
        type: s4
        doc: Control ID (optional, may be -1 or omitted)
      - id: tag
        type: str
        encoding: UTF-8
        doc: Control tag name

  gui_extent:
    doc: |
      EXTENT struct defining control position and size.
      Fields: LEFT (int32), TOP (int32), WIDTH (int32), HEIGHT (int32)
    seq:
      - id: left
        type: s4
        doc: Left edge X coordinate (pixels)
      - id: top
        type: s4
        doc: Top edge Y coordinate (pixels)
      - id: width
        type: s4
        doc: Control width (pixels)
      - id: height
        type: s4
        doc: Control height (pixels)

  gui_border:
    doc: |
      BORDER struct for border styling (also used for HILIGHT, SELECTED, HILIGHTSELECTED).
      Fields:
      - CORNER (ResRef): Corner tile texture resource
      - EDGE (ResRef): Edge tile texture resource
      - FILL (ResRef): Fill tile texture resource
      - FILLSTYLE (int32): Fill style mode (-1 if not set)
      - DIMENSION (int32): Border dimension/padding (default 0)
      - INNEROFFSET (int32, optional): Inner X offset
      - INNEROFFSETY (int32, optional): Inner Y offset
      - COLOR (vector3, optional): RGB color (0.0-1.0 range)
      - PULSING (uint8, optional): Pulsing animation flag (0=no, 1=yes)
    seq:
      - id: corner
        type: str
        encoding: ASCII
        size: 16
        doc: Corner texture ResRef (1-byte length + up to 16 chars)
      - id: edge
        type: str
        encoding: ASCII
        size: 16
        doc: Edge texture ResRef
      - id: fill
        type: str
        encoding: ASCII
        size: 16
        doc: Fill texture ResRef
      - id: fill_style
        type: s4
        doc: Fill style mode (-1 if not set, default 2)
      - id: dimension
        type: s4
        doc: Border dimension/padding (default 0)
      - id: inner_offset
        type: s4
        doc: Inner X offset (optional)
      - id: inner_offset_y
        type: s4
        doc: Inner Y offset (optional)
      - id: color
        type: vector3
        doc: RGB color (0.0-1.0 range, optional)
      - id: pulsing
        type: u1
        doc: Pulsing animation flag (optional)

  gui_text:
    doc: |
      TEXT struct for text properties.
      Fields:
      - TEXT (string): Text content
      - STRREF (uint32): String reference ID into dialog.tlk (0xFFFFFFFF = -1 if none)
      - FONT (ResRef): Font resource reference
      - ALIGNMENT (uint32): Text alignment (1=TopLeft, 2=TopCenter, 3=TopRight, 17=CenterLeft, 18=Center, 19=CenterRight, 33=BottomLeft, 34=BottomCenter, 35=BottomRight)
      - COLOR (vector3, optional): RGB color (0.0-1.0 range, default cyan for KotOR: 0.0, 0.659, 0.980)
      - PULSING (uint8, optional): Pulsing animation flag
    seq:
      - id: text
        type: str
        encoding: UTF-8
        doc: Text content string
      - id: strref
        type: u4
        doc: String reference ID (0xFFFFFFFF = -1 if none)
      - id: font
        type: str
        encoding: ASCII
        size: 16
        doc: Font ResRef (1-byte length + up to 16 chars)
      - id: alignment
        type: u4
        doc: Text alignment enum (default 18 = Center)
      - id: color
        type: vector3
        doc: RGB color (0.0-1.0 range, optional)
      - id: pulsing
        type: u1
        doc: Pulsing animation flag (optional)

  gui_moveto:
    doc: |
      MOVETO struct for keyboard navigation between controls.
      Fields: UP (int32), DOWN (int32), LEFT (int32), RIGHT (int32)
      Values are control IDs (-1 if no navigation in that direction).
    seq:
      - id: up
        type: s4
        doc: Control ID for UP navigation (-1 if none)
      - id: down
        type: s4
        doc: Control ID for DOWN navigation (-1 if none)
      - id: left
        type: s4
        doc: Control ID for LEFT navigation (-1 if none)
      - id: right
        type: s4
        doc: Control ID for RIGHT navigation (-1 if none)

  gui_scrollbar:
    doc: |
      SCROLLBAR struct for scrollbar properties (used in ListBox controls).
      Fields:
      - MAXVALUE (int32): Maximum scroll value
      - VISIBLEVALUE (int32): Number of visible items
      - CURVALUE (int32, optional): Current scroll position
      - HORIZONTAL (uint8): Horizontal orientation flag (0=vertical, 1=horizontal)
      - DIR (struct, optional): Direction arrow button properties (IMAGE ResRef, ALIGNMENT)
      - THUMB (struct, optional): Scrollbar thumb properties (IMAGE ResRef, ALIGNMENT, FLIPSTYLE, DRAWSTYLE, ROTATE)
    seq:
      - id: max_value
        type: s4
        doc: Maximum scroll value
      - id: visible_value
        type: s4
        doc: Number of visible items
      - id: cur_value
        type: s4
        doc: Current scroll position (optional)
      - id: horizontal
        type: u1
        doc: Horizontal orientation (0=vertical, 1=horizontal)

  gui_scrollbar_dir:
    doc: |
      DIR struct for scrollbar direction arrows.
      Fields: IMAGE (ResRef), ALIGNMENT (int32)
    seq:
      - id: image
        type: str
        encoding: ASCII
        size: 16
        doc: Direction arrow image ResRef
      - id: alignment
        type: s4
        doc: Alignment (default 18 = Center)

  gui_scrollbar_thumb:
    doc: |
      THUMB struct for scrollbar thumb (draggable handle).
      Fields:
      - IMAGE (ResRef): Thumb image resource
      - ALIGNMENT (int32): Alignment
      - FLIPSTYLE (int32, optional): Flip style
      - DRAWSTYLE (int32, optional): Draw style
      - ROTATE (float, optional): Rotation angle
    seq:
      - id: image
        type: str
        encoding: ASCII
        size: 16
        doc: Thumb image ResRef
      - id: alignment
        type: s4
        doc: Alignment (default 18 = Center)
      - id: flip_style
        type: s4
        doc: Flip style (optional)
      - id: draw_style
        type: s4
        doc: Draw style (optional)
      - id: rotate
        type: f4
        doc: Rotation angle in degrees (optional)

  gui_progress:
    doc: |
      PROGRESS struct for progress bar styling (similar to BORDER).
      Fields:
      - CORNER (ResRef): Corner texture
      - EDGE (ResRef): Edge texture
      - FILL (ResRef): Fill texture
      - FILLSTYLE (int32): Fill style (-1 if not set)
      - DIMENSION (int32): Dimension/padding
      - INNEROFFSET (int32): Inner X offset (default 0)
      - INNEROFFSETY (int32, optional): Inner Y offset
      - COLOR (vector3, optional): RGB color
      - PULSING (uint8, optional): Pulsing flag
    seq:
      - id: corner
        type: str
        encoding: ASCII
        size: 16
        doc: Corner texture ResRef
      - id: edge
        type: str
        encoding: ASCII
        size: 16
        doc: Edge texture ResRef
      - id: fill
        type: str
        encoding: ASCII
        size: 16
        doc: Fill texture ResRef
      - id: fill_style
        type: s4
        doc: Fill style (-1 if not set)
      - id: dimension
        type: s4
        doc: Dimension/padding (default 0)
      - id: inner_offset
        type: s4
        doc: Inner X offset (default 0)
      - id: inner_offset_y
        type: s4
        doc: Inner Y offset (optional)
      - id: color
        type: vector3
        doc: RGB color (optional)
      - id: pulsing
        type: u1
        doc: Pulsing flag (optional)

  gui_selected:
    doc: |
      SELECTED struct for selected state styling (used in CheckBox, ListBox items, ProtoItem).
      Same structure as BORDER but for selected state appearance.
      Fields:
      - CORNER (ResRef): Corner texture for selected state
      - EDGE (ResRef): Edge texture for selected state
      - FILL (ResRef): Fill texture for selected state
      - FILLSTYLE (int32): Fill style (-1 if not set)
      - DIMENSION (int32): Border dimension/padding (default 0)
      - INNEROFFSET (int32, optional): Inner X offset
      - INNEROFFSETY (int32, optional): Inner Y offset
      - COLOR (vector3, optional): RGB color (0.0-1.0 range)
      - PULSING (uint8, optional): Pulsing animation flag (0=no, 1=yes)
    seq:
      - id: corner
        type: str
        encoding: ASCII
        size: 16
        doc: Corner texture ResRef
      - id: edge
        type: str
        encoding: ASCII
        size: 16
        doc: Edge texture ResRef
      - id: fill
        type: str
        encoding: ASCII
        size: 16
        doc: Fill texture ResRef
      - id: fill_style
        type: s4
        doc: Fill style mode (-1 if not set)
      - id: dimension
        type: s4
        doc: Border dimension/padding (default 0)
      - id: inner_offset
        type: s4
        doc: Inner X offset (optional)
      - id: inner_offset_y
        type: s4
        doc: Inner Y offset (optional)
      - id: color
        type: vector3
        doc: RGB color (0.0-1.0 range, optional)
      - id: pulsing
        type: u1
        doc: Pulsing animation flag (optional)

  gui_hilight_selected:
    doc: |
      HILIGHTSELECTED struct for highlight+selected state styling (highest priority state).
      Same structure as BORDER but for combined highlight+selected state appearance.
      Fields:
      - CORNER (ResRef): Corner texture for highlight+selected state
      - EDGE (ResRef): Edge texture for highlight+selected state
      - FILL (ResRef): Fill texture for highlight+selected state
      - FILLSTYLE (int32): Fill style (-1 if not set)
      - DIMENSION (int32): Border dimension/padding (default 0)
      - INNEROFFSET (int32, optional): Inner X offset
      - INNEROFFSETY (int32, optional): Inner Y offset
      - COLOR (vector3, optional): RGB color (0.0-1.0 range)
      - PULSING (uint8, optional): Pulsing animation flag (0=no, 1=yes)
    seq:
      - id: corner
        type: str
        encoding: ASCII
        size: 16
        doc: Corner texture ResRef
      - id: edge
        type: str
        encoding: ASCII
        size: 16
        doc: Edge texture ResRef
      - id: fill
        type: str
        encoding: ASCII
        size: 16
        doc: Fill texture ResRef
      - id: fill_style
        type: s4
        doc: Fill style mode (-1 if not set)
      - id: dimension
        type: s4
        doc: Border dimension/padding (default 0)
      - id: inner_offset
        type: s4
        doc: Inner X offset (optional)
      - id: inner_offset_y
        type: s4
        doc: Inner Y offset (optional)
      - id: color
        type: vector3
        doc: RGB color (0.0-1.0 range, optional)
      - id: pulsing
        type: u1
        doc: Pulsing animation flag (optional)

  gui_listbox:
    doc: |
      ListBox control-specific properties (CONTROLTYPE = 11).
      Fields:
      - PROTOITEM (struct): Template for list item appearance
      - SCROLLBAR (struct): Embedded scrollbar control
      - PADDING (int32): Spacing between items (pixels, default 5)
      - MAXVALUE (int32): Maximum scroll value (total items - visible items)
      - CURVALUE (int32, optional): Current scroll position
      - LOOPING (uint8): Loop scrolling (0=no, 1=yes, default 1)
      - LEFTSCROLLBAR (uint8): Scrollbar on left side (0=right, 1=left)
    seq:
      - id: proto_item
        type: gui_control
        doc: PROTOITEM struct template for list items
      - id: scrollbar
        type: gui_scrollbar
        doc: SCROLLBAR struct for embedded scrollbar
      - id: padding
        type: s4
        doc: Spacing between items in pixels (default 5)
      - id: max_value
        type: s4
        doc: Maximum scroll value
      - id: cur_value
        type: s4
        doc: Current scroll position (optional)
      - id: looping
        type: u1
        doc: Loop scrolling flag (0=no, 1=yes)
      - id: left_scrollbar
        type: u1
        doc: Scrollbar position (0=right, 1=left)

  gui_slider:
    doc: |
      Slider control-specific properties (CONTROLTYPE = 8).
      Fields:
      - THUMB (struct): Slider thumb appearance
      - CURVALUE (int32): Current slider value (0 to MAXVALUE)
      - MAXVALUE (int32): Maximum slider value (typically 100)
      - DIRECTION (int32): Orientation (0=horizontal, 1=vertical)
    seq:
      - id: thumb
        type: gui_scrollbar_thumb
        doc: THUMB struct for slider thumb
      - id: cur_value
        type: s4
        doc: Current slider value (0 to MAXVALUE)
      - id: max_value
        type: s4
        doc: Maximum slider value (typically 100)
      - id: direction
        type: s4
        doc: Orientation (0=horizontal, 1=vertical)

  gui_checkbox:
    doc: |
      CheckBox control-specific properties (CONTROLTYPE = 7).
      Fields:
      - SELECTED (struct): Appearance when checked
      - HILIGHTSELECTED (struct): Appearance when checked and hovered
      - ISSELECTED (uint8): Default checked state (0=unchecked, 1=checked)
    seq:
      - id: selected
        type: gui_selected
        doc: SELECTED struct for checked appearance
      - id: hilight_selected
        type: gui_hilight_selected
        doc: HILIGHTSELECTED struct for checked+hovered appearance
      - id: is_selected
        type: u1
        doc: Default checked state (0=unchecked, 1=checked)

  gui_button:
    doc: |
      Button control-specific properties (CONTROLTYPE = 6).
      Fields:
      - HILIGHT (struct): Hover state appearance
      - MOVETO (struct): D-pad navigation targets
      - TEXT (struct): Button label text
      - PULSING (uint8, optional): Pulsing animation flag
    seq:
      - id: hilight
        type: gui_border
        doc: HILIGHT struct for hover state
      - id: moveto
        type: gui_moveto
        doc: MOVETO struct for keyboard navigation
      - id: text
        type: gui_text
        doc: TEXT struct for button label
      - id: pulsing
        type: u1
        doc: Pulsing animation flag (optional)

  gui_label:
    doc: |
      Label control-specific properties (CONTROLTYPE = 5).
      Fields:
      - TEXT (struct): Text display properties
    seq:
      - id: text
        type: gui_text
        doc: TEXT struct for text properties

  gui_panel:
    doc: |
      Panel control-specific properties (CONTROLTYPE = 2).
      Fields:
      - CONTROLS (list): Child controls list
      - BORDER (struct, optional): Panel border/background
      - COLOR (vector3, optional): Panel color modulation
      - ALPHA (float, optional): Panel transparency (0.0-1.0)
    seq:
      - id: controls
        type: gui_control
        repeat: until
        repeat-until: _io.eof
        doc: CONTROLS list of child controls
      - id: border
        type: gui_border
        doc: BORDER struct for panel background (optional)
      - id: color
        type: vector3
        doc: Panel color modulation (optional)
      - id: alpha
        type: f4
        doc: Panel transparency (0.0-1.0, optional)

  gui_protoitem:
    doc: |
      ProtoItem control-specific properties (CONTROLTYPE = 4).
      Template for list items used in ListBox controls.
      Fields:
      - TEXT (struct): Item label text
      - BORDER (struct): Item border appearance
      - HILIGHT (struct): Item highlight on hover
      - HILIGHTSELECTED (struct): Item highlight when selected
      - SELECTED (struct): Item appearance when selected
      - ISSELECTED (uint8): Default selected state (0=unselected, 1=selected)
    seq:
      - id: text
        type: gui_text
        doc: TEXT struct for item label
      - id: border
        type: gui_border
        doc: BORDER struct for item border
      - id: hilight
        type: gui_border
        doc: HILIGHT struct for hover state
      - id: hilight_selected
        type: gui_hilight_selected
        doc: HILIGHTSELECTED struct for selected+hovered state
      - id: selected
        type: gui_selected
        doc: SELECTED struct for selected state
      - id: is_selected
        type: u1
        doc: Default selected state (0=unselected, 1=selected)

  vector3:
    doc: |
      3D vector (3×float32) for RGB colors.
      Used for COLOR fields in various GUI structs.
    seq:
      - id: x
        type: f4
        doc: X component (Red for colors, 0.0-1.0 range)
      - id: y
        type: f4
        doc: Y component (Green for colors, 0.0-1.0 range)
      - id: z
        type: f4
        doc: Z component (Blue for colors, 0.0-1.0 range)

  vector4:
    doc: |
      4D vector (4×float32) for quaternions/orientations.
      Used in some GFF contexts, though GUI primarily uses Vector3 for colors.
    seq:
      - id: x
        type: f4
        doc: X component
      - id: y
        type: f4
        doc: Y component
      - id: z
        type: f4
        doc: Z component
      - id: w
        type: f4
        doc: W component

enums:
  gui_control_type:
    doc: GUI Control Type enumeration
    -1: invalid
    doc: Invalid control type
    0: control
    doc: Base control/container type
    2: panel
    doc: Panel container
    4: proto_item
    doc: Prototype item (used in ListBox)
    5: label
    doc: Label/text display
    6: button
    doc: Button control
    7: checkbox
    doc: Checkbox control
    8: slider
    doc: Slider control
    9: scrollbar
    doc: Scrollbar control
    10: progress
    doc: Progress bar
    11: listbox
    doc: List box with items

  gui_text_alignment:
    doc: Text alignment enumeration (bitfield: horizontal + vertical)
    1: top_left
    doc: Top-Left alignment
    2: top_center
    doc: Top-Center alignment
    3: top_right
    doc: Top-Right alignment
    17: center_left
    doc: Center-Left alignment
    18: center
    doc: Center alignment (most common)
    19: center_right
    doc: Center-Right alignment
    33: bottom_left
    doc: Bottom-Left alignment
    34: bottom_center
    doc: Bottom-Center alignment
    35: bottom_right
    doc: Bottom-Right alignment

  gui_fill_style:
    doc: Fill style enumeration for borders and progress bars
    -1: none
    doc: No fill style (not set)
    0: empty
    doc: Empty fill
    1: solid
    doc: Solid color fill
    2: texture
    doc: Texture fill (most common)

  gui_slider_direction:
    doc: Slider direction/orientation enumeration
    0: horizontal
    doc: Horizontal slider (left-right)
    1: vertical
    doc: Vertical slider (top-bottom)


