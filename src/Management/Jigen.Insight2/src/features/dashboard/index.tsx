import { useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { ConfigDrawer } from '@/components/config-drawer'
import { ConfigSidebar } from '@/components/layout/config-sidebar'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { Skeleton } from '@/components/ui/skeleton'
import { ThemeSwitch } from '@/components/theme-switch'
import { useServerStatusHistory } from './hooks'
import { StatCards } from './components/stat-cards'
import { CpuMemoryChart } from './components/cpu-memory-chart'
import { DatabaseSizesChart } from './components/database-sizes-chart'

type TimeWindow = '1m' | '5m' | '10m' | '1h'

export function Dashboard() {
  const [timeWindow, setTimeWindow] = useState<TimeWindow>('5m')
  const { data, isLoading } = useServerStatusHistory(timeWindow)

  const samples = data?.samples ?? []
  const latestSample = samples[samples.length - 1]
  const latestDatabases = latestSample?.databases ?? []

  return (
    <>
      {/* ===== Top Heading ===== */}
      <Header>
        <div className='me-auto' />
        <ThemeSwitch />
        <ConfigDrawer />
        <ProfileDropdown />
      </Header>

      {/* ===== Main ===== */}
      <Main fluid className='flex flex-1 flex-col gap-4 sm:gap-6 lg:flex-row lg:items-start'>
        <div className='flex min-w-0 flex-1 flex-col gap-4 sm:gap-6'>
          <StatCards sample={latestSample} />

          <div className='grid gap-4 lg:grid-cols-2'>
            <Card>
              <CardHeader>
                <CardTitle>CPU & Memory</CardTitle>
              </CardHeader>
              <CardContent>
                {isLoading ? (
                  <Skeleton className='h-80 w-full' />
                ) : (
                  <CpuMemoryChart samples={samples} />
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Storage by Database</CardTitle>
              </CardHeader>
              <CardContent>
                {isLoading ? (
                  <Skeleton className='h-80 w-full' />
                ) : (
                  <DatabaseSizesChart databases={latestDatabases} />
                )}
              </CardContent>
            </Card>
          </div>
        </div>

        <ConfigSidebar>
          <div className='space-y-2'>
            <Label>Time window</Label>
            <Select
              value={timeWindow}
              onValueChange={(val) => setTimeWindow(val as TimeWindow)}
            >
              <SelectTrigger className='w-full'>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value='1m'>Last minute</SelectItem>
                <SelectItem value='5m'>Last 5 minutes</SelectItem>
                <SelectItem value='10m'>Last 10 minutes</SelectItem>
                <SelectItem value='1h'>Last hour</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </ConfigSidebar>
      </Main>
    </>
  )
}
