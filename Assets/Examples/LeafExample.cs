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

namespace Leaf.Examples
{
    public class LeafExample : MonoBehaviour
    {
        #region Inspector

        public DialogBox DialogBox;
        public LeafAsset File;
        public LeafExampleActor Actor;

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

            BuildTagParser();
            yield return null;

            var package = LeafAsset.CompileAsync(File, new Parser(), out IEnumerator wait);
            yield return wait;
            LeafNode startNode;
            if (!package.TryGetNode("Start", out startNode))
            {
                Debug.LogError("[LeafExample] File does not contain a node named 'Start'");
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
                get { return LeafCompilerFlags.Default_Release; }
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