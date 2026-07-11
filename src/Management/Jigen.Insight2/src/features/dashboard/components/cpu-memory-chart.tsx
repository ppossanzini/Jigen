import { useMemo } from 'react'
import { EChart, type EChartOption } from '@/components/charts/echart'
import { formatBytes } from '@/lib/utils'
import type { ServerStatusSample } from '@/lib/api-types'

interface CpuMemoryChartProps {
  samples: ServerStatusSample[]
}

export function CpuMemoryChart({ samples }: CpuMemoryChartProps) {
  const option = useMemo<EChartOption>(() => {
    return {
      tooltip: {
        trigger: 'axis',
      },
      legend: {
        data: ['CPU %', 'Memory'],
        top: 8,
      },
      xAxis: {
        type: 'time',
      },
      yAxis: [
        {
          type: 'value',
          min: 0,
          max: 100,
          axisLabel: { formatter: '{value}%', hideOverlap: true },
        },
        {
          type: 'value',
          scale: true,
          axisLabel: {
            formatter: (value: number) => formatBytes(value),
            hideOverlap: true,
          },
          splitLine: { show: false },
        },
      ],
      series: [
        {
          name: 'CPU %',
          type: 'line',
          smooth: true,
          showSymbol: false,
          yAxisIndex: 0,
          data: samples.map((s) => [
            new Date(s.timestampUtc ?? '').getTime(),
            Number(s.cpuUsagePercent ?? 0),
          ]),
        },
        {
          name: 'Memory',
          type: 'line',
          smooth: true,
          showSymbol: false,
          yAxisIndex: 1,
          data: samples.map((s) => [
            new Date(s.timestampUtc ?? '').getTime(),
            Number(s.memoryUsageBytes ?? 0),
          ]),
        },
      ],
    }
  }, [samples])

  if (samples.length === 0) {
    return <p className='text-muted-foreground text-sm'>No data yet.</p>
  }

  return <EChart option={option} className='h-80 w-full' notMerge />
}
