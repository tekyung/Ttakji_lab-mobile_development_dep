using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

#if UNITY_XR_MANAGEMENT
using UnityEngine.XR.Management;

#if UNITY_EDITOR
    using UnityEditor;
#endif

namespace Modoium.Service {
    public class ModoiumLoader : XRLoaderHelper {
        private static List<XRDisplaySubsystemDescriptor> _displaySubsystemDescriptors = new List<XRDisplaySubsystemDescriptor>();
        private static List<XRInputSubsystemDescriptor> _inputSubsystemDescriptors = new List<XRInputSubsystemDescriptor>();

        public override bool Initialize() {
#if UNITY_INPUT_SYSTEM && UNITY_XR_MANAGEMENT
            MDMInputLayoutLoader.RegisterInputLayouts();
#endif

            CreateSubsystem<XRDisplaySubsystemDescriptor, XRDisplaySubsystem>(_displaySubsystemDescriptors, "Modoium Display");
            CreateSubsystem<XRInputSubsystemDescriptor, XRInputSubsystem>(_inputSubsystemDescriptors, "Modoium Input");
            return true;
        }

        public override bool Start() {
            var xrsettings = ModoiumXRSettings.instance;

            StartSubsystem<XRDisplaySubsystem>();
            StartSubsystem<XRInputSubsystem>();
            
            var display = GetLoadedSubsystem<XRDisplaySubsystem>();
            display.SetPreferredMirrorBlitMode(xrsettings.propDefaultMirrorBlitMode);

            switch (xrsettings.propDesiredRenderPass) {
                case MDMRenderPass.SinglePassInstanced:
                    if ((display.supportedTextureLayouts & XRDisplaySubsystem.TextureLayout.Texture2DArray) != 0) {
                        display.textureLayout = XRDisplaySubsystem.TextureLayout.Texture2DArray;
                    }
                    else if ((display.supportedTextureLayouts & XRDisplaySubsystem.TextureLayout.SeparateTexture2Ds) != 0) {
                        display.textureLayout = XRDisplaySubsystem.TextureLayout.SeparateTexture2Ds;

                        Debug.LogWarning($"[modoium] warning: Single Pass Instanced rendering is not supported. Fallback to Multi Pass rendering.");
                    }
                    else {
                        Debug.LogError("$[modoium] ERROR: any render pass is not supported.");
                    }
                    break;
                case MDMRenderPass.MultiPass:
                    if ((display.supportedTextureLayouts & XRDisplaySubsystem.TextureLayout.SeparateTexture2Ds) != 0) {
                        display.textureLayout = XRDisplaySubsystem.TextureLayout.SeparateTexture2Ds;
                    }
                    else if ((display.supportedTextureLayouts & XRDisplaySubsystem.TextureLayout.Texture2DArray) != 0) {
                        display.textureLayout = XRDisplaySubsystem.TextureLayout.Texture2DArray;

                        Debug.LogWarning($"[modoium] warning: Multi Pass rendering is not supported. Fallback to Single Pass Instanced rendering.");
                    }
                    else {
                        Debug.LogError("$[modoium] ERROR: any render pass is not supported.");
                    }
                    break;
            }
            return true;
        }

        public override bool Stop() {
            StopSubsystem<XRDisplaySubsystem>();
            StopSubsystem<XRInputSubsystem>();
            return true;
        }

        public override bool Deinitialize() {
            DestroySubsystem<XRDisplaySubsystem>();
            DestroySubsystem<XRInputSubsystem>();
            return true;
        }
    }
}

#endif
