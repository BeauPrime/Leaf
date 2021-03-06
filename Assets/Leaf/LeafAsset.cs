/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    25 Oct 2020
 * 
 * File:    LeafAsset.cs
 * Purpose: Leaf script asset.
 */

using System.Text;
using BeauUtil.Blocks;
using Leaf.Compiler;
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

        /// <summary>
        /// Compiles a node package from a LeafAsset.
        /// </summary>
        static public TPackage Compile<TNode, TPackage>(LeafAsset inAsset, LeafParser<TNode, TPackage> inParser)
            where TNode : LeafNode
            where TPackage : LeafNodePackage<TNode>
        {
            return BlockParser.Parse(inAsset.name, inAsset.Source(), BlockParsingRules.Default, inParser);
        }

        /// <summary>
        /// Compiles a node package from a LeafAsset.
        /// </summary>
        static public TPackage Compile<TNode, TPackage>(LeafAsset inAsset, IBlockParsingRules inParsingRules, LeafParser<TNode, TPackage> inParser)
            where TNode : LeafNode
            where TPackage : LeafNodePackage<TNode>
        {
            return BlockParser.Parse(inAsset.name, inAsset.Source(), inParsingRules, inParser);
        }
    }
}