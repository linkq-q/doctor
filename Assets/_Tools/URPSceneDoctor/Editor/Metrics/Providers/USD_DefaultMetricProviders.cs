using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_BrightnessBucketProvider : IUsdMetricProvider
    {
        public string Id => "brightness_bucket";
        public string DisplayNameZh => "亮度桶";
        public string DisplayNameEn => "Brightness Bucket";
        public USD_MetricRenderHint RenderHint => USD_MetricRenderHint.EnumBucket;
        public USD_MetricValue Evaluate(USD_ImageMetricsAggregate a) => new USD_MetricValue { DisplayText = a.brightness_bucket, HasNumeric = false };
    }

    public sealed class USD_LumaMeanProvider : IUsdMetricProvider
    {
        public string Id => "luma_mean";
        public string DisplayNameZh => "平均亮度";
        public string DisplayNameEn => "Luma Mean";
        public USD_MetricRenderHint RenderHint => USD_MetricRenderHint.Progress01;
        public USD_MetricValue Evaluate(USD_ImageMetricsAggregate a) => new USD_MetricValue { Numeric = a.luma_mean, HasNumeric = true, DisplayText = a.luma_mean.ToString("0.000") };
    }

    public sealed class USD_LumaStdProvider : IUsdMetricProvider
    {
        public string Id => "luma_std";
        public string DisplayNameZh => "亮度标准差";
        public string DisplayNameEn => "Luma Std";
        public USD_MetricRenderHint RenderHint => USD_MetricRenderHint.Progress01;
        public USD_MetricValue Evaluate(USD_ImageMetricsAggregate a) => new USD_MetricValue { Numeric = a.luma_std, HasNumeric = true, DisplayText = a.luma_std.ToString("0.000") };
    }

    public sealed class USD_OverexposureProvider : IUsdMetricProvider
    {
        public string Id => "overexposure_ratio";
        public string DisplayNameZh => "过曝比例";
        public string DisplayNameEn => "Overexposure Ratio";
        public USD_MetricRenderHint RenderHint => USD_MetricRenderHint.Progress01;
        public USD_MetricValue Evaluate(USD_ImageMetricsAggregate a) => new USD_MetricValue { Numeric = a.overexposure_ratio, HasNumeric = true, DisplayText = a.overexposure_ratio.ToString("0.000") };
    }

    public sealed class USD_CenterContrastProvider : IUsdMetricProvider
    {
        public string Id => "center_contrast_ratio";
        public string DisplayNameZh => "中心对比比率";
        public string DisplayNameEn => "Center Contrast Ratio";
        public USD_MetricRenderHint RenderHint => USD_MetricRenderHint.Ratio;
        public USD_MetricValue Evaluate(USD_ImageMetricsAggregate a) => new USD_MetricValue { Numeric = a.center_contrast_ratio, HasNumeric = true, DisplayText = a.center_contrast_ratio.ToString("0.000") };
    }

    public sealed class USD_SaturationMeanProvider : IUsdMetricProvider
    {
        public string Id => "saturation_mean";
        public string DisplayNameZh => "平均饱和度";
        public string DisplayNameEn => "Saturation Mean";
        public USD_MetricRenderHint RenderHint => USD_MetricRenderHint.Progress01;
        public USD_MetricValue Evaluate(USD_ImageMetricsAggregate a) => new USD_MetricValue { Numeric = a.saturation_mean, HasNumeric = true, DisplayText = a.saturation_mean.ToString("0.000") };
    }

    public sealed class USD_WarmCoolProvider : IUsdMetricProvider
    {
        public string Id => "warm_cool_balance";
        public string DisplayNameZh => "冷暖平衡";
        public string DisplayNameEn => "Warm/Cool Balance";
        public USD_MetricRenderHint RenderHint => USD_MetricRenderHint.SignedBar;
        public USD_MetricValue Evaluate(USD_ImageMetricsAggregate a) => new USD_MetricValue { Numeric = a.warm_cool_balance, HasNumeric = true, DisplayText = a.warm_cool_balance.ToString("+0.000;-0.000") };
    }

    // Sample extension provider: appears automatically without panel code changes.
    public sealed class USD_UnderexposureProxyProvider : IUsdMetricProvider
    {
        public string Id => "underexposure_ratio_proxy";
        public string DisplayNameZh => "欠曝比例（代理）";
        public string DisplayNameEn => "Underexposure Ratio (Proxy)";
        public USD_MetricRenderHint RenderHint => USD_MetricRenderHint.Progress01;
        public USD_MetricValue Evaluate(USD_ImageMetricsAggregate a)
        {
            var proxy = Mathf.Clamp01((0.2f - a.luma_mean) * 2.5f);
            return new USD_MetricValue { Numeric = proxy, HasNumeric = true, DisplayText = proxy.ToString("0.000") };
        }
    }
}
