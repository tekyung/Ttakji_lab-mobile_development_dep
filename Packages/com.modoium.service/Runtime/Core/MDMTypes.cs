using System.Runtime.InteropServices;
using UnityEngine;

namespace Modoium.Service {
    public enum MDMRecordFormat {
        MP4 = 0,
        H264_HEVC,
        Lossless,
        Raw
    }

    public enum MDMCodec {
        H264 = 0x01,
        H265 = 0x02,
        AV1 = 0x04,
        All = 0xFF
    }

    public enum MDMEncodingPreset {
        LowLatency = 1,
        UltraLowLatency = 2
    }

    public enum MDMEncodingQuality {
        VeryLow = 0,
        Low,
        Moderate,
        High,
        VeryHigh
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MDMVector2D {
        public float x;
        public float y;

        public MDMVector2D(Vector2 value) {
            x = value.x;
            y = value.y;
        }

        public Vector3 toVector2() {
            return new Vector2(x, y);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MDMVector3D {
        public float x;
        public float y;
        public float z;

        public MDMVector3D(Vector3 value) {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 toVector3() {
            return new Vector3(x, y, z);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MDMVector4D {
        public float x;
        public float y;
        public float z;
        public float w;

        public MDMVector4D(Quaternion value) {
            x = value.x;
            y = value.y;
            z = value.z;
            w = value.w;
        }

        public Quaternion toQuaternion() {
            return new Quaternion(x, y, z, w);
        }
    }
}
