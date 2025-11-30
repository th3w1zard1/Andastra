using System;
using JetBrains.Annotations;
using TSLPatcher.Core.Common.Script;

namespace TSLPatcher.Core.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a single value on the NCS execution stack.
    /// </summary>
    public class StackObject
    {
        public DataType DataType { get; set; }
        public object Value { get; set; }

        public StackObject(DataType dataType, [CanBeNull] object value)
        {
            DataType = dataType;
            Value = value;
        }

        public override string ToString()
        {
            return $"{DataType}={Value}";
        }

        public override bool Equals([CanBeNull] object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is StackObject other)
            {
                return Value != null ? Value.Equals(other.Value) : other.Value == null;
            }
            return Value != null ? Value.Equals(obj) : obj == null;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DataType, Value != null ? Value.GetHashCode() : 0);
        }
    }
}

