using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

#if UNITY_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modoium.Service {
    public enum MDMServiceState {
        Idle = 0,
        Disconnected,
        Connecting,
        Reconnecting,
        Ready
    }
    
    internal enum MDMServiceAvailability {
        Unspecified = 0,
        Available = 1,

        Unknown = -1,
        UnsupportedGraphicsAPI = -2,
        UnsupportedGraphicsAdapter = -3,
        FailedToInitEncoder = -4
    }

    internal static class ModoiumPlugin {
        private const string LibName = "modoium";

#if UNITY_XR_MANAGEMENT
    #if UNITY_EDITOR
        internal static XRGeneralSettings xrSettings {
            get {
                if (XRGeneralSettings.Instance != null) {
                    return XRGeneralSettings.Instance;
                }

                // Workaround: force to set XRGeneralSettings.Instance in non-play mode
                EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettings _);
                return XRGeneralSettings.Instance;
            }
        }

        public static bool isXR => xrSettings != null && xrSettings.InitManagerOnStart;
    #else
        public static bool isXR => XRSettings.enabled;
    #endif
#else
        public static bool isXR => false;
#endif

        public static MDMServiceState serviceState => (MDMServiceState)mdm_getServiceState();

        public static async Task<MDMServiceAvailability> CheckServiceAvailability(Action<MDMServiceAvailability> onShowUnavailable) {
            var availability = (MDMServiceAvailability)mdm_getAvailability();
            if (availability != MDMServiceAvailability.Unspecified) { return availability; }

            availability = checkAvailabilityFromSystemInfo();
            if (availability != MDMServiceAvailability.Unspecified) { 
                mdm_setAvailability((int)availability);
                onShowUnavailable?.Invoke(availability);
                return availability; 
            }

            GL.IssuePluginEvent(mdm_loadEncoderDevice_renderThread_func(), SystemInfo.graphicsDeviceVendorID);
            while (availability == MDMServiceAvailability.Unspecified) {
                await Task.Yield();
                availability = (MDMServiceAvailability)mdm_getAvailability();
            }
            if (availability != MDMServiceAvailability.Available) {
                onShowUnavailable?.Invoke(availability);
            }
            
            return availability;
        }

        private static MDMServiceAvailability checkAvailabilityFromSystemInfo() {
            if (Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.OSXPlayer) { 
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal) {
                    return MDMServiceAvailability.UnsupportedGraphicsAPI;
                }
            }
            else {
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11) {
                    return MDMServiceAvailability.UnsupportedGraphicsAPI;
                }

                switch (SystemInfo.graphicsDeviceVendorID) {
                    case 0x8086: // Intel
                    case 0x10DE: // NVIDIA
                    case 0x1002: // AMD
                        break;
                    default:
                        return MDMServiceAvailability.UnsupportedGraphicsAdapter;
                }
            }

            return MDMServiceAvailability.Unspecified;
        }
        
        [DllImport(LibName, EntryPoint = "mdm_startupService")]
        public static extern bool StartupService(string serviceName, string userAgent, string userdata);

        [DllImport(LibName, EntryPoint = "mdm_changeServiceProps")]
        public static extern void ChangeServiceProps(string serviceName);

        [DllImport(LibName, EntryPoint = "mdm_shutdownService")]
        public static extern void ShutdownService();

        [DllImport(LibName, EntryPoint = "mdm_updateService")]
        public static extern void UpdateService();

        [DllImport(LibName, EntryPoint = "mdm_reopenService")]
        public static extern void ReopenService(string serviceName, string userdata, int corePort);

        [DllImport(LibName, EntryPoint = "mdm_checkMessageQueue")]
        public static extern bool CheckMessageQueue(out IntPtr source, out IntPtr data, out int length);

        [DllImport(LibName, EntryPoint = "mdm_removeFirstMessageFromQueue")]
        public static extern void RemoveFirstMessageFromQueue();

        [DllImport(LibName, EntryPoint = "mdm_processMasterAudioOutput")]
        public static extern void ProcessMasterAudioOutput(float[] data, int sampleCount, int channels, double timestamp);

        [DllImport(LibName, EntryPoint = "mdm_play")]
        public static extern void Play(string clientAppData,
                                       float idleFrameRate,
                                       float maxFrameRate,
                                       int audioSampleRate,
                                       int displayTextureColorSpaceHint,
                                       int codecs,
                                       int encodingPreset,
                                       int encodingQuality);

        [DllImport(LibName, EntryPoint = "mdm_stop")]
        public static extern void Stop();

        [DllImport(LibName, EntryPoint = "mdm_sendConfigClient")]
        public static extern void SendConfigClient(string config);
        
        public static bool GetInputTouch2D(byte device, byte control, out Vector2 position, out byte state) {
            if (mdm_getInputTouch2D(device, control, out var pos, out state)) {
                position = pos.ToVector2();
                return true;
            }
            else {
                position = Vector2.zero;
                return false;
            }
        }

        public static bool GetInputState(byte device, byte control, out byte state) {
            if (mdm_getInputState(device, control, out state)) {
                return true;
            }
            else {
                state = 0;
                return false;
            }
        }

        [DllImport(LibName, EntryPoint = "mdm_updateInputFrame")]
        public static extern void UpdateInputFrame();

        [DllImport(LibName, EntryPoint = "mdm_isInputActive")]
        public static extern bool IsInputActive(byte device, byte control);

        [DllImport(LibName, EntryPoint = "mdm_getInputActivated")]
        public static extern bool GetInputActivated(byte device, byte control);

        [DllImport(LibName, EntryPoint = "mdm_getInputDeactivated")]
        public static extern bool GetInputDeactivated(byte device, byte control);

        public static void RenderStart(CommandBuffer commandBuffer) {
            commandBuffer.IssuePluginEvent(mdm_start_renderThread_func(), 0);
        }

        public static void RenderUpdate(CommandBuffer commandBuffer) {
            commandBuffer.IssuePluginEvent(mdm_update_renderThread_func(), 0);
        }

        public static void RenderFramebuffersReallocated(CommandBuffer commandBuffer, IntPtr nativeFramebufferArray) {
            commandBuffer.IssuePluginEventAndData(mdm_framebuffersReallocated_renderThread_func(), 0, nativeFramebufferArray);
        }

        public static void RenderPreRender(CommandBuffer commandBuffer) {
            commandBuffer.IssuePluginEvent(mdm_preRender_renderThread_func(), 0);
        }

        public static void RenderPostRender(CommandBuffer commandBuffer, int framebufferIndex, float aspect) {
            var framebufferIndexMask = framebufferIndex << 26;
            var aspectMask = (int)(aspect * 10000000);
            
            commandBuffer.IssuePluginEvent(mdm_postRender_renderThread_func(), framebufferIndexMask | aspectMask);
        }

        public static void RenderStop(CommandBuffer commandBuffer) {
            commandBuffer.IssuePluginEvent(mdm_stop_renderThread_func(), 0);
        }

        [DllImport(LibName)] private static extern int mdm_getAvailability();
        [DllImport(LibName)] private static extern void mdm_setAvailability(int value);
        [DllImport(LibName)] private static extern int mdm_getServiceState();
        [DllImport(LibName)] private static extern IntPtr mdm_loadEncoderDevice_renderThread_func();
        [DllImport(LibName)] private static extern IntPtr mdm_start_renderThread_func();
        [DllImport(LibName)] private static extern IntPtr mdm_update_renderThread_func();
        [DllImport(LibName)] private static extern IntPtr mdm_framebuffersReallocated_renderThread_func();
        [DllImport(LibName)] private static extern IntPtr mdm_preRender_renderThread_func();
        [DllImport(LibName)] private static extern IntPtr mdm_postRender_renderThread_func();
        [DllImport(LibName)] private static extern IntPtr mdm_stop_renderThread_func();
        [DllImport(LibName)] private static extern bool mdm_getInputTouch2D(byte device, byte control, out Vector2D position, out byte state);
        [DllImport(LibName)] private static extern bool mdm_getInputState(byte device, byte control, out byte state);

        [StructLayout(LayoutKind.Sequential)]
        private struct Vector2D {
            public float x;
            public float y;

            public Vector2D(Vector2 value) {
                x = value.x;
                y = value.y;
            }

            public Vector2 ToVector2() {
                return new Vector2(x, y);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Vector3D {
            public float x;
            public float y;
            public float z;

            public Vector3D(Vector3 value) {
                x = value.x;
                y = value.y;
                z = value.z;
            }

            public Vector3D(Color value) {
                x = value.r;
                y = value.g;
                z = value.b;
            }

            public Vector3 ToVector3() {
                return new Vector3(x, y, z);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Vector4D {
            public float x;
            public float y;
            public float z;
            public float w;

            public Vector4D(Vector4 value) {
                x = value.x;
                y = value.y;
                z = value.z;
                w = value.w;
            }

            public Vector4D(Quaternion value) {
                x = value.x;
                y = value.y;
                z = value.z;
                w = value.w;
            }

            public Vector4 ToVector4() {
                return new Vector4(x, y, z, w);
            }

            public Quaternion ToQuaternion() {
                return new Quaternion(x, y, z, w);
            }
        }

        // private apis
        public static void SetVolume(Mesh mesh) {
            if (mesh == null || mesh.subMeshCount == 0 || mesh.vertexCount == 0 || mesh.GetTopology(0) != MeshTopology.Triangles) {
                mdm_setVolume(null, 0, null, 0);
                return;
            }

            var vertices = new float[mesh.vertexCount * 3];
            for (var index = 0; index < mesh.vertexCount; index++) {
                var vertex = mesh.vertices[index];

                vertices[index * 3] = vertex.x;
                vertices[index * 3 + 1] = vertex.y;
                vertices[index * 3 + 2] = vertex.z;
            }

            var indices = mesh.GetIndices(0);
            mdm_setVolume(vertices, vertices.Length, indices, indices.Length);
        }

        public static void ClearVolume() {
            mdm_setVolume(null, 0, null, 0);
        }

        public static void UpdateVolumeInfo(Vector3 position, Quaternion rotation, Vector3 scale) {
            mdm_updateVolumeInfo(new Vector3D(position), new Vector4D(rotation), new Vector3D(scale));
        }

        public static void SetChromaKeyCamera(Color keyColor, float similarity, float smoothness, float spill) {
            mdm_setChromaKeyCamera(new Vector3D(keyColor), similarity, smoothness, spill);
        }

        public static void ClearChromaKeyCamera() {
            mdm_setChromaKeyCamera(new Vector3D(Color.clear), 0, 0, 0);
        }

        [DllImport(LibName)] private static extern void mdm_updateVolumeInfo(Vector3D position, Vector4D rotation, Vector3D scale);
        [DllImport(LibName)] private static extern void mdm_setVolume(float[] vertices, int vertexCount, int[] indices, int indexCount);
        [DllImport(LibName)] private static extern void mdm_setChromaKeyCamera(Vector3D keyColor, float similarity, float smoothness, float spill);

        // data caches
        private static StringBuilder _stringBuilder = new StringBuilder(2048);

        public static string hostName {
            get {
                _stringBuilder.Clear();
                mdm_getHostName(_stringBuilder, _stringBuilder.Capacity);

                return _stringBuilder.ToString();
            }
            set { mdm_setHostName(value); }
        }

        public static string verificationCode {
            get {
                _stringBuilder.Clear();
                mdm_getVerificationCode(_stringBuilder, _stringBuilder.Capacity);

                return _stringBuilder.ToString();
            }
            set { mdm_setVerificationCode(value); }
        }

        public static long videoBitrate {
            get { return mdm_getVideoBitrate(); }
            set { mdm_setVideoBitrate(value); }
        }

        public static string clientUserAgent {
            get {
                _stringBuilder.Clear();
                mdm_getClientUserAgent(_stringBuilder, _stringBuilder.Capacity);

                return _stringBuilder.ToString();
            }
            set { mdm_setClientUserAgent(value); }
        }

        public static string serviceName {
            get {
                _stringBuilder.Clear(); 
                mdm_getServiceName(_stringBuilder, _stringBuilder.Capacity);

                return _stringBuilder.ToString();
            }
            set { mdm_setServiceName(value); }
        }

        [DllImport(LibName)] private static extern void mdm_getHostName(StringBuilder outName, int maxLength);
        [DllImport(LibName)] private static extern void mdm_setHostName(string name);
        [DllImport(LibName)] private static extern void mdm_getVerificationCode(StringBuilder outCode, int maxLength);
        [DllImport(LibName)] private static extern void mdm_setVerificationCode(string code);
        [DllImport(LibName)] private static extern long mdm_getVideoBitrate();
        [DllImport(LibName)] private static extern void mdm_setVideoBitrate(long bitrate);
        [DllImport(LibName)] private static extern void mdm_getClientUserAgent(StringBuilder outUserAgent, int maxLength);
        [DllImport(LibName)] private static extern void mdm_setClientUserAgent(string userAgent);
        [DllImport(LibName)] private static extern void mdm_getServiceName(StringBuilder outName, int maxLength);
        [DllImport(LibName)] private static extern void mdm_setServiceName(string name);
    }
}
