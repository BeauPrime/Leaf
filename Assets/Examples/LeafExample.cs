using BeauUtil;
using BeauUtil.Tags;
using Leaf.Defaults;
using Leaf.Compiler;
using Leaf.Runtime;
using UnityEngine;
using BeauUtil.Variants;
using BeauRoutine;
using BeauUtil.Debugger;
using System.Collections;
using System.Collections.Generic;

namespace Leaf.Examples
{
    public class LeafExample : MonoBehaviour
    {
        #region Inspector

        public DialogBox DialogBox;
        public LeafAsset File;
        public LeafExampleActor Actor;
        public string StartNode;
        public bool IgnoreLineTables;

        #endregion // Inspector

        private DefaultLeafManager<LeafNode> m_Manager;

        private void Awake()
        {
            Routine.Start(this, StartRoutine());
        }

        private IEnumerator StartRoutine()
        {
            yield return null;
            yield return null;

            m_Manager = new DefaultLeafManager<LeafNode>(this, null, null);
            m_Manager.ConfigureDisplay(DialogBox, DialogBox);

            m_Manager.MethodCache.LoadStatic();
            yield return null;

            m_Manager.RuntimeConfig.IgnoreModuleLineTable = IgnoreLineTables;

            BuildTagParser();
            yield return null;

            var package = LeafAsset.CompileAsync(File, new Parser(), out IEnumerator wait);
            yield return wait;

            List<KeyValuePair<StringHash32, string>> linesWithCustomNames = new List<KeyValuePair<StringHash32, string>>();
            package.GatherAllLinesWithCustomNames(linesWithCustomNames);
            foreach (var line in linesWithCustomNames)
            {
                Debug.LogFormat("Line '{0}' has custom name '{1}'", line.Key, line.Value);
            }

            List<StringHash32> lines = new List<StringHash32>();
            foreach(var node in package)
            {
                lines.Clear();
                int lineCount = LeafUtils.ReadAllLineCodes(node, lines);
                Debug.LogFormat("Node '{0}' has {1} referenced line codes", node.Id().ToDebugString(), lineCount);
                foreach(var line in lines)
                {
                    Debug.Log(line.ToDebugString());
                }
            }

            HashSet<StringHash32> nodeRefs = new HashSet<StringHash32>();
            foreach (var node in package)
            {
                nodeRefs.Clear();
                int nodeCount = LeafUtils.ReadAllDirectlyReferencedNodes(node, nodeRefs);
                Debug.LogFormat("Node '{0}' has {1} referenced nodes", node.Id().ToDebugString(), nodeCount);
                foreach (var nodeId in nodeRefs)
                {
                    Debug.Log(nodeId.ToDebugString());
                }
            }

            LeafNode startNode;
            if (!package.TryGetNode(StartNode, out startNode))
            {
                Debug.LogError("[LeafExample] File does not contain a node named " + StartNode);
                yield break;
            }

            LeafNode anotherNode;
            package.TryGetNode("MoveLoop", out anotherNode);

            var startHandle = m_Manager.Run(startNode, null, null, null, false);
            m_Manager.Run(anotherNode, Actor);

            startHandle.GetThread().Interrupt(InterruptRoutine());
        }
        
        private void BuildTagParser()
        {
            CustomTagParserConfig parser = new CustomTagParserConfig();
            TagStringEventHandler handler = new TagStringEventHandler();

            m_Manager.ConfigureTagStringHandling(parser, handler);

            LeafUtils.ConfigureDefaultParsers(parser, m_Manager, null);
            LeafUtils.ConfigureDefaultHandlers(handler, m_Manager);
        }

        private class Parser : LeafParser<LeafNode, LeafNodePackage<LeafNode>>
        {
            public override bool IsVerbose { get { return true; } }

            public override LeafCompilerFlags CompilerFlags
            {
                get { return LeafCompilerFlags.Default_Development | LeafCompilerFlags.Dump_Stats | LeafCompilerFlags.Dump_Disassembly | LeafCompilerFlags.Preserve_CustomLineNameStrings; }
            }

            public override LeafNodePackage<LeafNode> CreatePackage(string inFileName)
            {
                return new LeafNodePackage<LeafNode>(inFileName);
            }

            protected override LeafNode CreateNode(string inFullId, StringSlice inExtraData, LeafNodePackage<LeafNode> inPackage)
            {
                return new LeafNode(inFullId, inPackage);
            }
        }

        private IEnumerator InterruptRoutine()
        {
            yield return null;
            yield return null;
            Debug.Log("interrupt successfully executed");
        }

        [LeafMember("DumpThreadState")]
        static private void DumpThreadState([BindContext] LeafThreadState<LeafNode> inThread)
        {
            Log.Msg("thread name: '{0}'", inThread.Name);
        }

        [LeafMember("DumpActorState")]
        static private void DumpActorState(ILeafActor inActor, string inPrefix)
        {
            Log.Msg("{0}: '{1}'", inPrefix, inActor);
        }
    }
}