import { useEffect, useRef, useState } from 'react'
import { init, registerTheme, use as echartsUse } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  DatasetComponent,
  TitleComponent,
} from 'echarts/components'
import { LineChart, BarChart, GraphChart } from 'echarts/charts'
import type { ECharts, EChartsOption } from 'echarts'
import { useTheme } from '@/context/theme-provider'

// Register modules once at module level for tree-shaking
echartsUse([
  CanvasRenderer,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  DatasetComponent,
  TitleComponent,
  LineChart,
  BarChart,
  GraphChart,
])

// Minimal dark theme so charts don't render with ECharts' light defaults
// (white split lines/text) against the app's dark surfaces. Registered once;
// selected automatically by resolvedTheme unless a consumer passes `theme`.
const DARK_THEME_NAME = 'jigen-dark'
const axisDarkStyle = {
  axisLine: { lineStyle: { color: '#475569' } },
  axisLabel: { color: '#94a3b8' },
  splitLine: { lineStyle: { color: '#334155' } },
}
registerTheme(DARK_THEME_NAME, {
  backgroundColor: 'transparent',
  textStyle: { color: '#cbd5e1' },
  title: { textStyle: { color: '#e2e8f0' } },
  legend: { textStyle: { color: '#cbd5e1' } },
  tooltip: {
    backgroundColor: '#1e293b',
    borderColor: '#334155',
    textStyle: { color: '#e2e8f0' },
  },
  categoryAxis: axisDarkStyle,
  valueAxis: axisDarkStyle,
  timeAxis: axisDarkStyle,
})

export type EChartOption = EChartsOption

interface EChartProps {
  option: EChartsOption
  className?: string
  style?: React.CSSProperties
  /** Overrides the theme auto-derived from the app's light/dark mode. */
  theme?: string | object
  notMerge?: boolean
  onEvents?: Record<string, (params: unknown) => void>
}

export const EChart = ({
  option,
  className,
  style,
  theme,
  notMerge = false,
  onEvents,
}: EChartProps) => {
  const { resolvedTheme } = useTheme()
  const effectiveTheme =
    theme ?? (resolvedTheme === 'dark' ? DARK_THEME_NAME : undefined)
  const divRef = useRef<HTMLDivElement>(null)
  const chartInstanceRef = useRef<ECharts | null>(null)
  // Bumped whenever the chart instance is (re)created, so the effects below
  // that depend on a live instance (option/resize/events) re-run even when
  // their own props haven't changed but `theme` forced a dispose+init.
  const [instanceVersion, setInstanceVersion] = useState(0)

  // Initialize chart instance on theme change
  useEffect(() => {
    if (!divRef.current) return

    chartInstanceRef.current?.dispose()
    chartInstanceRef.current = init(divRef.current, effectiveTheme)
    setInstanceVersion((v) => v + 1)

    return () => {
      chartInstanceRef.current?.dispose()
      chartInstanceRef.current = null
    }
  }, [effectiveTheme])

  // Update option when it changes (or the instance was just (re)created)
  useEffect(() => {
    if (!chartInstanceRef.current) return

    chartInstanceRef.current.setOption(option, { notMerge })
  }, [option, notMerge, instanceVersion])

  // Handle resize with ResizeObserver + window.resize fallback
  useEffect(() => {
    const chartInstance = chartInstanceRef.current
    if (!divRef.current || !chartInstance) return

    const handleResize = () => {
      chartInstance.resize()
    }

    // ResizeObserver for container size changes
    const resizeObserver = new ResizeObserver(handleResize)
    resizeObserver.observe(divRef.current)

    // Window resize fallback
    window.addEventListener('resize', handleResize)

    return () => {
      resizeObserver.disconnect()
      window.removeEventListener('resize', handleResize)
    }
  }, [instanceVersion])

  // Register event listeners
  useEffect(() => {
    const chartInstance = chartInstanceRef.current
    if (!chartInstance || !onEvents) return

    const entries = Object.entries(onEvents)
    entries.forEach(([eventName, handler]) => {
      chartInstance.on(eventName, handler)
    })

    return () => {
      entries.forEach(([eventName, handler]) => {
        chartInstance.off(eventName, handler)
      })
    }
  }, [onEvents, instanceVersion])

  return <div ref={divRef} className={className} style={style} />
}
