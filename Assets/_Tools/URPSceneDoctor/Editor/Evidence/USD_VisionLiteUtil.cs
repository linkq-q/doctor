using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_VisionLiteUtil
    {
        public static USD_ImageMetricsFile BuildMetrics(string sceneName, string timestamp, string packRoot, List<USD_ShotCapture> shots)
        {
            var result = new USD_ImageMetricsFile { sceneName = sceneName, timestamp = timestamp };
            foreach (var shot in shots)
            {
                var m = ComputeShot(packRoot, shot.path);
                m.id = Path.GetFileNameWithoutExtension(shot.path);
                m.path = shot.path.Replace(packRoot + "/", string.Empty).Replace('\\', '/');
                result.shots.Add(m);
            }

            if (result.shots.Count == 0) return result;
            result.aggregate = new USD_ImageMetricsAggregate
            {
                luma_mean = result.shots.Average(x => x.luma_mean),
                luma_std = result.shots.Average(x => x.luma_std),
                overexposure_ratio = result.shots.Average(x => x.overexposure_ratio),
                center_contrast_ratio = result.shots.Average(x => x.center_contrast_ratio),
                saturation_mean = result.shots.Average(x => x.saturation_mean),
                warm_cool_balance = result.shots.Average(x => x.warm_cool_balance)
            };
            result.aggregate.brightness_bucket = ToBucket(result.aggregate.luma_mean);
            return result;
        }

        public static USD_ImageMetricsAggregate BuildDiff(USD_ImageMetricsFile before, USD_ImageMetricsFile after)
        {
            return new USD_ImageMetricsAggregate
            {
                brightness_bucket = $"{before.aggregate.brightness_bucket}->{after.aggregate.brightness_bucket}",
                luma_mean = after.aggregate.luma_mean - before.aggregate.luma_mean,
                luma_std = after.aggregate.luma_std - before.aggregate.luma_std,
                overexposure_ratio = after.aggregate.overexposure_ratio - before.aggregate.overexposure_ratio,
                center_contrast_ratio = after.aggregate.center_contrast_ratio - before.aggregate.center_contrast_ratio,
                saturation_mean = after.aggregate.saturation_mean - before.aggregate.saturation_mean,
                warm_cool_balance = after.aggregate.warm_cool_balance - before.aggregate.warm_cool_balance
            };
        }

        private static USD_ImageMetricsShot ComputeShot(string packRoot, string imagePath)
        {
            var bytes = File.ReadAllBytes(imagePath);
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(bytes);
            var colors = tex.GetPixels32();
            var width = tex.width;
            var height = tex.height;
            UnityEngine.Object.DestroyImmediate(tex);

            var n = Mathf.Max(1, colors.Length);
            var lumas = new float[n];
            var sumLuma = 0f;
            var over = 0;
            var satSum = 0f;
            var warmSum = 0f;
            for (var i = 0; i < n; i++)
            {
                var c = colors[i];
                var r = c.r / 255f;
                var g = c.g / 255f;
                var b = c.b / 255f;
                var l = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                lumas[i] = l;
                sumLuma += l;
                if (l > 0.95f) over++;
                Color.RGBToHSV(new Color(r, g, b), out _, out var s, out _);
                satSum += s;
                warmSum += (r - b);
            }

            var mean = sumLuma / n;
            var varSum = 0f;
            for (var i = 0; i < n; i++) varSum += (lumas[i] - mean) * (lumas[i] - mean);
            var std = Mathf.Sqrt(varSum / n);

            var cx0 = Mathf.FloorToInt(width * 0.35f);
            var cy0 = Mathf.FloorToInt(height * 0.35f);
            var cx1 = Mathf.CeilToInt(width * 0.65f);
            var cy1 = Mathf.CeilToInt(height * 0.65f);
            var cCount = 0;
            var cMean = 0f;
            for (var y = cy0; y < cy1; y++)
            for (var x = cx0; x < cx1; x++)
            {
                var idx = y * width + x;
                cMean += lumas[idx];
                cCount++;
            }

            cMean = cCount > 0 ? cMean / cCount : mean;
            var cVar = 0f;
            for (var y = cy0; y < cy1; y++)
            for (var x = cx0; x < cx1; x++)
            {
                var idx = y * width + x;
                cVar += (lumas[idx] - cMean) * (lumas[idx] - cMean);
            }

            var cStd = cCount > 0 ? Mathf.Sqrt(cVar / cCount) : std;
            var globalStdSafe = std <= 1e-5f ? 1f : std;

            return new USD_ImageMetricsShot
            {
                brightness_bucket = ToBucket(mean),
                luma_mean = mean,
                luma_std = std,
                overexposure_ratio = over / (float)n,
                center_contrast_ratio = cStd / globalStdSafe,
                saturation_mean = satSum / n,
                warm_cool_balance = Mathf.Clamp(warmSum / n, -1f, 1f)
            };
        }

        private static string ToBucket(float lumaMean)
        {
            if (lumaMean >= 0.65f) return "high";
            if (lumaMean <= 0.35f) return "low";
            return "mid";
        }
    }
}
