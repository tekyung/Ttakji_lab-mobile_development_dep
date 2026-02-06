using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

#if UNITY_XR_MANAGEMENT

namespace Modoium.Service.Editor {
    [CustomEditor(typeof(ModoiumXRSettings))]
    public class ModoiumXRSettingsEditor : UnityEditor.Editor {
        private SerializedProperty _propDefaultMirrorBlitMode;
        private SerializedProperty _propDesiredRenderPass;

        private void OnEnable() {
            _propDefaultMirrorBlitMode = serializedObject.FindProperty("defaultMirrorBlitMode");
            _propDesiredRenderPass = serializedObject.FindProperty("desiredRenderPass");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 200;
            {
                EditorGUILayout.PropertyField(_propDesiredRenderPass, Styles.labelDesiredRenderPass);
                EditorGUILayout.PropertyField(_propDefaultMirrorBlitMode, Styles.labelMirrorBlitMode);
            }
            EditorGUIUtility.labelWidth = prevLabelWidth;

            serializedObject.ApplyModifiedProperties();
        }

        private static class Styles {
            public static GUIContent labelDesiredRenderPass = new GUIContent("Render Pass");
            public static GUIContent labelMirrorBlitMode = new GUIContent("Mirror View Mode");
        }
    }
}

#endif