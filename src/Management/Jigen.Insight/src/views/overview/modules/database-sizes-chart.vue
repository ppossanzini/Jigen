<script setup lang="ts">
import { watch } from 'vue';
import { useEcharts } from '@/hooks/common/echarts';
import { useChartTheme } from '@/hooks/common/chart-theme';
import { formatBytes } from '@/utils/format';
import { $t } from '@/locales';

defineOptions({
  name: 'DatabaseSizesChart'
});

/** One stacked series per database and storage kind */
export interface SizeSeriesGroup {
  /** Database name */
  database: string;
  /** Storage kind */
  kind: 'content' | 'vector' | 'index';
  /** Time series points: [epoch ms, bytes] */
  points: [number, number][];
}

interface Props {
  groups: SizeSeriesGroup[];
}

const props = defineProps<Props>();

const { chartColors, getBaseChartOptions, getTimeAxis, getValueAxis } = useChartTheme();

const kindLabelKey = {
  content: 'page.overview.charts.content',
  vector: 'page.overview.charts.vector',
  index: 'page.overview.charts.index'
} as const;

const { domRef, updateOptions } = useEcharts(() => {
  const base = getBaseChartOptions();

  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      trigger: 'axis',
      valueFormatter: (value: unknown) => formatBytes(Number(value))
    },
    legend: {
      ...base.legend,
      type: 'scroll',
      top: 0
    },
    grid: { left: 8, right: 16, top: 32, bottom: 8, containLabel: true },
    xAxis: getTimeAxis(),
    yAxis: {
      ...getValueAxis(),
      axisLabel: {
        ...getValueAxis().axisLabel,
        formatter: (value: number) => formatBytes(value, 0)
      }
    },
    series: props.groups.map(group => ({
      type: 'line' as const,
      // one stack per database so content/vector/index pile up per database
      stack: group.database,
      name: `${group.database} · ${$t(kindLabelKey[group.kind])}`,
      data: group.points,
      showSymbol: false,
      smooth: true,
      lineStyle: { width: 1 },
      areaStyle: {},
      animation: false
    }))
  };
});

watch([() => props.groups, chartColors], () => {
  updateOptions((_, factory) => factory());
});
</script>

<template>
  <div ref="domRef" class="size-full min-h-0"></div>
</template>
