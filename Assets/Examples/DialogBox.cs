using System.Collections;
using BeauUtil.Tags;
using Leaf.Defaults;
using Leaf.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BeauRoutine;

namespace Leaf.Examples
{
    public class DialogBox : MonoBehaviour, ITextDisplayer, IChoiceDisplayer
    {
        public GameObject TextGroup;
        public TMP_Text Text;
        public Button Continue;
        public float CharacterDelay = 0.03f;

        private void Awake()
        {
            TextGroup.gameObject.SetActive(false);
            Continue.gameObject.SetActive(false);
        }

        #region ITextDisplayer

        public IEnumerator CompleteLine()
        {
            if (Text.maxVisibleCharacters > 0)
            {
                Continue.gameObject.SetActive(true);
                yield return Continue.onClick.WaitForInvoke();
                Continue.gameObject.SetActive(false);
            }

            TextGroup.gameObject.SetActive(false);
        }

        public TagStringEventHandler PrepareLine(TagString inString, TagStringEventHandler inBaseHandler)
        {
            if (inString.RichText.Length > 0)
            {
                Text.text = inString.RichText;
                Text.maxVisibleCharacters = 0;

                TextGroup.gameObject.SetActive(true);
            }

            return null;
        }

        public IEnumerator TypeLine(TagString inString, TagTextData inType)
        {
            uint toType = inType.VisibleCharacterCount;
            while(toType > 0)
            {
                Text.maxVisibleCharacters++;
                toType--;
                yield return CharacterDelay;
            }
        }

        #endregion // ITextDisplayer

        #region IChoiceDisplayer

        public IEnumerator ShowChoice(LeafChoice inChoice, LeafThreadState inThread, ILeafContentResolver inContentResolver)
        {
            throw new System.NotImplementedException();
        }

        #endregion // IChoiceDisplayer
    }
}