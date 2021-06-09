using BeauUtil;
using BeauUtil.Tags;
using Leaf.Defaults;
using Leaf.Compiler;
using Leaf.Runtime;
using UnityEngine;
using BeauUtil.Variants;
using BeauRoutine;

namespace Leaf.Examples
{
    public class LeafExample : MonoBehaviour
    {
        #region Inspector

        public DialogBox DialogBox;
        public LeafAsset File;

        #endregion // Inspector

        private DefaultLeafManager<LeafNode> m_Manager;

        private void Awake()
        {
            m_Manager = new DefaultLeafManager<LeafNode>(this, null, null);
            m_Manager.ConfigureDisplay(DialogBox, DialogBox);

            BuildTagParser();

            var package = LeafAsset.Compile(File, new Parser());
            LeafNode startNode;
            if (!package.TryGetNode("Start", out startNode))
            {
                Debug.LogError("[LeafExample] File does not contain a node named 'Start'");
                return;
            }

            m_Manager.Run(startNode);
        }
        
        private void BuildTagParser()
        {
            CustomTagParserConfig parser = new CustomTagParserConfig();
            parser.AddReplace("var", (tag, context) => {
                LeafThreadState thread = (LeafThreadState) context;
                return thread.GetVariable(tag.Data).ToString();
            });
            parser.AddEvent("wait", "Wait").WithFloatData();
            parser.AddEvent("@*", "Target").ProcessWith(ParseTargetArgs);

            TagStringEventHandler handler = new TagStringEventHandler();
            handler.Register("Wait", (e, o) => Routine.WaitSeconds(e.Argument0.AsFloat()));

            m_Manager.ConfigureTagStringHandling(parser, handler);
        }

        private static void ParseTargetArgs(TagData inTag, object inContext, ref TagEventData ioEvent) 
        {
			ioEvent.StringArgument = inTag.Id.Substring(1);
		}

        private class Parser : LeafParser<LeafNode, LeafNodePackage<LeafNode>>
        {
            public override LeafNodePackage<LeafNode> CreatePackage(string inFileName)
            {
                return new LeafNodePackage<LeafNode>(inFileName);
            }

            protected override LeafNode CreateNode(string inFullId, StringSlice inExtraData, LeafNodePackage<LeafNode> inPackage)
            {
                return new LeafNode(inFullId, inPackage);
            }
        }
    }
}