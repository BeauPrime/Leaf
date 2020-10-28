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
using System.Collections.Generic;
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
            
            ioThreadState.PushNode(inNode, m_Plugin);
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
                    ioThreadState.PopNode(m_Plugin);
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
                                yield return m_Plugin.RunLine(ioThreadState, line, this);
                            }
                            else
                            {
                                Debug.LogErrorFormat("[LeafRuntime] Could not locate line '{0}' from node '{1}'", lineCode.ToDebugString(), node.Id().ToDebugString());
                            }
                            break;
                        }

                    case LeafOpcode.Stop:
                        {
                            ioThreadState.ClearNodes(m_Plugin);
                            break;
                        }

                    case LeafOpcode.Loop:
                        {
                            ioThreadState.ResetProgramCounter();
                            break;
                        }

                    case LeafOpcode.ReturnFromNode:
                        {
                            ioThreadState.PopNode(m_Plugin);
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

                    case LeafOpcode.AddChoiceOption:
                        {
                            bool bAvailable = ioThreadState.PopValue().AsBool();
                            StringHash32 lineCode = ioThreadState.PopValue().AsStringHash();
                            StringHash32 nodeId = ioThreadState.PopValue().AsStringHash();
                            ioThreadState.AddOption(nodeId, lineCode, bAvailable);
                            break;
                        }

                    case LeafOpcode.ShowChoices:
                        {
                            LeafChoice currentChoice = ioThreadState.GetOptions();
                            if (currentChoice.Count > 0)
                            {
                                yield return m_Plugin.ShowOptions(ioThreadState, currentChoice, this);
                                StringHash32 chosenNode = currentChoice.ChosenNode();
                                currentChoice.Reset();
                                ioThreadState.PushValue(chosenNode);
                            }
                            else
                            {
                                ioThreadState.PushValue(StringHash32.Null);
                            }
                            break;
                        }
                }
            }
        }

        #region Small Operations
    
        /// <summary>
        /// Attempts to switch the current thread to the given node.
        /// </summary>
        public void TryGotoNode(LeafThreadState<TNode> ioThreadState, TNode inLocalNode, StringHash32 inNodeId)
        {
            if (inNodeId.IsEmpty)
            {
                ioThreadState.GotoNode(null, m_Plugin);
                return;
            }

            TNode targetNode;
            if (TryLookupNode(inNodeId, inLocalNode, out targetNode))
            {
                ioThreadState.GotoNode(targetNode, m_Plugin);
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
                ioThreadState.PushNode(targetNode, m_Plugin);
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