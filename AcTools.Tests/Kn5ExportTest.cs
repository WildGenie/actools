﻿using System.IO;
using System.Runtime.CompilerServices;
using AcTools.Kn5File;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class Kn5ExportTest {
        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        [TestMethod]
        public void ExportBonesColladaTest() {
            var kn5 = Kn5.FromFile(TestDir + "/kn5/bones.kn5");
            kn5.ExportCollada(TestDir + "/kn5/bones-out.dae");
        }

        [TestMethod]
        public void ExportMultiplyMaterialsColladaTest() {
            var kn5 = Kn5.FromFile(TestDir + "/kn5/multiply_materials.kn5");
            kn5.ExportCollada(TestDir + "/kn5/multiply_materials-out.dae");
        }
    }
}