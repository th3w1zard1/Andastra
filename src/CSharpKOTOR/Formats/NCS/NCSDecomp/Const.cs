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

        public override StackEntry GetElement(int stackpos)
        {

            // For simple constants (non-struct), any position >= 1 returns this constant
            // The position calculation in LocalVarStack/VarStruct can produce values > 1
            // even for size-1 constants due to the formula (pos - offset + 1)
            // We treat constants as "atomic" - any sub-element access returns the whole constant
            if (stackpos < 1)
            {
                throw new Exception("Position " + stackpos + " must be >= 1");
            }

            return this;
        }
    }
}




