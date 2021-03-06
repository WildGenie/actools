﻿using System;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class DisposeHelper {
        [ContractAnnotation("=> disposable:null")]
        public static void Dispose<T>([CanBeNull] ref T disposable) where T : class, IDisposable {
            if (disposable == null) return;
            disposable.Dispose();
            disposable = null;
        }

        [ContractAnnotation("a:null, b:null => null; a:notnull => notnull; b:notnull => notnull")]
        public static IDisposable Join(this IDisposable a, IDisposable b) {
            return b == null ? a : (a == null ? b : new CombinedDisposable(a, b));
        }

        private class CombinedDisposable : IDisposable {
            private IDisposable _a, _b;

            public CombinedDisposable(IDisposable a, IDisposable b) {
                _a = a;
                _b = b;
            }

            public void Dispose() {
                DisposeHelper.Dispose(ref _a);
                DisposeHelper.Dispose(ref _b);
            }
        }
    }
}
