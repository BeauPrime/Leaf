/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafThreadState.cs
 * Purpose: Execution stacks for evaluating leaf nodes.
 */

using BeauUtil;
using BeauUtil.Variants;

namespace Leaf.Runtime
{
    public class LeafThreadState<TNode>
        where TNode : LeafNode
    {
        #region Types

        private struct Frame
        {
            public TNode Node;
            public int ProgramCounter;

            public Frame(TNode inNode)
            {
                Node = inNode;
                ProgramCounter = -1;
            }
        }

        #endregion // Types
        
        private readonly RingBuffer<Frame> m_FrameStack;
        private readonly RingBuffer<Variant> m_ValueStack;
        private readonly LeafChoice m_ChoiceBuffer;

        public LeafThreadState()
        {
            m_FrameStack = new RingBuffer<Frame>();
            m_ValueStack = new RingBuffer<Variant>();
            m_ChoiceBuffer = new LeafChoice();
        }

        #region Program Counters

        internal void AdvanceState(out TNode outNode, out int outProgramCounter)
        {
            ref Frame currentFrame = ref m_FrameStack[0];
            outNode = currentFrame.Node;
            outProgramCounter = ++currentFrame.ProgramCounter;
        }

        internal void JumpRelative(int inJump)
        {
            m_FrameStack[0].ProgramCounter += (inJump - 1);
        }

        internal void JumpAbsolute(int inIndex)
        {
            m_FrameStack[0].ProgramCounter = inIndex - 1;
        }

        internal void ResetProgramCounter()
        {
            m_FrameStack[0].ProgramCounter = -1;
        }

        #endregion // Program Counters

        #region Node Stack

        /// <summary>
        /// Pushes the given node onto the frame stack.
        /// </summary>
        public void PushNode(TNode inNode, ILeafPlugin<TNode> inPlugin)
        {
            m_FrameStack.PushFront(new Frame(inNode));
            inPlugin?.OnNodeEnter(inNode, this);
        }

        /// <summary>
        /// Peeks the node at the top of the frame stack.
        /// </summary>
        public TNode PeekNode()
        {
            return m_FrameStack.PeekFront().Node;
        }

        /// <summary>
        /// Pops the current node from the frame stack.
        /// </summary>
        public void PopNode(ILeafPlugin<TNode> inPlugin)
        {
            TNode node = m_FrameStack.PopFront().Node;
            inPlugin?.OnNodeExit(node, this);
        }

        /// <summary>
        /// Pops the current node from the frame stack
        /// and inserts the given node.
        /// </summary>
        public void GotoNode(TNode inNode, ILeafPlugin<TNode> inPlugin)
        {
            if (m_FrameStack.Count > 0)
            {
                PopNode(inPlugin);
            }

            PushNode(inNode, inPlugin);
        }

        /// <summary>
        /// Returns if there are any nodes in the frame stack.
        /// </summary>
        public bool HasNodes()
        {
            return m_FrameStack.Count > 0;
        }

        /// <summary>
        /// Flushes all nodes from the frame stack.
        /// </summary>
        public void ClearNodes(ILeafPlugin<TNode> inPlugin)
        {
            while(m_FrameStack.Count > 0)
            {
                PopNode(inPlugin);
            }
        }

        #endregion // Node Stack

        #region Value Stack

        /// <summary>
        /// Pushes a value onto the value stack.
        /// </summary>
        public void PushValue(Variant inValue)
        {
            m_ValueStack.PushBack(inValue);
        }

        /// <summary>
        /// Peeks the current value on the value stack.
        /// </summary>
        public Variant PeekValue()
        {
            return m_ValueStack.PeekBack();
        }

        /// <summary>
        /// Pops a value from the value stack.
        /// </summary>
        public Variant PopValue()
        {
            return m_ValueStack.PopBack();
        }

        #endregion // Stack
    
        #region Choices

        /// <summary>
        /// Adds an option to the choice buffer.
        /// </summary>
        public void AddOption(StringHash32 inNodeId, StringHash32 inLineCode, bool inbAvailable = true)
        {
            m_ChoiceBuffer.AddOption(new LeafChoice.Option(inNodeId, inLineCode, inbAvailable));
        }

        /// <summary>
        /// Locks and returns the choice buffer.
        /// </summary>
        public LeafChoice GetOptions()
        {
            m_ChoiceBuffer.Offer();
            return m_ChoiceBuffer;
        }

        /// <summary>
        /// Resets the choice buffer.
        /// </summary>
        public void ResetOptions()
        {
            m_ChoiceBuffer.Reset();
        }

        #endregion // Choices

        #region Cleanup

        public virtual void Reset(ILeafPlugin<TNode> inPlugin)
        {
            ClearNodes(inPlugin);
            m_ValueStack.Clear();
            m_ChoiceBuffer.Reset();
        }

        #endregion // Cleanup
    }
}