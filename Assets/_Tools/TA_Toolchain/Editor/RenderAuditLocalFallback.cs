using System;
using System.Collections.Generic;
using System.Text;

namespace TA.Toolchain.RenderAudit
{
    public static class RenderAuditLocalFallback
    {
        public static string Build(string slimJson)
        {
            var lines = new List<string>();

            var hasShadow = Contains(slimJson, "Realtime shadow") || Contains(slimJson, "Shadow distance") || Contains(slimJson, "shadow");
            var hasTexture = Contains(slimJson, "4K texture") || Contains(slimJson, "Large texture") || Contains(slimJson, "texture");
            var hasMesh = Contains(slimJson, "High poly") || Contains(slimJson, "tri") || Contains(slimJson, "vert");
            var hasTrans = Contains(slimJson, "Transparent") || Contains(slimJson, "transparency");
            var hasMissing = Contains(slimJson, "Missing MonoBehaviour") || Contains(slimJson, "missing script");

            if (hasShadow) lines.Add("P0 - 实时阴影预算偏高→GPU/CPU帧时抖动→先把主阴影距离与级联降一档并限制实时投影灯数。");
            if (hasTexture) lines.Add("P0 - 高分辨率纹理占用偏大→显存与带宽压力上升→优先把远景与次要材质降MaxSize并启用压缩。 ");
            if (hasMesh) lines.Add("P1 - 高面数网格集中→主线程与GPU栅格负担增加→给大模型补LOD并替换中远景简模。 ");
            if (hasTrans) lines.Add("P1 - 透明对象较多→过绘与排序风险增大→减少全屏透明层并合并同类特效材质。 ");
            if (hasMissing) lines.Add("P2 - 场景存在丢失脚本→运行稳定性与排查成本变差→先清理Missing组件并补齐引用。 ");

            while (lines.Count < 5)
            {
                var idx = lines.Count;
                lines.Add(idx switch
                {
                    0 => "P0 - 渲染热点分布不均→关键机位成本偏高→先锁定主相机路径做分级预算与限额。",
                    1 => "P0 - 关键材质特性偏重→移动镜头时掉帧风险增大→优先替换高成本关键词组合。",
                    2 => "P1 - 粒子/透明叠加偏密→画面稳定性下降→压缩生命周期并减少同屏发射峰值。",
                    3 => "P1 - 资源规格不统一→加载与切换抖动→建立纹理/网格分档并批量对齐。",
                    _ => "P2 - 缺少定期审计闭环→问题易反复→把Render Audit纳入提测前固定检查。"
                });
            }

            var sb = new StringBuilder(512);
            sb.AppendLine("【改进建议（按影响排序）】");
            for (var i = 0; i < 5; i++)
            {
                sb.AppendLine(lines[i].Trim());
            }

            sb.AppendLine();
            sb.AppendLine("【建议补充的氛围效果】");
            sb.AppendLine("P0 - 体积雾/高度雾+夕阳光束→强化黄昏层次与遗迹神秘感→在URP Volume启用Fog并配合主光低角度暖色。");
            sb.AppendLine("P1 - 落叶/飞虫粒子+风场→提升森林生命感与空间流动→用Particle/VFX按相机距离分级发射。");
            sb.Append("P2 - 水面泡沫/流向噪声/反射约束→增强河道可读性与质感→在水材质加入岸线遮罩并限制高频反射。");
            return sb.ToString();
        }

        private static bool Contains(string text, string token)
        {
            return text != null && token != null && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
