#if UNITY_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM

using System;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;

namespace Modoium.Service {
    internal class MDMTouchScreen : MonoBehaviour {
        private const int DeviceFlags_CanRunInBackground = 1 << 11;
        private const int DeviceFlags_CanRunInBackgroundHasBeenQueried = 1 << 12;

        private Touchscreen _touchscreen;
        private MDMInputProvider _inputProvider;
        private ModoiumSettings _settings;

        public void Configure(MDMInputProvider inputProvider) {
            _inputProvider = inputProvider;
            _settings = ModoiumSettings.instance;
        }

        private void OnEnable() {
            ensureTouchscreenDeviceAdded();

            foreach (var device in InputSystem.devices) {
                onDeviceChange(device, InputDeviceChange.Added);
            }

            InputSystem.onDeviceChange += onDeviceChange;
        }

        private void Update() {
            _inputProvider.UpdateInputFrame(_settings.simulateTouchWithMouse);
            enqueueInputEvents();
        }

        private void OnDisable() {
            if (_touchscreen != null && _touchscreen.added) {
                InputSystem.RemoveDevice(_touchscreen);
            }
            
            InputSystem.onDeviceChange -= onDeviceChange;
        }

        private void onDeviceChange(InputDevice device, InputDeviceChange change) {
            if (device == _touchscreen && change == InputDeviceChange.Removed) {
                enabled = false;
            }
        }

        private void ensureTouchscreenDeviceAdded() {
            if (_touchscreen != null) {
                if (_touchscreen.added == false) {
                    InputSystem.AddDevice(_touchscreen);
                }
            }
            else {
                _touchscreen = InputSystem.GetDevice("Modoium Touchscreen") as Touchscreen;
                if (_touchscreen == null) {
                    _touchscreen = InputSystem.AddDevice<Touchscreen>("Modoium Touchscreen");

                    addInputDeviceFlag(_touchscreen, DeviceFlags_CanRunInBackgroundHasBeenQueried | DeviceFlags_CanRunInBackground);
                }
            }
        }

        private void enqueueInputEvents() {
            if (enabled == false || _inputProvider == null || _touchscreen == null) { return; }

            for (var index = 0; index < Mathf.Min(_inputProvider.touchCount, _touchscreen.touches.Count); index++) {
                var touch = _inputProvider.GetTouch(index).Value;

                InputSystem.QueueStateEvent(_touchscreen, new TouchState {
                    touchId = touch.fingerId + 1,
                    phase = parsePhase(touch.phase),
                    position = touch.position,
                    pressure = 1.0f,
                    radius = new Vector2(1.0f, 1.0f)
                });
            }   

            var keyboard = Keyboard.current;
            if (keyboard != null) {
                if (_inputProvider.wasBackPressedThisFrame) {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                }
                if (_inputProvider.wasBackReleasedThisFrame) {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                }
            }
        }

        private void addInputDeviceFlag(InputDevice device, int flags) {
            var inputDeviceType = Type.GetType("UnityEngine.InputSystem.InputDevice,Unity.InputSystem");
            var deviceFlagsField = inputDeviceType.GetField("m_DeviceFlags", BindingFlags.NonPublic | BindingFlags.Instance);

            var deviceFlags = (int)deviceFlagsField.GetValue(device);
            deviceFlags |= flags;
            deviceFlagsField.SetValue(device, deviceFlags);
        }

        private UnityEngine.InputSystem.TouchPhase parsePhase(UnityEngine.TouchPhase phase) {
            return phase switch {
                UnityEngine.TouchPhase.Began => UnityEngine.InputSystem.TouchPhase.Began,
                UnityEngine.TouchPhase.Moved => UnityEngine.InputSystem.TouchPhase.Moved,
                UnityEngine.TouchPhase.Stationary => UnityEngine.InputSystem.TouchPhase.Stationary,
                UnityEngine.TouchPhase.Ended => UnityEngine.InputSystem.TouchPhase.Ended,
                UnityEngine.TouchPhase.Canceled => UnityEngine.InputSystem.TouchPhase.Canceled,
                _ => throw new NotImplementedException()
            };
        } 
    }
}

#endif
