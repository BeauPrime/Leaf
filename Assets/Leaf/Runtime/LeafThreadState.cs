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

        internal struct Handle
        {
            private LeafThreadState<TNode> m_State;
            private uint m_Handle;

            internal Handle(LeafThreadState<TNode> inState, uint inHandle)
            {
                m_State = inState;
                m_Handle = inHandle;
            }

            internal bool IsRunning()
            {
                return Get() != null;
            }

            internal LeafThreadState<TNode> Get()
            {
                if (m_Handle > 0 && m_State != null && m_State.m_Magic != m_Handle)
                {
                    m_State = null;
                    m_Handle = 0;
                }

                return m_State;
            }
        }

        #endregion // Types
        
        private readonly RingBuffer<Frame> m_FrameStack;
        private readonly RingBuffer<Variant> m_ValueStack;
        private readonly RingBuffer<Handle> m_Children;
        private readonly LeafChoice m_ChoiceBuffer;

        private uint m_Magic;
        private bool m_Running;

        public LeafThreadState()
        {
            m_FrameStack = new RingBuffer<Frame>();
            m_ValueStack = new RingBuffer<Variant>();
            m_ChoiceBuffer = new LeafChoice();
            m_Children = new RingBuffer<Handle>();
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

            if (inNode != null)
            {
                PushNode(inNode, inPlugin);
            }
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
        public void AddOption(Variant inTargetId, StringHash32 inLineCode, bool inbAvailable = true)
        {
            m_ChoiceBuffer.AddOption(new LeafChoice.Option(inTargetId, inLineCode, inbAvailable));
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

        #region Forks

        /// <summary>
        /// Returns if this thread has any children still running.
        /// </summary>
        public bool HasChildren()
        {
            while(m_Children.Count > 0)
            {
                if (m_Children.PeekBack().IsRunning())
                    return true;
                m_Children.PopBack();
            }

            return false;
        }

        /// <summary>
        /// Adds the given thread as a child of this thread.
        /// </summary>
        public void AddChild(LeafThreadState<TNode> inThreadState)
        {
            if (inThreadState != null && inThreadState.m_Running)
            {
                m_Children.PushBack(inThreadState.GetHandle());
            }
        }

        /// <summary>
        /// Kills all children.
        /// </summary>
        public void KillChildren(ILeafPlugin<TNode> inPlugin)
        {
            while(m_Children.Count > 0)
            {
                var thread = m_Children.PopBack().Get();
                if (thread != null)
                    inPlugin.Kill(thread);
            }
        }

        #endregion // Forks

        #region Handles

        /// <summary>
        /// Sets up the state.
        /// </summary>
        public virtual void Setup()
        {
            m_Running = true;
            m_Magic = (m_Magic == uint.MaxValue) ? 1 : m_Magic + 1;
        }

        /// <summary>
        /// Returns a handle to this LeafThreadState.
        /// </summary>
        internal Handle GetHandle()
        {
            return m_Running ? new Handle(this, m_Magic) : default(Handle);
        }

        #endregion // Handles

        #region Cleanup

        public virtual void Reset(ILeafPlugin<TNode> inPlugin)
        {
            ClearNodes(inPlugin);
            KillChildren(inPlugin);
            
            m_ValueStack.Clear();
            m_ChoiceBuffer.Reset();
            m_Children.Clear();
            m_ChoiceBuffer.Reset();
            m_Running = false;
        }

        #endregion // Cleanup
    }
}