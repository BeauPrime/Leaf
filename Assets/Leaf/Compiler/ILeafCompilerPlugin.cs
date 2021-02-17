/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    25 Oct 2020
 * 
 * File:    ILeafCompilerPlugin.cs
 * Purpose: Plugin interface for the LeafCompiler.
 */

using BeauUtil;
using Leaf.Runtime;

namespace Leaf.Compiler
{
    /// <summary>
    /// Plugin for the compiler.
    /// </summary>
    public interface ILeafCompilerPlugin<TNode>
        where TNode : LeafNode
    {
        char PathSeparator { get; }
        bool CollapseContent { get; }
        
        ILeafExpression<TNode> CompileExpression(StringSlice inExpression);
        ILeafInvocation<TNode> CompileInvocation(StringSlice inMethod, StringSlice inArguments);
    }
}