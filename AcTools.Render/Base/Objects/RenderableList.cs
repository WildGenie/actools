﻿using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public class RenderableList : List<IRenderableObject>, IRenderableObject {
        public string Name { get; }

        public Matrix Matrix { get; private set; }

        private Matrix _parentMatrix = Matrix.Identity;

        public Matrix ParentMatrix {
            get { return _parentMatrix; }
            set {
                if (Equals(_parentMatrix, value)) return;
                _parentMatrix = value;
                OnMatrixChanged();
            }
        }

        public bool IsReflectable { get; set; } = true;

        public bool IsEnabled { get; set; } = true;

        private Matrix _localMatrix = Matrix.Identity;

        public Matrix LocalMatrix {
            get { return _localMatrix; }
            set {
                if (Equals(_localMatrix, value)) return;
                _localMatrix = value;
                OnMatrixChanged();

                foreach (var clone in _clones) {
                    if (clone == null) continue;
                    clone.LocalMatrix = value;
                }
            }
        }

        public event EventHandler MatrixChanged;

        protected virtual void OnMatrixChanged() {
            MatrixChanged?.Invoke(this, EventArgs.Empty);
            UpdateMatrix();
        }

        public RenderableList([CanBeNull] string name, Matrix localMatrix, IEnumerable<IRenderableObject> children) : base(children) {
            LocalMatrix = localMatrix;
            Name = name;
            UpdateMatrix();
        }

        public RenderableList([CanBeNull] string name, Matrix localMatrix) {
            LocalMatrix = localMatrix;
            Name = name;
            UpdateMatrix();
        }

        public RenderableList() : this(null, Matrix.Identity) { }

        private void UpdateMatrix() {
            Matrix = _localMatrix * _parentMatrix;
            foreach (var child in this) {
                child.ParentMatrix = Matrix;
            }
        }

        private Vector3? _originalLocalPos;
        private Matrix _originalMatrix;
        private RenderableList _lookAt;

        private static Matrix RotateToFaceDirSpecial(Vector3 o, Vector3 p, Vector3 u) {
            var d = Vector3.Normalize(p - o);
            var s = Vector3.Normalize(Vector3.Cross(d, Vector3.Normalize(u)));
            var v = Vector3.Cross(s, d);
            return SlimDxExtension.ToMatrix(
                    d.X, d.Y, d.Z, 0,
                    v.X, v.Y, v.Z, 0,
                    s.X, s.Y, s.Z, 0,
                    o.X, o.Y, o.Z, 1);
        }

        private static Matrix RotateToFace(Vector3 o, Vector3 p, Vector3 u) {
            var d = Vector3.Normalize(o - p);
            var s = Vector3.Normalize(Vector3.Cross(Vector3.Normalize(u), d));
            var v = Vector3.Normalize(Vector3.Cross(d, s));
            return SlimDxExtension.ToMatrix(
                    v.X, v.Y, v.Z, 0,
                    d.X, d.Y, d.Z, 0,
                    s.X, s.Y, s.Z, 0,
                    o.X, o.Y, o.Z, 1);
        }

        public void LookAtDirMode(Vector3 globalPoint, Vector3 up) {
            if (!_originalLocalPos.HasValue) {
                _originalLocalPos = LocalMatrix.GetTranslationVector();
                _originalMatrix = LocalMatrix;
            }

            var globalPos = Vector3.TransformCoordinate(_originalLocalPos.Value, ParentMatrix);
            var yAxis = Vector3.TransformNormal(up, _originalMatrix * ParentMatrix);
            LocalMatrix = RotateToFaceDirSpecial(globalPos, globalPoint, yAxis) * Matrix.Invert(ParentMatrix);
        }

        public void LookAt(Vector3 globalPoint, Vector3 up) {
            var globalPos = Vector3.TransformCoordinate(LocalMatrix.GetTranslationVector(), ParentMatrix);
            var yAxis = up;
            LocalMatrix = RotateToFace(globalPos, globalPoint, yAxis) * Matrix.Invert(ParentMatrix);
        }

        private Matrix _prevThisMatrix, _prevLookAtMatrix;

        private void UpdateLookAt() {
            if (_lookAt == null || Equals(Matrix, _prevThisMatrix) && Equals(_prevLookAtMatrix, _lookAt.Matrix)) return;
            _prevThisMatrix = Matrix;
            _prevLookAtMatrix = _lookAt.Matrix;
            LookAtDirMode(_prevLookAtMatrix.GetTranslationVector(), Vector3.UnitY);
        }

        public void LookAt(Kn5RenderableList target) {
            _lookAt = target;
            UpdateLookAt();
        }

        public int GetTrianglesCount() {
            return this.Where(x => x.IsEnabled).Aggregate(0, (a, b) => a + b.GetTrianglesCount());
        }

        public int GetObjectsCount() {
            return this.Where(x => x.IsEnabled).Aggregate(0, (a, b) => a + b.GetObjectsCount());
        }

        public BoundingBox? BoundingBox { get; private set; }

        public void UpdateBoundingBox() {
            BoundingBox? bb = null;

            for (var i = 0; i < Count; i++) {
                var c = this[i];
                if (!c.IsEnabled) continue;

                c.UpdateBoundingBox();
                var cb = c.BoundingBox;
                if (!cb.HasValue) continue;

                bb = bb?.ExtendBy(cb.Value) ?? c.BoundingBox;
            }

            BoundingBox = bb;
        }

        public new void Add([NotNull] IRenderableObject obj) {
            base.Add(obj);
            obj.ParentMatrix = Matrix;
        }

        public new void AddRange([NotNull, ItemNotNull] IEnumerable<IRenderableObject> objs) {
            foreach (var o in objs) {
                base.Add(o);
                o.ParentMatrix = Matrix;
            }
        }

        public new void Insert(int index, [NotNull] IRenderableObject obj) {
            base.Insert(index, obj);
            obj.ParentMatrix = Matrix;
        }

        public new void InsertRange(int index, [NotNull, ItemNotNull] IEnumerable<IRenderableObject> objs) {
            foreach (var o in objs) {
                base.Insert(index++, o);
                o.ParentMatrix = Matrix;
            }
        }

        public virtual void Draw(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (!IsEnabled || filter?.Invoke(this) == false) return;
            if (mode == SpecialRenderMode.Reflection && !IsReflectable) return;
            if (camera != null && BoundingBox != null && !camera.Visible(BoundingBox.Value)) return;

            UpdateLookAt();

            var c = Count;
            for (var i = 0; i < c; i++) {
                var child = this[i];
                if (child.IsEnabled) {
                    child.Draw(contextHolder, camera, mode, filter);
                }
            }
        }

        private readonly WeakList<RenderableList> _clones = new WeakList<RenderableList>(1);

        public RenderableList Clone() {
            var result = new RenderableList(Name + "_copy", LocalMatrix, this.Select(x => x.Clone())) {
                IsEnabled = IsEnabled,
                IsReflectable = IsReflectable,
                ParentMatrix = ParentMatrix,
                BoundingBox = BoundingBox
            };
            _clones.Add(result);
            return result;
        }

        public float? CheckIntersection(Ray ray) {
            return null;
        }

        IRenderableObject IRenderableObject.Clone() {
            return Clone();
        }

        public virtual void Dispose() {
            this.DisposeEverything();
        }
    }
}
