using System.Text.RegularExpressions;
namespace JarVis.Warehouse.Backend
{
    // Decide using the original question, before bilingual catalogue/OCR context is added.
    public static class JarvisChatLanguage
    {
        public static bool English(string question,bool fallbackEnglish)
        {
            string q=(question??"").ToLowerInvariant();
            var explicitLanguage=Regex.Matches(q,@"\b(?:in english|in vietnamese)\b|(?:bằng|bang|trả lời|tra loi)\s+(?:bằng\s+)?(?:tiếng|tieng)\s+(anh|việt|viet)");
            if(explicitLanguage.Count>0){string last=explicitLanguage[explicitLanguage.Count-1].Value;return last.Contains("english")||last.EndsWith("anh");}
            if(Regex.IsMatch(q,@"[àáạảãăằắặẳẵâầấậẩẫèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]|\b(?:toi|ban|trong kho|la gi|giai thich|sang loc|sap xep|cam on|tiep tuc)\b"))return false;
            if(Regex.IsMatch(q,@"\b(?:what|how|why|where|when|which|explain|please|warehouse|inventory|hello|thanks|thank you|yes|continue|show|this|these|image|photo|safety|help|compare|calculate|tell|can|could|would)\b"))return true;
            return fallbackEnglish;
        }
    }
}
