<script setup lang="ts">
import { computed, watch } from 'vue';
// side-effect: registers the `graph` series type on top of the base echarts/core set (rule 3.6)
import '@/hooks/common/echarts-graph';
import { useEcharts } from '@/hooks/common/echarts';
import type { ECOption } from '@/hooks/common/echarts';
import { useChartTheme } from '@/hooks/common/chart-theme';
import type { IndexGraphSnapshot } from '@/service/api-types';
import { toNum } from '@/utils/format';
import { decodeKey } from '@/utils/key-codec';
import { $t } from '@/locales';
import { prepareGraphData } from './graph-data';
import type { PreparedEdge, PreparedNode } from './graph-data';

defineOptions({
  name: 'Graph2DChart'
});

interface Props {
  snapshot: IndexGraphSnapshot;
}

const props = defineProps<Props>();

const emit = defineEmits<{ nodeClick: [node: PreparedNode] }>();

const { chartColors, getSequentialShades } = useChartTheme();

const maxLevel = computed(() => toNum(props.snapshot.maxLevel));

const prepared = computed(() => {
  const shades = getSequentialShades(maxLevel.value + 1);

  return prepareGraphData(props.snapshot, shades, chartColors.value.deleted);
});

function nodeTooltip(node: PreparedNode) {
  const lines = [
    `${$t('page.graph-explorer.chart.tooltipPosition')}: #${node.positionId}`,
    node.key ? `${$t('page.graph-explorer.chart.tooltipKey')}: ${decodeKey(node.key)}` : '',
    `${$t('page.graph-explorer.chart.tooltipLevel')}: ${node.maxLevel}`,
    `${$t('page.graph-explorer.chart.tooltipDegree')}: ${node.degree}`,
    node.isDeleted ? $t('page.graph-explorer.chart.tooltipDeleted') : ''
  ].filter(Boolean);

  return lines.join('<br/>');
}

function edgeTooltip(edge: PreparedEdge) {
  return `${$t('page.graph-explorer.chart.tooltipEdgeLevel')}: ${edge.level}`;
}

function buildOptions(): ECOption {
  const c = chartColors.value;
  const shades = getSequentialShades(maxLevel.value + 1);
  const { nodes, edges } = prepared.value;

  const categories = shades.map((color, level) => ({
    name: $t('page.graph-explorer.chart.legendLevel', { level }),
    itemStyle: { color }
  }));
  const deletedCategoryIndex = categories.length;

  categories.push({
    name: $t('page.graph-explorer.chart.legendDeleted'),
    itemStyle: { color: c.deleted }
  });

  const data = nodes.map(node => ({
    id: node.id,
    name: node.isEntrypoint ? $t('page.graph-explorer.chart.entrypointLabel') : '',
    x: node.x,
    y: node.y,
    symbolSize: node.symbolSize,
    category: node.isDeleted ? deletedCategoryIndex : Math.min(node.maxLevel, maxLevel.value),
    itemStyle: {
      color: node.color,
      borderColor: node.isEntrypoint ? c.text : 'transparent',
      borderWidth: node.isEntrypoint ? 3 : 0,
      opacity: node.isDeleted ? 0.55 : 1
    },
    label: { show: node.isEntrypoint },
    raw: node
  }));

  const links = edges.map(edge => ({
    source: edge.sourceId,
    target: edge.targetId,
    lineStyle: { color: edge.color, width: edge.width, opacity: 0.5, curveness: 0 },
    raw: edge
  }));

  const options = {
    tooltip: {
      backgroundColor: c.tooltipBg,
      borderColor: c.tooltipBorder,
      textStyle: { color: c.text },
      formatter: (params: { dataType?: string; data: { raw: PreparedNode | PreparedEdge } }) =>
        params.dataType === 'edge' ? edgeTooltip(params.data.raw as PreparedEdge) : nodeTooltip(params.data.raw as PreparedNode)
    },
    legend: {
      type: 'scroll',
      bottom: 0,
      textStyle: { color: c.mutedText },
      pageTextStyle: { color: c.mutedText },
      data: categories.map(category => category.name)
    },
    series: [
      {
        type: 'graph',
        layout: 'none',
        roam: true,
        draggable: false,
        edgeSymbol: ['none', 'none'],
        label: { position: 'right', color: c.text, fontSize: 11 },
        emphasis: { focus: 'adjacency', lineStyle: { width: 3 } },
        data,
        links,
        categories
      }
    ]
  };

  return options as unknown as ECOption;
}

const { domRef, chart, updateOptions } = useEcharts(buildOptions);

watch([prepared, chartColors], () => {
  updateOptions((_, factory) => factory());
});

watch(chart, instance => {
  instance?.on('click', params => {
    if (params.dataType === 'edge') return;

    const raw = (params.data as { raw?: PreparedNode } | undefined)?.raw;
    if (raw && 'positionId' in raw) {
      emit('nodeClick', raw);
    }
  });
});
</script>

<template>
  <div ref="domRef" class="h-full min-h-320px w-full"></div>
</template>
