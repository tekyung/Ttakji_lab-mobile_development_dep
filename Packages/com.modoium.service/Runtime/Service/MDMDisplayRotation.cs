using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modoium.Service {
    internal class MDMDisplayRotation {
        private MDMService _owner;

        public MDMDisplayRotation(MDMService owner) {
            _owner = owner;
        }

        public (int width, int height) CalcTargetContentSize(MDMVideoDesc remoteViewDesc) {
            var rotation = evalTargetContentRotation(remoteViewDesc.viewWidth, remoteViewDesc.viewHeight);

            return rotation == MDMScreenRotation.LandscapeLeft ||
                   rotation == MDMScreenRotation.LandscapeRight ?
                (Mathf.Max(remoteViewDesc.viewWidth, remoteViewDesc.viewHeight),
                    Mathf.Min(remoteViewDesc.viewWidth, remoteViewDesc.viewHeight)) :
                (Mathf.Min(remoteViewDesc.viewWidth, remoteViewDesc.viewHeight),
                    Mathf.Max(remoteViewDesc.viewWidth, remoteViewDesc.viewHeight));
        }

        public (Matrix4x4 rotation, float aspect) EvalFramebufferBlitTransform((int width, int height) contentSize, 
                                                                               (int width, int height) remoteViewSize,
                                                                               MDMScreenRotation remoteViewRotation,
                                                                               (int width, int height) framebufferSize) {
            if (remoteViewRotation == MDMScreenRotation.Unspecified) {
                return (Matrix4x4.identity, 0);
            }

            var contentRotation = evalTargetContentRotation(contentSize.width, 
                                                            contentSize.height,
                                                            remoteViewRotation);
            var contentAspect = (float)contentSize.width / contentSize.height;

            if (contentRotation == remoteViewRotation &&
                contentSize.width == framebufferSize.width && 
                contentSize.height == framebufferSize.height) {
                return (Matrix4x4.identity, 0);
            }

            switch (remoteViewRotation) {
                case MDMScreenRotation.Portrait:
                    switch (contentRotation) {
                        case MDMScreenRotation.Portrait:
                            return (Matrix4x4.identity, contentAspect);
                        case MDMScreenRotation.LandscapeLeft:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, -90)), 1 / contentAspect);
                        case MDMScreenRotation.PortraitUpsideDown:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, 180)), contentAspect);
                        case MDMScreenRotation.LandscapeRight:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)), 1 / contentAspect);
                    }
                    break;
                case MDMScreenRotation.LandscapeLeft:
                    switch (contentRotation) {
                        case MDMScreenRotation.Portrait:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)), 1 / contentAspect);
                        case MDMScreenRotation.LandscapeLeft:
                            return (Matrix4x4.identity, contentAspect);
                        case MDMScreenRotation.PortraitUpsideDown:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, -90)), 1 / contentAspect);
                        case MDMScreenRotation.LandscapeRight:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, 180)), contentAspect);
                    }
                    break;
                case MDMScreenRotation.PortraitUpsideDown:
                    switch (contentRotation) {
                        case MDMScreenRotation.Portrait:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, 180)), contentAspect);
                        case MDMScreenRotation.LandscapeLeft:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)), 1 / contentAspect);
                        case MDMScreenRotation.PortraitUpsideDown:
                            return (Matrix4x4.identity, contentAspect);
                        case MDMScreenRotation.LandscapeRight:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, -90)), 1 / contentAspect);
                    }
                    break;
                case MDMScreenRotation.LandscapeRight:
                    switch (contentRotation) {
                        case MDMScreenRotation.Portrait:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, -90)), 1 / contentAspect);
                        case MDMScreenRotation.LandscapeLeft:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, 180)), contentAspect);
                        case MDMScreenRotation.PortraitUpsideDown:
                            return (Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)), 1 / contentAspect);
                        case MDMScreenRotation.LandscapeRight:
                            return (Matrix4x4.identity, contentAspect);
                    }
                    break;
            }
            return (Matrix4x4.identity, 0);
        }

        public Vector2 TranslateTouchInputPos(Vector2 pos,
                                              (int width, int height) contentSize, 
                                              MDMInputDesc remoteInputDesc) {            
            if (remoteInputDesc == null) { return pos; }

            var remoteViewSize = (remoteInputDesc.screenWidth, remoteInputDesc.screenHeight);
            if (remoteInputDesc.screenRotation == MDMScreenRotation.Unspecified) {
                return scaleTouchInputPos(pos, contentSize, remoteViewSize);
            }

            var remoteViewRotation = remoteInputDesc.screenRotation;
            var contentRotation = evalTargetContentRotation(contentSize.width, 
                                                            contentSize.height,
                                                            remoteViewRotation);

            switch (remoteViewRotation) {
                case MDMScreenRotation.Portrait:
                    switch (contentRotation) {
                        case MDMScreenRotation.Portrait:
                            return scaleTouchInputPos(pos, contentSize, remoteViewSize);
                        case MDMScreenRotation.LandscapeLeft:
                            return scaleTouchInputPos(new Vector2(remoteViewSize.screenHeight - pos.y, pos.x),
                                                      contentSize, 
                                                      (remoteViewSize.screenHeight, remoteViewSize.screenWidth));
                        case MDMScreenRotation.PortraitUpsideDown:
                            return scaleTouchInputPos(new Vector2(remoteViewSize.screenWidth - pos.x, remoteViewSize.screenHeight - pos.y),
                                                      contentSize, 
                                                      remoteViewSize);
                        case MDMScreenRotation.LandscapeRight:
                            return scaleTouchInputPos(new Vector2(pos.y, remoteViewSize.screenWidth - pos.x),
                                                      contentSize, 
                                                      (remoteViewSize.screenHeight, remoteViewSize.screenWidth));
                    }
                    break;
                case MDMScreenRotation.LandscapeLeft:
                    switch (contentRotation) {
                        case MDMScreenRotation.Portrait:
                            return scaleTouchInputPos(new Vector2(pos.y, remoteViewSize.screenWidth - pos.x),
                                                      contentSize, 
                                                      (remoteViewSize.screenHeight, remoteViewSize.screenWidth));
                        case MDMScreenRotation.LandscapeLeft:
                            return scaleTouchInputPos(pos, contentSize, remoteViewSize);
                        case MDMScreenRotation.PortraitUpsideDown:
                            return scaleTouchInputPos(new Vector2(remoteViewSize.screenHeight - pos.y, pos.x),
                                                      contentSize, 
                                                      (remoteViewSize.screenHeight, remoteViewSize.screenWidth));
                        case MDMScreenRotation.LandscapeRight:
                            return scaleTouchInputPos(new Vector2(remoteViewSize.screenWidth - pos.x, remoteViewSize.screenHeight - pos.y),
                                                      contentSize, 
                                                      remoteViewSize);
                    }
                    break;
                case MDMScreenRotation.PortraitUpsideDown:
                    switch (contentRotation) {
                        case MDMScreenRotation.Portrait:
                            return scaleTouchInputPos(new Vector2(remoteViewSize.screenWidth - pos.x, remoteViewSize.screenHeight - pos.y),
                                                      contentSize, 
                                                      remoteViewSize);
                        case MDMScreenRotation.LandscapeLeft:
                            return scaleTouchInputPos(new Vector2(pos.y, remoteViewSize.screenWidth - pos.x),
                                                      contentSize, 
                                                      (remoteViewSize.screenHeight, remoteViewSize.screenWidth));
                        case MDMScreenRotation.PortraitUpsideDown:
                            return scaleTouchInputPos(pos, contentSize, remoteViewSize);
                        case MDMScreenRotation.LandscapeRight:
                            return scaleTouchInputPos(new Vector2(remoteViewSize.screenHeight - pos.y, pos.x),
                                                      contentSize, 
                                                      (remoteViewSize.screenHeight, remoteViewSize.screenWidth));
                    }
                    break;
                case MDMScreenRotation.LandscapeRight:
                    switch (contentRotation) {
                        case MDMScreenRotation.Portrait:
                            return scaleTouchInputPos(new Vector2(remoteViewSize.screenHeight - pos.y, pos.x),
                                                      contentSize, 
                                                      (remoteViewSize.screenHeight, remoteViewSize.screenWidth));
                        case MDMScreenRotation.LandscapeLeft:
                            return scaleTouchInputPos(new Vector2(remoteViewSize.screenWidth - pos.x, remoteViewSize.screenHeight - pos.y),
                                                      contentSize, 
                                                      remoteViewSize);
                        case MDMScreenRotation.PortraitUpsideDown:
                            return scaleTouchInputPos(new Vector2(pos.y, remoteViewSize.screenWidth - pos.x),
                                                      contentSize, 
                                                      (remoteViewSize.screenHeight, remoteViewSize.screenWidth));
                        case MDMScreenRotation.LandscapeRight:
                            return scaleTouchInputPos(pos, contentSize, remoteViewSize);
                    }
                    break;
            }
            return scaleTouchInputPos(pos, contentSize, remoteViewSize);
        }

        private Vector2 scaleTouchInputPos(Vector2 pos,
                                           (int width, int height) contentSize,
                                           (int width, int height) remoteViewSize) {
            var contentAspect = (float)contentSize.width / contentSize.height;
            var remoteAspect = (float)remoteViewSize.width / remoteViewSize.height;

            if (contentAspect >= remoteAspect) {
                var scaleFromRemoteToContent = (float)contentSize.width / remoteViewSize.width;
                var x = pos.x * scaleFromRemoteToContent;
                var y = pos.y * scaleFromRemoteToContent;

                var offsetY = (contentSize.height - remoteViewSize.height * scaleFromRemoteToContent) / 2.0f;
                return new Vector2(x, y + offsetY);
            }
            else {
                var scaleFromRemoteToContent = (float)contentSize.height / remoteViewSize.height;
                var x = pos.x * scaleFromRemoteToContent;
                var y = pos.y * scaleFromRemoteToContent;

                var offsetX = (contentSize.width - remoteViewSize.width * scaleFromRemoteToContent) / 2.0f;
                return new Vector2(x + offsetX, y);
            }
        }

        private MDMScreenRotation evalTargetContentRotation(int viewWidth, 
                                                            int viewHeight, 
                                                            MDMScreenRotation viewRotation = MDMScreenRotation.Unspecified) {
            if (_owner.isAppMobilePlatform == false) {
                return rotationFromDimension(viewWidth, viewHeight, viewRotation);
            }

            switch (_owner.appOrientation) {
                case ScreenOrientation.Portrait:
                    return MDMScreenRotation.Portrait;
                case ScreenOrientation.LandscapeLeft:
                    return MDMScreenRotation.LandscapeLeft;
                case ScreenOrientation.PortraitUpsideDown:
                    return MDMScreenRotation.PortraitUpsideDown;
                case ScreenOrientation.LandscapeRight:
                    return MDMScreenRotation.LandscapeRight;
                default:
                    return viewRotation != MDMScreenRotation.Unspecified ?
                        viewRotation : rotationFromDimension(viewWidth, viewHeight);
            }
        }

        private MDMScreenRotation rotationFromDimension(int width, int height, MDMScreenRotation rotationHint = MDMScreenRotation.Unspecified) {
            return width > height ? 
                (rotationHint == MDMScreenRotation.LandscapeRight ? MDMScreenRotation.LandscapeRight : MDMScreenRotation.LandscapeLeft) : 
                (rotationHint == MDMScreenRotation.PortraitUpsideDown ? MDMScreenRotation.PortraitUpsideDown : MDMScreenRotation.Portrait);
        }

        private enum Orientation {
            Portrait,
            Landscape
        }
    }
}
