using System;

namespace PureAcidAlkali
{
    /// <summary>
    /// 纯酸/纯碱净化机的全部数值模型都集中在这里，调平衡只改这个文件即可。
    ///
    /// 设计口径（按用户最终确认）：
    /// - 质量（芯片质量加成 Q）→ 纯度上限：Q=0 时最高 98%，Q=50 时 100%，中间线性，Q≥50 恒定 100%。
    /// - 性能（P）→ 每晚去除量：P=0 时 10 mL，P=50 时 20 mL（线性：10 + 0.2×P mL）。
    /// - 效率（E）→ 耗电，由游戏自己的 ModuleHelper.ApplyBasicModuleEffect 结算（与净水器一致）。
    ///
    /// 机器只“往上”提纯：输入纯度已经高于上限时不会降纯，只是不工作。
    /// 每次结算把杂质按额度去除，并等量转成目标酸/碱，所以容器总体积不变。
    /// </summary>
    public static class PurifierMath
    {
        /// <summary>质量加成为 0 时的最高纯度（98.00%）。</summary>
        public const int PurityCapAtZeroQuality = 9800;

        /// <summary>纯度上限的封顶（100.00%）。</summary>
        public const int PurityCapMax = 10000;

        /// <summary>质量加成达到该值后纯度上限到 100%。</summary>
        public const int QualityForFullPurity = 50;

        /// <summary>性能加成为 0 时每晚的去除额度：10 mL。</summary>
        public const long BaseRemovalUl = 10000;

        /// <summary>每 1 点性能加成增加的去除额度：0.2 mL（P=50 时总额度 20 mL）。</summary>
        public const long RemovalUlPerPerformancePoint = 200;

        /// <summary>
        /// 质量加成 → 纯度上限（万分比）。
        /// 0 → 9800（98%），25 → 9900（99%），50 及以上 → 10000（100%）。
        /// </summary>
        public static int PurityCapPermyriad(int qualityBonus)
        {
            if (qualityBonus <= 0)
            {
                return PurityCapAtZeroQuality;
            }
            if (qualityBonus >= QualityForFullPurity)
            {
                return PurityCapMax;
            }
            return PurityCapAtZeroQuality
                + (PurityCapMax - PurityCapAtZeroQuality) * qualityBonus / QualityForFullPurity;
        }

        /// <summary>当前纯度（万分比）：目标液体 / 全部液体。</summary>
        public static int PurityPermyriad(int targetAmountUl, int totalVolumeUl)
        {
            if (totalVolumeUl <= 0)
            {
                return 0;
            }
            return (int)((long)targetAmountUl * 10000L / totalVolumeUl);
        }

        /// <summary>
        /// 每晚去除额度（µL）：10 mL + 0.2 mL × 性能加成。
        /// 性能为负时最多压到 1 mL，避免完全卡死。
        /// </summary>
        public static long RemovalBudgetUl(int performanceBonus)
        {
            long budget = BaseRemovalUl + (long)performanceBonus * RemovalUlPerPerformancePoint;
            if (budget < 1000L)
            {
                budget = 1000L;
            }
            return budget;
        }

        /// <summary>格式化万分比为两位小数的百分比，例如 9840 → “98.40”。</summary>
        public static string FormatPercent(int permyriad)
        {
            return (permyriad / 100.0).ToString("0.00");
        }
    }
}
