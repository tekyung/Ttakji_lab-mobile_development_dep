using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Modoium.Service.Editor {
    public class MDMRemoteStatusWindow : EditorWindow {
        internal enum State {
            Unknown,
            NoRunningCore,
            CoreConnected,
            ClientConnected,
            Unavailable
        }

        private MDMRemoteStatusNoRunningCoreView _noRunningCoreView;
        private MDMRemoteStatusCoreConnectedView _coreConnectedView;
        private MDMRemoteStatusClientConnectedView _clientConnectedView;
        private MDMRemoteStatusWarnings _warnings;
        private State _state = State.Unknown;
        private bool _embeddedCoreRunning = false;
        private float _videoBitrate = 10.0f;
        private string _hostName = "unknown host";
        private string _serviceName;
        private string _verificationCode = "000000";
        private string _connectedDeviceName;

        [MenuItem("Window/Modoium Remote/View Status...", false, 100)]
        public static void OpenWindow() {
            var window = GetWindow<MDMRemoteStatusWindow>();

            window.titleContent = new GUIContent {
                text = Styles.WindowTitle,
                image = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.modoium.service/Graphics/window_icon.png")
            };
            window.minSize = new Vector2(340, 100);
        }

        public void CreateGUI() {
            var scrollView = new ScrollView().FillParent();
            rootVisualElement.Add(scrollView);

            _noRunningCoreView = new MDMRemoteStatusNoRunningCoreView(scrollView);
            _coreConnectedView = new MDMRemoteStatusCoreConnectedView(scrollView);
            _clientConnectedView = new MDMRemoteStatusClientConnectedView(scrollView);
            _warnings = new MDMRemoteStatusWarnings(scrollView, MDMEditorService.statusMonitor);

            updateViews();
        }

        private void OnInspectorUpdate() {
            var shouldUpdateViews = false;
            updateState(MDMEditorService.service, ref shouldUpdateViews);
            updateServiceInfo(MDMEditorService.service, ref shouldUpdateViews);

            if (shouldUpdateViews) {
                updateViews();
            }

            _warnings?.UpdateView(_state);
        }

        private void updateState(MDMService service, ref bool changed) {
            State next;
            if (service != null) { 
                if (service.availability == MDMServiceAvailability.Available) {
                    switch (service.state) {
                        case MDMServiceState.Disconnected:
                        case MDMServiceState.Reconnecting:
                            next = State.NoRunningCore;
                            break;
                        case MDMServiceState.Ready:
                            if (service.remoteViewConnected) {
                                next = State.ClientConnected;
                            }
                            else {
                                next = State.CoreConnected;
                            }
                            break;
                        default:
                            next = State.Unknown;
                            break;
                    }
                }
                else {
                    next = service.availability == MDMServiceAvailability.Unspecified ? State.Unknown : State.Unavailable;
                }
            }
            else {
                next = State.Unknown;
            }
            
            if (_state != next) {
                _state = next;
                changed = true;
            }
        }

        private void updateServiceInfo(MDMService service, ref bool changed) {
            if (_embeddedCoreRunning != service.embeddedCoreRunning) {
                _embeddedCoreRunning = service.embeddedCoreRunning;
                changed = true;
            }
            if (_serviceName != service.serviceName) {
                _serviceName = service.serviceName;
                changed = true;
            }
            if (_hostName != service.hostName) {
                _hostName = service.hostName;
                changed = true;
            }
            if (_verificationCode != service.verificationCode) {
                _verificationCode = service.verificationCode;
                changed = true;
            }
            if (_videoBitrate != service.videoBitrateMbps) {
                _videoBitrate = service.videoBitrateMbps;
                changed = true;
            }
            if (_connectedDeviceName != service.connectedDeviceName) {
                _connectedDeviceName = service.connectedDeviceName;
                changed = true;
            }
        }

        private void updateViews() {
            _noRunningCoreView.UpdateView(_state);
            _coreConnectedView.UpdateView(_state, 
                                          _embeddedCoreRunning, 
                                          _videoBitrate, 
                                          _hostName, 
                                          _serviceName, 
                                          _verificationCode);
            _clientConnectedView.UpdateView(_state, 
                                            _embeddedCoreRunning,
                                            _videoBitrate, 
                                            _connectedDeviceName);
        }

        private class Styles {
            public static string WindowTitle = "Modoium Remote Status";
        }

#if UNITY_2021_3_OR_NEWER
        static MDMRemoteStatusWindow() {
            EditorGUI.hyperLinkClicked += onHyperLinkClicked;
        }

        private static void onHyperLinkClicked(EditorWindow window, HyperLinkClickedEventArgs args) {
            if (window.titleContent.text != Styles.WindowTitle) { return; }

            Debug.Assert(args.hyperLinkData.ContainsKey("href"));
            MDMUrlHandler.Open(args.hyperLinkData["href"]);
        }
#endif
    }

    internal static class VisualElementExtension {
        internal static VisualElement FillParent(this VisualElement element) {
            element.style.flexGrow = 1;
            element.style.flexShrink = 1;
            element.style.flexBasis = new StyleLength(StyleKeyword.Auto);

            return element;
        }

        internal static VisualElement Horizontal(this VisualElement element) {
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.FlexStart;
            element.style.flexWrap = Wrap.Wrap;

            return element;
        }

        internal static VisualElement Margin(this VisualElement element, int margin) {
            element.style.marginTop = margin;
            element.style.marginBottom = margin;
            element.style.marginLeft = margin;
            element.style.marginRight = margin;

            return element;
        }

        internal static VisualElement Padding(this VisualElement element, int padding) {
            element.style.paddingTop = padding;
            element.style.paddingBottom = padding;
            element.style.paddingLeft = padding;
            element.style.paddingRight = padding;

            return element;
        }

        internal static Box Divider(this Box box) {
            box.style.height = 1;
            box.style.alignSelf = Align.Stretch;
            box.style.borderTopColor = box.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            box.style.borderTopWidth = box.style.borderBottomWidth = 0.5f;
            box.style.marginTop = box.style.marginBottom = 10;

            return box;
        }
    }

    internal class HyperLinkTextElement : TextElement {
        private static Color _baseColor = new Color(0.0f, 0.5f, 1.0f);
        private static Color _mouseOverColor = new Color(0.3f, 0.7f, 1.0f);

        public HyperLinkTextElement() {
            style.color = _baseColor;
            style.unityFontStyleAndWeight = FontStyle.Bold;

            RegisterCallback<MouseEnterEvent>(onMouseEnter);
            RegisterCallback<MouseLeaveEvent>(onMouseLeave);   
        }

        private void onMouseEnter(MouseEnterEvent evt) {
            style.color = _mouseOverColor;
        }

        private void onMouseLeave(MouseLeaveEvent evt) {
            style.color = _baseColor;
        }
    }
}
