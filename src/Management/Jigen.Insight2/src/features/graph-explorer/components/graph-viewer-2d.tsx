import { useMemo } from 'react'
import { EChart, type EChartOption } from '@/components/charts/echart'
import { toDisplayKey } from '@/lib/base64'
import type { IndexGraphSnapshot } from '@/lib/api-types'
import { DELETED_COLOR, levelColor } from '../utils/graph-format'

const POSITION_SCALE = 500

type GraphViewer2DProps = {
  graph: IndexGraphSnapshot | null
  nodeLimit?: number
  pointScale?: number
}

export function GraphViewer2D({
  graph,
  nodeLimit,
  pointScale = 1,
}: GraphViewer2DProps) {
  const option = useMemo(
    () => buildOption(graph, nodeLimit, pointScale),
    [graph, nodeLimit, pointScale]
  )

  if (!option) {
    return (
      <p className='text-muted-foreground flex h-full items-center justify-center text-sm'>
        No graph data.
      </p>
    )
  }

  return <EChart option={option} className='h-full w-full' notMerge />
}

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

function buildOption(
  graph: IndexGraphSnapshot | null,
  nodeLimit: number | undefined,
  pointScale: number
): EChartOption | null {
  if (!graph?.nodes || graph.nodes.length === 0) return null

  const nodes =
    nodeLimit != null ? graph.nodes.slice(0, nodeLimit) : graph.nodes

  const maxLevel = Math.max(0, Number(graph.maxLevel ?? 0))
  const categories = Array.from({ length: maxLevel + 1 }, (_, level) => ({
    name: `L${level}`,
    itemStyle: { color: levelColor(level) },
  }))

  const data: GraphNodeDatum[] = nodes.map((node) => {
    const isDeleted = Boolean(node.isDeleted)
    const isEntrypoint =
      Number(node.positionId) === Number(graph.entrypointPositionId)
    const itemStyle: Record<string, unknown> = {}
    if (isDeleted) {
      itemStyle.color = DELETED_COLOR
      itemStyle.opacity = 0.5
    } else if (isEntrypoint) {
      itemStyle.borderColor = '#ffffff'
      itemStyle.borderWidth = 2
    }
    const degree = Number(node.degree ?? 0)

    return {
      id: String(node.positionId),
      name: toDisplayKey(node.key),
      x: Number(node.position?.[0] ?? 0) * POSITION_SCALE,
      y: Number(node.position?.[1] ?? 0) * POSITION_SCALE,
      category: Math.max(0, Number(node.maxLevel ?? 0)),
      symbolSize: Math.max(1.5, Math.min(4 + degree * 0.35, 18) * pointScale),
      value: degree,
      itemStyle: Object.keys(itemStyle).length ? itemStyle : undefined,
      isDeleted,
      isEntrypoint,
    }
  })

  const nodeIds = new Set(data.map((node) => node.id))
  const links = (graph.edges ?? [])
    .filter(
      (edge) =>
        nodeIds.has(String(edge.source)) && nodeIds.has(String(edge.target))
    )
    .map((edge) => ({
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
          `Key: ${entry.data.name || entry.data.id}`,
          `Layer: ${entry.data.category}`,
          `Degree: ${entry.data.value}`,
        ]
        if (entry.data.isDeleted) lines.push('Deleted')
        if (entry.data.isEntrypoint) lines.push('Entrypoint')
        return lines.join('<br/>')
      },
    },
    legend:
      categories.length > 1
        ? [{ data: categories.map((c) => c.name) }]
        : undefined,
    series: [
      {
        type: 'graph',
        layout: 'force',
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
