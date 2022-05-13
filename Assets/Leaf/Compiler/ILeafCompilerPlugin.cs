/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    25 Oct 2020
 * 
 * File:    ILeafCompilerPlugin.cs
 * Purpose: Plugin interface for the LeafCompiler.
 */

using System;

namespace Leaf.Compiler
{
    /// <summary>
    /// Plugin for the compiler.
    /// </summary>
    public interface ILeafCompilerPlugin
    {
        char PathSeparator { get; }
        LeafCompilerFlags CompilerFlags { get; }
    }

    [Flags]
    public enum LeafCompilerFlags : uint
    {
        CollapseContent = 0x01,
        VerboseLog = 0x02,
        DebugMode = 0x04
    }
}