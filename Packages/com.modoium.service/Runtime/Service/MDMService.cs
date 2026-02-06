using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Modoium.Service {
    internal class MDMService {
        public interface IApplication {
            bool useEmbeddedCore { get; }
            string verificationCodeForEmbeddedCore { get; }
            bool isMobilePlatform { get; }
            ScreenOrientation orientation { get; }
            bool isPlaying { get; }
        }

        private const float MaxFrameRate = 120f;

        private static string getAvailabilityDesc(MDMServiceAvailability availability) {
            return availability switch {
                MDMServiceAvailability.Unspecified => string.Empty,
                MDMServiceAvailability.Available => string.Empty,
                MDMServiceAvailability.UnsupportedGraphicsAPI => 
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                    "Unsupported graphics API. Metal is supported only.",
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                    "Unsupported graphics API. Direct3D 11 is supported only.",
#else
                    "Unsupported graphics API.",
#endif
                MDMServiceAvailability.UnsupportedGraphicsAdapter => 
                    $"Unsupported graphics adapter: {SystemInfo.graphicsDeviceName}",
                MDMServiceAvailability.FailedToInitEncoder =>
                    "Failed to initialize video encoder. Please check if your graphics driver is up to date.",
                _ => "System error"
            };
        }

        private IApplication _app;
        private MDMMessageDispatcher _messageDispatcher;
        private MDMAudioListener _audioListener;
        private MDMAppData _clientAppData;
        private MDMServiceConfigurator _serviceConfigurator;
        private MDMClientConfigurator _clientConfigurator;
        private MDMDisplayRotation _displayRotation;
        private MDMDisplayRenderer _displayRenderer;
        private MDMDisplayConfigurator _displayConfigurator;
        private MDMXRConfigurator _xrConfigurator;
        private MDMInputProvider _inputProvider;
        private ModoiumCore _embeddedCore = new ModoiumCore();
        private MDMServiceAvailability _availability = MDMServiceAvailability.Unspecified;
        private float _timeToReopenService = -1f;
        private Regex _regexDeviceName = new Regex(@"Modoium/[^\s]+ \((.+);.+\)");

        private bool coreConnected => state == MDMServiceState.Ready;

        internal MDMDisplayConfigurator displayConfigurator => _displayConfigurator;
        internal string serviceName => _serviceConfigurator.serviceName;
        internal string hostName => ModoiumPlugin.hostName;
        internal string verificationCode => ModoiumPlugin.verificationCode;
        internal float videoBitrateMbps => ModoiumPlugin.videoBitrate / 1000000.0f;
        internal MDMServiceState state => ModoiumPlugin.serviceState;
        internal MDMServiceAvailability availability => _availability;
        internal string availabilityDescription => getAvailabilityDesc(_availability);
        internal bool embeddedCoreRunning => _embeddedCore.running;
        internal MDMVideoDesc remoteViewDesc { get; private set; }
        internal MDMInputDesc remoteInputDesc { get; private set; }
        internal bool remoteViewConnected => coreConnected && remoteViewDesc != null;
        internal bool isAppMobilePlatform => _app.isMobilePlatform;
        internal ScreenOrientation appOrientation => _app.orientation;
        internal bool isAppPlaying => _app.isPlaying;

        internal string connectedDeviceName {
            get {
                var userAgent = ModoiumPlugin.clientUserAgent;
                var matches = _regexDeviceName.Matches(userAgent);
                if (matches.Count == 0 || matches[0].Groups.Count <= 1) { return userAgent; }

                return matches[0].Groups[1].Value;
            }
        }

        public MDMService(IApplication app) {
            _app = app;
            _messageDispatcher = new MDMMessageDispatcher();
            _serviceConfigurator = new MDMServiceConfigurator();
            _clientConfigurator = new MDMClientConfigurator(this);
            _displayRotation = new MDMDisplayRotation(this);
            _displayConfigurator = new MDMDisplayConfigurator(this, _displayRotation);
            _xrConfigurator = new MDMXRConfigurator(this);
            _displayRenderer = new MDMDisplayRenderer(this, _displayRotation, MaxFrameRate, app as MonoBehaviour);
            _inputProvider = new MDMInputProvider(this, _displayRotation);
 
            _messageDispatcher.onMessageReceived += onMDMMessageReceived;
        }

        public async Task<bool> Startup(Action<string> onShowUnavailable) {
            _availability = await ModoiumPlugin.CheckServiceAvailability((availability) => 
                onShowUnavailable?.Invoke(getAvailabilityDesc(availability))
            );
            if (_availability != MDMServiceAvailability.Available) { return false; }

            var settings = ModoiumSettings.instance;

            var gotStarted = ModoiumPlugin.StartupService(_serviceConfigurator.serviceName,
                                                          makeUserAgentString(),
                                                          settings.serviceUserdata);

            _displayConfigurator.OnPostFirstMessageDispatch(_messageDispatcher.Dispatch());
            return gotStarted;
        }

        public void Shutdown() {
            if (_availability != MDMServiceAvailability.Available) { return; }

            ModoiumPlugin.ShutdownService();
            _messageDispatcher.onMessageReceived -= onMDMMessageReceived;

            _embeddedCore.Shutdown();
        }

        public void SearchForModoiumHub() {
            if (embeddedCoreRunning == false) { return; }

            _embeddedCore.Shutdown();
        }

        public void Play() {
            if (remoteViewConnected == false) { return; } 

            _displayRenderer.Start();
            requestPlay();
        }

        public void Stop() {
            if (remoteViewConnected == false) { return; } 

            requestStop();
            _displayRenderer.Stop();
        }

        public void Update() {
            if (_availability != MDMServiceAvailability.Available) { return; }

            if (Application.isPlaying) { 
                ensureAudioListenerConfigured();
                _xrConfigurator.EnsureTrackersConfigured();
                _inputProvider.Update();
            }

            _messageDispatcher.Dispatch();

            updateReopenService();

            if (coreConnected) {
                _displayConfigurator.Update();
            }

            ModoiumPlugin.UpdateService();
            _serviceConfigurator.Update();

            _clientConfigurator.Update();
        }

        private string makeUserAgentString() {
            var servicePath = Regex.Escape(Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/"));
            return $"Modoium Remote/{ModoiumSettings.Version} (Unity Editor; {servicePath})";
        }

        private void ensureAudioListenerConfigured() {
            if (_audioListener != null && _audioListener.gameObject.activeInHierarchy) { return; }

            _audioListener = null;

#if UNITY_2023_1_OR_NEWER
            var audioListener = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
#else
            var audioListener = UnityEngine.Object.FindObjectOfType<AudioListener>();
#endif
            if (audioListener == null) { return; }

            _audioListener = audioListener.gameObject.GetComponent<MDMAudioListener>();
            if (_audioListener == null) {
                _audioListener = audioListener.gameObject.AddComponent<MDMAudioListener>();
            }
        }

        private void updateReopenService() {
            if (state != MDMServiceState.Disconnected) {
                _timeToReopenService = -1f;
                return;
            }

            if (_timeToReopenService < 0f) { 
                _timeToReopenService = Time.realtimeSinceStartup + 0.5f;
            }
            if (Time.realtimeSinceStartup < _timeToReopenService) { return; }

            var settings = ModoiumSettings.instance;
            ModoiumPlugin.ReopenService(_serviceConfigurator.serviceName, 
                                        settings.serviceUserdata,
                                        _embeddedCore.running ? _embeddedCore.port : 0);

            _timeToReopenService = -1f;
        }

        private void onMDMMessageReceived(MDMMessage message) {
            if (message is MDMMessageCoreConnected coreConnected) {
                onCoreConnected(coreConnected);
            }
            else if (message is MDMMessageCoreConnectionFailed coreConnectionFailed) {
                onCoreConnectionFailed(coreConnectionFailed);
            }
            else if (message is MDMMessageCoreDisconnected coreDisconnected) {
                onCoreDisconnected(coreDisconnected);
            }
            else if (message is MDMMessageSessionInitiated sessionInitiated) {
                onSessionInitiated(sessionInitiated);
            }
            else if (message is MDMMessageSessionCancelled sessionCancelled) {
                onSessionCancelled(sessionCancelled);
            }
            else if (message is MDMMessageAmpOpened ampOpened) {
                onAmpOpened(ampOpened);
            }
            else if (message is MDMMessageAmpClosed ampClosed) {
                onAmpClosed(ampClosed);
            }
            else if (message is MDMMessageClientAppData clientAppData) {
                onClientAppData(clientAppData);
            }
            else if (message is MDMMessageCoreEventConfigChanged coreConfigChanged) {
                onCoreConfigChanged(coreConfigChanged);
            }
            else if (message is MDMMessageCoreEventSetConfig coreSetConfig) {
                onCoreSetConfig(coreSetConfig);
            }
            else if (message is MDMMessageCoreEventClientConnected coreClientConnected) {
                onCoreClientConnected(coreClientConnected);
            }
        }

        private void onCoreConnected(MDMMessageCoreConnected message) {
            // do nothing
        }

        private void onCoreConnectionFailed(MDMMessageCoreConnectionFailed message) {
            var failureCode = (MDMFailureCode)message.code;
            if (failureCode != MDMFailureCode.CoreNotFound) {
                Debug.LogWarning($"[modoium] core connection failed: {failureCode} (status code {message.statusCode}): {message.reason}");
            }

            if (_app.useEmbeddedCore) {
                _embeddedCore.Startup(_app.verificationCodeForEmbeddedCore);
                Debug.Assert(_embeddedCore.running);
            }
        }

        private void onCoreDisconnected(MDMMessageCoreDisconnected message) {
            clearClientAppData();
        }

        private void onSessionInitiated(MDMMessageSessionInitiated message) {
            setClientAppData(message.appData);
        }
        
        private void onSessionCancelled(MDMMessageSessionCancelled message) {
            Debug.LogWarning($"[modoium] session cancelled: reason = {message.reason}");

            _displayRenderer.Stop(); 
            clearClientAppData();
        }

        private void onAmpOpened(MDMMessageAmpOpened message) {
            _clientConfigurator.SendConfigClient();

            if (_app.isPlaying) { 
                Play();
            }
        }

        private void onAmpClosed(MDMMessageAmpClosed message) {
            _displayRenderer.Stop(); 

            clearClientAppData();
        }

        private void onClientAppData(MDMMessageClientAppData message) {
            setClientAppData(message.appData);
        }

        private void onCoreConfigChanged(MDMMessageCoreEventConfigChanged message) {
            if (string.IsNullOrEmpty(message.hostname) == false) {
                ModoiumPlugin.hostName = message.hostname;
            }
            if (string.IsNullOrEmpty(message.verificationCode) == false) {
                ModoiumPlugin.verificationCode = message.verificationCode;
            }
        }

        private void onCoreSetConfig(MDMMessageCoreEventSetConfig message) {
            ModoiumPlugin.videoBitrate = message.bitrate;
        }

        private void onCoreClientConnected(MDMMessageCoreEventClientConnected message) {
            ModoiumPlugin.clientUserAgent = message.userAgent;
        }

        private void setClientAppData(MDMAppData appData) {
            _clientAppData = appData;
            remoteViewDesc = appData.videoDesc;
            remoteInputDesc = appData.inputDesc;
        }

        private void clearClientAppData() {
            _clientAppData = null;
            remoteViewDesc = null;
            remoteInputDesc = null;
        }

        private void requestPlay() {
            Debug.Assert(_clientAppData != null);
            var settings = ModoiumSettings.instance;

            var isXR = ModoiumPlugin.isXR;
            if ((_clientAppData.videoDesc is MDMStereoVideoDesc) != isXR) { 
                Debug.LogWarning($"[Modoium Remote] the connected client does not support {(isXR ? "XR" : "non-XR")} content.");
                return; 
            }

            ModoiumPlugin.Play(JsonConvert.SerializeObject(_clientAppData),
                               settings.idleFrameRate,
                               MaxFrameRate,
                               AudioSettings.outputSampleRate,
                               (int)settings.displayTextureColorSpaceHint,
                               (int)settings.codecs,
                               (int)settings.encodingPreset,
                               (int)settings.encodingQuality);
        }

        private void requestStop() {
            ModoiumPlugin.Stop();
        }
    }
}
