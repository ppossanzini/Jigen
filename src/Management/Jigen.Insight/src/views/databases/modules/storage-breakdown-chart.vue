<script setup lang="ts">
import { watch } from 'vue';
import { useEcharts } from '@/hooks/common/echarts';
import { useChartTheme } from '@/hooks/common/chart-theme';
import { formatBytes, toNum } from '@/utils/format';
import { $t } from '@/locales';
import type { CollectionInfo } from '@/service/api-types';

defineOptions({
  name: 'StorageBreakdownChart'
});

interface Props {
  collections: CollectionInfo[];
}

const props = defineProps<Props>();

const { chartColors, getBaseChartOptions, getValueAxis } = useChartTheme();

const { domRef, updateOptions } = useEcharts(() => {
  const base = getBaseChartOptions();
  const names = props.collections.map(c => c.name ?? '');

  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      trigger: 'axis' as const,
      axisPointer: { type: 'shadow' as const },
      valueFormatter: (value: unknown) => formatBytes(Number(value))
    },
    legend: { ...base.legend, top: 0 },
    grid: { left: 8, right: 16, top: 32, bottom: 8, containLabel: true },
    xAxis: {
      type: 'category' as const,
      data: names,
      axisLine: { lineStyle: { color: chartColors.value.axisLine } },
      axisTick: { show: false },
      axisLabel: { color: chartColors.value.mutedText }
    },
    yAxis: {
      ...getValueAxis(),
      axisLabel: {
        ...getValueAxis().axisLabel,
        formatter: (value: number) => formatBytes(value, 0)
      }
    },
    series: [
      {
        type: 'bar' as const,
        name: $t('page.overview.charts.content'),
        stack: 'size',
        data: props.collections.map(c => toNum(c.contentSize))
      },
      {
        type: 'bar' as const,
        name: $t('page.overview.charts.vector'),
        stack: 'size',
        data: props.collections.map(c => toNum(c.vectorSize))
      },
      {
        type: 'bar' as const,
        name: $t('page.overview.charts.index'),
        stack: 'size',
        data: props.collections.map(c => toNum(c.index?.indexSizeBytes))
      }
    ]
  };
});

watch([() => props.collections, chartColors], () => {
  updateOptions((_, factory) => factory());
});
</script>

<template>
  <div ref="domRef" class="size-full min-h-0"></div>
</template>
