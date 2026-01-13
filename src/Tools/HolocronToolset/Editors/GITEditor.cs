using BioWare.NET.Common;
using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource;
using HolocronToolset.Data;
using GFFAuto = BioWare.NET.Resource.Formats.GFF.GFFAuto;

namespace HolocronToolset.Editors
{
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
        // Original: class GITEditor(Editor):
        public class GITEditor : Editor
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
            // Original: self.git: GIT = GIT()
            private GIT _git;
            private GFF _originalGff;

        public GITEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "GIT Editor", "git",
                new[] { ResourceType.GIT },
                new[] { ResourceType.GIT },
                installation)
        {
            _git = new GIT();
            InitializeComponent();
            SetupUI();
            New();
        }

        private void InitializeComponent()
        {
            if (!TryLoadXaml())
            {
                SetupUI();
            }
        }

        private void SetupUI()
        {
            var panel = new StackPanel();
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
        // Original: def load(self, filepath: os.PathLike | str, resref: str, restype: ResourceType, data: bytes):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            // GIT is a GFF-based format - store original GFF to preserve unmodified fields
            _originalGff = GFF.FromBytes(data);
            _git = ResourceAutoHelpers.ReadGit(data);
            LoadGIT(_git);
        }

        private void LoadGIT(GIT git)
        {
            // Load GIT data into UI
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py:512-517
            // Original: def _loadGIT(self, git: GIT):
            _git = git;
            // UI loading will be expanded when full UI is implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            Game gameToUse = _installation?.Game ?? Game.K2;
            var gff = GITHelpers.DismantleGit(_git, gameToUse);
            
            // Preserve unmodified fields from original GFF that aren't yet supported by GIT object model
            // This ensures roundtrip tests pass by maintaining all original data
            if (_originalGff != null)
            {
                var originalRoot = _originalGff.Root;
                var newRoot = gff.Root;
                
                // List of fields that GITHelpers.DismantleGit explicitly sets
                var fieldsSetByDismantle = new System.Collections.Generic.HashSet<string>
                {
                    "UseTemplates",
                    "AreaProperties",
                    "CameraList",
                    "Creature List",
                    "Door List",
                    "Encounter List",
                    "Placeable List",
                    "SoundList",
                    "StoreList",
                    "TriggerList",
                    "WaypointList"
                };
                
                // Copy all fields from original that aren't explicitly set by DismantleGit
                foreach (var (label, fieldType, value) in originalRoot)
                {
                    if (!fieldsSetByDismantle.Contains(label) && !newRoot.Exists(label))
                    {
                        CopyGffField(originalRoot, newRoot, label, fieldType);
                    }
                }
            }
            
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GIT);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _git = new GIT();
            _originalGff = null; // Clear original GFF when creating new file
        }
        
        // Helper method to copy a GFF field from one struct to another, preserving type
        private static void CopyGffField(GFFStruct source, GFFStruct destination, string label, GFFFieldType fieldType)
        {
            switch (fieldType)
            {
                case GFFFieldType.UInt8:
                    destination.SetUInt8(label, source.GetUInt8(label));
                    break;
                case GFFFieldType.Int8:
                    destination.SetInt8(label, source.GetInt8(label));
                    break;
                case GFFFieldType.UInt16:
                    destination.SetUInt16(label, source.GetUInt16(label));
                    break;
                case GFFFieldType.Int16:
                    destination.SetInt16(label, source.GetInt16(label));
                    break;
                case GFFFieldType.UInt32:
                    destination.SetUInt32(label, source.GetUInt32(label));
                    break;
                case GFFFieldType.Int32:
                    destination.SetInt32(label, source.GetInt32(label));
                    break;
                case GFFFieldType.UInt64:
                    destination.SetUInt64(label, source.GetUInt64(label));
                    break;
                case GFFFieldType.Int64:
                    destination.SetInt64(label, source.GetInt64(label));
                    break;
                case GFFFieldType.Single:
                    destination.SetSingle(label, source.GetSingle(label));
                    break;
                case GFFFieldType.Double:
                    destination.SetDouble(label, source.GetDouble(label));
                    break;
                case GFFFieldType.String:
                    destination.SetString(label, source.GetString(label));
                    break;
                case GFFFieldType.ResRef:
                    destination.SetResRef(label, source.GetResRef(label));
                    break;
                case GFFFieldType.LocalizedString:
                    destination.SetLocString(label, source.GetLocString(label));
                    break;
                case GFFFieldType.Binary:
                    destination.SetBinary(label, source.GetBinary(label));
                    break;
                case GFFFieldType.Vector3:
                    destination.SetVector3(label, source.GetVector3(label));
                    break;
                case GFFFieldType.Vector4:
                    destination.SetVector4(label, source.GetVector4(label));
                    break;
                case GFFFieldType.Struct:
                    destination.SetStruct(label, source.GetStruct(label));
                    break;
                case GFFFieldType.List:
                    destination.SetList(label, source.GetList(label));
                    break;
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
