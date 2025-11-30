using System;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Utils
{
    public class Type
    {
        public const sbyte VT_NONE = 0;
        public const sbyte VT_STACK = 1;
        public const sbyte VT_INTEGER = 3;
        public const sbyte VT_FLOAT = 4;
        public const sbyte VT_STRING = 5;
        public const sbyte VT_OBJECT = 6;
        public const sbyte VT_EFFECT = 16;
        public const sbyte VT_EVENT = 17;
        public const sbyte VT_LOCATION = 18;
        public const sbyte VT_TALENT = 19;
        public const sbyte VT_INTINT = 32;
        public const sbyte VT_FLOATFLOAT = 33;
        public const sbyte VT_OBJECTOBJECT = 34;
        public const sbyte VT_STRINGSTRING = 35;
        public const sbyte VT_STRUCTSTRUCT = 36;
        public const sbyte VT_INTFLOAT = 37;
        public const sbyte VT_FLOATINT = 38;
        public const sbyte VT_EFFECTEFFECT = 48;
        public const sbyte VT_EVENTEVENT = 49;
        public const sbyte VT_LOCLOC = 50;
        public const sbyte VT_TALTAL = 51;
        public const sbyte VT_VECTORVECTOR = 58;
        public const sbyte VT_VECTORFLOAT = 59;
        public const sbyte VT_FLOATVECTOR = 60;
        public const sbyte VT_VECTOR = -16;
        public const sbyte VT_STRUCT = -15;
        public const sbyte VT_INVALID = -1;

        protected sbyte type;
        protected int size;

        public Type(sbyte type)
        {
            this.type = type;
            this.size = 1;
        }

        public Type(string str)
        {
            this.type = Decode(str);
            this.size = TypeSize(this.type) / 4;
        }

        public static Type ParseType(string str)
        {
            return new Type(str);
        }

        public virtual void Close()
        {
        }

        public sbyte ByteValue()
        {
            return this.type;
        }

        public override string ToString()
        {
            return ToString(this.type);
        }

        public static string ToString(Type atype)
        {
            return ToString(atype.type);
        }

        public virtual string ToDeclString()
        {
            return this.ToString();
        }

        public int Size()
        {
            return this.size;
        }

        public virtual bool IsTyped()
        {
            return this.type != -1;
        }

        public string ToValueString()
        {
            return this.type.ToString();
        }

        protected static string ToString(sbyte type)
        {
            switch (type)
            {
                case 3: return "int";
                case 4: return "float";
                case 5: return "string";
                case 6: return "object";
                case 16: return "effect";
                case 18: return "location";
                case 19: return "talent";
                case 32: return "intint";
                case 33: return "floatfloat";
                case 34: return "objectobject";
                case 35: return "stringstring";
                case 36: return "structstruct";
                case 37: return "intfloat";
                case 38: return "floatint";
                case 48: return "effecteffect";
                case 49: return "eventevent";
                case 50: return "locloc";
                case 51: return "taltal";
                case 58: return "vectorvector";
                case 59: return "vectorfloat";
                case 60: return "floatvector";
                case 0: return "void";
                case 1: return "stack";
                case -16: return "vector";
                case -1: return "invalid";
                case -15: return "struct";
                default: return "unknown";
            }
        }

        private static sbyte Decode(string type)
        {
            switch (type)
            {
                case "void": return 0;
                case "int": return 3;
                case "float": return 4;
                case "string": return 5;
                case "object": return 6;
                case "effect": return 16;
                case "event": return 17;
                case "location": return 18;
                case "talent": return 19;
                case "vector": return -16;
                case "action": return 0;
                case "INT": return 3;
                case "OBJECT_ID": return 6;
                default: throw new RuntimeException("Attempted to get unknown type " + type);
            }
        }

        public int TypeSize()
        {
            return TypeSize(this.type);
        }

        public static int TypeSize(string type)
        {
            return TypeSize(Decode(type));
        }

        private static int TypeSize(sbyte type)
        {
            switch (type)
            {
                case 3: return 4;
                case 4: return 4;
                case 5: return 4;
                case 6: return 4;
                case 16: return 4;
                case 18: return 4;
                case 19: return 4;
                case 17: return 4;
                case 0: return 0;
                case -16: return 12;
                default: throw new RuntimeException("Unknown type code: " + type);
            }
        }

        public virtual Type GetElement(int pos)
        {
            if (pos != 1)
            {
                throw new RuntimeException("Position > 1 for type, not struct");
            }
            return this;
        }

        public override bool Equals(object obj)
        {
            return obj is Type other && this.type == other.type;
        }

        public bool Equals(sbyte type)
        {
            return this.type == type;
        }

        public override int GetHashCode()
        {
            return this.type;
        }
    }

    public class RuntimeException : Exception
    {
        public RuntimeException(string message) : base(message)
        {
        }
    }
}

