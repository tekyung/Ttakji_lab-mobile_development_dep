#if UNITY_INPUT_SYSTEM && UNITY_XR_MANAGEMENT

using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem;
using UnityEditor;

namespace Modoium.Service {
    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    internal static class MDMInputLayoutLoader {
        static MDMInputLayoutLoader() {
            RegisterInputLayouts();
        }

        public static void RegisterInputLayouts() {
            InputSystem.RegisterLayout<MDMInputDeviceLayoutHMD>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct("^(Modoium Main Device)")
            );
            InputSystem.RegisterLayout<MDMInputDeviceLayoutXRController>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct("^(Modoium XR Controller)")
            );
        }
    }


    [Preserve][InputControlLayout(displayName = "Modoium XR HMD")]
    internal class MDMInputDeviceLayoutHMD : XRHMD {
        [Preserve][InputControl] public new Vector3Control devicePosition { get; private set; }
        [Preserve][InputControl] public new QuaternionControl deviceRotation { get; private set; }
        [Preserve][InputControl] public new Vector3Control centerEyePosition { get; private set; }
        [Preserve][InputControl] public new QuaternionControl centerEyeRotation { get; private set; }
        [Preserve][InputControl] public AxisControl batteryLevel { get; private set; }

        protected override void FinishSetup() {
            base.FinishSetup();

            devicePosition = GetChildControl<Vector3Control>("devicePosition");
            deviceRotation = GetChildControl<QuaternionControl>("deviceRotation");
            centerEyePosition = GetChildControl<Vector3Control>("centerEyePosition");
            centerEyeRotation = GetChildControl<QuaternionControl>("centerEyeRotation");
            batteryLevel = GetChildControl<AxisControl>("batteryLevel");
        }
    }

    [Preserve][InputControlLayout(displayName = "Modoium XR Controller", commonUsages = new[] { "LeftHand", "RightHand" })]
    internal class MDMInputDeviceLayoutXRController : XRControllerWithRumble {
        [Preserve][InputControl(aliases = new[] { "Primary2DAxis", "Joystick" })] public Vector2Control thumbstick { get; private set; }
        [Preserve][InputControl] public AxisControl trigger { get; private set; }
        [Preserve][InputControl] public AxisControl grip { get; private set; }
        [Preserve][InputControl(aliases = new[] { "A", "X" })] public ButtonControl primaryButton { get; private set; }
        [Preserve][InputControl(aliases = new[] { "B", "Y" })] public ButtonControl secondaryButton { get; private set; }
        [Preserve][InputControl(aliases = new[] { "GripButton" })] public ButtonControl gripPressed { get; private set; }
        [Preserve][InputControl(aliases = new[] { "MenuButton" })] public ButtonControl start { get; private set; }
        [Preserve][InputControl(aliases = new[] { "Primary2DAxisClick", "JoystickOrPadPressed", "thumbstickClick" })] public ButtonControl thumbstickClicked { get; private set; }
        [Preserve][InputControl(aliases = new[] { "Primary2DAxisTouch", "JoystickOrPadTouched", "thumbstickTouch" })] public ButtonControl thumbstickTouched { get; private set; }
        [Preserve][InputControl(aliases = new[] { "PrimaryTouch", "ATouched", "XTouched", "ATouch", "XTouch" })] public ButtonControl primaryTouched { get; private set; }
        [Preserve][InputControl(aliases = new[] { "SecondaryTouch", "BTouched", "YTouched", "BTouch", "YTouch" })] public ButtonControl secondaryTouched { get; private set; }
        [Preserve][InputControl(aliases = new[] { "TriggerButton", "indexButton" })] public ButtonControl triggerPressed { get; private set; }
        [Preserve][InputControl(aliases = new[] { "triggerTouch", "indexTouch" })] public AxisControl triggerTouched { get; private set; }
        [Preserve][InputControl(aliases = new[] { "controllerPosition" })] public new Vector3Control devicePosition { get; private set; }
        [Preserve][InputControl(aliases = new[] { "controllerRotation" })] public new QuaternionControl deviceRotation { get; private set; }
        [Preserve][InputControl] public AxisControl batteryLevel { get; private set; }

        protected override void FinishSetup() {
            base.FinishSetup();

            thumbstick = GetChildControl<Vector2Control>("thumbstick");
            trigger = GetChildControl<AxisControl>("trigger");
            grip = GetChildControl<AxisControl>("grip");
            primaryButton = GetChildControl<ButtonControl>("primaryButton");
            secondaryButton = GetChildControl<ButtonControl>("secondaryButton");
            gripPressed = GetChildControl<ButtonControl>("gripPressed");
            start = GetChildControl<ButtonControl>("menuButton");
            thumbstickClicked = GetChildControl<ButtonControl>("primary2DAxisClick");
            thumbstickTouched = GetChildControl<ButtonControl>("primary2DAxisTouch");
            primaryTouched = GetChildControl<ButtonControl>("primaryTouch");
            secondaryTouched = GetChildControl<ButtonControl>("secondaryTouch");
            triggerPressed = GetChildControl<ButtonControl>("triggerPressed");
            triggerTouched = GetChildControl<AxisControl>("triggerTouch");
            devicePosition = GetChildControl<Vector3Control>("devicePosition");
            deviceRotation = GetChildControl<QuaternionControl>("deviceRotation");
            batteryLevel = GetChildControl<AxisControl>("batteryLevel");
        }
    }
}

#endif
