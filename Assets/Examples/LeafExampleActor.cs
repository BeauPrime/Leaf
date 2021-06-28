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
    public class LeafExampleActor : MonoBehaviour, ILeafActor
    {
        public StringHash32 Id { get { return name; } }
        public VariantTable Locals { get { return null; } }

        [LeafMember("Shift")]
        static private IEnumerator MoveOffset([BindActor] LeafExampleActor inActor, float inX, float inY, float inZ, float inDuration = 0, Curve inCurve = Curve.Linear)
        {
            return inActor.transform.MoveTo(inActor.transform.localPosition + new Vector3(inX, inY, inZ), inDuration, Axis.XYZ, Space.Self).Ease(inCurve);
        }
    }
}