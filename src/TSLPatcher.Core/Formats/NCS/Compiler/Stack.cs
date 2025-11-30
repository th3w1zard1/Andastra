using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Common.Script;

namespace TSLPatcher.Core.Formats.NCS.Compiler
{

    /// <summary>
    /// Stack implementation for NCS bytecode execution.
    /// </summary>
    public class Stack
    {
        private readonly List<StackObject> _stack = new List<StackObject>();
        // These fields are intentionally mutable and will be used by save_bp/restore_bp methods (to be implemented)
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        // ReSharper disable once UnusedMember.Local
        private int _bp; // Mutable - modified by save_bp/restore_bp
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        // ReSharper disable once UnusedMember.Local
        private int _globalBp; // Mutable - set in save_bp when entering main
        private readonly List<int> _bpBuffer = new List<int>();

        /// <summary>
        /// Get a copy of the current stack state.
        /// </summary>
        public List<StackObject> State()
        {
            return _stack.ToList();
        }

        /// <summary>
        /// Add a value to the stack.
        /// </summary>
        public void Add(DataType dataType, [CanBeNull] object value)
        {
            _stack.Add(new StackObject(dataType, value));
        }

        /// <summary>
        /// Get the stack pointer (size in bytes, assuming 4 bytes per element).
        /// </summary>
        public int StackPointer()
        {
            return _stack.Count * 4;
        }

        /// <summary>
        /// Get the base pointer.
        /// </summary>
        public int BasePointer()
        {
            return _bp;
        }

        /// <summary>
        /// Peek at a value at the specified offset from the top of the stack.
        /// Returns the StackObject at that position.
        /// </summary>
        public StackObject Peek(int offset)
        {
            int realIndex = StackIndex(offset);
            return _stack[realIndex];
        }

        /// <summary>
        /// Move the stack pointer by offset (shrink or grow stack).
        /// </summary>
        public void Move(int offset)
        {
            if (offset == 0)
            {
                return;
            }

            if (offset > 0)
            {
                if (offset % 4 != 0)
                {
                    throw new ArgumentException($"Stack growth offset must be multiple of 4, got {offset}");
                }
                int words = offset / 4;
                for (int i = 0; i < words; i++)
                {
                    _stack.Add(new StackObject(DataType.Int, 0));
                }
                return;
            }

            int removeTo = StackIndex(offset);
            _stack.RemoveRange(removeTo, _stack.Count - removeTo);
        }

        /// <summary>
        /// Copy values from the top of the stack down to the specified offset.
        /// </summary>
        public void CopyDown(int offset, int size)
        {
            if (size % 4 != 0)
            {
                throw new ArgumentException("Size must be divisible by 4");
            }

            int numElements = size / 4;

            if (numElements > _stack.Count)
            {
                throw new IndexOutOfRangeException("Size exceeds the current stack size");
            }

            // Find the target indices first
            var targetIndices = new List<int>();
            int tempOffset = offset;

            for (int i = 0; i < numElements; i++)
            {
                int targetIndex = StackIndex(tempOffset);
                targetIndices.Add(targetIndex);
                tempOffset += 4; // Move to the next position
            }

            // Now copy the elements down the stack
            for (int i = 0; i < numElements; i++)
            {
                int sourceIndex = _stack.Count - 1 - i; // Counting from the end of the list
                int targetIndex = targetIndices[numElements - 1 - i]; // The last target index corresponds to the first source index
                _stack[targetIndex] = new StackObject(_stack[sourceIndex].DataType, _stack[sourceIndex].Value);
            }
        }

        /// <summary>
        /// Copy values from the stack to the top.
        /// </summary>
        public void CopyToTop(int offset, int size)
        {
            if (size <= 0 || size % 4 != 0)
            {
                throw new ArgumentException($"Size must be a positive multiple of 4, got {size}");
            }
            if (offset == 0 || offset % 4 != 0)
            {
                throw new ArgumentException($"Offset must be a non-zero multiple of 4, got {offset}");
            }
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Cannot copy from an empty stack");
            }

            int offsetAbs = Math.Abs(offset);
            int totalBytes = _stack.Sum(obj => obj.DataType.Size());
            if (offsetAbs > totalBytes)
            {
                throw new IndexOutOfRangeException($"Offset {offset} exceeds stack size {totalBytes}");
            }

            int lowerBound = offsetAbs - size;
            var copied = new List<StackObject>();
            int accumulated = 0;

            for (int index = _stack.Count - 1; index >= 0; index--)
            {
                StackObject element = _stack[index];
                accumulated += element.DataType.Size();
                if (accumulated <= lowerBound)
                {
                    continue;
                }
                if (accumulated > offsetAbs)
                {
                    break;
                }
                copied.Add(new StackObject(element.DataType, element.Value));
            }

            if (copied.Count == 0 || copied.Sum(obj => obj.DataType.Size()) != size)
            {
                throw new IndexOutOfRangeException(
                    $"Unable to copy block at offset {offset} with size {size}; " +
                    "stack contents do not align with requested segment");
            }

            copied.Reverse();
            _stack.AddRange(copied);
        }

        /// <summary>
        /// Pop and return the top stack element.
        /// </summary>
        public StackObject Pop()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Cannot pop from empty stack");
            }
            StackObject top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            return top;
        }

        /// <summary>
        /// Save the base pointer.
        /// </summary>
        public void SaveBp()
        {
            int prevBp = _bp;
            int newBp = StackPointer();
            _stack.Add(new StackObject(DataType.Int, prevBp));
            _bpBuffer.Add(prevBp);
            if (_globalBp == 0 && prevBp == 0)
            {
                _globalBp = newBp;
            }
            _bp = newBp;
        }

        /// <summary>
        /// Restore the base pointer.
        /// </summary>
        public void RestoreBp()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Cannot restore base pointer from an empty stack");
            }
            StackObject savedBp = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            if (savedBp.DataType != DataType.Int)
            {
                throw new InvalidOperationException(
                    $"Encountered non-integer value while restoring base pointer; found {savedBp.DataType}");
            }
            if (!(savedBp.Value is int))
            {
                throw new InvalidOperationException(
                    $"Base pointer restore requires integer value but received {savedBp.Value?.GetType()}");
            }
            _bp = (int)savedBp.Value;
            if (_bpBuffer.Count > 0)
            {
                _bpBuffer.RemoveAt(_bpBuffer.Count - 1);
            }
        }

        /// <summary>
        /// Increment value at stack offset.
        /// </summary>
        public void Increment(int offset)
        {
            int index = StackIndex(offset);
            StackObject element = _stack[index];
            StackObject newValue = new StackObject(element.DataType, element.Value);
            if (newValue.Value is int intVal)
            {
                newValue.Value = intVal + 1;
            }
            else if (newValue.Value is float floatVal)
            {
                newValue.Value = floatVal + 1.0f;
            }
            else
            {
                throw new InvalidOperationException($"Cannot increment non-numeric type {newValue.DataType}");
            }
            _stack[index] = newValue;
        }

        /// <summary>
        /// Decrement value at stack offset.
        /// </summary>
        public void Decrement(int offset)
        {
            int index = StackIndex(offset);
            StackObject element = _stack[index];
            StackObject newValue = new StackObject(element.DataType, element.Value);
            if (newValue.Value is int intVal)
            {
                newValue.Value = intVal - 1;
            }
            else if (newValue.Value is float floatVal)
            {
                newValue.Value = floatVal - 1.0f;
            }
            else
            {
                throw new InvalidOperationException($"Cannot decrement non-numeric type {newValue.DataType}");
            }
            _stack[index] = newValue;
        }

        /// <summary>
        /// Convert a base-pointer-relative offset to an actual list index.
        /// </summary>
        private int StackIndexBp(int offset)
        {
            if (offset >= 0)
            {
                throw new ArgumentException($"BP-relative offset must be negative, got {offset}");
            }

            int bpIndex = _bp / 4;
            int relativeIndex = Math.Abs(offset) / 4;
            int absoluteIndex = bpIndex - relativeIndex;

            if (absoluteIndex < 0 || absoluteIndex >= _stack.Count)
            {
                throw new ArgumentException($"BP-relative offset {offset} results in invalid index {absoluteIndex}");
            }

            return absoluteIndex;
        }

        /// <summary>
        /// Increment value at base-pointer-relative offset.
        /// </summary>
        public void IncrementBp(int offset)
        {
            int index = StackIndexBp(offset);
            StackObject element = _stack[index];
            StackObject newValue = new StackObject(element.DataType, element.Value);
            if (newValue.Value is int intVal)
            {
                newValue.Value = intVal + 1;
            }
            else if (newValue.Value is float floatVal)
            {
                newValue.Value = floatVal + 1.0f;
            }
            else
            {
                throw new InvalidOperationException($"Cannot increment non-numeric type {newValue.DataType}");
            }
            _stack[index] = newValue;
        }

        /// <summary>
        /// Decrement value at base-pointer-relative offset.
        /// </summary>
        public void DecrementBp(int offset)
        {
            int index = StackIndexBp(offset);
            StackObject element = _stack[index];
            StackObject newValue = new StackObject(element.DataType, element.Value);
            if (newValue.Value is int intVal)
            {
                newValue.Value = intVal - 1;
            }
            else if (newValue.Value is float floatVal)
            {
                newValue.Value = floatVal - 1.0f;
            }
            else
            {
                throw new InvalidOperationException($"Cannot decrement non-numeric type {newValue.DataType}");
            }
            _stack[index] = newValue;
        }

        /// <summary>
        /// Copy value from base-pointer-relative location to stack top.
        /// </summary>
        public void CopyTopBp(int offset, int size)
        {
            if (size % 4 != 0)
            {
                throw new ArgumentException($"Size must be multiple of 4, got {size}");
            }

            int copyIndex = StackIndexBp(offset);
            StackObject topValue = _stack[copyIndex];
            _stack.Add(new StackObject(topValue.DataType, topValue.Value));
        }

        /// <summary>
        /// Copy value from stack top down to base-pointer-relative location.
        /// </summary>
        public void CopyDownBp(int offset, int size)
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Cannot copy from empty stack");
            }

            if (size % 4 != 0)
            {
                throw new ArgumentException($"Size must be multiple of 4, got {size}");
            }

            StackObject topValue = _stack[_stack.Count - 1];
            int toIndex = StackIndexBp(offset);
            _stack[toIndex] = new StackObject(topValue.DataType, topValue.Value);
        }

        /// <summary>
        /// Perform addition operation on top two stack values.
        /// </summary>
        public void AdditionOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in addition operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            if (value1.Value is int || value1.Value is float || value2.Value is int || value2.Value is float)
            {
                double result = Convert.ToDouble(value2.Value) + Convert.ToDouble(value1.Value);
                _stack.RemoveAt(_stack.Count - 1);
                _stack.RemoveAt(_stack.Count - 1);
                if (value2.DataType == DataType.Int)
                {
                    Add(DataType.Int, (int)result);
                }
                else if (value2.DataType == DataType.Float)
                {
                    Add(DataType.Float, (float)result);
                }
                else
                {
                    // Check if result is a whole number (integer)
                    bool isWholeNumber = Math.Abs(result % 1) < double.Epsilon;
                    Add(isWholeNumber ? DataType.Int : DataType.Float, result);
                }
                return;
            }
            if (value1.Value is string && value2.Value is string)
            {
                string result = (string)value2.Value + (string)value1.Value;
                _stack.RemoveAt(_stack.Count - 1);
                _stack.RemoveAt(_stack.Count - 1);
                Add(DataType.String, result);
                return;
            }
            throw new InvalidOperationException(
                $"Addition requires numeric or string operands; got {value2.DataType} and {value1.DataType}");
        }

        /// <summary>
        /// Perform subtraction operation on top two stack values.
        /// </summary>
        public void SubtractionOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in subtraction operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            if (!(value1.Value is int || value1.Value is float) || !(value2.Value is int || value2.Value is float))
            {
                throw new InvalidOperationException("Subtraction requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) - Convert.ToDouble(value1.Value);
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);
            Add(value2.DataType, result);
        }

        /// <summary>
        /// Perform multiplication operation on top two stack values.
        /// </summary>
        public void MultiplicationOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in multiplication operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            if (!(value1.Value is int || value1.Value is float) || !(value2.Value is int || value2.Value is float))
            {
                throw new InvalidOperationException("Multiplication requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) * Convert.ToDouble(value1.Value);
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);
            Add(value2.DataType, result);
        }

        /// <summary>
        /// Perform division operation on top two stack values.
        /// </summary>
        public void DivisionOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in division operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            if (!(value1.Value is int || value1.Value is float) || !(value2.Value is int || value2.Value is float))
            {
                throw new InvalidOperationException("Division requires numeric operands");
            }
            double divisor = Convert.ToDouble(value1.Value);
            if (Math.Abs(divisor) < double.Epsilon)
            {
                throw new DivideByZeroException("Division by zero in NCS interpreter");
            }
            double result = Convert.ToDouble(value2.Value) / divisor;
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);
            Add(value2.DataType, (float)result);
        }

        /// <summary>
        /// Perform modulus operation on top two stack values.
        /// </summary>
        public void ModulusOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in modulus operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            if (!(value1.Value is int || value1.Value is float) || !(value2.Value is int || value2.Value is float))
            {
                throw new InvalidOperationException("Modulus requires numeric operands");
            }
            double divisor = Convert.ToDouble(value1.Value);
            if (Math.Abs(divisor) < double.Epsilon)
            {
                throw new DivideByZeroException("Modulus by zero in NCS interpreter");
            }
            double result = Convert.ToDouble(value2.Value) % divisor;
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);
            Add(value2.DataType, result);
        }

        /// <summary>
        /// Perform unary negation on top stack value.
        /// </summary>
        public void NegationOp()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Stack underflow in negation operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            if (!(value1.Value is int || value1.Value is float))
            {
                throw new InvalidOperationException($"Cannot negate non-numeric type {value1.DataType}");
            }
            double result = -Convert.ToDouble(value1.Value);
            _stack.RemoveAt(_stack.Count - 1);
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform logical NOT on top stack value.
        /// </summary>
        public void LogicalNotOp()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Stack underflow in logical NOT operation");
            }
            StackObject value1 = Pop();
            int result = value1.Value != null && !IsZeroValue(value1.Value) ? 0 : 1;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform logical AND on top two stack values.
        /// </summary>
        public void LogicalAndOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in logical AND operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            int result = (!IsZeroValue(value1.Value) && !IsZeroValue(value2.Value)) ? 1 : 0;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform logical OR on top two stack values.
        /// </summary>
        public void LogicalOrOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in logical OR operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            int result = (!IsZeroValue(value1.Value) || !IsZeroValue(value2.Value)) ? 1 : 0;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform bitwise NOT on top stack value.
        /// </summary>
        public void BitwiseNotOp()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Stack underflow in bitwise NOT operation");
            }
            StackObject value1 = Pop();
            if (!(value1.Value is int))
            {
                throw new InvalidOperationException($"Cannot perform bitwise NOT on non-integer type {value1.DataType}");
            }
            Add(value1.DataType, ~(int)value1.Value);
        }

        /// <summary>
        /// Perform bitwise OR on top two stack values.
        /// </summary>
        public void BitwiseOrOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in bitwise OR operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise OR requires integer operands");
            }
            Add(value1.DataType, (int)value1.Value | (int)value2.Value);
        }

        /// <summary>
        /// Perform bitwise XOR on top two stack values.
        /// </summary>
        public void BitwiseXorOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in bitwise XOR operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise XOR requires integer operands");
            }
            Add(value1.DataType, (int)value1.Value ^ (int)value2.Value);
        }

        /// <summary>
        /// Perform bitwise AND on top two stack values.
        /// </summary>
        public void BitwiseAndOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in bitwise AND operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise AND requires integer operands");
            }
            Add(value1.DataType, (int)value1.Value & (int)value2.Value);
        }

        /// <summary>
        /// Perform bitwise left shift on top two stack values.
        /// </summary>
        public void BitwiseLeftShiftOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in left shift operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise shift requires integer operands");
            }
            Add(value1.DataType, (int)value2.Value << (int)value1.Value);
        }

        /// <summary>
        /// Perform bitwise right shift on top two stack values.
        /// </summary>
        public void BitwiseRightShiftOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in right shift operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise shift requires integer operands");
            }
            Add(value1.DataType, (int)value2.Value >> (int)value1.Value);
        }

        /// <summary>
        /// Perform equality comparison on top two stack values.
        /// </summary>
        public void LogicalEqualityOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in equality comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            int result = Equals(value1.Value, value2.Value) ? 1 : 0;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform inequality comparison on top two stack values.
        /// </summary>
        public void LogicalInequalityOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in inequality comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            int result = !Equals(value1.Value, value2.Value) ? 1 : 0;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform greater-than comparison on top two stack values.
        /// </summary>
        public void CompareGreaterThanOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in greater-than comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value2.Value is int || value2.Value is float) || !(value1.Value is int || value1.Value is float))
            {
                throw new InvalidOperationException("Comparison requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) > Convert.ToDouble(value1.Value) ? 1 : 0;
            Add(value1.DataType, (int)result);
        }

        /// <summary>
        /// Perform greater-than-or-equal comparison on top two stack values.
        /// </summary>
        public void CompareGreaterThanOrEqualOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in greater-than-or-equal comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value2.Value is int || value2.Value is float) || !(value1.Value is int || value1.Value is float))
            {
                throw new InvalidOperationException("Comparison requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) >= Convert.ToDouble(value1.Value) ? 1 : 0;
            Add(value1.DataType, (int)result);
        }

        /// <summary>
        /// Perform less-than comparison on top two stack values.
        /// </summary>
        public void CompareLessThanOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in less-than comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value2.Value is int || value2.Value is float) || !(value1.Value is int || value1.Value is float))
            {
                throw new InvalidOperationException("Comparison requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) < Convert.ToDouble(value1.Value) ? 1 : 0;
            Add(value1.DataType, (int)result);
        }

        /// <summary>
        /// Perform less-than-or-equal comparison on top two stack values.
        /// </summary>
        public void CompareLessThanOrEqualOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in less-than-or-equal comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value2.Value is int || value2.Value is float) || !(value1.Value is int || value1.Value is float))
            {
                throw new InvalidOperationException("Comparison requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) <= Convert.ToDouble(value1.Value) ? 1 : 0;
            Add(value1.DataType, (int)result);
        }

        /// <summary>
        /// Store state (for action queue).
        /// </summary>
        public void StoreState()
        {
            // Placeholder - full implementation would handle action queue
        }

        private static bool IsZeroValue([CanBeNull] object value)
        {
            if (value == null)
            {
                return true;
            }
            if (value is int i)
            {
                return i == 0;
            }
            if (value is float f)
            {
                return Math.Abs(f) < float.Epsilon;
            }
            if (value is string s)
            {
                return string.IsNullOrEmpty(s);
            }
            return false;
        }

        private int StackIndex(int offset)
        {
            if (offset == 0)
            {
                throw new ArgumentException("Stack offset of zero is not valid");
            }
            if (offset % 4 != 0)
            {
                throw new ArgumentException($"Stack offset must be a multiple of 4 bytes, got {offset}");
            }
            if (_stack.Count == 0)
            {
                throw new ArgumentException("Cannot resolve stack offset on an empty stack");
            }

            int remaining = Math.Abs(offset);
            int listIndex = _stack.Count - 1;

            while (true)
            {
                if (listIndex < 0)
                {
                    throw new ArgumentException($"Stack offset {offset} is out of range");
                }

                StackObject element = _stack[listIndex];
                int elementSize = element.DataType.Size();
                if (elementSize <= 0)
                {
                    throw new ArgumentException($"Unsupported element size {elementSize} for {element.DataType}");
                }

                if (remaining <= elementSize)
                {
                    return listIndex;
                }

                remaining -= elementSize;
                listIndex -= 1;
            }
        }

    }
}

