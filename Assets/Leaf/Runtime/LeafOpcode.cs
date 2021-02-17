/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafOpcode.cs
 * Purpose: Opcodes for leaf execution.
 */

using BeauUtil;

namespace Leaf.Runtime
{
    /// <summary>
    /// Enumeration for small operations.
    /// </summary>
    internal enum LeafOpcode : byte
    {
        RunLine, // text id
        
        Invoke, // expression index
        InvokeWithTarget, // expression index [pop target id]

        EvaluateExpression, // expression index [push val]
        SetFromExpression, // expression index
        
        Jump, // instruction displacement
        JumpIfFalse, // instruction displacement, [pop bool]
        JumpIndirect, // [pop int]
        
        AddChoiceOption, // [pop bool, pop text id, pop node id]
        ShowChoices, // no args

        PushValue, // value [push value]
        PopValue, // [pop value]

        GotoNode, // node id
        GotoNodeIndirect, // [pop node id]

        BranchNode, // node id
        BranchNodeIndirect, // [pop node id]

        ForkNode, // node id
        ForkNodeIndirect, // [pop node id]

        ForkNodeUntracked, // node id
        ForkNodeIndirectUntracked, // [pop node id]

        JoinForks, // no args

        ReturnFromNode, // no args
        Stop, // no args
        Loop, // no args

        Yield // no args
    }
}