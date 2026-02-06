using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Threading.Tasks;

namespace Modoium.Service {
    internal class MDMDisplayRenderer {
        private MDMService _owner;
        private MDMDisplayRotation _rotation;
        private float _maxFramerate;
        private MonoBehaviour _driver;
        private CommandBuffer _commandBuffer;
        private SwapChain _swapChain;
        private Material _blitMaterial;
        private bool _running;
        private double _lastFrameTime;

        public bool running => _running;

        public MDMDisplayRenderer(MDMService owner, MDMDisplayRotation rotation, float maxFramerate, MonoBehaviour driver = null) {
            _owner = owner;
            _rotation = rotation;
            _maxFramerate = maxFramerate;
            _driver = driver;
            _commandBuffer = new CommandBuffer();
        }

        public async void Start() {
            if (ModoiumPlugin.isXR || _running) { return; }

            while (_swapChain != null) { await Task.Yield(); }
            _running = true;

            if (_blitMaterial == null) {
                _blitMaterial = new Material(Shader.Find("Modoium/Fullscreen Blit"));
            }

            _swapChain = new SwapChain(_owner.remoteViewDesc, _blitMaterial);
            
            startCoroutine(renderLoop());
        }

        public void Stop() {
            _running = false;
        }

        private IEnumerator renderLoop() {
            var prevFrameRate = Application.targetFrameRate;
            var encodeFrameRate = Mathf.Min(_owner.remoteViewDesc.framerate, _maxFramerate);
            Application.targetFrameRate = Mathf.RoundToInt(Mathf.Min(encodeFrameRate * 2, _maxFramerate));

            _lastFrameTime = Time.unscaledTimeAsDouble;
            var frameInterval = 1.0 / encodeFrameRate;
            var resetFrameTimeThreshold = 0.18;

            ModoiumPlugin.RenderStart(_commandBuffer);
            flushCommandBuffer(_commandBuffer);

            while (_running) {
                yield return new WaitForEndOfFrame();
                if (_running == false) { break; }

                var elapsed = Time.unscaledTimeAsDouble - _lastFrameTime;
                if (elapsed < frameInterval) { continue; }

                _lastFrameTime = elapsed >= resetFrameTimeThreshold ? Time.unscaledTimeAsDouble : 
                                                                      (_lastFrameTime + frameInterval);

                var remoteViewDesc = _owner.remoteViewDesc;
                var remoteInputDesc = _owner.remoteInputDesc;

                ModoiumPlugin.RenderUpdate(_commandBuffer);

                if (_swapChain.reallocated) {
                    ModoiumPlugin.RenderFramebuffersReallocated(_commandBuffer, _swapChain.nativeFramebufferArray);
                }

                ModoiumPlugin.RenderPreRender(_commandBuffer);
                _swapChain.CopyFrameBuffer(_commandBuffer, 
                                           remoteViewDesc, 
                                           remoteInputDesc,
                                           _rotation, 
                                           out var framebufferIndex, 
                                           out var aspect);
                ModoiumPlugin.RenderPostRender(_commandBuffer, framebufferIndex, aspect);

                flushCommandBuffer(_commandBuffer);
            }

            ModoiumPlugin.RenderStop(_commandBuffer);
            flushCommandBuffer(_commandBuffer);

            _swapChain.Release();
            _swapChain = null;

            Application.targetFrameRate = prevFrameRate;
        }

        private void flushCommandBuffer(CommandBuffer commandBuffer) {
            Graphics.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
        }

        private void startCoroutine(IEnumerator coroutine) {
            if (_driver == null) {
                _driver = Driver.Create();
            }

            _driver.StartCoroutine(coroutine);
        }

        private class SwapChain {
            private const int Length = 4;

            private Material _blitMaterial;
            private RenderTexture[] _textures;
            private RenderTexture _blitBufferTexture;
            private FramebufferArray _framebufferArray;
            private int _cursor;
            private bool _reallocated;

            public IntPtr nativeFramebufferArray { get; private set; } = IntPtr.Zero;
            
            public bool reallocated {
                get {
                    if (_reallocated == false) { return false; }

                    _reallocated = false;
                    return true;
                }
            }

            public SwapChain(MDMVideoDesc remoteViewDesc, Material blitMaterial) {
                _blitMaterial = blitMaterial;
                _textures = new RenderTexture[Length];
                _framebufferArray = new FramebufferArray(Length);
                nativeFramebufferArray = Marshal.AllocHGlobal(_framebufferArray.count * IntPtr.Size + sizeof(int));

                reallocate(remoteViewDesc);
            }

            public void CopyFrameBuffer(CommandBuffer commandBuffer, 
                                        MDMVideoDesc remoteViewDesc,
                                        MDMInputDesc remoteInputDesc,
                                        MDMDisplayRotation rotation, 
                                        out int framebufferIndex, 
                                        out float aspect) {
                var fbtexture = _textures[_cursor];
                var blitTransform = rotation.EvalFramebufferBlitTransform(
                    (Display.main.renderingWidth, Display.main.renderingHeight),
                    (remoteViewDesc.viewWidth, remoteViewDesc.viewHeight),
                    remoteInputDesc.screenRotation,
                    (fbtexture.width, fbtexture.height)
                );

                if (blitTransform.rotation == Matrix4x4.identity && 
                    blitTransform.aspect == 0) {
                    aspect = 0;
                    commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, fbtexture);
                }
                else {
                    aspect = blitTransform.aspect;

                    _blitMaterial.SetMatrix("_Rotation", blitTransform.rotation);

                    var blitBufferTexture = updateBlitBufferTexture();
                    if (blitBufferTexture != null) {
                        commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, blitBufferTexture);
                        commandBuffer.Blit(blitBufferTexture, fbtexture, _blitMaterial);
                    }
                    else {
                        commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, fbtexture, _blitMaterial);
                    }
                }

                framebufferIndex = _cursor;
                _cursor = (_cursor + 1) % _textures.Length;
            }

            public void Release() {
                for (var index = 0; index < _textures.Length; index++) {
                    _textures[index]?.Release();
                    _textures[index] = null;
                }

                _blitBufferTexture?.Release();
                _blitBufferTexture = null;

                Marshal.DestroyStructure(nativeFramebufferArray, typeof(FramebufferArray));
                Marshal.FreeHGlobal(nativeFramebufferArray);
            }

            private RenderTexture updateBlitBufferTexture() {
                // workaround: blitting framebuffer to render texture with material does not work, so should use buffer texture
                if (Application.isEditor) { return null; }

                if (_blitBufferTexture != null &&
                    _blitBufferTexture.width == Display.main.renderingWidth &&
                    _blitBufferTexture.height == Display.main.renderingHeight) {
                    return _blitBufferTexture;
                }

                _blitBufferTexture?.Release();
                _blitBufferTexture = createTexture(Display.main.renderingWidth, Display.main.renderingHeight);
                return _blitBufferTexture;
            }

            private void reallocate(MDMVideoDesc remoteViewDesc) {
                for (var index = 0; index < _textures.Length; index++) {
                    _textures[index]?.Release();

                    _textures[index] = createTexture(remoteViewDesc.videoWidth, remoteViewDesc.videoHeight);
                    _framebufferArray.framebuffers[index] = _textures[index].GetNativeTexturePtr();
                }

                Marshal.StructureToPtr(_framebufferArray, nativeFramebufferArray, false);

                _cursor = 0;
                _reallocated = true;
            }

            private RenderTexture createTexture(int width, int height) {
                var texture = new RenderTexture(width, height, 0, RenderTextureFormat.BGRA32) {
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 0
                };
                texture.Create();

                return texture;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct FramebufferArray {
                [MarshalAs(UnmanagedType.LPArray)]
                public IntPtr[] framebuffers;
                public int count;

                public FramebufferArray(int length) {
                    framebuffers = new IntPtr[length];
                    count = length;
                }
            }
        }

        private class Driver : MonoBehaviour {
            public static Driver Create() {
                var go = new GameObject("DisplayRendererDriver") {
                    hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector
                };
                DontDestroyOnLoad(go);

                return go.AddComponent<Driver>();
            }
        }
    }
}
