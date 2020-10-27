/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafChoice.cs
 * Purpose: Set of options to display, linked to different nodes.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BeauUtil;

namespace Leaf
{
    /// <summary>
    /// A set of options to display.
    /// </summary>
    public class LeafChoice : IReadOnlyList<LeafChoice.Option>
    {
        /// <summary>
        /// Single option within a choice.
        /// </summary>
        public struct Option
        {
            public readonly StringHash32 NodeId;
            public readonly StringHash32 LineCode;
            public readonly bool IsAvailable;

            public Option(StringHash32 inNodeId, StringHash32 inLineCode, bool inbIsAvailable = true)
            {
                NodeId = inNodeId;
                LineCode = inLineCode;
                IsAvailable = inbIsAvailable;
            }
        }

        private enum State
        {
            Accumulating,
            Choosing,
            Chosen
        }

        private readonly List<Option> m_AllOptions = new List<Option>(4);
        private State m_State = State.Accumulating;
        private StringHash32 m_ChosenOption;

        #region IReadOnlyList

        public int Count { get { return m_AllOptions.Count; } }

        public Option this[int index]
        {
            get { return m_AllOptions[index]; }
        }

        public IEnumerable<Option> AllAvailableOptions()
        {
            for(int i = 0; i < m_AllOptions.Count; ++i)
            {
                Option op = m_AllOptions[i];
                if (op.IsAvailable)
                    yield return op;
            }
        }

        public IEnumerator<Option> GetEnumerator()
        {
            return m_AllOptions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion // IReadOnlyList

        /// <summary>
        /// Adds an option to the choice.
        /// </summary>
        public void AddOption(Option inOption)
        {
            if (m_State != State.Accumulating)
                throw new InvalidOperationException(string.Format("Cannot add options while in {0} state", m_State));
            m_AllOptions.Add(inOption);
        }

        /// <summary>
        /// Locks options in place to present.
        /// </summary>
        public void Offer()
        {
            if (m_State != State.Accumulating)
                throw new InvalidOperationException(string.Format("Cannot offer options while in {0} state", m_State));
            m_State = State.Choosing;
        }

        /// <summary>
        /// Chooses the option with the given node id.
        /// </summary>
        public void Choose(StringHash32 inNodeId)
        {
            if (m_State != State.Choosing)
                throw new InvalidOperationException(string.Format("Cannot choose an option while in {0} state", m_State));

            for(int i = m_AllOptions.Count - 1; i >= 0; --i)
            {
                if (m_AllOptions[i].NodeId == inNodeId)
                {
                    m_ChosenOption = inNodeId;
                    m_State = State.Chosen;
                    return;
                }
            }

            throw new Exception(string.Format("No option with node id {0} is present in this choice", inNodeId.ToDebugString()));
        }

        /// <summary>
        /// Chooses the option with the given index.
        /// </summary>
        public void Choose(int inIndex)
        {
            if (m_State != State.Choosing)
                throw new InvalidOperationException(string.Format("Cannot choose an option while in {0} state", m_State));
            if (inIndex < 0 || inIndex >= m_AllOptions.Count)
                throw new ArgumentOutOfRangeException("inIndex");
            
            m_ChosenOption = m_AllOptions[inIndex].NodeId;
            m_State = State.Chosen;
        }

        /// <summary>
        /// Returns if an option has been chosen.
        /// </summary>
        public bool HasChosen()
        {
            return m_State == State.Chosen;
        }

        /// <summary>
        /// Returns the chosen option's node.
        /// </summary>
        public StringHash32 ChosenNode()
        {
            return m_ChosenOption;
        }

        /// <summary>
        /// Resets choice state.
        /// </summary>
        public void Reset()
        {
            m_AllOptions.Clear();
            m_State = State.Accumulating;
            m_ChosenOption = StringHash32.Null;
        }
    }
}