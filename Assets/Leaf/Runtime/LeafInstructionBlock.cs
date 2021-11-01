/*
 * Copyright (C) 2021. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    31 Oct 2021
 * 
 * File:    LeafInstructionBlock.cs
 * Purpose: Instruction block.
 */

namespace Leaf.Runtime
{
    public struct LeafInstructionBlock
    #if USING_BEAUDATA
        : BeauData.ISerializedObject
    #endif // USING_BEAUDATA
    {
        internal byte[] InstructionStream;
        internal string[] StringTable;
        internal LeafExpression[] ExpressionTable;

        #if USING_BEAUDATA

        public void Serialize(BeauData.Serializer ioSerializer)
        {
            ioSerializer.Binary("instructionStream", ref InstructionStream);
            ioSerializer.Array("stringTable", ref StringTable);
            ioSerializer.ObjectArray("expressionTable", ref ExpressionTable);
        }

        #endif // USING_BEAUDATA
    }
}