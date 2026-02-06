using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Modoium.Service {
    internal static class MDMUtils {
        public static Dictionary<string, string> ParseCommandLine(string[] args) {
            if (args == null) { return null; }

            var result = new Dictionary<string, string>();
            foreach (var arg in args) {
                var split = arg.IndexOf("=");
                if (split <= 0) { continue; }

                var name = arg.Substring(0, split);
                var value = arg.Substring(split + 1);
                if (string.IsNullOrEmpty(name)) { continue; }

                result.Add(name, string.IsNullOrEmpty(value) == false ? value : null);
            }
            return result;
        }

        public static int ParseInt(string value, int defaultValue, Func<int, bool> predicate, Action<string> failed = null) {
            int result;
            if (int.TryParse(value, out result) && predicate(result)) {
                return result;
            }

            if (failed != null) {
                failed(value);
            }
            return defaultValue;
        }

        public static float ParseFloat(string value, float defaultValue, Func<float, bool> predicate, Action<string> failed = null) {
            float result;
            if (float.TryParse(value, out result) && predicate(result)) {
                return result;
            }

            if (failed != null) {
                failed(value);
            }
            return defaultValue;
        }

        public static Transform GetChildTransform(Transform xform, string name, bool create = false) {
            return GetChildTransform(xform, name, Vector3.zero, Quaternion.identity, create);
        }

        public static Transform GetChildTransform(Transform xform, string name, Vector3 initialPosition, Quaternion initialRotation, bool create = false) {
            var result = xform.Find(name);
            if (result == null && create) {
                result = new GameObject(name).transform;
                result.parent = xform;
                result.localPosition = initialPosition;
                result.localRotation = initialRotation;
                result.localScale = Vector3.one;
            }
            return result;
        }

        public static T GetComponent<T>(GameObject go, bool create = false) where T : Component {
            var result = go.GetComponent<T>();
            if (result == null && create) {
                result = go.AddComponent<T>();
            }
            return result;
        }

        public static void CopyChildren(Transform src, Transform dest) {
            var count = src.childCount;
            if (count <= 0) { return; }

            for (var i = 0; i < count; i++) {
                var child = src.GetChild(i);
                Assert.IsNotNull(child);

                var copied = UnityEngine.Object.Instantiate(child.gameObject).transform;
                copied.parent = dest;
                copied.localPosition = child.localPosition;
                copied.localRotation = child.localRotation;
                copied.localScale = child.localScale;
            }
        }

        public static void AttachAndResetToOrigin(Transform xform, Transform parent) {
            xform.parent = parent;
            xform.localPosition = Vector3.zero;
            xform.localRotation = Quaternion.identity;
            xform.localScale = Vector3.one;
        }

        public static void ActivateChildren(Transform xform, bool activate) {
            var count = xform.childCount;
            if (count <= 0) { return; }

            for (var i = 0; i < count; i++) {
                var child = xform.GetChild(i);
                Assert.IsNotNull(child);

                child.gameObject.SetActive(activate);
            }
        }
    }
}
