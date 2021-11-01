/*
 * Copyright (C) 2021. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    31 Oct 2021
 * 
 * File:    LeafExpression.cs
 * Purpose: Single leaf expression.
 */

using System.Runtime.InteropServices;
using BeauUtil;
using BeauUtil.Variants;

namespace Leaf.Runtime
{
    /// <summary>
    /// Single expression.
    /// </summary>
    public struct LeafExpression
    #if USING_BEAUDATA
        : BeauData.ISerializedObject
    #endif // USING_BEAUDATA
    {
        #region Types

        public enum TypeFlags : byte
        {
            IsLogical = 0x01
        }

        public enum OperandType : byte
        {
            Value,
            Read,
            Method
        }

        public struct Operand
        #if USING_BEAUDATA
            : BeauData.ISerializedObject
        #endif // USING_BEAUDATA
        {
            public OperandType Type;
            public OperandData Data;

            private Operand(OperandType inType)
            {
                Type = inType;
                Data = default(OperandData);
            }

            public Operand(Variant inValue)
                : this(OperandType.Value)
            {
                Data.Value = inValue;
            }

            public Operand(TableKeyPair inTableKey)
                : this(OperandType.Read)
            {
                Data.TableKey = inTableKey;
            }

            public Operand(StringHash32 inMethodId, uint inArgsIndex)
                : this(OperandType.Method)
            {
                Data.MethodId = inMethodId;
                Data.MethodArgsIndex = inArgsIndex;
            }

            #if USING_BEAUDATA

            public void Serialize(BeauData.Serializer ioSerializer)
            {
                ioSerializer.Enum("type", ref Type);
                switch(Type)
                {
                    case OperandType.Value:
                        ioSerializer.Object("value", ref Data.Value);
                        break;

                    case OperandType.Read:
                        ioSerializer.UInt32Proxy("tableId", ref Data.TableKey.TableId);
                        ioSerializer.UInt32Proxy("varId", ref Data.TableKey.VariableId);
                        break;

                    case OperandType.Method:
                        ioSerializer.UInt32Proxy("methodId", ref Data.MethodId);
                        ioSerializer.Serialize("methodArgsIndex", ref Data.MethodArgsIndex);
                        break;
                }
            }

            #endif // USING_BEAUDATA
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct OperandData
        {
            [FieldOffset(0)] public Variant Value;
            [FieldOffset(0)] public TableKeyPair TableKey;
            [FieldOffset(0)] public StringHash32 MethodId;
            [FieldOffset(4)] public uint MethodArgsIndex;
        }

        #endregion // Types

        public TypeFlags Flags;
        public VariantCompareOperator Operator;
        public Operand Left;
        public Operand Right;

        #if USING_BEAUDATA

        public void Serialize(BeauData.Serializer ioSerializer)
        {
            ioSerializer.Enum("flags", ref Flags);
            ioSerializer.Object("left", ref Left);
            
            if ((Flags & TypeFlags.IsLogical) != 0)
            {
                ioSerializer.Enum("operator", ref Operator);
                if (Operator <= VariantCompareOperator.GreaterThan)
                    ioSerializer.Object("right", ref Right);
            }
        }

        #endif // USING_BEAUDATA
    }
}