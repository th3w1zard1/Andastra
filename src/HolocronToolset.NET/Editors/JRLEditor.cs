using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/jrl.py:40
    // Original: class JRLEditor(Editor):
    public class JRLEditor : Editor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/jrl.py:57-58
        // Original: self._jrl: JRL = JRL(); self._model: QStandardItemModel = QStandardItemModel(self)
        private JRL _jrl;
        private List<JournalTreeItem> _model;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/jrl.py:53-78
        // Original: def __init__(self, parent: QWidget | None, installation: HTInstallation | None = None):
        public JRLEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Journal Editor", "journal",
                new[] { ResourceType.JRL },
                new[] { ResourceType.JRL },
                installation)
        {
            _jrl = new JRL();
            _model = new List<JournalTreeItem>();
            InitializeComponent();
            SetupUI();
            Width = 400;
            Height = 250;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/jrl.py:129-174
        // Original: def load(self, filepath: os.PathLike | str, resref: str, restype: ResourceType, data: bytes):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            _jrl = JRLHelper.ReadJrl(data);

            _model.Clear();
            foreach (JRLQuest quest in _jrl.Quests)
            {
                var questItem = new JournalTreeItem { Data = quest };
                RefreshQuestItem(questItem);
                _model.Add(questItem);

                foreach (JRLQuestEntry entry in quest.Entries)
                {
                    var entryItem = new JournalTreeItem { Data = entry };
                    RefreshEntryItem(entryItem);
                    questItem.Children.Add(entryItem);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/jrl.py:175-178
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            byte[] data = JRLHelper.BytesJrl(_jrl, ResourceType.JRL);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/jrl.py:180-183
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _jrl = new JRL();
            _model.Clear();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/jrl.py:185-197
        // Original: def refresh_entry_item(self, entryItem: QStandardItem):
        private void RefreshEntryItem(JournalTreeItem entryItem)
        {
            if (entryItem.Data is JRLQuestEntry entry)
            {
                string text;
                if (_installation == null)
                {
                    text = $"[{entry.EntryId}] {entry.Text}";
                }
                else
                {
                    text = $"[{entry.EntryId}] {_installation.String(entry.Text)}";
                }
                entryItem.Text = text;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/jrl.py:199-210
        // Original: def refresh_quest_item(self, questItem: QStandardItem):
        private void RefreshQuestItem(JournalTreeItem questItem)
        {
            if (questItem.Data is JRLQuest quest)
            {
                string text;
                if (_installation == null)
                {
                    text = quest.Name?.ToString() ?? "[Unnamed]";
                }
                else
                {
                    text = _installation.String(quest.Name, "[Unnamed]");
                }
                questItem.Text = text;
            }
        }

        public override void SaveAs()
        {
            Save();
        }

        // Property to access model for tests
        public int ModelRowCount => _model.Count;
    }

    // Simple tree item class to hold quest/entry data (similar to QStandardItem)
    internal class JournalTreeItem
    {
        public string Text { get; set; } = string.Empty;
        public object Data { get; set; }
        public List<JournalTreeItem> Children { get; set; } = new List<JournalTreeItem>();
    }
}
