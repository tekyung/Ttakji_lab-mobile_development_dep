#if MODOIUM_PRIVATE_API

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Modoium.Service.Editor {
    [CustomEditor(typeof(ChromaKeyCamera))]
    public class ChromaKeyCameraEditor : UnityEditor.Editor {
        private SerializedProperty _propPreset;
        private SerializedProperty _propClearColor;
        private SerializedProperty _propKeyColor;
        private SerializedProperty _propSimilarity;
        private SerializedProperty _propSmoothness;
        private SerializedProperty _propSpill;

        private void OnEnable() {
            _propPreset = serializedObject.FindProperty("_preset");
            _propClearColor = serializedObject.FindProperty("_clearColor");
            _propKeyColor = serializedObject.FindProperty("_keyColor");
            _propSimilarity = serializedObject.FindProperty("_similarity");
            _propSmoothness = serializedObject.FindProperty("_smoothness");
            _propSpill = serializedObject.FindProperty("_spill");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_propPreset);

            switch ((ChromaKeyCamera.Preset)_propPreset.intValue) {
                case ChromaKeyCamera.Preset.Green:
                    renderPresetDesc("clear color = #00a40000\nkey color = green\nsimilarity = 0.42\nsmoothness = 0.08\nspill = 0.1");
                    break;
                case ChromaKeyCamera.Preset.Blue:
                    renderPresetDesc("clear color = #0000a400\nkey color = blue\nsimilarity = 0.42\nsmoothness = 0.08\nspill = 0.1");
                    break;
                default:
                    renderCustomSettings();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void renderPresetDesc(string desc) {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("", GUILayout.MaxWidth(EditorGUIUtility.labelWidth));

                EditorGUILayout.LabelField(desc, new GUIStyle(EditorStyles.label) {
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft
                });
            }
            EditorGUILayout.EndHorizontal();
        }

        private void renderCustomSettings() {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.PropertyField(_propClearColor);
                EditorGUILayout.PropertyField(_propKeyColor);
                EditorGUILayout.PropertyField(_propSimilarity, Styles.labelSimilarity);
                EditorGUILayout.PropertyField(_propSmoothness, Styles.labelSmoothness);
                EditorGUILayout.PropertyField(_propSpill, Styles.labelSpill);
            }
            EditorGUILayout.EndVertical();
        }

        private class Styles {
            public static GUIContent labelSimilarity = new GUIContent("Similarity", "threshold between the key color and those found in the image");
            public static GUIContent labelSmoothness = new GUIContent("Smoothness", "the smoothness of the color removal");
            public static GUIContent labelSpill = new GUIContent("Key Color Spill Reduction", "how aggressively to remove traces of the key color around the edge of other colors");
        }
    }
}

#endif
