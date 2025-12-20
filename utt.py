# This is a generated file! Please edit source .ksy file and use kaitai-struct-compiler to rebuild
# type: ignore

import kaitaistruct
from kaitaistruct import KaitaiStruct, KaitaiStream, BytesIO
from enum import IntEnum


if getattr(kaitaistruct, 'API_VERSION', (0, 9)) < (0, 11):
    raise Exception("Incompatible Kaitai Struct Python API: 0.11 or later is required, but you have %s" % (kaitaistruct.__version__))

class Utt(KaitaiStruct):
    """UTT (User Template Trigger) files are GFF-based format files that define trigger blueprints.
    UTT files use the GFF (Generic File Format) binary structure with file type signature "UTT ".
    
    Triggers are invisible objects in the game world that detect when creatures enter or exit their area
    and execute scripts. UTT files define the template/blueprint for triggers, which are then instantiated
    in areas (GIT files) with specific positions and orientations.
    
    UTT files contain:
    - Root struct with trigger metadata:
      - ResRef: Trigger template ResRef (unique identifier, max 16 characters, ResRef type)
      - LocName: Localized trigger name (LocalizedString/CExoLocString type)
      - Tag: Trigger tag identifier (String/CExoString type, used for scripting references)
      - Comment: Developer comment string (String/CExoString type, not used by game engine)
      - Type: Trigger type identifier (UInt32/DWord type)
        - 0 = Unknown/Generic trigger
        - 1 = Trap trigger
        - 2 = Proximity trigger
        - 3 = Clickable trigger
        - 4 = Script trigger
        - Other values: Custom trigger types
      - TrapDetectable: Whether trap is detectable (UInt8/Byte type, 0 = not detectable, 1 = detectable)
      - TrapDetectDC: Trap detection difficulty class (UInt8/Byte type, 0-255)
      - DisarmDC: Disarm difficulty class (UInt8/Byte type, 0-255)
      - DetectTrap: Whether trigger detects traps (UInt8/Byte type, 0 = no, 1 = yes)
      - Faction: Faction identifier (UInt32/DWord type, references faction.2da)
      - Cursor: Cursor type when hovering over trigger (UInt32/DWord type, references cursors.2da)
      - HighlightHeight: Height of highlight box (Single/Float32 type, in game units)
      - KeyName: Key required to unlock trigger (ResRef type, max 16 characters, references key item UTI)
      - TriggerOnClick: Whether trigger activates on click (UInt8/Byte type, 0 = no, 1 = yes)
      - Disarmable: Whether trigger can be disarmed (UInt8/Byte type, 0 = no, 1 = yes)
      - Detectable: Whether trigger is detectable (UInt8/Byte type, 0 = no, 1 = yes)
      - IsTrap: Whether trigger is a trap (UInt8/Byte type, 0 = no, 1 = yes)
      - TrapType: Type of trap if IsTrap is true (UInt32/DWord type)
      - ScriptOnEnter: Script ResRef executed when creature enters trigger area (ResRef type, max 16 characters)
      - ScriptOnExit: Script ResRef executed when creature exits trigger area (ResRef type, max 16 characters)
      - ScriptOnHeartbeat: Script ResRef executed periodically while creature is in trigger area (ResRef type, max 16 characters)
      - ScriptOnUserDefined: Script ResRef executed for user-defined events (ResRef type, max 16 characters)
      - ScriptOnDisarm: Script ResRef executed when trap is disarmed (ResRef type, max 16 characters)
      - ScriptOnTrapTriggered: Script ResRef executed when trap is triggered (ResRef type, max 16 characters)
      - LinkedTo: ResRef of object this trigger is linked to (ResRef type, max 16 characters, typically door or placeable)
      - LinkedToFlags: Flags for linked object behavior (UInt32/DWord type)
      - TransitionDestin: Destination area ResRef for transition triggers (ResRef type, max 16 characters)
      - TransitionDestinTag: Destination object tag for transition triggers (String/CExoString type)
      - LinkedToModule: Module ResRef for transition triggers (ResRef type, max 16 characters, references IFO file)
      - LinkedToWaypoint: Waypoint tag for transition triggers (String/CExoString type)
      - LinkedToStrRef: String reference for transition description (UInt32/DWord type, references dialog.tlk)
      - LinkedToTransition: Transition type (UInt32/DWord type)
      - LinkedToTransitionStrRef: String reference for transition text (UInt32/DWord type, references dialog.tlk)
      - LoadScreenID: Loading screen ID for transitions (UInt32/DWord type, references loadscreens.2da)
      - LoadScreenResRef: Loading screen ResRef for transitions (ResRef type, max 16 characters)
      - LoadScreenTransition: Transition effect type (UInt32/DWord type)
      - LoadScreenFade: Fade effect type (UInt32/DWord type)
      - LoadScreenFadeSpeed: Fade speed (Single/Float32 type)
      - LoadScreenFadeColor: Fade color (Vector3 type, RGB values 0.0-1.0)
      - LoadScreenFadeDelay: Fade delay in seconds (Single/Float32 type)
      - LoadScreenMusic: Music ResRef for transitions (ResRef type, max 16 characters)
      - LoadScreenAmbient: Ambient sound ResRef for transitions (ResRef type, max 16 characters)
      - LoadScreenSound: Sound effect ResRef for transitions (ResRef type, max 16 characters)
      - LoadScreenVoice: Voice ResRef for transitions (ResRef type, max 16 characters)
      - LoadScreenMovie: Movie ResRef for transitions (ResRef type, max 16 characters)
      - LoadScreenCamera: Camera animation ResRef for transitions (ResRef type, max 16 characters)
      - LoadScreenCameraEffect: Camera effect type (UInt32/DWord type)
      - LoadScreenCameraFov: Camera field of view (Single/Float32 type)
      - LoadScreenCameraHeight: Camera height (Single/Float32 type)
      - LoadScreenCameraAngle: Camera angle (UInt32/DWord type)
      - LoadScreenCameraAnim: Camera animation ID (UInt32/DWord type)
      - LoadScreenCameraId: Camera ID (UInt32/DWord type)
      - LoadScreenCameraDelay: Camera delay in seconds (Single/Float32 type)
      - LoadScreenCameraSpeed: Camera speed (Single/Float32 type)
      - LoadScreenCameraShake: Camera shake intensity (Single/Float32 type)
      - LoadScreenCameraShakeDuration: Camera shake duration in seconds (Single/Float32 type)
      - LoadScreenCameraShakeFrequency: Camera shake frequency (Single/Float32 type)
      - LoadScreenCameraShakeAmplitude: Camera shake amplitude (Single/Float32 type)
      - LoadScreenCameraShakeDirection: Camera shake direction (Vector3 type)
      - LoadScreenCameraShakeRandomShakeRandomDelay: Random camera shake random delay in seconds (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomSpeed: Random camera shake random speed (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShake: Random camera shake random shake intensity (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeDuration: Random camera shake random shake duration in seconds (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeFrequency: Random camera shake random shake frequency (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeAmplitude: Random camera shake random shake amplitude (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeDirection: Random camera shake random shake direction (Vector3 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandom: Random camera shake random shake random flag (UInt8/Byte type, 0 = no, 1 = yes)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomAmount: Random camera shake random shake random amount (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomFrequency: Random camera shake random shake random frequency (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomAmplitude: Random camera shake random shake random amplitude (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomDirection: Random camera shake random shake random direction (Vector3 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomDuration: Random camera shake random shake random duration in seconds (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomSpeed: Random camera shake random shake random speed (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomDelay: Random camera shake random shake random delay in seconds (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomId: Random camera shake random shake random ID (UInt32/DWord type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomEffect: Random camera shake random shake random effect (UInt32/DWord type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomFov: Random camera shake random shake random field of view (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomHeight: Random camera shake random shake random height (Single/Float32 type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomAngle: Random camera shake random shake random angle (UInt32/DWord type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomAnim: Random camera shake random shake random animation ID (UInt32/DWord type)
      - LoadScreenCameraShakeRandomShakeRandomShakeRandomCameraId: Random camera shake random shake random camera ID (UInt32/DWord type)
    
    References:
    - vendor/PyKotor/wiki/GFF-UTT.md
    - vendor/PyKotor/wiki/Bioware-Aurora-Trigger-Format.md
    - vendor/PyKotor/wiki/GFF-File-Format.md
    - vendor/reone/src/libs/resource/parser/gff/utt.cpp
    - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/utt.py
    """

    class GffFieldType(IntEnum):
        uint8 = 0
        int8 = 1
        uint16 = 2
        int16 = 3
        uint32 = 4
        int32 = 5
        uint64 = 6
        int64 = 7
        single = 8
        double = 9
        string = 10
        resref = 11
        localized_string = 12
        binary = 13
        struct = 14
        list = 15
        vector4 = 16
        vector3 = 17
    def __init__(self, _io, _parent=None, _root=None):
        super(Utt, self).__init__(_io)
        self._parent = _parent
        self._root = _root or self
        self._read()

    def _read(self):
        self.gff_header = Utt.GffHeader(self._io, self, self._root)
        if self.gff_header.label_count > 0:
            pass
            self.label_array = Utt.LabelArray(self._io, self, self._root)

        self.struct_array = Utt.StructArray(self._io, self, self._root)
        self.field_array = Utt.FieldArray(self._io, self, self._root)
        if self.gff_header.field_data_count > 0:
            pass
            self.field_data = Utt.FieldDataSection(self._io, self, self._root)

        if self.gff_header.field_indices_count > 0:
            pass
            self.field_indices = Utt.FieldIndicesArray(self._io, self, self._root)

        if self.gff_header.list_indices_count > 0:
            pass
            self.list_indices = Utt.ListIndicesArray(self._io, self, self._root)



    def _fetch_instances(self):
        pass
        self.gff_header._fetch_instances()
        if self.gff_header.label_count > 0:
            pass
            self.label_array._fetch_instances()

        self.struct_array._fetch_instances()
        self.field_array._fetch_instances()
        if self.gff_header.field_data_count > 0:
            pass
            self.field_data._fetch_instances()

        if self.gff_header.field_indices_count > 0:
            pass
            self.field_indices._fetch_instances()

        if self.gff_header.list_indices_count > 0:
            pass
            self.list_indices._fetch_instances()


    class FieldArray(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.FieldArray, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.entries = []
            for i in range(self._root.gff_header.field_count):
                self.entries.append(Utt.FieldEntry(self._io, self, self._root))



        def _fetch_instances(self):
            pass
            for i in range(len(self.entries)):
                pass
                self.entries[i]._fetch_instances()



    class FieldDataSection(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.FieldDataSection, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.data = (self._io.read_bytes(self._root.gff_header.field_data_count)).decode(u"UTF-8")


        def _fetch_instances(self):
            pass


    class FieldEntry(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.FieldEntry, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.field_type = self._io.read_u4le()
            self.label_index = self._io.read_u4le()
            self.data_or_offset = self._io.read_u4le()


        def _fetch_instances(self):
            pass

        @property
        def field_data_offset_value(self):
            """Absolute file offset to field data for complex types."""
            if hasattr(self, '_m_field_data_offset_value'):
                return self._m_field_data_offset_value

            if self.is_complex_type:
                pass
                self._m_field_data_offset_value = self._root.gff_header.field_data_offset + self.data_or_offset

            return getattr(self, '_m_field_data_offset_value', None)

        @property
        def is_complex_type(self):
            """True if field stores data in field_data section (complex types: UInt64, Int64, Double, String, ResRef, LocalizedString, Binary, Vector3, Vector4)."""
            if hasattr(self, '_m_is_complex_type'):
                return self._m_is_complex_type

            self._m_is_complex_type =  (( ((self.field_type >= 6) and (self.field_type <= 13)) ) or ( ((self.field_type >= 16) and (self.field_type <= 17)) )) 
            return getattr(self, '_m_is_complex_type', None)

        @property
        def is_list_type(self):
            """True if field is a list of structs."""
            if hasattr(self, '_m_is_list_type'):
                return self._m_is_list_type

            self._m_is_list_type = self.field_type == 15
            return getattr(self, '_m_is_list_type', None)

        @property
        def is_simple_type(self):
            """True if field stores data inline (simple types: Byte, Char, UInt16, Int16, UInt32, Int32, Float)."""
            if hasattr(self, '_m_is_simple_type'):
                return self._m_is_simple_type

            self._m_is_simple_type =  (( ((self.field_type >= 0) and (self.field_type <= 5)) ) or (self.field_type == 8)) 
            return getattr(self, '_m_is_simple_type', None)

        @property
        def is_struct_type(self):
            """True if field is a nested struct."""
            if hasattr(self, '_m_is_struct_type'):
                return self._m_is_struct_type

            self._m_is_struct_type = self.field_type == 14
            return getattr(self, '_m_is_struct_type', None)

        @property
        def list_indices_offset_value(self):
            """Absolute file offset to list indices for list type fields."""
            if hasattr(self, '_m_list_indices_offset_value'):
                return self._m_list_indices_offset_value

            if self.is_list_type:
                pass
                self._m_list_indices_offset_value = self._root.gff_header.list_indices_offset + self.data_or_offset

            return getattr(self, '_m_list_indices_offset_value', None)

        @property
        def struct_index_value(self):
            """Struct index for struct type fields."""
            if hasattr(self, '_m_struct_index_value'):
                return self._m_struct_index_value

            if self.is_struct_type:
                pass
                self._m_struct_index_value = self.data_or_offset

            return getattr(self, '_m_struct_index_value', None)


    class FieldIndicesArray(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.FieldIndicesArray, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.indices = []
            for i in range(self._root.gff_header.field_indices_count):
                self.indices.append(self._io.read_u4le())



        def _fetch_instances(self):
            pass
            for i in range(len(self.indices)):
                pass



    class GffHeader(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.GffHeader, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.file_type = (self._io.read_bytes(4)).decode(u"ASCII")
            self.file_version = (self._io.read_bytes(4)).decode(u"ASCII")
            self.struct_array_offset = self._io.read_u4le()
            self.struct_count = self._io.read_u4le()
            self.field_array_offset = self._io.read_u4le()
            self.field_count = self._io.read_u4le()
            self.label_array_offset = self._io.read_u4le()
            self.label_count = self._io.read_u4le()
            self.field_data_offset = self._io.read_u4le()
            self.field_data_count = self._io.read_u4le()
            self.field_indices_offset = self._io.read_u4le()
            self.field_indices_count = self._io.read_u4le()
            self.list_indices_offset = self._io.read_u4le()
            self.list_indices_count = self._io.read_u4le()


        def _fetch_instances(self):
            pass


    class LabelArray(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.LabelArray, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.labels = []
            for i in range(self._root.gff_header.label_count):
                self.labels.append(Utt.LabelEntry(self._io, self, self._root))



        def _fetch_instances(self):
            pass
            for i in range(len(self.labels)):
                pass
                self.labels[i]._fetch_instances()



    class LabelEntry(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.LabelEntry, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.name = (self._io.read_bytes(16)).decode(u"ASCII")


        def _fetch_instances(self):
            pass

        @property
        def name_trimmed(self):
            """Label name (trailing nulls should be trimmed by parser)."""
            if hasattr(self, '_m_name_trimmed'):
                return self._m_name_trimmed

            self._m_name_trimmed = self.name
            return getattr(self, '_m_name_trimmed', None)


    class ListIndicesArray(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.ListIndicesArray, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.raw_data = (self._io.read_bytes(self._root.gff_header.list_indices_count)).decode(u"UTF-8")


        def _fetch_instances(self):
            pass


    class LocalizedStringData(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.LocalizedStringData, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.total_size = self._io.read_u4le()
            self.string_ref = self._io.read_u4le()
            self.num_substrings = self._io.read_u4le()
            self.substrings = []
            for i in range(self.num_substrings):
                self.substrings.append(Utt.LocalizedSubstring(self._io, self, self._root))



        def _fetch_instances(self):
            pass
            for i in range(len(self.substrings)):
                pass
                self.substrings[i]._fetch_instances()


        @property
        def string_ref_value(self):
            """String reference as signed integer (-1 if none)."""
            if hasattr(self, '_m_string_ref_value'):
                return self._m_string_ref_value

            self._m_string_ref_value = (-1 if self.string_ref == 4294967295 else self.string_ref)
            return getattr(self, '_m_string_ref_value', None)


    class LocalizedSubstring(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.LocalizedSubstring, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.string_id = self._io.read_u4le()
            self.string_length = self._io.read_u4le()
            self.string_data = (self._io.read_bytes(self.string_length)).decode(u"UTF-8")


        def _fetch_instances(self):
            pass

        @property
        def gender_id(self):
            """Gender ID (extracted from string_id, bits 0-7: 0 = Male, 1 = Female)."""
            if hasattr(self, '_m_gender_id'):
                return self._m_gender_id

            self._m_gender_id = self.string_id & 255
            return getattr(self, '_m_gender_id', None)

        @property
        def language_id(self):
            """Language ID (extracted from string_id, bits 8-15)."""
            if hasattr(self, '_m_language_id'):
                return self._m_language_id

            self._m_language_id = self.string_id >> 8 & 255
            return getattr(self, '_m_language_id', None)


    class ResrefData(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.ResrefData, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.length = self._io.read_u1()
            self.name = (self._io.read_bytes(16)).decode(u"ASCII")


        def _fetch_instances(self):
            pass


    class StringData(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.StringData, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.length = self._io.read_u4le()
            self.data = (self._io.read_bytes(self.length)).decode(u"ASCII")


        def _fetch_instances(self):
            pass

        @property
        def data_trimmed(self):
            """String data (trailing nulls should be trimmed by parser)."""
            if hasattr(self, '_m_data_trimmed'):
                return self._m_data_trimmed

            self._m_data_trimmed = self.data
            return getattr(self, '_m_data_trimmed', None)


    class StructArray(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.StructArray, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.entries = []
            for i in range(self._root.gff_header.struct_count):
                self.entries.append(Utt.StructEntry(self._io, self, self._root))



        def _fetch_instances(self):
            pass
            for i in range(len(self.entries)):
                pass
                self.entries[i]._fetch_instances()



    class StructEntry(KaitaiStruct):
        def __init__(self, _io, _parent=None, _root=None):
            super(Utt.StructEntry, self).__init__(_io)
            self._parent = _parent
            self._root = _root
            self._read()

        def _read(self):
            self.struct_id = self._io.read_s4le()
            self.data_or_offset = self._io.read_u4le()
            self.field_count = self._io.read_u4le()


        def _fetch_instances(self):
            pass

        @property
        def field_indices_offset(self):
            """Byte offset into field_indices_array when struct has multiple fields."""
            if hasattr(self, '_m_field_indices_offset'):
                return self._m_field_indices_offset

            if self.has_multiple_fields:
                pass
                self._m_field_indices_offset = self.data_or_offset

            return getattr(self, '_m_field_indices_offset', None)

        @property
        def has_multiple_fields(self):
            """True if struct has multiple fields (offset to field indices in data_or_offset)."""
            if hasattr(self, '_m_has_multiple_fields'):
                return self._m_has_multiple_fields

            self._m_has_multiple_fields = self.field_count > 1
            return getattr(self, '_m_has_multiple_fields', None)

        @property
        def has_single_field(self):
            """True if struct has exactly one field (direct field index in data_or_offset)."""
            if hasattr(self, '_m_has_single_field'):
                return self._m_has_single_field

            self._m_has_single_field = self.field_count == 1
            return getattr(self, '_m_has_single_field', None)

        @property
        def single_field_index(self):
            """Direct field index when struct has exactly one field."""
            if hasattr(self, '_m_single_field_index'):
                return self._m_single_field_index

            if self.has_single_field:
                pass
                self._m_single_field_index = self.data_or_offset

            return getattr(self, '_m_single_field_index', None)



