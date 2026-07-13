import { computed } from 'vue';
import { addColorAlpha, getPaletteColorByNumber } from '@sa/color';
import { useThemeStore } from '@/store/modules/theme';

/**
 * Shared chart theme (rule 3.6): every chart derives its colors from here — text, axis, grid and
 * tooltip colors plus the brand series palette all come from the current theme store tokens, never
 * from hex literals in chart options. Charts should watch `chartColors` and rebuild their options
 * when it changes so theme/dark-mode switches restyle them.
 */
export function useChartTheme() {
  const themeStore = useThemeStore();

  /** Effective base tokens for the active scheme (settings tokens: dark overrides light) */
  const tokens = computed(() => {
    const { light, dark } = themeStore.tokens;

    return themeStore.darkMode ? { ...light.colors, ...dark?.colors } : light.colors;
  });

  /** All theme-derived colors used by charts */
  const chartColors = computed(() => {
    const text = tokens.value['base-text'];
    const container = tokens.value.container;
    const { primary, info, success, warning, error } = themeStore.themeColors;

    return {
      /** Primary text (titles, tooltip text) */
      text,
      /** Secondary text (axis labels, legend) */
      mutedText: addColorAlpha(text, 0.65),
      /** Grid split lines */
      gridLine: addColorAlpha(text, 0.1),
      /** Axis lines and ticks */
      axisLine: addColorAlpha(text, 0.25),
      /** Tooltip background */
      tooltipBg: container,
      /** Tooltip border */
      tooltipBorder: addColorAlpha(text, 0.15),
      /** Brand series palette; extended with lighter variants for charts with many series */
      palette: [
        primary,
        info,
        success,
        warning,
        error,
        getPaletteColorByNumber(primary, 300),
        getPaletteColorByNumber(info, 300),
        getPaletteColorByNumber(success, 300),
        getPaletteColorByNumber(warning, 300),
        getPaletteColorByNumber(error, 300)
      ],
      /** Primary brand color (for single-series accents, area gradients) */
      primary
    };
  });

  /** Base option fragments to spread into every chart's options factory */
  function getBaseChartOptions() {
    const c = chartColors.value;

    return {
      color: c.palette,
      textStyle: {
        color: c.text
      },
      legend: {
        textStyle: {
          color: c.mutedText
        }
      },
      tooltip: {
        backgroundColor: c.tooltipBg,
        borderColor: c.tooltipBorder,
        textStyle: {
          color: c.text
        }
      }
    };
  }

  /** Time axis styled from theme tokens */
  function getTimeAxis() {
    const c = chartColors.value;

    return {
      type: 'time' as const,
      axisLine: { lineStyle: { color: c.axisLine } },
      axisTick: { lineStyle: { color: c.axisLine } },
      axisLabel: { color: c.mutedText },
      splitLine: { show: false }
    };
  }

  /** Value axis styled from theme tokens */
  function getValueAxis() {
    const c = chartColors.value;

    return {
      type: 'value' as const,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: c.mutedText },
      splitLine: { lineStyle: { color: c.gridLine } }
    };
  }

  return {
    chartColors,
    getBaseChartOptions,
    getTimeAxis,
    getValueAxis
  };
}
