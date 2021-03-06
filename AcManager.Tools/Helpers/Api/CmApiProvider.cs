﻿using System;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    [Localizable(false)]
    public static class CmApiProvider {
        #region Initialization
        public static readonly string UserAgent;

        static CmApiProvider() {
            var windows = $"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            UserAgent = $"ContentManager/{BuildInformation.AppVersion} ({windows})";
        }
        #endregion

        [CanBeNull]
        public static string GetString(string url) {
            try {
                var result = InternalUtils.CmGetData(url, UserAgent);
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning($"Cannot read as UTF8 from {url}: " + e);
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<string> GetStringAsync(string url, CancellationToken cancellation = default(CancellationToken)) {
            try {
                var result = await InternalUtils.CmGetDataAsync(url, UserAgent, null, cancellation);
                if (cancellation.IsCancellationRequested) return null;
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning($"Cannot read as UTF8 from {url}: " + e);
                return null;
            }
        }

        [CanBeNull]
        public static T Get<T>(string url) {
            try {
                var json = GetString(url);
                return json == null ? default(T) : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) {
                Logging.Warning($"Cannot read as JSON from {url}: " + e);
                return default(T);
            }
        }

        [ItemCanBeNull]
        public static async Task<T> GetAsync<T>(string url, CancellationToken cancellation = default(CancellationToken)) {
            try {
                var json = await GetStringAsync(url, cancellation);
                if (cancellation.IsCancellationRequested) return default(T);
                return json == null ? default(T) : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) {
                Logging.Warning($"Cannot read as JSON from {url}: " + e);
                return default(T);
            }
        }

        [CanBeNull]
        public static byte[] GetData(string url) {
            return InternalUtils.CmGetData(url, UserAgent);
        }

        [ItemCanBeNull]
        public static Task<byte[]> GetDataAsync(string url, IProgress<double?> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            return InternalUtils.CmGetDataAsync(url, UserAgent, progress, cancellation);
        }
    }
}
