using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Modoium.Service {
    internal class MDMXRConfigurator {
        private MDMService _owner;

        public MDMXRConfigurator(MDMService owner) {
            _owner = owner;
        }

        public void EnsureTrackersConfigured() {
            if (ModoiumPlugin.isXR == false) { return; }

            ensureTrackersConfigured();
        }

#if META_XR_SDK_CORE && XR_LEGACY_INPUT_HELPERS
        private bool _failedToConfigure;
        private UnityEngine.SpatialTracking.TrackedPoseDriver _headTracker;
        private UnityEngine.SpatialTracking.TrackedPoseDriver _leftHandTracker;
        private UnityEngine.SpatialTracking.TrackedPoseDriver _rightHandTracker;

        private void ensureTrackersConfigured() {
            if (_failedToConfigure) { return; }
            else if (_headTracker != null || _leftHandTracker != null || _rightHandTracker != null) { return; }

            var trackers = Object.FindObjectsOfType<UnityEngine.SpatialTracking.TrackedPoseDriver>();
            foreach (var tracker in trackers ?? Enumerable.Empty<UnityEngine.SpatialTracking.TrackedPoseDriver>()) {
                switch (tracker.deviceType) {
                    case UnityEngine.SpatialTracking.TrackedPoseDriver.DeviceType.GenericXRDevice:
                        _headTracker = _headTracker ?? tracker;
                        break;
                    case UnityEngine.SpatialTracking.TrackedPoseDriver.DeviceType.GenericXRController:
                        switch (tracker.poseSource) {
                            case UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.LeftPose:
                                _leftHandTracker = _leftHandTracker ?? tracker;
                                break;
                            case UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.RightPose:
                                _rightHandTracker = _rightHandTracker ?? tracker;
                                break;
                        }
                        break;
                }
            }

            if (_headTracker == null) {
                var camera = Camera.main;
                if (camera != null) {
                    _headTracker = camera.gameObject.AddComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
                    _headTracker.SetPoseSource(
                        UnityEngine.SpatialTracking.TrackedPoseDriver.DeviceType.GenericXRDevice,
                        UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.Center
                    );
                }
                else {
                    _failedToConfigure = true;
                    return;
                }
            }
            if (_leftHandTracker == null && _headTracker != null) {
                var leftHandAnchor = _headTracker.transform.parent?.Find("LeftHandAnchor");
                if (leftHandAnchor != null) {
                    _leftHandTracker = leftHandAnchor.gameObject.AddComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
                    _leftHandTracker.SetPoseSource(
                        UnityEngine.SpatialTracking.TrackedPoseDriver.DeviceType.GenericXRController,
                        UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.LeftPose
                    );
                }
                else {
                    _failedToConfigure = true;
                    return;
                }
            }
            if (_rightHandTracker == null && _headTracker != null) {
                var rightHandAnchor = _headTracker.transform.parent?.Find("RightHandAnchor");
                if (rightHandAnchor != null) {
                    _rightHandTracker = rightHandAnchor.gameObject.AddComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
                    _rightHandTracker.SetPoseSource(
                        UnityEngine.SpatialTracking.TrackedPoseDriver.DeviceType.GenericXRController,
                        UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.RightPose
                    );
                }
                else {
                    _failedToConfigure = true;
                    return;
                }
            }
        }
#else
        private void ensureTrackersConfigured() {}
#endif
    }
}
