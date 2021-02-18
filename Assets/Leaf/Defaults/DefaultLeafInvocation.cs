/*
 * Copyright (C) 2021. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    17 Feb 2021
 * 
 * File:    DefaultLeafInvocation.cs
 * Purpose: Default leaf invocation implementation.
 */

using System.Collections;
using BeauUtil;

namespace Leaf.Runtime
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
            var cache = inPlugin.MethodCache;
            bool bSuccess;
            object result;
            if (inTarget == null)
            {
                bSuccess = cache.TryStaticInvoke(m_MethodId, m_Arguments, out result);
            }
            else
            {
                bSuccess = cache.TryInvoke(inTarget, m_MethodId, m_Arguments, out result);
            }

            if (!bSuccess)
                UnityEngine.Debug.LogErrorFormat("[DefaultLeafInvocation] Unable to execute method '{0}' with args '{1}'", m_MethodId, m_Arguments);

            return result as IEnumerator;
        }
    }
}