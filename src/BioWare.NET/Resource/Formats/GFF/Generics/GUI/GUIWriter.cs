using System;
using System.Collections.Generic;
using System.Numerics;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF.Generics.GUI
{
    /// <summary>
    /// Writes KOTOR GUI files to GFF format.
    /// Based on PyKotor dismantle_gui implementation: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:730
    /// This is the reverse operation of GUIReader, converting GUI objects back to GFF format.
    /// </summary>
    [PublicAPI]
    public class GUIWriter
    {
        private GUI _gui;

        public GUIWriter(GUI gui)
        {
            if (gui == null)
            {
                throw new ArgumentNullException("gui");
            }
            _gui = gui;
        }

        /// <summary>
        /// Converts the GUI to a GFF structure.
        /// </summary>
        public GFF ToGFF()
        {
            var gff = new GFF(GFFContent.GUI);

            // Write root GUI tag
            if (!string.IsNullOrEmpty(_gui.Tag))
            {
                gff.Root.SetString("Tag", _gui.Tag);
            }

            // Write top-level controls list
            if (_gui.Controls != null && _gui.Controls.Count > 0)
            {
                var controlsList = new GFFList();
                foreach (var control in _gui.Controls)
                {
                    var controlStruct = controlsList.Add(0);
                    WriteControl(control, controlStruct);
                }
                gff.Root.SetList("CONTROLS", controlsList);
            }

            return gff;
        }

        /// <summary>
        /// Writes the GUI to a byte array.
        /// </summary>
        public byte[] Write()
        {
            var gff = ToGFF();
            var writer = new GFFBinaryWriter(gff);
            return writer.Write();
        }

        /// <summary>
        /// Writes the GUI to a file.
        /// </summary>
        public void WriteToFile(string filepath)
        {
            var data = Write();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filepath));
            System.IO.File.WriteAllBytes(filepath, data);
        }

        /// <summary>
        /// Converts a GUI control to a GFF struct.
        /// Based on PyKotor dismantle_control: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:947
        /// </summary>
        private void WriteControl(GUIControl control, GFFStruct gffStruct)
        {
            // Basic properties
            gffStruct.SetInt32("CONTROLTYPE", (int)control.GuiType);
            if (control.Id.HasValue)
            {
                gffStruct.SetInt32("ID", control.Id.Value);
            }
            if (!string.IsNullOrEmpty(control.Tag))
            {
                gffStruct.SetString("TAG", control.Tag);
            }
            if (!string.IsNullOrEmpty(control.ParentTag))
            {
                gffStruct.SetString("Obj_Parent", control.ParentTag);
            }
            if (control.ParentId.HasValue)
            {
                gffStruct.SetInt32("Obj_ParentID", control.ParentId.Value);
            }
            if (control.Locked.HasValue)
            {
                gffStruct.SetUInt8("Obj_Locked", (byte)(control.Locked.Value ? 1 : 0));
            }

            // Color and Alpha
            // Based on PyKotor: Color is Vector3 (RGB), ALPHA is separate float field
            // Original implementation: swkotor.exe/swkotor2.exe stores COLOR as RGB and ALPHA separately
            if (control.Color != null)
            {
                gffStruct.SetVector3("COLOR", new Vector3(control.Color.R, control.Color.G, control.Color.B));
                // Write ALPHA if it's not the default (1.0f) or if Color has non-default alpha
                // Based on PyKotor: if control.color.a is not None, write ALPHA field
                if (control.Alpha != 1.0f)
                {
                    gffStruct.SetSingle("ALPHA", control.Alpha);
                }
            }
            else if (control.Alpha != 1.0f)
            {
                // If Color is null but Alpha is set, write ALPHA with default white color
                // This handles the case where ALPHA exists without COLOR
                gffStruct.SetSingle("ALPHA", control.Alpha);
            }

            // Padding, Looping, LeftScrollbar
            if (control.Padding.HasValue)
            {
                gffStruct.SetInt32("PADDING", control.Padding.Value);
            }
            if (control.Looping.HasValue)
            {
                gffStruct.SetUInt8("LOOPING", (byte)(control.Looping.Value != 0 ? 1 : 0));
            }
            if (control.LeftScrollbar.HasValue)
            {
                gffStruct.SetUInt8("LEFTSCROLLBAR", (byte)(control.LeftScrollbar.Value != 0 ? 1 : 0));
            }

            // Extent (position and size)
            WriteExtent(gffStruct, control);

            // Common values for Progress and Slider (only if not overridden by specific control types)
            if (control.GuiType != GUIControlType.Progress && control.GuiType != GUIControlType.Slider)
            {
                if (control.CurrentValue.HasValue)
                {
                    gffStruct.SetInt32("CURVALUE", control.CurrentValue.Value);
                }
                if (control.MaxValue.HasValue)
                {
                    gffStruct.SetInt32("MAXVALUE", control.MaxValue.Value);
                }
            }

            // Progress bar specific
            if (control.GuiType == GUIControlType.Progress)
            {
                WriteProgressBarProperties(gffStruct, (GUIProgressBar)control);
            }

            // Slider specific
            if (control.GuiType == GUIControlType.Slider)
            {
                WriteSliderProperties(gffStruct, (GUISlider)control);
            }

            // Border
            if (control.Border != null)
            {
                WriteBorder(gffStruct, control.Border);
            }

            // Text
            if (control.GuiText != null)
            {
                WriteText(gffStruct, control.GuiText);
            }

            // Hilight
            if (control.Hilight != null)
            {
                WriteHilight(gffStruct, control.Hilight);
            }

            // MoveTo
            if (control.Moveto != null)
            {
                WriteMoveTo(gffStruct, control.Moveto);
            }

            // ListBox specific
            if (control.GuiType == GUIControlType.ListBox)
            {
                WriteListBoxProperties(gffStruct, (GUIListBox)control);
            }

            // CheckBox specific
            if (control.GuiType == GUIControlType.CheckBox)
            {
                WriteCheckBoxProperties(gffStruct, (GUICheckBox)control);
            }

            // Button specific
            if (control.GuiType == GUIControlType.Button)
            {
                WriteButtonProperties(gffStruct, (GUIButton)control);
            }

            // Panel specific
            if (control.GuiType == GUIControlType.Panel)
            {
                WritePanelProperties(gffStruct, (GUIPanel)control);
            }

            // ScrollBar specific
            if (control.GuiType == GUIControlType.ScrollBar)
            {
                WriteScrollBarProperties(gffStruct, control);
            }

            // ProtoItem specific
            if (control.GuiType == GUIControlType.ProtoItem)
            {
                WriteProtoItemProperties(gffStruct, (GUIProtoItem)control);
            }

            // Handle child controls
            if (control.Children != null && control.Children.Count > 0)
            {
                var childControlsList = new GFFList();
                foreach (var child in control.Children)
                {
                    var childStruct = childControlsList.Add(0);
                    WriteControl(child, childStruct);
                }
                gffStruct.SetList("CONTROLS", childControlsList);
            }
        }

        /// <summary>
        /// Writes extent values to a GFF struct.
        /// Based on PyKotor write_extent: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:739
        /// </summary>
        private void WriteExtent(GFFStruct gffStruct, GUIControl control)
        {
            var extentStruct = gffStruct.Acquire<GFFStruct>("EXTENT", new GFFStruct(0));
            extentStruct.SetInt32("LEFT", (int)control.Extent.X);
            extentStruct.SetInt32("TOP", (int)control.Extent.Y);
            extentStruct.SetInt32("WIDTH", (int)control.Extent.Z);
            extentStruct.SetInt32("HEIGHT", (int)control.Extent.W);
        }

        /// <summary>
        /// Writes border values to a GFF struct.
        /// Based on PyKotor write_border: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:753
        /// </summary>
        private void WriteBorder(GFFStruct gffStruct, GUIBorder border)
        {
            var borderStruct = gffStruct.Acquire<GFFStruct>("BORDER", new GFFStruct(0));
            if (border.Color != null)
            {
                borderStruct.SetVector3("COLOR", new Vector3(border.Color.R, border.Color.G, border.Color.B));
                if (border.Color.A != 1.0f)
                {
                    borderStruct.SetSingle("ALPHA", border.Color.A);
                }
            }
            borderStruct.SetResRef("CORNER", border.Corner);
            borderStruct.SetInt32("DIMENSION", border.Dimension);
            borderStruct.SetResRef("EDGE", border.Edge);
            borderStruct.SetResRef("FILL", border.Fill);
            borderStruct.SetInt32("FILLSTYLE", border.FillStyle);
            if (border.InnerOffset.HasValue)
            {
                borderStruct.SetInt32("INNEROFFSET", border.InnerOffset.Value);
            }
            if (border.InnerOffsetY.HasValue)
            {
                borderStruct.SetInt32("INNEROFFSETY", border.InnerOffsetY.Value);
            }
            if (border.Pulsing.HasValue)
            {
                borderStruct.SetUInt8("PULSING", (byte)border.Pulsing.Value);
            }
            gffStruct.SetStruct("BORDER", borderStruct);
        }

        /// <summary>
        /// Writes text values to a GFF struct.
        /// Based on PyKotor write_text: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:860
        /// </summary>
        private void WriteText(GFFStruct gffStruct, GUIText text)
        {
            var textStruct = new GFFStruct(0);
            if (!string.IsNullOrEmpty(text.Text))
            {
                textStruct.SetString("TEXT", text.Text);
            }
            textStruct.SetUInt32("STRREF", text.StrRef == -1 ? 0xFFFFFFFFU : (uint)text.StrRef);
            if (text.Pulsing.HasValue)
            {
                textStruct.SetUInt8("PULSING", (byte)text.Pulsing.Value);
            }
            textStruct.SetResRef("FONT", text.Font);
            textStruct.SetInt32("ALIGNMENT", text.Alignment);
            if (text.Color != null)
            {
                textStruct.SetVector3("COLOR", new Vector3(text.Color.R, text.Color.G, text.Color.B));
                if (text.Color.A != 1.0f)
                {
                    textStruct.SetSingle("ALPHA", text.Color.A);
                }
            }
            gffStruct.SetStruct("TEXT", textStruct);
        }

        /// <summary>
        /// Writes moveto values to a GFF struct.
        /// Based on PyKotor write_moveto: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:878
        /// </summary>
        private void WriteMoveTo(GFFStruct gffStruct, GUIMoveTo moveto)
        {
            var movetoStruct = gffStruct.Acquire<GFFStruct>("MOVETO", new GFFStruct(0)); movetoStruct.SetInt32("UP", moveto.Up);
            movetoStruct.SetInt32("DOWN", moveto.Down);
            movetoStruct.SetInt32("LEFT", moveto.Left);
            movetoStruct.SetInt32("RIGHT", moveto.Right);
        }

        /// <summary>
        /// Writes hilight values to a GFF struct.
        /// Based on PyKotor write_hilight: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:889
        /// </summary>
        private void WriteHilight(GFFStruct gffStruct, GUIBorder hilight)
        {
            var hilightStruct = new GFFStruct(0);
            if (hilight.Color != null)
            {
                hilightStruct.SetVector3("COLOR", new Vector3(hilight.Color.R, hilight.Color.G, hilight.Color.B));
            }
            hilightStruct.SetResRef("CORNER", hilight.Corner);
            hilightStruct.SetInt32("DIMENSION", hilight.Dimension);
            hilightStruct.SetResRef("EDGE", hilight.Edge);
            hilightStruct.SetResRef("FILL", hilight.Fill);
            hilightStruct.SetInt32("FILLSTYLE", hilight.FillStyle);
            if (hilight.InnerOffset.HasValue)
            {
                hilightStruct.SetInt32("INNEROFFSET", hilight.InnerOffset.Value);
            }
            if (hilight.InnerOffsetY.HasValue)
            {
                hilightStruct.SetInt32("INNEROFFSETY", hilight.InnerOffsetY.Value);
            }
            if (hilight.Pulsing.HasValue)
            {
                hilightStruct.SetUInt8("PULSING", (byte)hilight.Pulsing.Value);
            }
            gffStruct.SetStruct("HILIGHT", hilightStruct);
        }

        /// <summary>
        /// Writes border-like values (SELECTED, HILIGHTSELECTED) to a GFF struct.
        /// Based on PyKotor write_border_like: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:909
        /// </summary>
        private void WriteBorderLike(GFFStruct gffStruct, string fieldName, GUIBorder border)
        {
            var borderStruct = new GFFStruct(0);
            if (border.Color != null)
            {
                borderStruct.SetVector3("COLOR", new Vector3(border.Color.R, border.Color.G, border.Color.B));
                if (border.Color.A != 1.0f)
                {
                    borderStruct.SetSingle("ALPHA", border.Color.A);
                }
            }
            borderStruct.SetResRef("CORNER", border.Corner);
            borderStruct.SetInt32("DIMENSION", border.Dimension);
            borderStruct.SetResRef("EDGE", border.Edge);
            borderStruct.SetResRef("FILL", border.Fill);
            borderStruct.SetInt32("FILLSTYLE", border.FillStyle);
            if (border.InnerOffset.HasValue)
            {
                borderStruct.SetInt32("INNEROFFSET", border.InnerOffset.Value);
            }
            if (border.InnerOffsetY.HasValue)
            {
                borderStruct.SetInt32("INNEROFFSETY", border.InnerOffsetY.Value);
            }
            if (border.Pulsing.HasValue)
            {
                borderStruct.SetUInt8("PULSING", (byte)border.Pulsing.Value);
            }
            gffStruct.SetStruct(fieldName, borderStruct);
        }

        /// <summary>
        /// Writes selected state values to a GFF struct.
        /// Based on PyKotor write_border_like for SELECTED: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:909
        /// </summary>
        private void WriteSelected(GFFStruct gffStruct, GUISelected selected)
        {
            var selectedStruct = new GFFStruct(0);
            if (selected.Color != null)
            {
                selectedStruct.SetVector3("COLOR", new Vector3(selected.Color.R, selected.Color.G, selected.Color.B));
            }
            selectedStruct.SetResRef("CORNER", selected.Corner);
            selectedStruct.SetInt32("DIMENSION", selected.Dimension);
            selectedStruct.SetResRef("EDGE", selected.Edge);
            selectedStruct.SetResRef("FILL", selected.Fill);
            selectedStruct.SetInt32("FILLSTYLE", selected.FillStyle);
            if (selected.InnerOffset.HasValue)
            {
                selectedStruct.SetInt32("INNEROFFSET", selected.InnerOffset.Value);
            }
            if (selected.InnerOffsetY.HasValue)
            {
                selectedStruct.SetInt32("INNEROFFSETY", selected.InnerOffsetY.Value);
            }
            if (selected.Pulsing.HasValue)
            {
                selectedStruct.SetUInt8("PULSING", (byte)selected.Pulsing.Value);
            }
            gffStruct.SetStruct("SELECTED", selectedStruct);
        }

        /// <summary>
        /// Writes hilight+selected state values to a GFF struct.
        /// Based on PyKotor write_hilight_selected: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:929
        /// </summary>
        private void WriteHilightSelected(GFFStruct gffStruct, GUIHilightSelected hilightSelected)
        {
            var hilightSelectedStruct = new GFFStruct(0);
            if (hilightSelected.Color != null)
            {
                hilightSelectedStruct.SetVector3("COLOR", new Vector3(hilightSelected.Color.R, hilightSelected.Color.G, hilightSelected.Color.B));
            }
            hilightSelectedStruct.SetResRef("CORNER", hilightSelected.Corner);
            hilightSelectedStruct.SetInt32("DIMENSION", hilightSelected.Dimension);
            hilightSelectedStruct.SetResRef("EDGE", hilightSelected.Edge);
            hilightSelectedStruct.SetResRef("FILL", hilightSelected.Fill);
            hilightSelectedStruct.SetInt32("FILLSTYLE", hilightSelected.FillStyle);
            if (hilightSelected.InnerOffset.HasValue)
            {
                hilightSelectedStruct.SetInt32("INNEROFFSET", hilightSelected.InnerOffset.Value);
            }
            if (hilightSelected.InnerOffsetY.HasValue)
            {
                hilightSelectedStruct.SetInt32("INNEROFFSETY", hilightSelected.InnerOffsetY.Value);
            }
            if (hilightSelected.Pulsing.HasValue)
            {
                hilightSelectedStruct.SetUInt8("PULSING", (byte)hilightSelected.Pulsing.Value);
            }
            gffStruct.SetStruct("HILIGHTSELECTED", hilightSelectedStruct);
        }

        /// <summary>
        /// Writes scrollbar thumb or direction values to a GFF struct.
        /// Based on PyKotor write_scrollbar_thumb_or_dir: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:776
        /// </summary>
        private void WriteScrollbarThumbOrDir(GFFStruct gffStruct, string fieldName, GUIScrollbarThumb thumbOrDir)
        {
            var thumbStruct = new GFFStruct(0);
            thumbStruct.SetResRef("IMAGE", thumbOrDir.Image);
            thumbStruct.SetInt32("ALIGNMENT", thumbOrDir.Alignment);
            if (thumbOrDir.Rotate.HasValue)
            {
                thumbStruct.SetSingle("ROTATE", thumbOrDir.Rotate.Value);
            }
            if (thumbOrDir.FlipStyle.HasValue)
            {
                thumbStruct.SetInt32("FLIPSTYLE", thumbOrDir.FlipStyle.Value);
            }
            if (thumbOrDir.DrawStyle.HasValue)
            {
                thumbStruct.SetInt32("DRAWSTYLE", thumbOrDir.DrawStyle.Value);
            }
            gffStruct.SetStruct(fieldName, thumbStruct);
        }

        /// <summary>
        /// Writes scrollbar direction values to a GFF struct.
        /// </summary>
        private void WriteScrollbarDir(GFFStruct gffStruct, GUIScrollbarDir dir)
        {
            var dirStruct = new GFFStruct(0);
            dirStruct.SetResRef("IMAGE", dir.Image);
            dirStruct.SetInt32("ALIGNMENT", dir.Alignment);
            if (dir.Rotate.HasValue)
            {
                dirStruct.SetSingle("ROTATE", dir.Rotate.Value);
            }
            if (dir.FlipStyle.HasValue)
            {
                dirStruct.SetInt32("FLIPSTYLE", dir.FlipStyle.Value);
            }
            if (dir.DrawStyle.HasValue)
            {
                dirStruct.SetInt32("DRAWSTYLE", dir.DrawStyle.Value);
            }
            gffStruct.SetStruct("DIR", dirStruct);
        }

        /// <summary>
        /// Writes proto item to a GFF struct.
        /// Based on PyKotor write_proto_item: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:792
        /// </summary>
        private void WriteProtoItem(GFFStruct gffStruct, GUIProtoItem proto)
        {
            var protoStruct = new GFFStruct(0);
            protoStruct.SetInt32("CONTROLTYPE", (int)GUIControlType.ProtoItem);
            protoStruct.SetString("TAG", "PROTOITEM");
            if (!string.IsNullOrEmpty(proto.ParentTag))
            {
                protoStruct.SetString("Obj_Parent", proto.ParentTag);
            }
            if (proto.ParentId.HasValue)
            {
                protoStruct.SetInt32("Obj_ParentID", proto.ParentId.Value);
            }

            // Basic properties
            if (proto.GuiText != null)
            {
                WriteText(protoStruct, proto.GuiText);
            }
            if (proto.Hilight != null)
            {
                WriteHilight(protoStruct, proto.Hilight);
            }

            // Extent
            WriteExtent(protoStruct, proto);

            // Border
            if (proto.Border != null)
            {
                WriteBorder(protoStruct, proto.Border);
            }

            gffStruct.SetStruct("PROTOITEM", protoStruct);
        }

        /// <summary>
        /// Writes scrollbar to a GFF struct.
        /// Based on PyKotor write_scrollbar: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:818
        /// </summary>
        private void WriteScrollbar(GFFStruct gffStruct, GUIScrollbar scroll)
        {
            var scrollStruct = new GFFStruct(0);
            if (scroll.DrawMode.HasValue)
            {
                scrollStruct.SetUInt8("DRAWMODE", (byte)scroll.DrawMode.Value);
            }
            scrollStruct.SetInt32("CONTROLTYPE", (int)GUIControlType.ScrollBar);
            scrollStruct.SetString("TAG", "SCROLLBAR");
            if (!string.IsNullOrEmpty(scroll.ParentTag))
            {
                scrollStruct.SetString("Obj_Parent", scroll.ParentTag);
            }
            if (scroll.ParentId.HasValue)
            {
                scrollStruct.SetInt32("Obj_ParentID", scroll.ParentId.Value);
            }
            if (scroll.Locked.HasValue)
            {
                scrollStruct.SetUInt8("Obj_Locked", (byte)(scroll.Locked.Value ? 1 : 0));
            }
            if (scroll.MaxValue.HasValue)
            {
                scrollStruct.SetInt32("MAXVALUE", scroll.MaxValue.Value);
            }
            if (scroll.VisibleValue != 0)
            {
                scrollStruct.SetInt32("VISIBLEVALUE", scroll.VisibleValue);
            }
            if (scroll.CurrentValue.HasValue)
            {
                scrollStruct.SetInt32("CURVALUE", scroll.CurrentValue.Value);
            }
            if (scroll.Padding.HasValue)
            {
                scrollStruct.SetInt32("PADDING", scroll.Padding.Value);
            }

            // Extent
            WriteExtent(scrollStruct, scroll);

            // Border
            if (scroll.Border != null)
            {
                WriteBorder(scrollStruct, scroll.Border);
            }

            // Direction and thumb
            if (scroll.GuiDirection != null)
            {
                WriteScrollbarDir(scrollStruct, scroll.GuiDirection);
            }
            if (scroll.GuiThumb != null)
            {
                WriteScrollbarThumbOrDir(scrollStruct, "THUMB", scroll.GuiThumb);
            }
            if (scroll.Color != null)
            {
                if (scroll.Color.A != 1.0f)
                {
                    scrollStruct.SetSingle("ALPHA", scroll.Color.A);
                }
                scrollStruct.SetVector3("COLOR", new Vector3(scroll.Color.R, scroll.Color.G, scroll.Color.B));
            }

            gffStruct.SetStruct("SCROLLBAR", scrollStruct);
        }

        /// <summary>
        /// Writes progress bar properties to a GFF struct.
        /// Based on PyKotor dismantle_control progress handling: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:984
        /// </summary>
        private void WriteProgressBarProperties(GFFStruct gffStruct, GUIProgressBar progressBar)
        {
            if (progressBar.StartFromLeft != 0)
            {
                gffStruct.SetUInt8("STARTFROMLEFT", (byte)progressBar.StartFromLeft);
            }

            // Write MAXVALUE and CURVALUE for progress bar
            gffStruct.SetInt32("MAXVALUE", (int)progressBar.MaxValue);
            gffStruct.SetInt32("CURVALUE", progressBar.CurrentValue);

            // Write PROGRESS struct - note that GUIProgressBar.Progress is float? but we need GUIProgress struct
            // Check if there's a Progress struct in the base class Progress property
            var baseControl = (GUIControl)progressBar;
            if (baseControl.Progress != null)
            {
                var progressStruct = gffStruct.Acquire<GFFStruct>("PROGRESS", new GFFStruct(0));
                if (baseControl.Progress.Color != null)
                {
                    progressStruct.SetVector3("COLOR", new Vector3(baseControl.Progress.Color.R, baseControl.Progress.Color.G, baseControl.Progress.Color.B));
                    if (baseControl.Progress.Color.A != 1.0f)
                    {
                        progressStruct.SetSingle("ALPHA", baseControl.Progress.Color.A);
                    }
                }
                progressStruct.SetResRef("CORNER", baseControl.Progress.Corner);
                progressStruct.SetInt32("DIMENSION", baseControl.Progress.Dimension);
                progressStruct.SetResRef("EDGE", baseControl.Progress.Edge);
                progressStruct.SetResRef("FILL", baseControl.Progress.Fill);
                progressStruct.SetInt32("FILLSTYLE", baseControl.Progress.FillStyle);
                if (baseControl.Progress.InnerOffsetY.HasValue)
                {
                    progressStruct.SetInt32("INNEROFFSETY", baseControl.Progress.InnerOffsetY.Value);
                }
                progressStruct.SetInt32("INNEROFFSET", baseControl.Progress.InnerOffset);
                if (baseControl.Progress.Pulsing.HasValue)
                {
                    progressStruct.SetUInt8("PULSING", (byte)baseControl.Progress.Pulsing.Value);
                }
            }
        }

        /// <summary>
        /// Writes slider properties to a GFF struct.
        /// Based on PyKotor dismantle_control slider handling: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:1006
        /// </summary>
        private void WriteSliderProperties(GFFStruct gffStruct, GUISlider slider)
        {
            // Write MAXVALUE and CURVALUE for slider (stored as int in GFF)
            gffStruct.SetInt32("MAXVALUE", (int)slider.MaxValue);
            gffStruct.SetInt32("CURVALUE", (int)slider.Value);

            var baseControl = (GUIControl)slider;
            if (baseControl.Thumb != null)
            {
                var thumbStruct = gffStruct.Acquire<GFFStruct>("THUMB", new GFFStruct(0));
                thumbStruct.SetResRef("IMAGE", baseControl.Thumb.Image);
                thumbStruct.SetInt32("ALIGNMENT", baseControl.Thumb.Alignment);
                if (baseControl.Thumb.Rotate.HasValue)
                {
                    thumbStruct.SetSingle("ROTATE", baseControl.Thumb.Rotate.Value);
                }
                if (baseControl.Thumb.FlipStyle.HasValue)
                {
                    thumbStruct.SetInt32("FLIPSTYLE", baseControl.Thumb.FlipStyle.Value);
                }
                if (baseControl.Thumb.DrawStyle.HasValue)
                {
                    thumbStruct.SetInt32("DRAWSTYLE", baseControl.Thumb.DrawStyle.Value);
                }
            }

            // Direction: 0 = horizontal, 1 = vertical
            int direction = slider.Direction == "vertical" ? 1 : 0;
            gffStruct.SetInt32("DIRECTION", direction);
        }

        /// <summary>
        /// Writes listbox properties to a GFF struct.
        /// Based on PyKotor dismantle_control listbox handling: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:1035
        /// </summary>
        private void WriteListBoxProperties(GFFStruct gffStruct, GUIListBox listBox)
        {
            if (listBox.ProtoItem != null)
            {
                WriteProtoItem(gffStruct, listBox.ProtoItem);
            }
            if (listBox.ScrollBar != null)
            {
                WriteScrollbar(gffStruct, listBox.ScrollBar);
            }
        }

        /// <summary>
        /// Writes checkbox properties to a GFF struct.
        /// Based on PyKotor dismantle_control checkbox handling: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:1042
        /// </summary>
        private void WriteCheckBoxProperties(GFFStruct gffStruct, GUICheckBox checkBox)
        {
            var baseControl = (GUIControl)checkBox;
            if (baseControl.Selected != null)
            {
                WriteSelected(gffStruct, baseControl.Selected);
            }
            if (baseControl.HilightSelected != null)
            {
                WriteHilightSelected(gffStruct, baseControl.HilightSelected);
            }
            if (checkBox.IsSelected.HasValue && checkBox.IsSelected.Value != 0)
            {
                gffStruct.SetUInt8("ISSELECTED", 1);
            }
        }

        /// <summary>
        /// Writes button properties to a GFF struct.
        /// Button-specific properties are mostly in TEXT struct, which is already written.
        /// </summary>
        private void WriteButtonProperties(GFFStruct gffStruct, GUIButton button)
        {
            var baseControl = (GUIControl)button;
            if (button.Pulsing.HasValue)
            {
                gffStruct.SetUInt8("PULSING", (byte)button.Pulsing.Value);
            }
        }

        /// <summary>
        /// Writes panel properties to a GFF struct.
        /// </summary>
        private void WritePanelProperties(GFFStruct gffStruct, GUIPanel panel)
        {
            if (panel.Alpha != 1.0f)
            {
                gffStruct.SetSingle("ALPHA", panel.Alpha);
            }
        }

        /// <summary>
        /// Writes scrollbar control properties to a GFF struct.
        /// </summary>
        private void WriteScrollBarProperties(GFFStruct gffStruct, GUIControl control)
        {
            var scrollBar = control as GUIScrollbar;
            if (scrollBar == null)
            {
                return;
            }

            if (scrollBar.GuiDirection != null)
            {
                WriteScrollbarDir(gffStruct, scrollBar.GuiDirection);
            }
            if (scrollBar.GuiThumb != null)
            {
                WriteScrollbarThumbOrDir(gffStruct, "THUMB", scrollBar.GuiThumb);
            }

            // DIR image stored in Properties for non-GUIScrollbar controls
            if (control.Properties.ContainsKey("DIR_IMAGE"))
            {
                var dirStruct = gffStruct.Acquire<GFFStruct>("DIR", new GFFStruct(0));
                dirStruct.SetResRef("IMAGE", (ResRef)control.Properties["DIR_IMAGE"]);
                dirStruct.SetInt32("ALIGNMENT", 18); // Default alignment
            }

            // Thumb stored in base control
            if (control.Thumb != null)
            {
                WriteScrollbarThumbOrDir(gffStruct, "THUMB", control.Thumb);
            }
        }

        /// <summary>
        /// Writes protoitem properties to a GFF struct.
        /// </summary>
        private void WriteProtoItemProperties(GFFStruct gffStruct, GUIProtoItem protoItem)
        {
            if (protoItem.Pulsing.HasValue)
            {
                gffStruct.SetUInt8("PULSING", (byte)protoItem.Pulsing.Value);
            }
        }
    }
}
