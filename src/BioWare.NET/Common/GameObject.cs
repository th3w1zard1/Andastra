using System;
using System.Numerics;
using BioWare.NET.Common;

namespace BioWare.NET.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/game_object.py:28-327
    // Original: class ObjectType(IntEnum): class GameObjectState: class GameObject(ABC):
    public enum ObjectType
    {
        INVALID = 0,
        CREATURE = 1,
        DOOR = 2,
        ITEM = 3,
        TRIGGER = 4,
        PLACEABLE = 5,
        WAYPOINT = 6,
        ENCOUNTER = 7,
        STORE = 8,
        AREA = 9,
        SOUND = 10,
        CAMERA = 11
    }

    public class GameObjectState
    {
        public int ObjectId { get; set; }
        public string Tag { get; set; }
        public string Name { get; set; }
        public ResRef BlueprintResRef { get; set; }
        public Vector3 Position { get; set; }
        public float Facing { get; set; }
        public bool Visible { get; set; }
        public bool PlotFlag { get; set; }
        public bool Commandable { get; set; }
        public bool MinOneHp { get; set; }
        public bool Dead { get; set; }
        public bool Open { get; set; }

        public GameObjectState()
        {
            ObjectId = 0;
            Tag = string.Empty;
            Name = string.Empty;
            BlueprintResRef = ResRef.FromBlank();
            Position = Vector3.Zero;
            Facing = 0.0f;
            Visible = true;
            PlotFlag = false;
            Commandable = true;
            MinOneHp = false;
            Dead = false;
            Open = false;
        }
    }

    public abstract class GameObject
    {
        private readonly ObjectType _type;
        private readonly GameObjectState _state;

        protected GameObject(ObjectType objectType, GameObjectState state = null)
        {
            _type = objectType;
            _state = state ?? new GameObjectState();
        }

        public int ObjectId
        {
            get { return _state.ObjectId; }
            set { _state.ObjectId = value; }
        }

        public string Tag
        {
            get { return _state.Tag; }
            set { _state.Tag = value; }
        }

        public string Name
        {
            get { return _state.Name; }
            set { _state.Name = value; }
        }

        public ResRef BlueprintResRef
        {
            get { return _state.BlueprintResRef; }
            set { _state.BlueprintResRef = value; }
        }

        public Vector3 Position
        {
            get { return _state.Position; }
            set
            {
                _state.Position = value;
                OnPositionChanged();
            }
        }

        public float Facing
        {
            get { return _state.Facing; }
            set
            {
                _state.Facing = value;
                OnFacingChanged();
            }
        }

        public bool Visible
        {
            get { return _state.Visible; }
            set
            {
                _state.Visible = value;
                OnVisibilityChanged();
            }
        }

        public bool PlotFlag
        {
            get { return _state.PlotFlag; }
            set { _state.PlotFlag = value; }
        }

        public bool Commandable
        {
            get { return _state.Commandable; }
            set { _state.Commandable = value; }
        }

        public bool MinOneHp
        {
            get { return _state.MinOneHp; }
            set { _state.MinOneHp = value; }
        }

        public bool Dead
        {
            get { return _state.Dead; }
            set { _state.Dead = value; }
        }

        public bool Open
        {
            get { return _state.Open; }
            set { _state.Open = value; }
        }

        public ObjectType ObjectType
        {
            get { return _type; }
        }

        public float GetDistanceTo(Vector3 point)
        {
            float dx = Position.X - point.X;
            float dy = Position.Y - point.Y;
            float dz = Position.Z - point.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public float GetDistanceTo2D(Vector3 point)
        {
            float dx = Position.X - point.X;
            float dy = Position.Y - point.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public float GetDistanceToObject(GameObject other)
        {
            return GetDistanceTo(other.Position);
        }

        public float GetDistanceToObject2D(GameObject other)
        {
            return GetDistanceTo2D(other.Position);
        }

        public void FacePoint(Vector3 point)
        {
            float dx = point.X - Position.X;
            float dy = point.Y - Position.Y;
            Facing = (float)Math.Atan2(dy, dx);
        }

        public void FaceObject(GameObject other)
        {
            FacePoint(other.Position);
        }

        public abstract void Update(float deltaTime);

        public abstract void Die();

        public abstract bool IsSelectable();

        protected virtual void OnPositionChanged()
        {
        }

        protected virtual void OnFacingChanged()
        {
        }

        protected virtual void OnVisibilityChanged()
        {
        }
    }
}
