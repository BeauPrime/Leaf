/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    16 Feb 2021
 * 
 * File:    LeafTokens.cs
 * Purpose: Tokens used for parsing leaf files.
 */

namespace Leaf.Compiler
{
    /// <summary>
    /// Tokens for parsing leaf files.
    /// </summary>
    static public class LeafTokens
    {
        static public readonly string Branch = "branch";
        static public readonly string Break = "break";
        static public readonly string Call = "call";
        static public readonly string Choice = "choice";
        static public readonly string Choose = "choose";
        static public readonly string Continue = "continue";
        static public readonly string Else = "else";
        static public readonly string ElseIf = "elseif";
        static public readonly string EndIf = "endif";
        static public readonly string EndWhile = "endwhile";
        static public readonly string Fork = "fork";
        static public readonly string Goto = "goto";
        static public readonly string If = "if";
        static public readonly string Join = "join";
        static public readonly string Loop = "loop";
        static public readonly string Return = "return";
        static public readonly string Set = "set";
        static public readonly string Start = "start";
        static public readonly string Stop = "stop";
        static public readonly string While = "while";
        static public readonly string Yield = "yield";
    }
}