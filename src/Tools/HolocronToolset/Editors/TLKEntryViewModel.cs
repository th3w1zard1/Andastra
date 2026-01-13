using System.ComponentModel;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.TLK;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py
    // Original: QStandardItem model with text and sound columns
    public class TLKEntryViewModel : INotifyPropertyChanged
    {
        private string _text;
        private string _sound;

        public TLKEntryViewModel(int stringref, string text = "", string sound = "")
        {
            StringRef = stringref;
            _text = text;
            _sound = sound;
        }

        public int StringRef { get; }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }

        public string Sound
        {
            get => _sound;
            set
            {
                if (_sound != value)
                {
                    _sound = value;
                    OnPropertyChanged(nameof(Sound));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public TLKEntry ToTLKEntry()
        {
            return new TLKEntry(_text, new ResRef(_sound));
        }
    }
}
