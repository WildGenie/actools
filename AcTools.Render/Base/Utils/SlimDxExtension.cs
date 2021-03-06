﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;
using Debug = System.Diagnostics.Debug;

namespace AcTools.Render.Base.Utils {
    public static class SlimDxExtension {
        public static BoundingBox ToBoundingBox(this IEnumerable<Vector3> vertices) {
            using (var ie = vertices.GetEnumerator()) {
                if (!ie.MoveNext()) return new BoundingBox();

                var v = ie.Current;
                var minX = v.X;
                var minY = v.Y;
                var minZ = v.Z;
                var maxX = v.X;
                var maxY = v.Y;
                var maxZ = v.Z;

                while (ie.MoveNext()) {
                    var n = ie.Current;
                    if (minX > n.X) minX = n.X;
                    if (minY > n.Y) minY = n.Y;
                    if (minZ > n.Z) minZ = n.Z;
                    if (maxX < n.X) maxX = n.X;
                    if (maxY < n.Y) maxY = n.Y;
                    if (maxZ < n.Z) maxZ = n.Z;
                }

                return new BoundingBox(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
            }
        }

        public static BoundingBox ExtendBy(this BoundingBox bb, BoundingBox next) {
            return new BoundingBox(
                    new Vector3(
                            Math.Min(bb.Minimum.X, next.Minimum.X),
                            Math.Min(bb.Minimum.Y, next.Minimum.Y),
                            Math.Min(bb.Minimum.Z, next.Minimum.Z)),
                    new Vector3(
                            Math.Max(bb.Maximum.X, next.Maximum.X),
                            Math.Max(bb.Maximum.Y, next.Maximum.Y),
                            Math.Max(bb.Maximum.Z, next.Maximum.Z)));
        }

        public static Vector3 GetCenter(this BoundingBox bb) {
            return (bb.Minimum + bb.Maximum) / 2f;
        }

        public static Vector3 GetSize(this BoundingBox bb) {
            return bb.Maximum - bb.Minimum;
        }

        public static Vector3 GetXyz(this Vector4 vec) {
            return new Vector3(vec.X, vec.Y, vec.Z);
        }

        public static Vector3 ToVector3(this Color color) {
            return new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        }

        public static Vector4 ToVector4(this Color color) {
            return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        }

        public static float GetBrightness(this Vector3 vec) {
            return vec.X * 0.299f + vec.Y * 0.587f + vec.Z * 0.114f;
        }

        public static Vector3 FlipX(this Vector3 vec) {
            vec.X *= -1f;
            return vec;
        }

        public static BoundingBox Transform(this BoundingBox bb, Matrix matrix) {
            var a = Vector3.Transform(bb.Minimum, matrix);
            var b = Vector3.Transform(bb.Maximum, matrix);
            return new BoundingBox(
                    new Vector3(
                            Math.Min(a.X, b.X),
                            Math.Min(a.Y, b.Y),
                            Math.Min(a.Z, b.Z)),
                    new Vector3(
                            Math.Max(a.X, b.X),
                            Math.Max(a.Y, b.Y),
                            Math.Max(a.Z, b.Z)));
        }

        public static void DrawAllPasses(this EffectTechnique tech, DeviceContext context, int indexCount) {
            for (var i = 0; i < tech.Description.PassCount; i++) {
                tech.GetPassByIndex(i).Apply(context);
                context.DrawIndexed(indexCount, 0, 0);
            }
        }

        public static Quaternion ToQuaternion(this float[] quaternion4) {
            return new Quaternion(quaternion4[0], quaternion4[1], quaternion4[2], quaternion4[3]);
        }

        public static Vector2 ToVector2(this float[] vec2) {
            return new Vector2(vec2[0], vec2[1]);
        }

        public static Vector3 ToVector3(this float[] vec3) {
            return new Vector3(vec3[0], vec3[1], vec3[2]);
        }

        public static Vector4 ToVector4(this float[] vec3) {
            return new Vector4(vec3[0], vec3[1], vec3[2], vec3[3]);
        }

        public static Vector3 ToVector3FixX(this float[] vec3) {
            return new Vector3(-vec3[0], vec3[1], vec3[2]);
        }

        public static Vector3 ToVector3Tangent(this float[] vec3) {
            return new Vector3(-vec3[0], vec3[1], vec3[2]);
        }

        public static Vector3 GetTranslationVector(this Matrix matrix) {
            return new Vector3(matrix.M41, matrix.M42, matrix.M43);
        }

        // ReSharper disable once InconsistentNaming
        public static Matrix ToMatrix(this float[] mat4x4) {
            return new Matrix {
                M11 = mat4x4[0],
                M12 = mat4x4[1],
                M13 = mat4x4[2],
                M14 = mat4x4[3],
                M21 = mat4x4[4],
                M22 = mat4x4[5],
                M23 = mat4x4[6],
                M24 = mat4x4[7],
                M31 = mat4x4[8],
                M32 = mat4x4[9],
                M33 = mat4x4[10],
                M34 = mat4x4[11],
                M41 = mat4x4[12],
                M42 = mat4x4[13],
                M43 = mat4x4[14],
                M44 = mat4x4[15]
            };
        }

        public static Matrix ToMatrix(float m11, float m12, float m13, float m14, float m21, float m22, float m23, float m24, float m31, float m32, float m33,
                float m34, float m41, float m42, float m43, float m44) {
            return new Matrix {
                M11 = m11,
                M12 = m12,
                M13 = m13,
                M14 = m14,
                M21 = m21,
                M22 = m22,
                M23 = m23,
                M24 = m24,
                M31 = m31,
                M32 = m32,
                M33 = m33,
                M34 = m34,
                M41 = m41,
                M42 = m42,
                M43 = m43,
                M44 = m44
            };
        }

        public static Matrix ToMatrixFixX(this float[] mat4x4) {
            var matrix = mat4x4.ToMatrix();

            Vector3 translation, scale;
            Quaternion rotation;
            matrix.Decompose(out scale, out rotation, out translation);
            translation.X *= -1;

            var axis = rotation.Axis;
            var angle = rotation.Angle;

            if (angle.Abs() < 0.0001f) {
                return Matrix.Scaling(scale) * Matrix.Translation(translation);
            }

            axis.Y *= -1;
            axis.Z *= -1;
            rotation = Quaternion.RotationAxis(axis, angle);

            var result = Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotation) * Matrix.Translation(translation);
            if (float.IsNaN(result[0, 0])) {
                AcToolsLogging.Write("CAN’T FIX MATRIX! PLEASE, SEND THE MODEL TO THE DEVELOPER");
                return matrix;
            }

            return result;
        }

        public static ushort[] ToIndicesFixX(this ushort[] indices) {
            var result = new ushort[indices.Length];
            for (var i = 0; i < indices.Length; i += 3) {
                result[i] = indices[i];
                result[i + 1] = indices[i + 2];
                result[i + 2] = indices[i + 1];
            }
            return result;
        }

        private static readonly Dictionary<int, CacheObject> Cache = new Dictionary<int, CacheObject>(); 

        private class CacheObject : IDisposable {
            public static byte[] Array = new byte[512];
            public readonly IntPtr Pointer;
            public readonly DataStream Data;

            public CacheObject(int len) {
                if (len > Array.Length) {
                    Array = new byte[len];
                } else {
                    System.Array.Clear(Array, 0, Array.Length);
                }
                
                Pointer = Marshal.AllocHGlobal(len);
                Data = new DataStream(Array, false, false);
            }

            public void Dispose() {
                Marshal.FreeHGlobal(Pointer);
            }
        }

        public static void Set_(EffectVariable variable, object o, int len) {
            var arr = new byte[len];
            var ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(o, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);
            variable.SetRawValue(new DataStream(arr, false, false), len);
        }

        public static void Set(EffectVariable variable, object o, int len) {
            if (o == null) {
                // TODO (?)
            } else {
                CacheObject cobj;
                if (!Cache.TryGetValue(len, out cobj)) {
                    cobj = new CacheObject(len);
                    Cache[len] = cobj;
                    Debug.WriteLine("CACHED MEMORY AREA CREATED: " + len);
                }

                Marshal.StructureToPtr(o, cobj.Pointer, true);
                Marshal.Copy(cobj.Pointer, CacheObject.Array, 0, len);
                variable.SetRawValue(cobj.Data, len);
            }
        }

        public static void Set(this EffectVariable variable, object o) {
            Set(variable, o, Marshal.SizeOf(o));
        }

        public static BlendState CreateBlendState(this Device device, RenderTargetBlendDescription description) {
            var desc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };
            desc.RenderTargets[0] = description;
            return BlendState.FromDescription(device, desc);
        }
    }
}
