using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Modoium.Service.Editor {
    [CustomEditor(typeof(RuntimeService))]
    public class RuntimeServiceEditor : UnityEditor.Editor {
        private SerializedProperty _propRequiresHub;
        private SerializedProperty _propVerificationCode;
        private SerializedProperty _propOrientation;

        private void OnEnable() {
            _propRequiresHub = serializedObject.FindProperty("_requiresHub");
            _propVerificationCode = serializedObject.FindProperty("_verificationCode");
            _propOrientation = serializedObject.FindProperty("_orientation");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_propRequiresHub, Styles.labelRequiresHub);
            if (_propRequiresHub.boolValue == false) {
                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.PropertyField(_propVerificationCode, Styles.labelVerificationCode);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.PropertyField(_propOrientation);

            serializedObject.ApplyModifiedProperties();
        }

        private class Styles {
            public static GUIContent labelRequiresHub = new GUIContent("Requires Hub", "Check if you want to force the built executable to use Modoium Hub.");
            public static GUIContent labelVerificationCode = new GUIContent("Verification Code", "The verification code used when the built executable runs without Modoium Hub.");
        }
    }
}
