using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Modoium.Service {
    public class RuntimeService : MonoBehaviour, MDMService.IApplication {
        public static RuntimeService instance { get; private set; }

        private MDMService _service;

        [SerializeField] private bool _requiresHub = true;
        [SerializeField] private string _verificationCode = null;
        [SerializeField] private ScreenOrientation _orientation = ScreenOrientation.AutoRotation;

        private void Awake() {
            if (instance != null) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (Application.isEditor) { return; }
            
            _service = new MDMService(this);
        }

        private async void Start() {
            if (Application.isEditor) { return; }

            await _service.Startup((message) => 
                Debug.LogError($"[modoium] cannot start modoium service: {message})")
            );
        }

        private void Update() {
            if (Application.isEditor) { return; }

            _service.Update();
        }

        private void OnApplicationQuit() {
            if (Application.isEditor) { return; }
            
            if (instance != null) {
                Destroy(instance.gameObject);
            }
        }

        private void OnDestroy() {
            if (Application.isEditor) { return; }

            _service.Shutdown();
            
            instance = null;
        }

        // implements MDMService.IApplication
        bool MDMService.IApplication.useEmbeddedCore => _requiresHub == false;
        string MDMService.IApplication.verificationCodeForEmbeddedCore => _verificationCode;
        bool MDMService.IApplication.isMobilePlatform => true;
        ScreenOrientation MDMService.IApplication.orientation => _orientation;
        bool MDMService.IApplication.isPlaying => true;
    }
}
