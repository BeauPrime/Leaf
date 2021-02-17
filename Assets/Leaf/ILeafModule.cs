/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    ILeafModule.cs
 * Purpose: Leaf module, for retrieving nodes, lines, and expressions.
 */

using System.Collections.Generic;
using BeauUtil;
using Leaf.Runtime;

namespace Leaf
{
    public interface ILeafModule : ILeafContentResolver
    {
        bool TryGetExpression(uint inExpressionCode, out ILeafExpression outExpression);
        bool TryGetInvocation(uint inInvocationCode, out ILeafInvocation outInvocation);
        IEnumerable<KeyValuePair<StringHash32, string>> AllLines();
    }
}