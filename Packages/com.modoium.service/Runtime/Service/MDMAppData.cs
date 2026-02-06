using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Modoium.Service {
    public enum MDMScreenRotation {
        Unspecified = -1,
        Portrait = 0,
        LandscapeLeft = 1,
        PortraitUpsideDown = 2,
        LandscapeRight = 3
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MDMAppData {
        [JsonProperty] private MDMMediaDesc[] medias;

        public MDMVideoDesc videoDesc {
            get {
                foreach (var iter in medias) {
                    if (iter is MDMVideoDesc videoDesc) {
                        return videoDesc;
                    }
                }
                return null;
            }
        }

        public MDMAudioDesc audioDesc {
            get {
                foreach (var iter in medias) {
                    if (iter is MDMAudioDesc audioDesc) {
                        return audioDesc;
                    }
                }
                return null;
            }
        }

        public MDMInputDesc inputDesc {
            get {
                foreach (var iter in medias) {
                    if (iter is MDMInputDesc inputDesc) {
                        return inputDesc;
                    }
                }
                return null;
            }
        }

        public MDMAppData(MDMVideoDesc videoDesc, MDMInputDesc inputDesc) {
            medias = new MDMMediaDesc[] {
                videoDesc,
                new MDMAudioDesc(),
                inputDesc
            };
        }

        public MDMAppData(object obj) {
            Debug.Assert(obj is JObject);
            var dict = obj as JObject;

            Debug.Assert(dict.ContainsKey("medias"));
            medias = dict.Value<JArray>("medias").Select((iter) => MDMMediaDesc.Parse(iter)).Where((iter) => iter != null).ToArray();
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MDMMediaDesc {
        internal static MDMMediaDesc Parse(object obj) {
            if (obj is JObject == false) { return null; }

            var dict = obj as JObject;
            if (dict.ContainsKey("type") == false || dict.ContainsKey("accept") == false) { return null; }

            switch (dict.Value<string>("type")) {
                case "video":
                    return MDMVideoDesc.Parse(obj);
                case "audio":
                    return new MDMAudioDesc(obj);
                case "application":
                    return MDMApplicationDesc.Parse(obj);
                default:
                    return new MDMMediaDesc(obj);
            }
        }

        [JsonProperty] private string type;
        [JsonProperty] protected string[] accept;

        internal MDMMediaDesc(string type, string[] accept) {
            this.type = type;
            this.accept = accept;
        }

        internal MDMMediaDesc(object obj) {
            Debug.Assert(obj is JObject);
            var dict = obj as JObject;

            type = dict.Value<string>("type");
            Debug.Assert(string.IsNullOrEmpty(type) == false);

            accept = dict.Value<JArray>("accept").Select((iter) => iter.ToObject<string>()).ToArray();
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MDMVideoDesc : MDMMediaDesc {
        internal static new MDMVideoDesc Parse(object obj) {
            var dict = obj as JObject;
            if (dict.ContainsKey("stereoscopy")) {
                return new MDMStereoVideoDesc(obj);
            }
            else if (dict.ContainsKey("monoscopy")) {
                return new MDMMonoVideoDesc(obj);
            }
            return null;
        }

        [JsonProperty] private ImageAttr[] imageattr;
        [JsonProperty("max-width")] private int maxWidth;
        [JsonProperty("max-height")] private int maxHeight;
        [JsonProperty("max-fps")] private float maxFps;
        [JsonProperty("max-br")] private long maxBitrate;
        [JsonProperty] private string direction;
        [JsonProperty] private Xfmtp xfmtp;

        internal int viewWidth => imageattr != null && imageattr.Length > 0 ? imageattr[0].x : maxWidth;
        internal int viewHeight => imageattr != null && imageattr.Length > 0 ? imageattr[0].y : maxHeight;
        internal int videoWidth => maxWidth;
        internal int videoHeight => maxHeight;
        internal float framerate => maxFps;
        internal long bitrate => maxBitrate;
        internal string[] codecs => accept;
        internal bool useMPEG4BitstreamFormat => xfmtp.useSizePrefix;

        internal MDMVideoDesc(string[] codecs, 
                              int contentWidth, 
                              int contentHeight, 
                              int videoWidth,
                              int videoHeight,
                              float framerate,
                              long maxBitrate) : base("video", codecs) { 
            imageattr = new ImageAttr[] {
                new ImageAttr { x = contentWidth, y = contentHeight }
            };
            maxWidth = videoWidth;
            maxHeight = videoHeight;
            maxFps = framerate;
            this.maxBitrate = maxBitrate;
            direction = "recvonly";
            xfmtp = new Xfmtp { useSizePrefix = false };
        }

        internal MDMVideoDesc(object obj) : base(obj) {
            Debug.Assert(obj is JObject);
            var dict = obj as JObject;

            if (dict.ContainsKey("imageattr")) {
                imageattr = dict.Value<JArray>("imageattr").Select((iter) => { 
                    return new ImageAttr { 
                        x = iter.Value<int>("x"), 
                        y = iter.Value<int>("y")
                    }; 
                }).ToArray();
            }
            else {
                imageattr = null;
            }

            Debug.Assert(dict.ContainsKey("max-width"));
            maxWidth = dict.Value<int>("max-width");

            Debug.Assert(dict.ContainsKey("max-height"));
            maxHeight = dict.Value<int>("max-height");

            Debug.Assert(dict.ContainsKey("max-fps"));
            maxFps = dict.Value<float>("max-fps");

            Debug.Assert(dict.ContainsKey("max-br"));
            maxBitrate = dict.Value<long>("max-br");

            Debug.Assert(dict.ContainsKey("direction"));
            direction = dict.Value<string>("direction");

            Debug.Assert(dict.ContainsKey("xfmtp"));
            var xfmtp = dict.Value<JObject>("xfmtp");
            this.xfmtp = new Xfmtp { 
                useSizePrefix = xfmtp.Value<bool>("useSizePrefix")
            };
        }

        [JsonObject(MemberSerialization.OptIn)]
        private struct ImageAttr {
            [JsonProperty] public int x;
            [JsonProperty] public int y;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private struct Xfmtp {
            [JsonProperty] public bool useSizePrefix;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MDMStereoVideoDesc : MDMVideoDesc {
        [JsonProperty] private Stereoscopy stereoscopy;

        public MDMStereoVideoDesc(string[] codecs, 
                                  int width, 
                                  int height, 
                                  float framerate,
                                  long maxBitrate,
                                  Vector4 leftEyeProjection,
                                  float ipd) : base(codecs, width, height, width, height, framerate, maxBitrate) { 
            stereoscopy = new Stereoscopy { 
                leftEyeProjection = new float[] { leftEyeProjection.x, leftEyeProjection.y, leftEyeProjection.z, leftEyeProjection.w },
                ipd = ipd
            };
        }

        public MDMStereoVideoDesc(object obj) : base(obj) {
            Debug.Assert(obj is JObject);
            var dict = obj as JObject;

            Debug.Assert(dict.ContainsKey("stereoscopy"));
            var stereoscopy = dict.Value<JObject>("stereoscopy");
            this.stereoscopy = new Stereoscopy { 
                leftEyeProjection = stereoscopy.Value<JArray>("leftEyeProjection").Select((iter) => iter.ToObject<float>()).ToArray(),
                ipd = stereoscopy.Value<float>("ipd")
            };
        }

        [JsonObject(MemberSerialization.OptIn)]
        private struct Stereoscopy {
            [JsonProperty] public float[] leftEyeProjection;
            [JsonProperty] public float ipd;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MDMMonoVideoDesc : MDMVideoDesc {
        [JsonProperty] private Monoscopy monoscopy;

        public MDMMonoVideoDesc(string[] codecs, 
                                int contentWidth,
                                int contentHeight,
                                int videoWidth, 
                                int videoHeight, 
                                float sampleAspect,
                                float framerate,
                                long maxBitrate,
                                Vector4 cameraProjection) : base(codecs, contentWidth, contentHeight, videoWidth, videoHeight, framerate, maxBitrate) {
            monoscopy = new Monoscopy {
                cameraProjection = new float[] { cameraProjection.x, cameraProjection.y, cameraProjection.z, cameraProjection.w }
            };
        }

        public MDMMonoVideoDesc(object obj) : base(obj) {
            Debug.Assert(obj is JObject);
            var dict = obj as JObject;

            Debug.Assert(dict.ContainsKey("monoscopy"));
            var monoscopy = dict.Value<JObject>("monoscopy");
            this.monoscopy = new Monoscopy { 
                cameraProjection = monoscopy.Value<JArray>("cameraProjection").Select((iter) => iter.ToObject<float>()).ToArray()
            };
        }

        [JsonObject(MemberSerialization.OptIn)]
        private struct Monoscopy {
            [JsonProperty] public float[] cameraProjection;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MDMAudioDesc : MDMMediaDesc {
        public string[] codecs => accept;

        public MDMAudioDesc() : base("audio", new string[] { "opus" }) {}
        public MDMAudioDesc(object obj) : base(obj) {}
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MDMApplicationDesc : MDMMediaDesc {
        internal static new MDMApplicationDesc Parse(object obj) {
            var dict = obj as JObject;
        
            if (dict.Value<JArray>("accept").Any((iter) => iter.ToObject<string>() == "onairxr-input")) {
                return new MDMInputDesc(obj);
            }
            return new MDMApplicationDesc(obj);
        }

        internal MDMApplicationDesc(string[] accept) : base("application", accept) {}
        internal MDMApplicationDesc(object obj) : base(obj) {}
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MDMInputDesc : MDMApplicationDesc {
        [JsonProperty] public int screenWidth;
        [JsonProperty] public int screenHeight;
        [JsonProperty("screenRotation")] public int screenRotationRaw;

        public MDMScreenRotation screenRotation => screenRotationRaw switch {
            0 => MDMScreenRotation.Portrait,
            1 => MDMScreenRotation.LandscapeLeft,
            2 => MDMScreenRotation.PortraitUpsideDown,
            3 => MDMScreenRotation.LandscapeRight,
            _ => MDMScreenRotation.Unspecified
        };

        public MDMInputDesc(int screenWidth, int screenHeight) : base(new string[] { "onairxr-input" }) {
            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;
        }

        public MDMInputDesc(object obj) : base(obj) {
            Debug.Assert(obj is JObject);
            var dict = obj as JObject;

            Debug.Assert(dict.ContainsKey("screenWidth"));
            screenWidth = dict.Value<int>("screenWidth");

            Debug.Assert(dict.ContainsKey("screenHeight"));
            screenHeight = dict.Value<int>("screenHeight");

            Debug.Assert(dict.ContainsKey("screenRotation"));
            screenRotationRaw = dict.Value<int>("screenRotation");
        }
    }
}
