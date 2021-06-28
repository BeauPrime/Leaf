/*
 * Copyright (C) 2021. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    17 Feb 2021
 * 
 * File:    DefaultLeafInvocation.cs
 * Purpose: Default leaf invocation implementation.
 */

using System;
using System.Collections;
using BeauUtil;
using BeauUtil.Debugger;
using Leaf.Runtime;

namespace Leaf.Defaults
{
    public class DefaultLeafInvocation<TNode> : ILeafInvocation<TNode>
        where TNode : LeafNode
    {
        private readonly StringHash32 m_MethodId;
        private readonly string m_Arguments;

        public DefaultLeafInvocation(StringHash32 inMethod, StringSlice inArgs)
        {
            m_MethodId = inMethod;
            m_Arguments = inArgs.ToString();
        }

        public IEnumerator Invoke(LeafThreadState<TNode> inThreadState, ILeafPlugin<TNode> inPlugin, object inTarget)
        {
            IMethodCache cache = inPlugin.MethodCache;
            if (cache == null)
                throw new InvalidOperationException("Cannot use DefaultLeafInvocation if ILeafPlugin.MethodCache is not specified for plugin");
            
            bool bSuccess;
            object result;
            if (inTarget == null)
            {
                bSuccess = cache.TryStaticInvoke(m_MethodId, m_Arguments, inThreadState, out result);
            }
            else
            {
                bSuccess = cache.TryInvoke(inTarget, m_MethodId, m_Arguments, inThreadState, out result);
            }

            if (!bSuccess)
                Log.Error("[DefaultLeafInvocation] Unable to execute method '{0}' with args '{1}'", m_MethodId, m_Arguments);

            return result as IEnumerator;
        }
    }
}