﻿using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public interface IKn5RenderableObject : IRenderableObject {
        [NotNull]
        Kn5Node OriginalNode { get; }

        Matrix ModelMatrixInverted { set; }

        bool IsInitialized { get; }

        void SetMirrorMode(IDeviceContextHolder holder, bool enabled);

        void SetDebugMode(IDeviceContextHolder holder, bool enabled);

        void SetEmissive(Vector3? color);

        int TrianglesCount { get; }

        void SetTransparent(bool? isTransparent);
    }

    public sealed class Kn5RenderableObject : TrianglesRenderableObject<InputLayouts.VerticePNTG>, IKn5RenderableObject {
        public readonly bool IsCastingShadows;
        
        public Kn5Node OriginalNode { get; }

        public Matrix ModelMatrixInverted { get; set; }

        private static InputLayouts.VerticePNTG[] Convert(Kn5Node.Vertice[] vertices) {
            var size = vertices.Length;
            var result = new InputLayouts.VerticePNTG[size];
            
            for (var i = 0; i < size; i++) {
                var x = vertices[i];
                result[i] = new InputLayouts.VerticePNTG(
                        x.Co.ToVector3(),
                        x.Normal.ToVector3(),
                        x.Uv.ToVector2(),
                        x.Tangent.ToVector3());
            }

            return result;
        }

        private static ushort[] Convert(ushort[] indices) {
            return indices.ToIndicesFixX();
        }

        private bool _isTransparent;
        private readonly float _distanceFromSqr, _distanceToSqr;

        public Kn5RenderableObject(Kn5Node node) : base(node.Name, Convert(node.Vertices), Convert(node.Indices)) {
            OriginalNode = node;
            IsCastingShadows = node.CastShadows;

            if (IsEnabled && (!OriginalNode.Active || !OriginalNode.IsVisible || !OriginalNode.IsRenderable)) {
                IsEnabled = false;
            }

            if (OriginalNode.IsTransparent || OriginalNode.Layer == 1 /* WHAT? WHAT DOES IT DO? BUT KUNOS PREVIEWS SHOWROOM WORKS THIS WAY, SO… */) {
                IsReflectable = false;
            }

            _isTransparent = OriginalNode.IsTransparent;
            _distanceFromSqr = OriginalNode.LodIn.Pow(2f);
            _distanceToSqr = OriginalNode.LodOut.Pow(2f);
        }

        private IRenderableMaterial Material => _debugMaterial ?? _mirrorMaterial ?? _material;

        [CanBeNull]
        private IRenderableMaterial _mirrorMaterial;
        private bool _mirrorMaterialInitialized;

        public void SetMirrorMode(IDeviceContextHolder holder, bool enabled) {
            if (enabled == (_mirrorMaterial != null)) return;

            if (enabled) {
                _mirrorMaterial = holder.GetMaterial(BasicMaterials.MirrorKey);
                if (IsInitialized) {
                    _mirrorMaterial.Initialize(holder);
                    _mirrorMaterialInitialized = true;
                }
            } else {
                _mirrorMaterialInitialized = false;
                DisposeHelper.Dispose(ref _mirrorMaterial);
            }
        }

        [CanBeNull]
        private IRenderableMaterial _debugMaterial;
        private bool _debugMaterialInitialized;

        public void SetDebugMode(IDeviceContextHolder holder, bool enabled) {
            if (enabled == (_debugMaterial != null)) return;

            if (enabled) {
                _debugMaterial = holder.Get<SharedMaterials>().GetMaterial(new Tuple<object, uint>(BasicMaterials.DebugKey, OriginalNode.MaterialId));
                if (_debugMaterial == null) return;

                if (IsInitialized) {
                    _debugMaterial.Initialize(holder);
                    _debugMaterialInitialized = true;
                }
            } else {
                _debugMaterialInitialized = false;
                DisposeHelper.Dispose(ref _debugMaterial);
            }
        }

        public Vector3? Emissive { get; set; }

        public void SetEmissive(Vector3? color) {
            Emissive = color;
        }

        int IKn5RenderableObject.TrianglesCount => GetTrianglesCount();

        public void SetTransparent(bool? isTransparent) {
            _isTransparent = isTransparent ?? OriginalNode.IsTransparent;
        }

        private IRenderableMaterial _material;

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);
            
            _material = contextHolder.Get<SharedMaterials>().GetMaterial(OriginalNode.MaterialId);
            _material.Initialize(contextHolder);

            if (_mirrorMaterial != null && !_mirrorMaterialInitialized) {
                _mirrorMaterialInitialized = true;
                _mirrorMaterial.Initialize(contextHolder);
            }

            if (_debugMaterial != null && !_debugMaterialInitialized) {
                _debugMaterialInitialized = true;
                _debugMaterial.Initialize(contextHolder);
            }
        }

        internal static readonly SpecialRenderMode TransparentModes = SpecialRenderMode.SimpleTransparent |
                SpecialRenderMode.Outline | SpecialRenderMode.GBuffer |
                SpecialRenderMode.DeferredTransparentForw | SpecialRenderMode.DeferredTransparentDef | SpecialRenderMode.DeferredTransparentMask;

        internal static readonly SpecialRenderMode OpaqueModes = SpecialRenderMode.Simple |
                SpecialRenderMode.Outline | SpecialRenderMode.GBuffer |
                SpecialRenderMode.Deferred | SpecialRenderMode.Reflection | SpecialRenderMode.Shadow;

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!(_isTransparent ? TransparentModes : OpaqueModes).HasFlag(mode)) return;
            if (mode == SpecialRenderMode.Shadow && !IsCastingShadows) return;

            if (_distanceFromSqr != 0f || _distanceToSqr != 0f) {
                var distance = (BoundingBox?.GetCenter() - camera.Position)?.LengthSquared();
                if (distance < _distanceFromSqr || distance > _distanceToSqr) return;
            }

            var material = Material;
            if (!material.Prepare(contextHolder, mode)) return;

            base.DrawOverride(contextHolder, camera, mode);

            if (Emissive.HasValue) {
                (material as IEmissiveMaterial)?.SetEmissiveNext(Emissive.Value);
            }

            material.SetMatrices(ParentMatrix, camera);
            material.Draw(contextHolder, Indices.Length, mode);
        }

        public override BaseRenderableObject Clone() {
            return new ClonedKn5RenderableObject(this);
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _material);
            DisposeHelper.Dispose(ref _mirrorMaterial);
            DisposeHelper.Dispose(ref _debugMaterial);
            base.Dispose();
        }

        internal class ClonedKn5RenderableObject : TrianglesRenderableObject<InputLayouts.VerticePNTG> {
            private readonly Kn5RenderableObject _original;

            internal ClonedKn5RenderableObject(Kn5RenderableObject original) : base(original.Name + "_copy", original.Vertices, original.Indices) {
                _original = original;
            }

            public override bool IsEnabled => _original.IsEnabled;

            public override bool IsReflectable => _original.IsReflectable;

            protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
                if (!(_original._isTransparent ? TransparentModes : OpaqueModes).HasFlag(mode)) return;
                if (mode == SpecialRenderMode.Shadow && !_original.IsCastingShadows || _original._material == null) return;

                if (_original._distanceFromSqr != 0f || _original._distanceToSqr != 0f) {
                    var distance = (BoundingBox?.GetCenter() - camera.Position)?.LengthSquared();
                    if (distance < _original._distanceFromSqr || distance > _original._distanceToSqr) return;
                }

                var material = _original.Material;
                if (!material.Prepare(contextHolder, mode)) return;

                base.DrawOverride(contextHolder, camera, mode);

                if (_original.Emissive.HasValue) {
                    (material as IEmissiveMaterial)?.SetEmissiveNext(_original.Emissive.Value);
                }

                material.SetMatrices(ParentMatrix, camera);
                material.Draw(contextHolder, Indices.Length, mode);
            }

            public override BaseRenderableObject Clone() {
                return new ClonedKn5RenderableObject(_original);
            }
        }
    }
}
