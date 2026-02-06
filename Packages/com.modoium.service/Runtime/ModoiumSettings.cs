using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modoium.Service {
    public enum MDMTextureColorSpaceHint {
        None = 0,
        Gamma = 1,
        Linear = 2
    }

    public class ModoiumSettings : ScriptableObject {
        internal const string Version = "0.8.3";
        internal const string AssetDir = "Assets/Modoium";
        internal const string AssetPath = "Assets/Modoium/ModoiumSettings.asset";

        internal static bool IsUniversalRenderPipeline() => UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.GetType()?.Name?.Equals("UniversalRenderPipelineAsset") ?? false;
        internal static bool IsHDRenderPipeline() => UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.GetType()?.Name?.Equals("HDRenderPipelineAsset") ?? false;        

#if UNITY_EDITOR
        internal static ModoiumSettings instance => GetSettings();
        internal static SerializedObject GetSerializedSettings() => new SerializedObject(GetSettings());

        internal static ModoiumSettings GetSettings() {
            var settings = AssetDatabase.LoadAssetAtPath<ModoiumSettings>(AssetPath);
            if (settings != null) { return settings; }

            if (Directory.Exists(AssetDir) == false) {
                Directory.CreateDirectory(AssetDir);
            }

            settings = CreateInstance<ModoiumSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();

            return settings;
        }
#else
        internal static ModoiumSettings runtimeInstance { get; private set; } = null;
        internal static ModoiumSettings instance => runtimeInstance;

        private void Awake() {
            if (runtimeInstance != null) { return; }

            runtimeInstance = this;
        }
#endif

        [SerializeField] private bool _simulateTouchWithMouse = true;
        [SerializeField] private bool _enableAndroidBackButtonInput = false;
        [SerializeField] private bool _advancedSettingsEnabled = false;
        [SerializeField] private MDMTextureColorSpaceHint _displayTextureColorSpaceHint = MDMTextureColorSpaceHint.None;

        internal string serviceUserdata => string.Empty;

        internal int idleFrameRate {
            get {
                var resolution = Screen.currentResolution;

                const int DefaultIdleFrameRate = 60;
                
#if UNITY_2022_2_OR_NEWER
                return resolution.refreshRateRatio.denominator > 0 ? (int)(resolution.refreshRateRatio.numerator / resolution.refreshRateRatio.denominator) : DefaultIdleFrameRate;
#else
                return resolution.refreshRate > 0 ? resolution.refreshRate : DefaultIdleFrameRate;
#endif
            }
        }

        internal MDMTextureColorSpaceHint displayTextureColorSpaceHint {
            get {
                var value = _advancedSettingsEnabled ? _displayTextureColorSpaceHint : MDMTextureColorSpaceHint.None;
                if (value != MDMTextureColorSpaceHint.None) { return value; }
                else if (ModoiumPlugin.isXR == false) { return MDMTextureColorSpaceHint.Gamma; }

                if (IsUniversalRenderPipeline()) {
                    // workaround: URP uses always non-sRGB texture even if color space is set to linear. (but xr plugin misleads as if it were sRGB.)
                    value = MDMTextureColorSpaceHint.Gamma;
                }
                else if (IsHDRenderPipeline() && QualitySettings.activeColorSpace == ColorSpace.Gamma) {
                    // workaround: On HDRP, xr plugin misleads as if texture were sRGB even when color space is set to gamma.
                    value = MDMTextureColorSpaceHint.Gamma;
                }
                return value;
            }
        }

        internal bool simulateTouchWithMouse => _simulateTouchWithMouse;
        internal bool enableAndroidBackButtonInput => _enableAndroidBackButtonInput;

        // private apis
#pragma warning disable 0414
        [SerializeField] private MDMCodec _codecs = MDMCodec.All;
        [SerializeField] private MDMEncodingPreset _encodingPreset = MDMEncodingPreset.LowLatency;
        [SerializeField] private MDMEncodingQuality _encodingQuality = MDMEncodingQuality.VeryHigh;
#pragma warning restore 0414

#if MODOIUM_PRIVATE_API
        internal MDMCodec codecs => _advancedSettingsEnabled ? _codecs : MDMCodec.All;
        internal MDMEncodingPreset encodingPreset => _advancedSettingsEnabled ? _encodingPreset : MDMEncodingPreset.LowLatency;
        internal MDMEncodingQuality encodingQuality => _advancedSettingsEnabled ? _encodingQuality : MDMEncodingQuality.VeryHigh;
#else
        private bool isH264AvailableOnly => SystemInfo.processorType.Contains("Apple M") && RuntimeInformation.ProcessArchitecture == Architecture.X64;

        internal MDMCodec codecs => isH264AvailableOnly ? MDMCodec.H264 : MDMCodec.All;
        internal MDMEncodingPreset encodingPreset => MDMEncodingPreset.LowLatency;
        internal MDMEncodingQuality encodingQuality => MDMEncodingQuality.VeryHigh;
#endif
    }

#if UNITY_EDITOR
    public class ModoiumSettingsProvider : SettingsProvider {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() {
            return new ModoiumSettingsProvider("Project/ModoiumRemoteSettings", SettingsScope.Project) {
                label = "Modoium Remote",
                keywords = new HashSet<string>(new[] { "Modoium", "Remote" })
            };
        }

        private SerializedObject _settings;
        private SerializedProperty _propSimulateTouchWithMouse;
        private SerializedProperty _propEnableAndroidBackButtonInput;
        private SerializedProperty _propAdvancedSettingsEnabled;
        private SerializedProperty _propDisplayTextureColorSpaceHint;
        private SerializedProperty _propCodecs;
        private SerializedProperty _propEncodingPreset;
        private SerializedProperty _propEncodingQuality;

        public ModoiumSettingsProvider(string path, SettingsScope scopes = SettingsScope.Project) : base(path, scopes) {
            // NOTE: create settings asset if not exists
            _settings = ModoiumSettings.GetSerializedSettings();
        }

        public override void OnActivate(string searchContext, VisualElement rootElement) {
            _settings = ModoiumSettings.GetSerializedSettings();

            _propSimulateTouchWithMouse = _settings.FindProperty("_simulateTouchWithMouse");
            _propEnableAndroidBackButtonInput = _settings.FindProperty("_enableAndroidBackButtonInput");

#if MODOIUM_PRIVATE_API
            _propAdvancedSettingsEnabled = _settings.FindProperty("_advancedSettingsEnabled");
            _propDisplayTextureColorSpaceHint = _settings.FindProperty("_displayTextureColorSpaceHint");
            _propCodecs = _settings.FindProperty("_codecs");
            _propEncodingPreset = _settings.FindProperty("_encodingPreset");
            _propEncodingQuality = _settings.FindProperty("_encodingQuality");
#endif
        }

        public override void OnGUI(string searchContext) {
            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 240;
            EditorGUI.indentLevel++;
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(Styles.labelInput, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_propSimulateTouchWithMouse, Styles.labelSimulateTouchWithMouse);
                EditorGUILayout.PropertyField(_propEnableAndroidBackButtonInput, Styles.labelEnableAndroidBackButtonInput);

#if MODOIUM_PRIVATE_API
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.PropertyField(_propAdvancedSettingsEnabled, Styles.labelAdvancedSettingsEnabled);
                    if (_propAdvancedSettingsEnabled.boolValue) {
                        EditorGUILayout.Space();

                        EditorGUILayout.PropertyField(_propDisplayTextureColorSpaceHint, Styles.labelDisplayTextureColorSpaceHint);
                        EditorGUILayout.PropertyField(_propCodecs, Styles.labelCodecs);
                        EditorGUILayout.PropertyField(_propEncodingPreset, Styles.labelEncodingPreset);
                        EditorGUILayout.PropertyField(_propEncodingQuality, Styles.labelEncodingQuality);
                    }
                }
                EditorGUILayout.EndVertical();
#endif
            }
            EditorGUI.indentLevel--;
            EditorGUIUtility.labelWidth = prevLabelWidth;

            _settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static class Styles {
            public static GUIContent labelInput = new GUIContent("Input");
            public static GUIContent labelSimulateTouchWithMouse = new GUIContent("Simulate Touch With Mouse");
            public static GUIContent labelEnableAndroidBackButtonInput = new GUIContent("Enable Android Back Button Input");
            public static GUIContent labelAdvancedSettingsEnabled = new GUIContent("Advanced Settings");
            public static GUIContent labelDisplayTextureColorSpaceHint = new GUIContent("Display Texture Color Space Hint");
            public static GUIContent labelCodecs = new GUIContent("Codecs");
            public static GUIContent labelEncodingPreset = new GUIContent("Encoding Preset");
            public static GUIContent labelEncodingQuality = new GUIContent("Encoding Quality");
        }
    }
#endif //UNITY_EDITOR
}
