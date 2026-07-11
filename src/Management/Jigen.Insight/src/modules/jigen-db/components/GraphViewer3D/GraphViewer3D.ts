import { defineComponent, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { PropType } from 'vue'
import { useI18n } from 'vue-i18n'
import { DELETED_COLOR, levelColor, toDisplayKey } from '@/modules/jigen-db/utils/graphFormat'

interface Point3D {
  x: number
  y: number
  z: number
  color: string
  radius: number
  keyLabel: string
  maxLevel: number
  degree: number
  isDeleted: boolean
  isEntrypoint: boolean
}

interface Projected {
  sx: number
  sy: number
  depth: number
  scale: number
}

interface HoverNode {
  keyLabel: string
  maxLevel: number
  degree: number
  isDeleted: boolean
  isEntrypoint: boolean
}

export default defineComponent({
  name: 'GraphViewer3D',
  props: {
    graph: {
      type: Object as PropType<server.database.CollectionGraph | null>,
      default: null,
    },
  },
  setup(props) {
    const { t } = useI18n()
    const wrapperRef = ref<HTMLElement | null>(null)
    const canvasRef = ref<HTMLCanvasElement | null>(null)
    const hoverNode = ref<HoverNode | null>(null)
    const tooltipStyle = ref<Record<string, string>>({})

    let points: Point3D[] = []
    let edges: Array<[number, number]> = []

    let yaw = 0.6
    let pitch = 0.35
    let zoom = 1
    let dragging = false
    let lastX = 0
    let lastY = 0
    let dirty = true
    let rafHandle: number | null = null
    let resizeObserver: ResizeObserver | null = null

    const rebuildScene = () => {
      const graph = props.graph
      points = []
      edges = []

      if (!graph || !graph.nodes.length) {
        dirty = true
        return
      }

      const indexByPosition = new Map<number, number>()
      graph.nodes.forEach((node, index) => {
        indexByPosition.set(node.positionId, index)

        const isDeleted = Boolean(node.isDeleted)
        const isEntrypoint = node.positionId === graph.entrypointPositionId
        const degree = node.degree ?? 0

        points.push({
          x: node.position?.[0] ?? 0,
          y: node.position?.[1] ?? 0,
          z: node.position?.[2] ?? 0,
          color: isDeleted ? DELETED_COLOR : levelColor(node.maxLevel ?? 0),
          radius: Math.min(2 + degree * 0.15, 7),
          keyLabel: toDisplayKey(node.key) || String(node.positionId),
          maxLevel: node.maxLevel ?? 0,
          degree,
          isDeleted,
          isEntrypoint,
        })
      })

      for (const edge of graph.edges) {
        const sourceIndex = indexByPosition.get(edge.source)
        const targetIndex = indexByPosition.get(edge.target)
        if (sourceIndex === undefined || targetIndex === undefined) continue
        edges.push([sourceIndex, targetIndex])
      }

      dirty = true
    }

    const project = (p: { x: number; y: number; z: number }, w: number, h: number): Projected => {
      const cy = Math.cos(yaw)
      const sy = Math.sin(yaw)
      const cp = Math.cos(pitch)
      const sp = Math.sin(pitch)

      const x1 = p.x * cy + p.z * sy
      const z1 = -p.x * sy + p.z * cy
      const y2 = p.y * cp - z1 * sp
      const z2 = p.y * sp + z1 * cp

      const f = 3
      const scale = f / (f + z2)
      const base = Math.min(w, h) * 0.42 * zoom

      return {
        sx: w / 2 + x1 * scale * base,
        sy: h / 2 - y2 * scale * base,
        depth: z2,
        scale,
      }
    }

    const render = () => {
      rafHandle = requestAnimationFrame(render)
      if (!dirty) return
      dirty = false

      const canvas = canvasRef.value
      const wrapper = wrapperRef.value
      if (!canvas || !wrapper) return

      const dpr = window.devicePixelRatio || 1
      const width = wrapper.clientWidth
      const height = wrapper.clientHeight
      if (width === 0 || height === 0) return

      if (canvas.width !== width * dpr || canvas.height !== height * dpr) {
        canvas.width = width * dpr
        canvas.height = height * dpr
      }

      const ctx = canvas.getContext('2d')
      if (!ctx) return

      ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
      ctx.clearRect(0, 0, width, height)

      if (!points.length) return

      const projected = points.map((p) => project(p, width, height))

      ctx.strokeStyle = 'rgba(140, 165, 190, 0.14)'
      ctx.lineWidth = 1
      ctx.beginPath()
      for (const [a, b] of edges) {
        const pa = projected[a]
        const pb = projected[b]
        if (!pa || !pb) continue
        ctx.moveTo(pa.sx, pa.sy)
        ctx.lineTo(pb.sx, pb.sy)
      }
      ctx.stroke()

      const order = points
        .map((_, index) => index)
        .sort((a, b) => (projected[b]?.depth ?? 0) - (projected[a]?.depth ?? 0))

      for (const index of order) {
        const point = points[index]
        const proj = projected[index]
        if (!point || !proj) continue
        const radius = Math.max(1, point.radius * proj.scale * zoom)

        ctx.beginPath()
        ctx.arc(proj.sx, proj.sy, radius, 0, Math.PI * 2)
        ctx.fillStyle = point.color
        ctx.globalAlpha = point.isDeleted ? 0.5 : 1
        ctx.fill()
        ctx.globalAlpha = 1

        if (point.isEntrypoint) {
          ctx.lineWidth = 1.5
          ctx.strokeStyle = '#ffffff'
          ctx.stroke()
        }
      }
    }

    const markDirty = () => {
      dirty = true
    }

    const findNearestPoint = (clientX: number, clientY: number): { index: number; sx: number; sy: number } | null => {
      const canvas = canvasRef.value
      const wrapper = wrapperRef.value
      if (!canvas || !wrapper || !points.length) return null

      const rect = canvas.getBoundingClientRect()
      const width = wrapper.clientWidth
      const height = wrapper.clientHeight
      const mx = clientX - rect.left
      const my = clientY - rect.top

      let bestIndex = -1
      let bestDistance = 8
      let bestSx = 0
      let bestSy = 0

      points.forEach((point, index) => {
        const proj = project(point, width, height)
        const dx = proj.sx - mx
        const dy = proj.sy - my
        const distance = Math.sqrt(dx * dx + dy * dy)
        if (distance < bestDistance) {
          bestDistance = distance
          bestIndex = index
          bestSx = proj.sx
          bestSy = proj.sy
        }
      })

      return bestIndex >= 0 ? { index: bestIndex, sx: bestSx, sy: bestSy } : null
    }

    const onMouseDown = (event: MouseEvent) => {
      dragging = true
      lastX = event.clientX
      lastY = event.clientY
    }

    const onMouseMove = (event: MouseEvent) => {
      if (dragging) {
        const dx = event.clientX - lastX
        const dy = event.clientY - lastY
        lastX = event.clientX
        lastY = event.clientY
        yaw += dx * 0.008
        pitch = Math.min(1.5, Math.max(-1.5, pitch + dy * 0.008))
        hoverNode.value = null
        markDirty()
        return
      }

      const hit = findNearestPoint(event.clientX, event.clientY)
      if (!hit) {
        hoverNode.value = null
        return
      }

      const point = points[hit.index]
      if (!point) {
        hoverNode.value = null
        return
      }
      hoverNode.value = {
        keyLabel: point.keyLabel,
        maxLevel: point.maxLevel,
        degree: point.degree,
        isDeleted: point.isDeleted,
        isEntrypoint: point.isEntrypoint,
      }
      tooltipStyle.value = {
        left: `${hit.sx + 12}px`,
        top: `${hit.sy + 12}px`,
      }
    }

    const onMouseUp = () => {
      dragging = false
    }

    const onMouseLeave = () => {
      dragging = false
      hoverNode.value = null
    }

    const onWheel = (event: WheelEvent) => {
      event.preventDefault()
      zoom = Math.min(5, Math.max(0.2, zoom * Math.exp(-event.deltaY * 0.001)))
      markDirty()
    }

    watch(
      () => props.graph,
      () => {
        rebuildScene()
      },
      { deep: true },
    )

    onMounted(() => {
      rebuildScene()
      render()

      const canvas = canvasRef.value
      canvas?.addEventListener('mousedown', onMouseDown)
      canvas?.addEventListener('mousemove', onMouseMove)
      canvas?.addEventListener('mouseup', onMouseUp)
      canvas?.addEventListener('mouseleave', onMouseLeave)
      canvas?.addEventListener('wheel', onWheel, { passive: false })

      if (wrapperRef.value) {
        resizeObserver = new ResizeObserver(() => markDirty())
        resizeObserver.observe(wrapperRef.value)
      }
    })

    onBeforeUnmount(() => {
      if (rafHandle !== null) cancelAnimationFrame(rafHandle)
      resizeObserver?.disconnect()

      const canvas = canvasRef.value
      canvas?.removeEventListener('mousedown', onMouseDown)
      canvas?.removeEventListener('mousemove', onMouseMove)
      canvas?.removeEventListener('mouseup', onMouseUp)
      canvas?.removeEventListener('mouseleave', onMouseLeave)
      canvas?.removeEventListener('wheel', onWheel)
    })

    return {
      t,
      wrapperRef,
      canvasRef,
      hoverNode,
      tooltipStyle,
    }
  },
})
