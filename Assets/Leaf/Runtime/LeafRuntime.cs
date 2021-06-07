/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafRuntime.cs
 * Purpose: Execution environment for LeafThreadState objects.
 */

using System;
using System.Collections;
using BeauUtil;
using BeauUtil.Variants;
using UnityEngine;

namespace Leaf.Runtime
{
    /// <summary>
    /// Runtime environment for LeafThreadState.
    /// </summary>
    public class LeafRuntime<TNode> : ILeafContentResolver
        where TNode : LeafNode
    {
        private ILeafPlugin<TNode> m_Plugin;

        public LeafRuntime(ILeafPlugin<TNode> inPlugin)
        {
            if (inPlugin == null)
                throw new ArgumentNullException("inPlugin");
            m_Plugin = inPlugin;
        }

        /// <summary>
        /// The plugin dictating specific funcionality
        /// of the Leaf runtime environment.
        /// </summary>
        public ILeafPlugin<TNode> Plugin
        {
            get { return m_Plugin; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                m_Plugin = value;
            }
        }

        /// <summary>
        /// Evaluates the given node, using the provided thread state.
        /// </summary>
        public IEnumerator Execute(LeafThreadState<TNode> ioThreadState, TNode inNode)
        {
            if (inNode == null)
                return null;
            
            ioThreadState.PushNode(inNode);
            return Execute(ioThreadState);
        }

        /// <summary>
        /// Executes the given thread.
        /// </summary>
        public IEnumerator Execute(LeafThreadState<TNode> ioThreadState)
        {
            TNode node;
            int pc;
            while (ioThreadState.HasNodes())
            {
                ioThreadState.AdvanceState(out node, out pc);

                var allNodeInstructions = node.Instructions();
                if (pc >= allNodeInstructions.Length)
                {
                    ioThreadState.PopNode();
                    continue;
                }

                LeafInstruction instruction = allNodeInstructions[pc];

                switch (instruction.Op)
                {
                    case LeafOpcode.RunLine:
                        {
                            StringHash32 lineCode = instruction.Arg.AsStringHash();
                            string line;
                            if (TryLookupLine(lineCode, node, out line))
                            {
                                IEnumerator process = m_Plugin.RunLine(ioThreadState, line, this);
                                if (process != null)
                                    yield return process;
                            }
                            else
                            {
                                Debug.LogErrorFormat("[LeafRuntime] Could not locate line '{0}' from node '{1}'", lineCode.ToDebugString(), node.Id().ToDebugString());
                            }
                            break;
                        }

                    case LeafOpcode.Invoke:
                        {
                            ILeafInvocation<TNode> invocation;
                            if (TryLookupInvocation(instruction.Arg.AsUInt(), node, out invocation))
                            {
                                IEnumerator process = invocation.Invoke(ioThreadState, m_Plugin, null);
                                if (process != null)
                                    yield return process;
                            }
                            else
                            {
                                Debug.LogErrorFormat("[LeafRuntime] Could not locate invocation {0} from node '{1}'", instruction.Arg.AsUInt(), node.Id().ToDebugString());
                            }
                            break;
                        }

                    case LeafOpcode.InvokeWithTarget:
                        {
                            StringHash32 objectId = ioThreadState.PopValue().AsStringHash();
                            object target;

                            if (!TryLookupObject(objectId, ioThreadState, out target))
                            {
                                Debug.LogWarningFormat("[LeafRuntime] Could not locate target {0} from node '{1}'", objectId.ToDebugString(), node.Id().ToDebugString());
                                break;
                            }

                            ILeafInvocation<TNode> invocation;
                            if (TryLookupInvocation(instruction.Arg.AsUInt(), node, out invocation))
                            {
                                IEnumerator process = invocation.Invoke(ioThreadState, m_Plugin, target);
                                if (process != null)
                                    yield return process;
                            }
                            else
                            {
                                Debug.LogErrorFormat("[LeafRuntime] Could not locate invocation {0} from node '{1}'", instruction.Arg.AsUInt(), node.Id().ToDebugString());
                            }
                            break;
                        }

                    case LeafOpcode.Stop:
                        {
                            ioThreadState.ClearNodes();
                            break;
                        }

                    case LeafOpcode.Yield:
                        {
                            yield return null;
                            break;
                        }

                    case LeafOpcode.Loop:
                        {
                            ioThreadState.ResetProgramCounter();
                            break;
                        }

                    case LeafOpcode.ReturnFromNode:
                        {
                            ioThreadState.PopNode();
                            break;
                        }

                    case LeafOpcode.BranchNode:
                        {
                            TryBranchNode(ioThreadState, node, instruction.Arg.AsStringHash());
                            break;
                        }

                    case LeafOpcode.BranchNodeIndirect:
                        {
                            StringHash32 nodeId = ioThreadState.PopValue().AsStringHash();
                            TryBranchNode(ioThreadState, node, nodeId);
                            break;
                        }

                    case LeafOpcode.ForkNode:
                        {
                            TryForkNode(ioThreadState, node, instruction.Arg.AsStringHash(), true);
                            break;
                        }

                    case LeafOpcode.ForkNodeIndirect:
                        {
                            StringHash32 nodeId = ioThreadState.PopValue().AsStringHash();
                            TryForkNode(ioThreadState, node, nodeId, true);
                            break;
                        }

                    case LeafOpcode.ForkNodeUntracked:
                        {
                            TryForkNode(ioThreadState, node, instruction.Arg.AsStringHash(), false);
                            break;
                        }

                    case LeafOpcode.ForkNodeIndirectUntracked:
                        {
                            StringHash32 nodeId = ioThreadState.PopValue().AsStringHash();
                            TryForkNode(ioThreadState, node, nodeId, false);
                            break;
                        }

                    case LeafOpcode.JoinForks:
                        {
                            // TODO: Maybe a better way to wait?
                            while(ioThreadState.HasChildren())
                                yield return null;
                            break;
                        }

                    case LeafOpcode.EvaluateExpression:
                        {
                            ILeafExpression<TNode> expression;
                            if (TryLookupExpression(instruction.Arg.AsUInt(), node, out expression))
                            {
                                Variant result = expression.Evaluate(ioThreadState, m_Plugin);
                                ioThreadState.PushValue(result);
                            }
                            else
                            {
                                Debug.LogErrorFormat("[LeafRuntime] Could not locate expression {0} from node '{1}'", instruction.Arg.AsUInt(), node.Id().ToDebugString());
                                ioThreadState.PushValue(Variant.Null);
                            }
                            break;
                        }

                    case LeafOpcode.SetFromExpression:
                        {
                            ILeafExpression<TNode> expression;
                            if (TryLookupExpression(instruction.Arg.AsUInt(), node, out expression))
                            {
                                expression.Set(ioThreadState, m_Plugin);
                            }
                            else
                            {
                                Debug.LogErrorFormat("[LeafRuntime] Could not locate expression {0} from node '{1}'", instruction.Arg.AsUInt(), node.Id().ToDebugString());
                            }
                            break;
                        }

                    case LeafOpcode.GotoNode:
                        {
                            TryGotoNode(ioThreadState, node, instruction.Arg.AsStringHash());
                            break;
                        }

                    case LeafOpcode.GotoNodeIndirect:
                        {
                            StringHash32 nodeId = ioThreadState.PopValue().AsStringHash();
                            TryGotoNode(ioThreadState, node, nodeId);
                            break;
                        }

                    case LeafOpcode.PushValue:
                        {
                            ioThreadState.PushValue(instruction.Arg);
                            break;
                        }

                    case LeafOpcode.PopValue:
                        {
                            ioThreadState.PopValue();
                            break;
                        }

                    case LeafOpcode.Jump:
                        {
                            ioThreadState.JumpRelative(instruction.Arg.AsInt());
                            break;
                        }

                    case LeafOpcode.JumpIfFalse:
                        {
                            bool bValue = ioThreadState.PopValue().AsBool();
                            if (!bValue)
                            {
                                ioThreadState.JumpRelative(instruction.Arg.AsInt());
                            }
                            break;
                        }

                    case LeafOpcode.JumpIndirect:
                        {
                            int jump = ioThreadState.PopValue().AsInt();
                            ioThreadState.JumpRelative(jump);
                            break;
                        }

                    case LeafOpcode.AddChoiceOption:
                        {
                            bool bAvailable = ioThreadState.PopValue().AsBool();
                            StringHash32 lineCode = ioThreadState.PopValue().AsStringHash();
                            Variant nodeId = ioThreadState.PopValue();
                            ioThreadState.AddOption(nodeId, lineCode, bAvailable);
                            break;
                        }

                    case LeafOpcode.ShowChoices:
                        {
                            LeafChoice currentChoice = ioThreadState.GetOptions();
                            if (currentChoice.AvailableCount > 0)
                            {
                                yield return m_Plugin.ShowOptions(ioThreadState, currentChoice, this);
                                Variant chosenNode = currentChoice.ChosenTarget();
                                currentChoice.Reset();
                                ioThreadState.PushValue(chosenNode);
                            }
                            else
                            {
                                currentChoice.Reset();
                                ioThreadState.PushValue(Variant.Null);
                            }
                            break;
                        }
                }
            }

            m_Plugin.OnEnd(ioThreadState);
        }

        #region Small Operations
    
        /// <summary>
        /// Attempts to switch the current thread to the given node.
        /// </summary>
        public void TryGotoNode(LeafThreadState<TNode> ioThreadState, TNode inLocalNode, StringHash32 inNodeId)
        {
            if (inNodeId.IsEmpty)
            {
                ioThreadState.GotoNode(null);
                return;
            }

            TNode targetNode;
            if (TryLookupNode(inNodeId, inLocalNode, out targetNode))
            {
                ioThreadState.GotoNode(targetNode);
            }
            else
            {
                Debug.LogErrorFormat("[LeafRuntime] Could not go to node '{0}' from '{1}' - node not found",
                    inNodeId.ToDebugString(), inLocalNode.Id().ToDebugString());
            }
        }

        /// <summary>
        /// Attempts to branch the current thread to the given node.
        /// Once the given node is finished, execution will resume at the previously loaded node.
        /// </summary>
        public void TryBranchNode(LeafThreadState<TNode> ioThreadState, TNode inLocalNode, StringHash32 inNodeId)
        {
            if (inNodeId.IsEmpty)
            {
                return;
            }

            TNode targetNode;
            if (TryLookupNode(inNodeId, inLocalNode, out targetNode))
            {
                ioThreadState.PushNode(targetNode);
            }
            else
            {
                Debug.LogErrorFormat("[LeafRuntime] Could not branch to node '{0}' from '{1}' - node not found",
                    inNodeId.ToDebugString(), inLocalNode.Id().ToDebugString());
            }
        }

        /// <summary>
        /// Attempts to fork a thread for the given node.
        /// </summary>
        public void TryForkNode(LeafThreadState<TNode> ioThreadState, TNode inLocalNode, StringHash32 inNodeId, bool inbTrack)
        {
            if (inNodeId.IsEmpty)
            {
                return;
            }

            TNode targetNode;
            if (TryLookupNode(inNodeId, inLocalNode, out targetNode))
            {
                var newThread = m_Plugin.Fork(ioThreadState, targetNode);
                if (inbTrack && newThread != null)
                {
                    ioThreadState.AddChild(newThread);
                }
            }
            else
            {
                Debug.LogErrorFormat("[LeafRuntime] Could not branch to node '{0}' from '{1}' - node not found",
                    inNodeId.ToDebugString(), inLocalNode.Id().ToDebugString());
            }
        }

        #endregion // Small Operations
    
        #region Lookups

        /// <summary>
        /// Attempts to look up the line with the given code, first using the plugin
        /// and falling back to searching the node's module.
        /// </summary>
        public bool TryLookupLine(StringHash32 inLineCode, TNode inLocalNode, out string outLine)
        {
            if (!m_Plugin.TryLookupLine(inLineCode, inLocalNode, out outLine))
            {
                var module = inLocalNode.Module();
                return module.TryGetLine(inLineCode, inLocalNode, out outLine);
            }

            return true;
        }
        
        /// <summary>
        /// Attempts to look up the node with the given id, first using the plugin
        /// and falling back to searching the node's module.
        /// </summary>
        public bool TryLookupNode(StringHash32 inNodeId, TNode inLocalNode, out TNode outNode)
        {
            if (!m_Plugin.TryLookupNode(inNodeId, inLocalNode, out outNode))
            {
                var module = inLocalNode.Module();
                
                LeafNode node;
                bool bResult = module.TryGetNode(inNodeId, inLocalNode, out node);
                outNode = (TNode) node;
                return bResult;
            }

            return true;
        }

        protected bool TryLookupExpression(uint inExpressionCode, TNode inLocalNode, out ILeafExpression<TNode> outExpression)
        {
            var module = inLocalNode.Module();

            ILeafExpression expression;
            bool bResult = module.TryGetExpression(inExpressionCode, out expression);
            outExpression = (ILeafExpression<TNode>) expression;
            return bResult;
        }

        protected bool TryLookupInvocation(uint inInvocationCode, TNode inLocalNode, out ILeafInvocation<TNode> outInvocation)
        {
            var module = inLocalNode.Module();

            ILeafInvocation invocation;
            bool bResult = module.TryGetInvocation(inInvocationCode, out invocation);
            outInvocation = (ILeafInvocation<TNode>) invocation;
            return bResult;
        }

        protected bool TryLookupObject(StringHash32 inTargetId, LeafThreadState<TNode> ioThreadState, out object outTarget)
        {
            if (inTargetId.IsEmpty)
            {
                outTarget = null;
                return true;
            }

            return m_Plugin.TryLookupObject(inTargetId, ioThreadState, out outTarget);
        }

        #endregion // Lookups

        #region ILeafContentResolver

        bool ILeafContentResolver.TryGetNode(StringHash32 inNodeId, LeafNode inLocalNode, out LeafNode outNode)
        {
            TNode node;
            bool bResult = TryLookupNode(inNodeId, (TNode) inLocalNode, out node);
            outNode = node;
            return bResult;
        }

        bool ILeafContentResolver.TryGetLine(StringHash32 inLineCode, LeafNode inLocalNode, out string outLine)
        {
            return TryLookupLine(inLineCode, (TNode) inLocalNode, out outLine);
        }

        #endregion // ILeafContentResolver
    }
}