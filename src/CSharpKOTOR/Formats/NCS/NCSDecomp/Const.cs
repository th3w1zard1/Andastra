//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Stack
{
    public class Const : StackEntry
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:14-19
        // Original: public static Const newConst(Type type, Long intValue)
        public static Const NewConst(UtilsType type, long intValue)
        {
            if (type.ByteValue() != 3)
            {
                throw new Exception("Invalid const type for int value: " + type);
            }
            return new IntConst(intValue);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:21-26
        // Original: public static Const newConst(Type type, Float floatValue)
        public static Const NewConst(UtilsType type, float floatValue)
        {
            if (type.ByteValue() != 4)
            {
                throw new Exception("Invalid const type for float value: " + type);
            }
            return new FloatConst(floatValue);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:28-33
        // Original: public static Const newConst(Type type, String stringValue)
        public static Const NewConst(UtilsType type, string stringValue)
        {
            if (type.ByteValue() != 5)
            {
                throw new Exception("Invalid const type for string value: " + type);
            }
            return new StringConst(stringValue);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:35-40
        // Original: public static Const newConst(Type type, Integer objectValue)
        public static Const NewConst(UtilsType type, int objectValue)
        {
            if (type.ByteValue() != 6)
            {
                throw new Exception("Invalid const type for object value: " + type);
            }
            return new ObjectConst(objectValue);
        }

        // Convenience method for object-based calls (used internally)
        public static Const NewConst(UtilsType type, object value)
        {
            switch (type.ByteValue())
            {
                case 3:
                    {
                        return new IntConst(value);
                    }

                case 4:
                    {
                        return new FloatConst(value);
                    }

                case 5:
                    {
                        return new StringConst(value);
                    }

                case 6:
                    {
                        return new ObjectConst(value);
                    }

                default:
                    {
                        throw new Exception("Invalid const type " + type);
                    }

            }
        }

        public override void RemovedFromStack(LocalStack stack)
        {
        }

        public override void AddedToStack(LocalStack stack)
        {
        }

        public override void DoneParse()
        {
        }

        public override void DoneWithStack(LocalVarStack stack)
        {
        }

        public override string ToString()
        {
            return "";
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:64-70
        // Original: @Override public StackEntry getElement(int stackpos)
        public override StackEntry GetElement(int stackpos)
        {
            if (stackpos != 1)
            {
                throw new Exception("Position > 1 for const, not struct");
            }
            else
            {
                return this;
            }
        }
    }
}




