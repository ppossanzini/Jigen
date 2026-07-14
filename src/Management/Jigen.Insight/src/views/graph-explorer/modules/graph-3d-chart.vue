<script setup lang="ts">
import { computed, watch } from 'vue';
// side-effect: registers scatter3D/grid3D (echarts-gl) on top of the base echarts/core set
import '@/hooks/common/echarts-graph';
import { useEcharts } from '@/hooks/common/echarts';
import type { ECOption } from '@/hooks/common/echarts';
import { useChartTheme } from '@/hooks/common/chart-theme';
import type { IndexGraphSnapshot } from '@/service/api-types';
import { toNum } from '@/utils/format';
import { decodeKey } from '@/utils/key-codec';
import { $t } from '@/locales';
import { prepareGraphData } from './graph-data';
import type { PreparedNode } from './graph-data';

defineOptions({
  name: 'Graph3DChart'
});

interface Props {
  snapshot: IndexGraphSnapshot;
  /** Search-result keys (base64, same format as `IndexGraphNode.key`) → score, for highlighting */
  matches?: Map<string, number> | null;
}

const props = defineProps<Props>();

const emit = defineEmits<{ nodeClick: [node: PreparedNode] }>();

const { chartColors, getSequentialShades } = useChartTheme();

const maxLevel = computed(() => toNum(props.snapshot.maxLevel));

// Match highlighting uses a color channel ('success') distinct from the level ramp ('primary')
// so the two scales stay visually separable, ranked so the best score is the most intense shade.
const matchShades = computed(() => {
  const count = props.matches?.size ?? 0;

  return count ? getSequentialShades(count, 'success').reverse() : [];
});

const prepared = computed(() => {
  const shades = getSequentialShades(maxLevel.value + 1);

  return prepareGraphData(props.snapshot, shades, chartColors.value.deleted, props.matches ?? undefined, matchShades.value);
});

function nodeTooltip(node: PreparedNode) {
  const lines = [
    `${$t('page.graph-explorer.chart.tooltipPosition')}: #${node.positionId}`,
    node.key ? `${$t('page.graph-explorer.chart.tooltipKey')}: ${decodeKey(node.key)}` : '',
    `${$t('page.graph-explorer.chart.tooltipLevel')}: ${node.maxLevel}`,
    `${$t('page.graph-explorer.chart.tooltipDegree')}: ${node.degree}`,
    node.isMatch ? `${$t('page.graph-explorer.chart.tooltipScore')}: ${node.matchScore?.toFixed(4)}` : '',
    node.isDeleted ? $t('page.graph-explorer.chart.tooltipDeleted') : ''
  ].filter(Boolean);

  return lines.join('<br/>');
}

// Edges are NOT drawn with echarts-gl's `lines3D` series: its layout stage
// (lines3DLayout.js) only implements 'globe' | 'geo3D' | 'mapbox3D' | 'maptalks3D'
// coordinate systems — `coordinateSystem: 'cartesian3D'` (the grid3D used here for the PCA
// projection) is not one of them, and `getItemLayout()` comes back `undefined`, crashing the
// renderer (verified against the installed echarts-gl@2.1.0). Native 3D line segments on an
// arbitrary cartesian3D grid aren't offered by this library, so edges are approximated as a
// second scatter3D series of faint points sampled along each source→target segment — same
// per-level color/density signal as the 2D view's edges, without the unsupported series type.
const EDGE_SAMPLES = 6;

function buildOptions(): ECOption {
  const c = chartColors.value;
  const { nodes, edges, hasMatches } = prepared.value;

  const axisStyle = {
    axisLine: { lineStyle: { color: c.axisLine } },
    axisLabel: { textStyle: { color: c.mutedText } },
    splitLine: { lineStyle: { color: [c.gridLine] } },
    axisPointer: { label: { textStyle: { color: c.text, backgroundColor: c.tooltipBg } } }
  };

  const scatterData = nodes.map(node => ({
    name: node.isEntrypoint ? $t('page.graph-explorer.chart.entrypointLabel') : node.id,
    value: [node.x, node.y, node.z ?? 0],
    symbolSize: node.symbolSize,
    itemStyle: {
      color: node.color,
      borderColor: node.isMatch || node.isEntrypoint ? c.text : 'transparent',
      borderWidth: node.isMatch ? 1.5 : node.isEntrypoint ? 2 : 0,
      opacity: node.isDeleted ? 0.4 : hasMatches && !node.isMatch ? 0.2 : 0.9
    },
    label: { show: node.isEntrypoint, formatter: () => $t('page.graph-explorer.chart.entrypointLabel') },
    raw: node
  }));

  const nodeById = new Map(nodes.map(node => [node.id, node]));

  const edgeDotsData: { value: number[]; itemStyle: { color: string; opacity: number }; symbolSize: number }[] = [];

  for (const edge of edges) {
    const source = nodeById.get(edge.sourceId);
    const target = nodeById.get(edge.targetId);
    if (!source || !target) continue;

    for (let step = 1; step < EDGE_SAMPLES; step += 1) {
      const t = step / EDGE_SAMPLES;

      edgeDotsData.push({
        value: [
          source.x + (target.x - source.x) * t,
          source.y + (target.y - source.y) * t,
          (source.z ?? 0) + ((target.z ?? 0) - (source.z ?? 0)) * t
        ],
        itemStyle: { color: edge.color, opacity: hasMatches ? 0.12 : 0.3 },
        symbolSize: Math.max(2, edge.width)
      });
    }
  }

  const options = {
    tooltip: {
      backgroundColor: c.tooltipBg,
      borderColor: c.tooltipBorder,
      textStyle: { color: c.text },
      formatter: (params: { data: { raw?: PreparedNode } }) => (params.data.raw ? nodeTooltip(params.data.raw) : '')
    },
    xAxis3D: { type: 'value', ...axisStyle },
    yAxis3D: { type: 'value', ...axisStyle },
    zAxis3D: { type: 'value', ...axisStyle },
    grid3D: {
      viewControl: { distance: 220, alpha: 20, beta: 30, autoRotate: false },
      axisLine: { lineStyle: { color: c.axisLine } },
      axisLabel: { textStyle: { color: c.mutedText } },
      splitLine: { lineStyle: { color: [c.gridLine] } },
      light: { main: { intensity: 1.2 }, ambient: { intensity: 0.4 } }
    },
    series: [
      {
        type: 'scatter3D',
        coordinateSystem: 'cartesian3D',
        silent: true,
        symbolSize: 2,
        data: edgeDotsData
      },
      {
        type: 'scatter3D',
        coordinateSystem: 'cartesian3D',
        data: scatterData
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
