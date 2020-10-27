/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    25 Oct 2020
 * 
 * File:    LeafAssetImporter.cs
 * Purpose: Leaf script asset importer.
 */

using System.IO;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace Leaf.Editor
{
    [ScriptedImporter(1, "leaf")]
    public class LeafAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            LeafAsset asset = ScriptableObject.CreateInstance<LeafAsset>();
            
            byte[] sourceBytes = File.ReadAllBytes(ctx.assetPath);
            asset.Create(sourceBytes);

            EditorUtility.SetDirty(asset);

            ctx.AddObjectToAsset("txt", asset);
            ctx.SetMainObject(asset);

            AssetDatabase.SaveAssets();
        }
    }
}