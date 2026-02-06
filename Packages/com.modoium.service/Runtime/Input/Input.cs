#if ENABLE_LEGACY_INPUT_MANAGER

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modoium.Service {
    public static class Input {
        // touch
        public static bool touchSupported {
            get {
                if (MDMInput.instance == null) { return UnityEngine.Input.touchSupported; }

                return MDMInput.instance.touchSupported;
            }
        }

        public static bool touchPressureSupported {
            get {
                if (MDMInput.instance == null) { return UnityEngine.Input.touchPressureSupported; }

                return MDMInput.instance.touchPressureSupported;
            }
        }

        public static bool multiTouchEnabled {
            get {
                if (MDMInput.instance == null) { return UnityEngine.Input.multiTouchEnabled; }

                return MDMInput.instance.touchSupported;
            }
        }

        public static int touchCount {
            get {
                if (MDMInput.instance == null) { return UnityEngine.Input.touchCount; }

                return MDMInput.instance.touchCount;
            }
        }

        public static bool simulateMouseWithTouches { get; set; } // TODO: implement

        public static Touch GetTouch(int index) {
            if (MDMInput.instance == null) { return UnityEngine.Input.GetTouch(index); }

            return MDMInput.instance.GetTouch(index);
        }

        // mouse
        public static Vector3 mousePosition => UnityEngine.Input.mousePosition;
        public static bool mousePresent => UnityEngine.Input.mousePresent;
        public static Vector2 mouseScrollDelta => UnityEngine.Input.mouseScrollDelta;

        public static bool GetMouseButton(int button) => UnityEngine.Input.GetMouseButton(button);
        public static bool GetMouseButtonUp(int button) => UnityEngine.Input.GetMouseButtonUp(button);
        public static bool GetMouseButtonDown(int button) => UnityEngine.Input.GetMouseButtonDown(button);

        // pen
        public static bool stylusTouchSupported => UnityEngine.Input.stylusTouchSupported;


        // keyboard
        public static bool anyKey => UnityEngine.Input.anyKey;
        public static bool anyKeyDown => UnityEngine.Input.anyKeyDown;
        public static string inputString => UnityEngine.Input.inputString;

        // acceleration
        public static Vector3 acceleration => UnityEngine.Input.acceleration;
        public static int accelerationEventCount => UnityEngine.Input.accelerationEventCount;
        public static AccelerationEvent[] accelerationEvents => UnityEngine.Input.accelerationEvents;

        public static AccelerationEvent GetAccelerationEvent(int index) => UnityEngine.Input.GetAccelerationEvent(index);

        // compass
        public static Compass compass => UnityEngine.Input.compass;

        // gyro
        public static Gyroscope gyro => UnityEngine.Input.gyro;

        // platform
        public static bool backButtonLeavesApp => UnityEngine.Input.backButtonLeavesApp;
        public static bool compensateSensors => UnityEngine.Input.compensateSensors;
        public static DeviceOrientation deviceOrientation => UnityEngine.Input.deviceOrientation;

        // IME
        public static Vector2 compositionCursorPos => UnityEngine.Input.compositionCursorPos;
        public static string compositionString => UnityEngine.Input.compositionString;
        public static IMECompositionMode imeCompositionMode => UnityEngine.Input.imeCompositionMode;
        public static bool imeIsSelected => UnityEngine.Input.imeIsSelected;

        // location
        public static LocationService location => UnityEngine.Input.location;

        // input manager
        public static bool GetKey(KeyCode key) {
            var unityInput = UnityEngine.Input.GetKey(key);
            if (MDMInput.instance == null) { return unityInput; }

            switch (key) {
                case KeyCode.Escape:
                    return unityInput || MDMInput.instance.isBackPressed;
                default:
                    return unityInput;
            }
        } 

        public static bool GetKeyDown(KeyCode key) {
            var unityInput = UnityEngine.Input.GetKeyDown(key);
            if (MDMInput.instance == null) { return unityInput; }

            switch (key) {
                case KeyCode.Escape:
                    return unityInput || MDMInput.instance.wasBackPressedThisFrame;
                default:
                    return unityInput;
            }
        }

        public static bool GetKeyUp(KeyCode key) {
            var unityInput = UnityEngine.Input.GetKeyUp(key);
            if (MDMInput.instance == null) { return unityInput; }

            switch (key) {
                case KeyCode.Escape:
                    return unityInput || MDMInput.instance.wasBackReleasedThisFrame;
                default:
                    return unityInput;
            }
        }

        public static float GetAxis(string axisName) => UnityEngine.Input.GetAxis(axisName);
        public static float GetAxisRaw(string axisName) => UnityEngine.Input.GetAxisRaw(axisName);
        public static bool GetButton(string buttonName) => UnityEngine.Input.GetButton(buttonName);
        public static bool GetButtonDown(string buttonName) => UnityEngine.Input.GetButtonDown(buttonName);
        public static bool GetButtonUp(string buttonName) => UnityEngine.Input.GetButtonUp(buttonName);
        public static string[] GetJoystickNames() => UnityEngine.Input.GetJoystickNames();
        public static bool GetKey(string name) => UnityEngine.Input.GetKey(name);
        public static bool GetKeyDown(string name) => UnityEngine.Input.GetKeyDown(name);
        public static bool GetKeyUp(string name) => UnityEngine.Input.GetKeyUp(name);
        public static void ResetInputAxes() => UnityEngine.Input.ResetInputAxes();
    }
}

#endif
