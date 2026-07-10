import { formatBytes, formatNumber } from '@/lib/utils'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import type { ServerStatusSample } from '@/lib/api-types'

interface StatCardsProps {
  sample: ServerStatusSample | undefined
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <Card className='gap-1 py-3'>
      <CardHeader className='px-3'>
        <CardTitle className='text-muted-foreground text-xs font-normal'>
          {label}
        </CardTitle>
      </CardHeader>
      <CardContent className='px-3 text-lg font-semibold'>{value}</CardContent>
    </Card>
  )
}

export function StatCards({ sample }: StatCardsProps) {
  const cpuUsage =
    sample === undefined ? '—' : `${Number(sample.cpuUsagePercent ?? 0).toFixed(1)}%`

  const memoryUsage = formatBytes(sample?.memoryUsageBytes)

  const databasesCount =
    sample === undefined ? '—' : formatNumber(sample.databases?.length ?? 0)

  const totalElements =
    sample === undefined
      ? '—'
      : formatNumber(
          (sample.databases ?? []).reduce((sum, db) => {
            return sum + Number(db.totalElementsCount ?? 0)
          }, 0)
        )

  return (
    <div className='grid gap-4 sm:grid-cols-2 lg:grid-cols-4'>
      <StatCard label='CPU Usage' value={cpuUsage} />
      <StatCard label='Memory Usage' value={memoryUsage} />
      <StatCard label='Databases' value={databasesCount} />
      <StatCard label='Total Elements' value={totalElements} />
    </div>
  )
}
