import { useMemo } from 'react'
import { use as echartsUse } from 'echarts/core'
import { Grid3DComponent } from 'echarts-gl/components'
import { Scatter3DChart, Line3DChart } from 'echarts-gl/charts'
import { EChart, type EChartOption } from '@/components/charts/echart'
import { toDisplayKey } from '@/lib/base64'
import type { IndexGraphNode, IndexGraphSnapshot } from '@/lib/api-types'
import { DELETED_COLOR, levelColor } from '../utils/graph-format'

// Registered once at module scope, alongside the 2D modules registered by
// the shared `EChart` wrapper (echarts' module registry is a global/additive
// singleton, so this is safe to call from multiple feature files).
echartsUse([Grid3DComponent, Scatter3DChart, Line3DChart])

/**
 * echarts-gl has no series for "many independent 3D line segments on a fixed
 * cartesian3D grid": `lines3D` only supports globe/geo3D/mapbox3D coordinate
 * systems (verified in its source — its layout function has no cartesian3D
 * branch), and `graphGL` always recomputes its own ForceAtlas2 layout,
 * discarding the backend's PCA-projected positions. `line3D` draws a single
 * continuous polyline through all of a series' data points, so one `line3D`
 * series per edge is the only primitive that both respects our fixed node
 * positions and stays on the cartesian3D grid. Capped to keep the number of
 * series (and draw calls) bounded for larger graphs.
 */
const MAX_RENDERED_EDGES = 300

type GraphViewer3DProps = {
  graph: IndexGraphSnapshot | null
  nodeLimit?: number
  pointScale?: number
}

export function GraphViewer3D({
  graph,
  nodeLimit,
  pointScale = 1,
}: GraphViewer3DProps) {
  const { option, totalEdges, renderedEdges } = useMemo(
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

  return (
    <div className='relative h-full w-full'>
      <EChart option={option} className='h-full w-full' notMerge />
      {renderedEdges < totalEdges && (
        <p className='text-muted-foreground bg-background/80 absolute bottom-2 start-2 rounded px-2 py-1 text-xs'>
          Showing {renderedEdges} of {totalEdges} edges to keep the 3D view
          responsive.
        </p>
      )}
    </div>
  )
}

function position3D(node: IndexGraphNode | undefined): [number, number, number] {
  return [
    Number(node?.position?.[0] ?? 0),
    Number(node?.position?.[1] ?? 0),
    Number(node?.position?.[2] ?? 0),
  ]
}

function buildOption(
  graph: IndexGraphSnapshot | null,
  nodeLimit: number | undefined,
  pointScale: number
): {
  option: EChartOption | null
  totalEdges: number
  renderedEdges: number
} {
  if (!graph?.nodes || graph.nodes.length === 0) {
    return { option: null, totalEdges: 0, renderedEdges: 0 }
  }

  const nodes =
    nodeLimit != null ? graph.nodes.slice(0, nodeLimit) : graph.nodes

  const nodeById = new Map(nodes.map((node) => [Number(node.positionId), node]))

  const nodeData = nodes.map((node) => {
    const isDeleted = Boolean(node.isDeleted)
    const isEntrypoint =
      Number(node.positionId) === Number(graph.entrypointPositionId)
    const degree = Number(node.degree ?? 0)
    const level = Math.max(0, Number(node.maxLevel ?? 0))

    return {
      name: toDisplayKey(node.key) || String(node.positionId),
      value: position3D(node),
      symbolSize: Math.max(1.5, Math.min(4 + degree * 0.35, 18) * pointScale),
      itemStyle: {
        color: isDeleted ? DELETED_COLOR : levelColor(level),
        opacity: isDeleted ? 0.4 : 1,
        borderColor: isEntrypoint ? '#ffffff' : undefined,
        borderWidth: isEntrypoint ? 1 : undefined,
      },
    }
  })

  // Filter to edges between currently-rendered nodes first, so the "showing
  // X of Y" count and the MAX_RENDERED_EDGES cap both reflect what's actually
  // eligible for the current node subset, not the full unfiltered graph.
  const eligibleEdges = (graph.edges ?? []).filter(
    (edge) =>
      nodeById.has(Number(edge.source)) && nodeById.has(Number(edge.target))
  )
  const edgesToRender = eligibleEdges.slice(0, MAX_RENDERED_EDGES)

  const edgeSeries = edgesToRender.map((edge) => {
    const source = nodeById.get(Number(edge.source))!
    const target = nodeById.get(Number(edge.target))!

    return {
      type: 'line3D',
      coordinateSystem: 'cartesian3D',
      polyline: false,
      lineStyle: { color: 'rgba(140, 165, 190, 0.35)', width: 1 },
      data: [position3D(source), position3D(target)],
    }
  })

  const option = {
    animation: false,
    tooltip: {
      formatter: (params: { data?: { name?: string } }) =>
        params.data?.name ? String(params.data.name) : '',
    },
    grid3D: {
      viewControl: { autoRotate: false, projection: 'perspective' },
    },
    xAxis3D: { type: 'value', show: false },
    yAxis3D: { type: 'value', show: false },
    zAxis3D: { type: 'value', show: false },
    series: [
      {
        type: 'scatter3D',
        coordinateSystem: 'cartesian3D',
        data: nodeData,
      },
      ...edgeSeries,
    ],
    // echarts-gl ships no TypeScript definitions; see `src/types/echarts-gl.d.ts`.
  } as unknown as EChartOption

  return {
    option,
    totalEdges: eligibleEdges.length,
    renderedEdges: edgesToRender.length,
  }
}
