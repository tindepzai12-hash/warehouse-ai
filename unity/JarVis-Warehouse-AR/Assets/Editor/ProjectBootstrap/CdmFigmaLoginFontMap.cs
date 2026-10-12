using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ProjectBootstrap
{
    public static class CdmFigmaLoginFontMap
    {
        private const string Source = "Assets/Resources/BeVietnamPro-Regular.ttf";
        private const string Output = "Assets/JarVis/DesignImports/BeVietnamPro Figma SDF.asset";
        private const string Login = "Assets/JarVis/DesignImports/JarVis-Login-5-36.figma";

        public static void Apply()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(Source);
            if (source == null) throw new InvalidOperationException("Be Vietnam Pro TTF is missing.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Output);
            if (font != null && (font.atlasTextures == null || font.atlasTextures.Length == 0 ||
                font.atlasTextures[0] == null || font.material == null))
            {
                AssetDatabase.DeleteAsset(Output);
                font = null;
            }
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source, 72, 9,
                    GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (font == null) throw new InvalidOperationException("Could not create TMP font.");
                font.name = "BeVietnamPro Figma SDF";
                AssetDatabase.CreateAsset(font, Output);
                if (font.atlasTextures == null || font.atlasTextures.Length == 0 ||
                    font.atlasTextures[0] == null || font.material == null)
                    throw new InvalidOperationException("TMP did not create atlas/material dependencies.");
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
                AssetDatabase.AddObjectToAsset(font.material, font);
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
            }

            AssetImporter importer = AssetImporter.GetAtPath(Login);
            if (importer == null) throw new InvalidOperationException("Login Figma importer is missing.");
            SerializedObject serialized = new SerializedObject(importer);
            SerializedProperty fonts = serialized.FindProperty("_fonts");
            if (fonts == null) throw new InvalidOperationException("Figma font map is unavailable.");
            int mapped = 0;
            for (int i = 0; i < fonts.arraySize; i++)
            {
                SerializedProperty entry = fonts.GetArrayElementAtIndex(i);
                string fontName = entry.FindPropertyRelative("fontName")?.stringValue ?? "";
                if (!fontName.StartsWith("Be Vietnam Pro", StringComparison.OrdinalIgnoreCase)) continue;
                entry.FindPropertyRelative("font").objectReferenceValue = font;
                mapped++;
            }
            serialized.FindProperty("_fallbackFont").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(importer);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Login, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[JarVis Figma] Mapped " + mapped + " Login font styles to Be Vietnam Pro TMP.");
        }
    }
}
