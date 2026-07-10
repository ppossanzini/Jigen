import { useMemo } from 'react'
import { EChart, type EChartOption } from '@/components/charts/echart'
import { formatBytes } from '@/lib/utils'
import type { DatabaseStatus } from '@/lib/api-types'

interface DatabaseSizesChartProps {
  databases: DatabaseStatus[]
}

export function DatabaseSizesChart({ databases }: DatabaseSizesChartProps) {
  const option = useMemo<EChartOption>(() => {
    return {
      tooltip: {
        trigger: 'axis',
        valueFormatter: (value) => formatBytes(value as number),
      },
      legend: {
        data: ['Content', 'Vectors', 'Index'],
        top: 8,
      },
      xAxis: {
        type: 'category',
        data: databases.map((d) => d.name ?? ''),
      },
      yAxis: {
        type: 'value',
        axisLabel: {
          formatter: (value: number) => formatBytes(value),
          hideOverlap: true,
        },
      },
      series: [
        {
          name: 'Content',
          type: 'bar',
          data: databases.map((d) => Number(d.contentSizeBytes ?? 0)),
        },
        {
          name: 'Vectors',
          type: 'bar',
          data: databases.map((d) => Number(d.vectorSizeBytes ?? 0)),
        },
        {
          name: 'Index',
          type: 'bar',
          data: databases.map((d) => Number(d.indexSizeBytes ?? 0)),
        },
      ],
    }
  }, [databases])

  if (databases.length === 0) {
    return <p className='text-muted-foreground text-sm'>No databases yet.</p>
  }

  return <EChart option={option} className='h-80 w-full' notMerge />
}
