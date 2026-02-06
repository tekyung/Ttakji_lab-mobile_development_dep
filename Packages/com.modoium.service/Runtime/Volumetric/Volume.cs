#if MODOIUM_PRIVATE_API

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modoium.Service {
    [RequireComponent(typeof(MeshFilter))]
    public class Volume : MonoBehaviour {
        private Transform _thisTransform;
        private MeshFilter _meshFilter;
        private MeshRenderer _renderer;
        private Transform _cameraSpace;
        private bool _volumetricEnabled;

        [SerializeField] private Camera _camera = null;

        private void Awake() {
            _thisTransform = transform;
            _meshFilter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();

            var mesh = _meshFilter?.sharedMesh;
            if (mesh == null || mesh.subMeshCount == 0 || mesh.vertexCount == 0) {
                throw new UnityException($"[ERROR] Volume ({name}) requires MeshFilter with a valid mesh.");
            }
            else if (mesh.GetTopology(0) != MeshTopology.Triangles) {
                throw new UnityException($"[ERROR] Volume ({name}) requires a mesh of topology Triangles.");
            }
        }

        private void Start() {
            if (_renderer != null) {
                _renderer.enabled = false;
            }

            var mesh = _meshFilter?.sharedMesh;
            if (mesh != null) {
                ModoiumPlugin.SetVolume(mesh);
            }

            _volumetricEnabled = mesh != null;
        }

        private void Update() {
            if (_volumetricEnabled == false) { return; }

            _cameraSpace = findCameraSpace();

            var position = _cameraSpace != null ? _cameraSpace.InverseTransformPoint(_thisTransform.position) : _thisTransform.position;
            var rotation = _cameraSpace != null ? Quaternion.Inverse(_cameraSpace.rotation) * _thisTransform.rotation : _thisTransform.rotation;

            ModoiumPlugin.UpdateVolumeInfo(position, rotation, _thisTransform.lossyScale);
        }

        private void OnDestroy() {
            if (_volumetricEnabled == false) { return; }

            ModoiumPlugin.ClearVolume();
        }

        private Transform findCameraSpace() {
            if (_cameraSpace != null && _cameraSpace.gameObject.activeInHierarchy) {
                return _cameraSpace;
            }

            if (_camera != null && _camera.gameObject.activeInHierarchy) {
                return _camera.transform.parent;
            }

            var camera = Camera.main;
            if (camera != null && camera.gameObject.activeInHierarchy) {
                return camera.transform.parent;
            }

            return null;
        }
    }
}

#endif
