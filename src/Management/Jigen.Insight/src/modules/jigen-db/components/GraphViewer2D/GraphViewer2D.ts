import { defineComponent, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import type { PropType } from 'vue'
import { useI18n } from 'vue-i18n'
import { GraphChart } from 'echarts/charts'
import { LegendComponent, TooltipComponent } from 'echarts/components'
import { init, use } from 'echarts/core'
import type { ECharts, EChartsCoreOption } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { DELETED_COLOR, levelColor, toDisplayKey } from '@/modules/jigen-db/utils/graphFormat'

use([GraphChart, TooltipComponent, LegendComponent, CanvasRenderer])

const POSITION_SCALE = 500

interface GraphNodeDatum {
  id: string
  name: string
  x: number
  y: number
  category: number
  symbolSize: number
  value: number
  itemStyle?: Record<string, unknown>
  isDeleted: boolean
  isEntrypoint: boolean
}

export default defineComponent({
  name: 'GraphViewer2D',
  props: {
    graph: {
      type: Object as PropType<server.database.CollectionGraph | null>,
      default: null,
    },
    forceLayout: {
      type: Boolean,
      default: true,
    },
  },
  setup(props) {
    const { t } = useI18n()
    const chartRef = ref<HTMLElement | null>(null)
    let chartInstance: ECharts | null = null

    const buildOption = (
      graph: server.database.CollectionGraph | null,
      forceLayout: boolean,
    ): EChartsCoreOption | null => {
      if (!graph || !graph.nodes.length) return null

      const maxLevel = graph.maxLevel ?? 0
      const categories = Array.from({ length: maxLevel + 1 }, (_, level) => ({
        name: `L${level}`,
        itemStyle: { color: levelColor(level) },
      }))

      const data: GraphNodeDatum[] = graph.nodes.map((node) => {
        const isDeleted = Boolean(node.isDeleted)
        const isEntrypoint = node.positionId === graph.entrypointPositionId
        const itemStyle: Record<string, unknown> = {}
        if (isDeleted) {
          itemStyle.color = DELETED_COLOR
          itemStyle.opacity = 0.5
        } else if (isEntrypoint) {
          itemStyle.borderColor = '#ffffff'
          itemStyle.borderWidth = 2
        }

        return {
          id: String(node.positionId),
          name: toDisplayKey(node.key),
          x: (node.position?.[0] ?? 0) * POSITION_SCALE,
          y: (node.position?.[1] ?? 0) * POSITION_SCALE,
          category: Math.max(0, node.maxLevel ?? 0),
          symbolSize: Math.min(4 + (node.degree ?? 0) * 0.35, 18),
          value: node.degree ?? 0,
          itemStyle: Object.keys(itemStyle).length ? itemStyle : undefined,
          isDeleted,
          isEntrypoint,
        }
      })

      const links = graph.edges.map((edge) => ({
        source: String(edge.source),
        target: String(edge.target),
      }))

      return {
        animation: false,
        tooltip: {
          trigger: 'item',
          formatter: (params: unknown) => {
            const entry = params as { dataType?: string; data?: GraphNodeDatum }
            if (entry.dataType !== 'node' || !entry.data) return ''

            const lines = [
              `${t('graphExplorer.tooltip.key')}: ${entry.data.name || entry.data.id}`,
              `${t('graphExplorer.tooltip.layer')}: ${entry.data.category}`,
              `${t('graphExplorer.tooltip.degree')}: ${entry.data.value}`,
            ]
            if (entry.data.isDeleted) lines.push(t('graphExplorer.tooltip.deleted'))
            if (entry.data.isEntrypoint) lines.push(t('graphExplorer.tooltip.entrypoint'))
            return lines.join('<br/>')
          },
        },
        legend: categories.length > 1 ? [{ data: categories.map((c) => c.name), textStyle: { color: '#a8b6c7' } }] : undefined,
        series: [
          {
            type: 'graph',
            layout: forceLayout ? 'force' : 'none',
            roam: true,
            draggable: true,
            data,
            links,
            categories,
            lineStyle: {
              color: 'rgba(140, 165, 190, 0.25)',
              width: 1,
              curveness: 0,
            },
            emphasis: {
              focus: 'adjacency',
            },
            force: {
              repulsion: 45,
              edgeLength: [12, 60],
              gravity: 0.08,
              friction: 0.6,
            },
          },
        ],
      }
    }

    const disposeChart = () => {
      if (!chartInstance) return
      chartInstance.dispose()
      chartInstance = null
    }

    const ensureChart = (): ECharts | null => {
      const container = chartRef.value
      if (!container) return null
      if (!chartInstance) chartInstance = init(container)
      return chartInstance
    }

    const render = () => {
      const option = buildOption(props.graph, props.forceLayout)
      if (!option) {
        disposeChart()
        return
      }

      const chart = ensureChart()
      if (!chart) return
      chart.setOption(option, true)
      chart.resize()
    }

    const onWindowResize = () => {
      chartInstance?.resize()
    }

    watch(
      [() => props.graph, () => props.forceLayout],
      async () => {
        await nextTick()
        render()
      },
      { deep: true },
    )

    onMounted(async () => {
      await nextTick()
      render()
      window.addEventListener('resize', onWindowResize)
    })

    onUnmounted(() => {
      window.removeEventListener('resize', onWindowResize)
      disposeChart()
    })

    return {
      chartRef,
    }
  },
})
