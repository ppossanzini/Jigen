<script setup lang="ts">
import { computed, watch } from 'vue';
import { useEcharts } from '@/hooks/common/echarts';
import { useChartTheme } from '@/hooks/common/chart-theme';
import type { SearchCollectionsResult } from '@/service/api-types';
import { toNum } from '@/utils/format';
import { $t } from '@/locales';

defineOptions({
  name: 'TimingStrip'
});

interface Props {
  result: SearchCollectionsResult;
}

const props = defineProps<Props>();

function formatMs(value: number | string | null | undefined) {
  return `${toNum(value).toFixed(1)} ms`;
}

const stats = computed(() => [
  { label: $t('page.workbench.timing.embedding'), value: formatMs(props.result.embeddingsCalculationTime) },
  { label: $t('page.workbench.timing.search'), value: formatMs(props.result.searchTime) },
  { label: $t('page.workbench.timing.merge'), value: formatMs(props.result.mergeTime) },
  { label: $t('page.workbench.timing.sort'), value: formatMs(props.result.sortingTime) }
]);

const perCollection = computed(() => props.result.collectionsResults ?? []);

const { chartColors, getBaseChartOptions, getValueAxis } = useChartTheme();

const { domRef, updateOptions } = useEcharts(() => {
  const base = getBaseChartOptions();
  const names = perCollection.value.map(c => c.collection ?? '');

  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      trigger: 'axis' as const,
      axisPointer: { type: 'shadow' as const },
      valueFormatter: (value: unknown) => formatMs(Number(value))
    },
    grid: { left: 8, right: 16, top: 8, bottom: 8, containLabel: true },
    xAxis: {
      ...getValueAxis(),
      axisLabel: { ...getValueAxis().axisLabel, formatter: (value: number) => formatMs(value) }
    },
    yAxis: {
      type: 'category' as const,
      data: names,
      axisLine: { lineStyle: { color: chartColors.value.axisLine } },
      axisTick: { show: false },
      axisLabel: { color: chartColors.value.mutedText }
    },
    series: [
      {
        type: 'bar' as const,
        data: perCollection.value.map(c => toNum(c.searchTime)),
        color: chartColors.value.primary,
        barMaxWidth: 18
      }
    ]
  };
});

watch([perCollection, chartColors], () => {
  updateOptions((_, factory) => factory());
});
</script>

<template>
  <div class="flex flex-col gap-16px lg:flex-row">
    <div class="flex shrink-0 flex-nowrap items-center gap-16px overflow-x-auto">
      <div v-for="stat in stats" :key="stat.label" class="flex shrink-0 items-baseline gap-6px">
        <span class="text-12px text-gray-500">{{ stat.label }}:</span>
        <span class="text-16px font-600">{{ stat.value }}</span>
      </div>
    </div>
    <div v-if="perCollection.length > 1" class="min-h-100px min-w-0 flex-1">
      <span class="text-12px text-gray-500">{{ $t('page.workbench.timing.perCollection') }}</span>
      <div ref="domRef" class="min-h-100px w-full"></div>
    </div>
  </div>
</template>
