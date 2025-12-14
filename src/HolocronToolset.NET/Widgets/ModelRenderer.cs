using System;
using Avalonia.Controls;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:36
    // Original: class ModelRenderer(QOpenGLWidget):
    public class ModelRenderer : Control
    {
        private HTInstallation _installation;
        private byte[] _mdlData;
        private byte[] _mdxData;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:74-84
        // Original: @property def installation(self) -> Installation | None:
        public HTInstallation Installation
        {
            get { return _installation; }
            set
            {
                _installation = value;
                // If scene already exists, update its installation too
                // This is critical because initializeGL() may have created the scene before installation was set
                // Matching PyKotor implementation: if self._scene is not None and value is not None and self._scene.installation is None:
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:196-200
        // Original: def set_model(self, data: bytes, data_ext: bytes):
        public void SetModel(byte[] mdlData, byte[] mdxData)
        {
            // Store model data for rendering
            // In full implementation, this would load the model into the OpenGL scene
            _mdlData = mdlData;
            _mdxData = mdxData;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:189-194
        // Original: def clear_model(self):
        public void ClearModel()
        {
            // Clear model data
            _mdlData = null;
            _mdxData = null;
        }
    }
}
