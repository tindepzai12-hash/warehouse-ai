using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace JarVis.Warehouse.Localization
{
    // Text only. Imported node IDs and navigation bindings never change with language.
    public sealed class ImportedAppLocalization : MonoBehaviour
    {
        [Serializable] public sealed class Entry { public string vi,en; }
        [Serializable] sealed class Table { public Entry[] entries; }
        sealed class Label { public TMP_Text view; public string source,last; public float size,max,min; public bool auto; }
        static Dictionary<string,Entry> phrases;
        static ImportedAppLocalization active;
        readonly List<Label> labels=new List<Label>();
        readonly Dictionary<TMP_Text,Label> lookup=new Dictionary<TMP_Text,Label>();
        public int LabelCount=>labels.Count;
        public static bool IsEnglish=>JarvisLocalization.CurrentLanguage==SupportedLanguage.En;
        public event Action Changed;

        public void Configure(Transform root)
        {
            active=this;Load();
            JarvisLocalization.SetLanguage(PlayerPrefs.GetString("JarVis.InterfaceLanguage","vi")=="en"?SupportedLanguage.En:SupportedLanguage.Vi);
            var editable=new HashSet<TMP_Text>();
            foreach(var field in root.GetComponentsInChildren<TMP_InputField>(true))if(field.textComponent!=null)editable.Add(field.textComponent);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))if(!editable.Contains(text))Register(text);
            JarvisLocalization.LanguageChanged+=ApplyAll;
            ApplyAll();
        }
        static void Load()
        {
            if(phrases!=null)return;phrases=new Dictionary<string,Entry>(StringComparer.Ordinal);
            var asset=Resources.Load<TextAsset>("JarVisImportedTranslations");
            if(asset==null)throw new InvalidOperationException("Imported app translation table is missing.");
            foreach(var entry in JsonUtility.FromJson<Table>(asset.text).entries)
            {phrases[entry.vi]=entry;if(!phrases.ContainsKey(entry.en))phrases[entry.en]=entry;}
        }
        public static string Text(string source)
        {
            if(string.IsNullOrEmpty(source))return source;Load();
            if(phrases.TryGetValue(source,out var pair))return IsEnglish?pair.en:pair.vi;
            // Counts may update without changing the task-filter semantics.
            string zero=Regex.Replace(source,@"\(\d+\)","(0)");
            if(zero!=source&&phrases.TryGetValue(zero,out pair))
            {
                var count=Regex.Match(source,@"\(\d+\)").Value;
                return (IsEnglish?pair.en:pair.vi).Replace("(0)",count);
            }
            if(source.Contains("\n"))return string.Join("\n",Array.ConvertAll(source.Split('\n'),line=>phrases.TryGetValue(line,out var entry)?IsEnglish?entry.en:entry.vi:line));
            return source;
        }
        public static void Bind(TMP_Text view){if(active!=null)active.Register(view);}
        void Register(TMP_Text view)
        {
            if(view==null||lookup.ContainsKey(view))return;
            var label=new Label{view=view,source=view.text,last=view.text,size=view.fontSize,max=view.fontSizeMax,min=view.fontSizeMin,auto=view.enableAutoSizing};labels.Add(label);lookup.Add(view,label);
            Apply(label);
        }
        public static string Source(TMP_Text view)=>active!=null&&active.lookup.TryGetValue(view,out var label)?label.source:view.text;
        public void Select(SupportedLanguage value)
        {
            PlayerPrefs.SetString("JarVis.InterfaceLanguage",value==SupportedLanguage.En?"en":"vi");PlayerPrefs.Save();
            JarvisLocalization.SetLanguage(value);ApplyAll();
        }
        void LateUpdate()
        {
            // No hierarchy searches or allocations for unchanged labels.
            for(int i=labels.Count-1;i>=0;i--)
            {
                var label=labels[i];
                if(label.view==null){lookup.Remove(label.view);labels.RemoveAt(i);continue;}
                if(label.view.text!=label.last){label.source=label.view.text;Apply(label);}
            }
        }
        void ApplyAll()
        {
            foreach(var label in labels)if(label.view!=null)
            {
                // A status may change immediately before the language button is pressed.
                if(label.view.text!=label.last)label.source=label.view.text;
                Apply(label);
            }
            Changed?.Invoke();
        }
        public void RefreshPresentation(){ApplyAll();}
        static void Apply(Label label)
        {
            float scale=JarVis.Warehouse.UI.ImportedSettings.TextScale;
            label.last=Text(label.source);label.view.text=label.last;
            // Fit translated short labels inside their original imported bounds.
            if(!label.auto&&label.last!=label.source&&!label.last.Contains("\n")&&label.view.rectTransform.rect.height<=48&&label.view.rectTransform.rect.width>0)
            {label.view.enableAutoSizing=true;label.view.fontSizeMax=label.size*scale;label.view.fontSizeMin=Mathf.Min(label.size,Mathf.Max(11,label.size*.78f))*Mathf.Min(scale,1);}
            else if(!label.auto){label.view.enableAutoSizing=false;label.view.fontSize=label.size*scale;}
            else {label.view.fontSizeMax=label.max*scale;label.view.fontSizeMin=label.min*Mathf.Min(scale,1);}
        }
        void OnDestroy(){JarvisLocalization.LanguageChanged-=ApplyAll;if(active==this)active=null;}
    }
}
