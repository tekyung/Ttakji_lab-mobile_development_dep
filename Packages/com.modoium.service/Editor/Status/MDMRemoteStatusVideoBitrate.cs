using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Modoium.Service.Editor {
    internal class MDMRemoteStatusVideoBitrate : VisualElement {
        private const float MinBitrate = 10f;
        private const float MaxBitrate = 200f;

        private TextElement _bodyReadonly;
        private MDMRemoteStatusSlider _slider;

        public MDMRemoteStatusVideoBitrate(VisualElement parent) {
            Add(createBodyReadonly());
            Add(createSlider());

            parent.Add(this);
        }

        public void UpdateView(float bitrate, bool readOnly) {
            _bodyReadonly.text = string.Format(Styles.bodyReadonly, Mathf.RoundToInt(bitrate));
            _bodyReadonly.style.display = readOnly ? DisplayStyle.Flex : DisplayStyle.None;

            _slider.SetValue(bitrate);
            _slider.style.display = readOnly ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement createBodyReadonly() {
            _bodyReadonly = new TextElement { text = string.Format(Styles.bodyReadonly, 0) };
            return _bodyReadonly;
        }

        private VisualElement createSlider() {
            _slider = new MDMRemoteStatusSlider(this, 
                                                Styles.sliderLabel, 
                                                Styles.sliderUnit, 
                                                MinBitrate, 
                                                MaxBitrate, 
                                                ModoiumPlugin.videoBitrate / 1000000f,
                                                onValueChanged);
            _slider.style.display = DisplayStyle.None;
            return _slider;
        }

        private void onValueChanged(float value) {
            const long ChangeThreshold = 100000;

            var bps = value * 1000000;
            if (Mathf.Abs(ModoiumPlugin.videoBitrate - bps) < ChangeThreshold) { return; }

            ModoiumPlugin.videoBitrate = (long)bps;
        }
        
        private class Styles {
            public static string bodyReadonly = "Video Bitrate : {0} Mbps";
            public static string sliderLabel = "Video Bitrate";
            public static string sliderUnit = "Mbps";
        }
    }

    internal class MDMRemoteStatusSlider : VisualElement {
        private string _unit;
        private Slider _slider;
        private TextElement _value;
        private Action<float> _onValueChanged;

        public MDMRemoteStatusSlider(VisualElement parent, 
                                     string label, 
                                     string unit, 
                                     float min, 
                                     float max, 
                                     float initialValue,
                                     Action<float> onValueChanged) {
            _unit = unit;
            _onValueChanged = onValueChanged;

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center; 

            Add(createLabel(label));
            Add(createSlider(min, max, initialValue));
            Add(createValue());

            parent.Add(this);
        }

        public void SetValue(float value) {
            _slider.value = value;
            _value.text = makeValueText(value);
        }

        private VisualElement createLabel(string label) {
            var element = new TextElement { text = label };
            element.style.flexShrink = 0;
            element.style.minWidth = 84;
            return element;
        }

        private VisualElement createSlider(float min, float max, float value) {
            _slider = new Slider();
            _slider.style.flexGrow = 1;
            _slider.style.flexShrink = 1;
            _slider.lowValue = min;
            _slider.highValue = max;
            _slider.value = value;
            _slider.RegisterValueChangedCallback(onValueChange);

            return _slider;
        }

        private VisualElement createValue() {
            _value = new TextElement { text = makeValueText(_slider.value) };
            _value.style.flexShrink = 0;
            _value.style.minWidth = 74;
            _value.style.unityTextAlign = TextAnchor.MiddleRight;
            return _value;
        }

        private void onValueChange(ChangeEvent<float> evt) {
            _value.text = makeValueText(evt.newValue);

            _onValueChanged?.Invoke(evt.newValue);
        }

        private string makeValueText(float value) => $"{Mathf.RoundToInt(value)} {_unit}";
    }
}
