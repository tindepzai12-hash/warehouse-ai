using TMPro;
using UnityEngine;

namespace JarVis.Warehouse.UI
{
    // TMP may create a caret before the imported text is moved into its final viewport.
    public sealed class ImportedInputCaretRepair:MonoBehaviour
    {
        TMP_InputField field;
        RectTransform caret;
        public void Configure(TMP_InputField value){field=value;Repair();}
        void LateUpdate(){Repair();}
        public void Repair()
        {
            if(field==null||field.textComponent==null||field.textViewport==null)return;
            if(caret==null)
            {
                var selection=GetComponentInChildren<TMP_SelectionCaret>(true);
                if(selection!=null)caret=(RectTransform)selection.transform;
            }
            if(caret==null)return;
            var text=field.textComponent.rectTransform;
            if(caret.parent!=text.parent){caret.SetParent(text.parent,false);caret.SetAsFirstSibling();}
            caret.anchorMin=text.anchorMin;caret.anchorMax=text.anchorMax;caret.pivot=text.pivot;
            caret.anchoredPosition=text.anchoredPosition;caret.sizeDelta=text.sizeDelta;
            caret.localRotation=text.localRotation;caret.localScale=text.localScale;
        }
    }
}
