/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    25 Oct 2020
 * 
 * File:    LeafAsset.cs
 * Purpose: Leaf script asset.
 */

using System.Text;
using UnityEngine;

namespace Leaf
{
    /// <summary>
    /// Leaf source file.
    /// </summary>
    public class LeafAsset : ScriptableObject
    {
        #region Inspector

        [SerializeField, HideInInspector] private byte[] m_Bytes = null;

        #endregion // Inspector

        /// <summary>
        /// Raw bytes making up the source text.
        /// </summary>
        public byte[] Bytes()
        {
            return m_Bytes;
        }

        /// <summary>
        /// Returns the source text.
        /// </summary>
        public string Source()
        {
            return Encoding.UTF8.GetString(m_Bytes);
        }

        /// <summary>
        /// Initializes the asset with the given bytes.
        /// </summary>
        public void Create(byte[] inBytes)
        {
            m_Bytes = inBytes;
        }
    }
}