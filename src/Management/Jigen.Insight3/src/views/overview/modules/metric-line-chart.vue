<script setup lang="ts">
import { watch } from 'vue';
import { addColorAlpha } from '@sa/color';
import { useEcharts } from '@/hooks/common/echarts';
import { useChartTheme } from '@/hooks/common/chart-theme';
import { formatBytes, formatCount, formatPercent } from '@/utils/format';

defineOptions({
  name: 'MetricLineChart'
});

interface Props {
  /** Series name (already translated) */
  seriesName: string;
  /** Time series points: [epoch ms, value] */
  points: [number, number][];
  /** Value unit, drives axis/tooltip formatting */
  unit?: 'percent' | 'bytes' | 'count';
  /** Index into the theme series palette */
  colorIndex?: number;
}

const props = withDefaults(defineProps<Props>(), {
  unit: 'count',
  colorIndex: 0
});

const { chartColors, getBaseChartOptions, getTimeAxis, getValueAxis } = useChartTheme();

function formatValue(value: number) {
  if (props.unit === 'percent') return formatPercent(value);
  if (props.unit === 'bytes') return formatBytes(value);

  return formatCount(value);
}

const { domRef, updateOptions } = useEcharts(() => {
  const base = getBaseChartOptions();
  const color = chartColors.value.palette[props.colorIndex % chartColors.value.palette.length];

  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      trigger: 'axis',
      valueFormatter: (value: unknown) => formatValue(Number(value))
    },
    grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
    xAxis: getTimeAxis(),
    yAxis: {
      ...getValueAxis(),
      max: props.unit === 'percent' ? 100 : undefined,
      axisLabel: {
        ...getValueAxis().axisLabel,
        formatter: (value: number) => formatValue(value)
      }
    },
    series: [
      {
        type: 'line',
        name: props.seriesName,
        data: props.points,
        color,
        showSymbol: false,
        smooth: true,
        lineStyle: { width: 2 },
        areaStyle: { color: addColorAlpha(color, 0.15) },
        animation: false
      }
    ]
  };
});

watch([() => props.points, chartColors], () => {
  updateOptions((_, factory) => factory());
});
</script>

<template>
  <div ref="domRef" class="size-full min-h-0"></div>
</template>
