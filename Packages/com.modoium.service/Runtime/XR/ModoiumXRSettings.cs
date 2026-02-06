using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_XR_MANAGEMENT
using UnityEngine.XR.Management;

namespace Modoium.Service {
    public enum MDMMirrorBlitMode {
        None = 0,
        Left = -1,
        Right = -2,
        SideBySide = -3
    }

    public enum MDMRenderPass {
        SinglePassInstanced = 0,
        MultiPass = 1
    }

    public enum MDMContentType {
        None = 0,
        MR = 0x01,
        VR = 0x02,
        Scene = 0x04
    }

    [Serializable]
    [XRConfigurationData("Modoium", SettingsKey)]
    public class ModoiumXRSettings : ScriptableObject {
        internal const string SettingsKey = "com.modoium.service.settings.xr";

#if UNITY_EDITOR
        internal static ModoiumXRSettings instance {
            get {
                UnityEngine.Object obj;
                UnityEditor.EditorBuildSettings.TryGetConfigObject(SettingsKey, out obj);
                if (obj == null || (obj is ModoiumXRSettings) == false) { return null; }

                return obj as ModoiumXRSettings;
            }
        }
#else
        internal static ModoiumXRSettings runtimeInstance { get; private set; } = null;
        internal static ModoiumXRSettings instance => runtimeInstance;

        public void Awake() {
            if (runtimeInstance != null) { return; }

            runtimeInstance = this;
        }
#endif

        [SerializeField] private MDMMirrorBlitMode defaultMirrorBlitMode = MDMMirrorBlitMode.Left;
        [SerializeField] private MDMRenderPass desiredRenderPass = MDMRenderPass.SinglePassInstanced;

        internal int propDefaultMirrorBlitMode => (int)defaultMirrorBlitMode;
        internal MDMRenderPass propDesiredRenderPass => desiredRenderPass;
    }
}

#endif
