/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafInstruction.cs
 * Purpose: Pairing of opcode and optional argument.
 */

using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using BeauUtil.Variants;
using BeauUtil;
using System;

namespace Leaf.Runtime
{
    /// <summary>
    /// Leaf operation.
    /// </summary>
    [DebuggerDisplay("{ToDebugString()}")]
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size=8)]
    public struct LeafInstruction : IDebugString
        #if USING_BEAUDATA
        , BeauData.ISerializedProxy<ulong>
        #endif // USING_BEAUDATA
    {
        [FieldOffset(0)] public ulong Data;
        
        // because there are three bytes of unused space in Variant, we can nest the ScriptOpcode in those bytes
        // this keeps LeafInstruction to only 8 bytes, great for x64
        [FieldOffset(0), NonSerialized] internal Variant Arg;
        [FieldOffset(2), NonSerialized] internal LeafOpcode Op;

        public LeafInstruction(ulong inData)
        {
            Arg = default(Variant);
            Op = default(LeafOpcode);
            Data = inData;
        }

        internal LeafInstruction(LeafOpcode inOpcode, Variant inArg = default(Variant))
        {
            Data = 0;
            Arg = inArg;
            Op = inOpcode;
        }

        internal void SetArg(Variant inArgument)
        {
            LeafOpcode opcode = Op;
            Arg = inArgument;
            Op = opcode;
        }

        public override string ToString()
        {
            if (Arg.Type == VariantType.Null && Op != LeafOpcode.PushValue)
                return Op.ToString();
            return string.Format("{0}: {1}", Op, Arg);
        }

        public string ToDebugString()
        {
            if (Arg.Type == VariantType.Null && Op != LeafOpcode.PushValue)
                return Op.ToString();
            return string.Format("{0}: {1}", Op, Arg.ToDebugString());
        }

        static public string ToDebugString(IReadOnlyCollection<LeafInstruction> inInstructions)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(inInstructions.Count).Append(" instruction(s)");
            int idx = 0;
            foreach(var instruction in inInstructions)
            {
                builder.Append("\n[").Append(idx++).Append("] ").Append(instruction.ToDebugString());
            }
            return builder.ToString();
        }

        #region ISerializedProxy

        #if USING_BEAUDATA

        public ulong GetProxyValue(BeauData.ISerializerContext unused)
        {
            return Data;
        }

        public void SetProxyValue(ulong inValue, BeauData.ISerializerContext unused)
        {
            Data = inValue;
        }

        #endif // USING_BEAUDATA

        #endregion // ISerializedProxy
    }
}