using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using BioWare.NET.Resource.Formats.LYT;
using ResRef = BioWare.NET.Common.ResRef;

namespace HolocronToolset.Editors.LYT
{
    /// <summary>
    /// Graphics scene control for rendering LYT layout elements.
    /// Uses Avalonia Canvas for 2D visualization of 3D LYT data.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:50-52
    /// Original: self.scene: QGraphicsScene = QGraphicsScene()
    /// </summary>
    public class LYTGraphicsScene : Canvas
    {
        private readonly List<LYTGraphicsItem> _items = new List<LYTGraphicsItem>();
        private double _zoomLevel = 1.0;
        private Vector2 _panOffset = Vector2.Zero;

        /// <summary>
        /// Gets or sets the zoom level for the scene (1.0 = 100%).
        /// </summary>
        public double ZoomLevel
        {
            get { return _zoomLevel; }
            set
            {
                _zoomLevel = Math.Max(0.1, Math.Min(10.0, value));
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets or sets the pan offset for the scene.
        /// </summary>
        public Vector2 PanOffset
        {
            get { return _panOffset; }
            set
            {
                _panOffset = value;
                InvalidateVisual();
            }
        }

        public LYTGraphicsScene()
        {
            Background = Brushes.Black;
            ClipToBounds = true;
        }

        /// <summary>
        /// Clears all graphics items from the scene.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:226
        /// Original: self.scene.clear()
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            Children.Clear();
            InvalidateVisual();
        }

        /// <summary>
        /// Adds a graphics item to the scene.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:228-234
        /// Original: self.scene.addItem(RoomItem(room, self))
        /// </summary>
        public void AddItem(LYTGraphicsItem item)
        {
            if (item == null)
            {
                return;
            }

            _items.Add(item);
            Children.Add(item);
            InvalidateVisual();
        }

        /// <summary>
        /// Removes a graphics item from the scene.
        /// </summary>
        public void RemoveItem(LYTGraphicsItem item)
        {
            if (item == null)
            {
                return;
            }

            _items.Remove(item);
            Children.Remove(item);
            InvalidateVisual();
        }

        /// <summary>
        /// Converts 3D world position to 2D screen coordinates.
        /// Projects X,Y coordinates to screen space (Z is ignored for 2D view).
        /// </summary>
        public Point WorldToScreen(Vector3 worldPos)
        {
            float screenX = (worldPos.X + _panOffset.X) * (float)_zoomLevel;
            float screenY = (worldPos.Y + _panOffset.Y) * (float)_zoomLevel;
            return new Point(screenX, screenY);
        }

        /// <summary>
        /// Converts 2D screen coordinates to 3D world position.
        /// </summary>
        public Vector3 ScreenToWorld(Point screenPos)
        {
            float worldX = ((float)screenPos.X / (float)_zoomLevel) - _panOffset.X;
            float worldY = ((float)screenPos.Y / (float)_zoomLevel) - _panOffset.Y;
            return new Vector3(worldX, worldY, 0);
        }
    }

    /// <summary>
    /// Base class for LYT graphics items rendered in the scene.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py
    /// Original: class RoomItem(QGraphicsItem), class TrackItem(QGraphicsItem), etc.
    /// </summary>
    public abstract class LYTGraphicsItem : Control
    {
        protected LYTGraphicsScene _scene;
        protected Vector3 _worldPosition;

        /// <summary>
        /// Gets or sets the world position of this item.
        /// </summary>
        public Vector3 WorldPosition
        {
            get { return _worldPosition; }
            set
            {
                _worldPosition = value;
                UpdatePosition();
            }
        }

        protected LYTGraphicsItem(LYTGraphicsScene scene, Vector3 worldPosition)
        {
            _scene = scene;
            _worldPosition = worldPosition;
            UpdatePosition();
        }

        /// <summary>
        /// Updates the screen position based on world position and scene transform.
        /// </summary>
        protected void UpdatePosition()
        {
            if (_scene != null)
            {
                Point screenPos = _scene.WorldToScreen(_worldPosition);
                Canvas.SetLeft(this, screenPos.X);
                Canvas.SetTop(this, screenPos.Y);
            }
        }

        /// <summary>
        /// Renders the graphics item.
        /// </summary>
        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            RenderItem(context);
        }

        /// <summary>
        /// Renders the specific graphics item (implemented by subclasses).
        /// </summary>
        protected abstract void RenderItem(DrawingContext context);
    }

    /// <summary>
    /// Graphics item for rendering LYT rooms.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py
    /// Original: class RoomItem(QGraphicsRectItem)
    /// </summary>
    public class RoomItem : LYTGraphicsItem
    {
        private readonly LYTRoom _room;
        private const double RoomSize = 50.0; // Default room size in screen pixels

        public RoomItem(LYTGraphicsScene scene, LYTRoom room)
            : base(scene, room != null ? room.Position : Vector3.Zero)
        {
            _room = room;
            Width = RoomSize;
            Height = RoomSize;
        }

        protected override void RenderItem(DrawingContext context)
        {
            if (_room == null)
            {
                return;
            }

            // Draw room as a rectangle
            var rect = new Rect(0, 0, RoomSize, RoomSize);
            var brush = new SolidColorBrush(Colors.Blue);
            var pen = new Pen(new SolidColorBrush(Colors.LightBlue), 2.0);

            context.DrawRectangle(brush, pen, rect);

            // Draw room label (model name)
            if (_room.Model != null)
            {
                var formattedText = new FormattedText(
                    _room.Model.ToString(),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    10,
                    Brushes.White);

                context.DrawText(formattedText, new Point(5, RoomSize + 2));
            }
        }
    }

    /// <summary>
    /// Graphics item for rendering LYT tracks.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py
    /// Original: class TrackItem(QGraphicsEllipseItem)
    /// </summary>
    public class TrackItem : LYTGraphicsItem
    {
        private readonly LYTTrack _track;
        private const double TrackSize = 20.0; // Default track size in screen pixels

        public TrackItem(LYTGraphicsScene scene, LYTTrack track)
            : base(scene, track != null ? track.Position : Vector3.Zero)
        {
            _track = track;
            Width = TrackSize;
            Height = TrackSize;
        }

        protected override void RenderItem(DrawingContext context)
        {
            if (_track == null)
            {
                return;
            }

            // Draw track as a circle
            var ellipse = new EllipseGeometry(new Rect(0, 0, TrackSize, TrackSize));
            var brush = new SolidColorBrush(Colors.Green);
            var pen = new Pen(new SolidColorBrush(Colors.LightGreen), 2.0);

            context.DrawGeometry(brush, pen, ellipse);
        }
    }

    /// <summary>
    /// Graphics item for rendering LYT obstacles.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py
    /// Original: class ObstacleItem(QGraphicsRectItem)
    /// </summary>
    public class ObstacleItem : LYTGraphicsItem
    {
        private readonly LYTObstacle _obstacle;
        private const double ObstacleSize = 30.0; // Default obstacle size in screen pixels

        public ObstacleItem(LYTGraphicsScene scene, LYTObstacle obstacle)
            : base(scene, obstacle != null ? obstacle.Position : Vector3.Zero)
        {
            _obstacle = obstacle;
            Width = ObstacleSize;
            Height = ObstacleSize;
        }

        protected override void RenderItem(DrawingContext context)
        {
            if (_obstacle == null)
            {
                return;
            }

            // Draw obstacle as a rectangle with X pattern
            var rect = new Rect(0, 0, ObstacleSize, ObstacleSize);
            var brush = new SolidColorBrush(Colors.Red);
            var pen = new Pen(new SolidColorBrush(Colors.DarkRed), 2.0);

            context.DrawRectangle(brush, pen, rect);

            // Draw X pattern
            var xPen = new Pen(new SolidColorBrush(Colors.White), 2.0);
            context.DrawLine(xPen, new Point(0, 0), new Point(ObstacleSize, ObstacleSize));
            context.DrawLine(xPen, new Point(ObstacleSize, 0), new Point(0, ObstacleSize));
        }
    }

    /// <summary>
    /// Graphics item for rendering LYT door hooks.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py
    /// Original: class DoorHookItem(QGraphicsEllipseItem)
    /// </summary>
    public class DoorHookItem : LYTGraphicsItem
    {
        private readonly LYTDoorHook _doorHook;
        private const double DoorHookSize = 15.0; // Default door hook size in screen pixels

        public DoorHookItem(LYTGraphicsScene scene, LYTDoorHook doorHook)
            : base(scene, doorHook != null ? doorHook.Position : Vector3.Zero)
        {
            _doorHook = doorHook;
            Width = DoorHookSize;
            Height = DoorHookSize;
        }

        protected override void RenderItem(DrawingContext context)
        {
            if (_doorHook == null)
            {
                return;
            }

            // Draw door hook as a diamond shape
            var diamond = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = new Point(DoorHookSize / 2, 0),
                IsClosed = true
            };
            figure.Segments.Add(new LineSegment { Point = new Point(DoorHookSize, DoorHookSize / 2) });
            figure.Segments.Add(new LineSegment { Point = new Point(DoorHookSize / 2, DoorHookSize) });
            figure.Segments.Add(new LineSegment { Point = new Point(0, DoorHookSize / 2) });
            diamond.Figures.Add(figure);

            var brush = new SolidColorBrush(Colors.Orange);
            var pen = new Pen(new SolidColorBrush(Colors.Yellow), 2.0);

            context.DrawGeometry(brush, pen, diamond);
        }
    }
}

