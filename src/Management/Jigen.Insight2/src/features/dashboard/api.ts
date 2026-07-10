import { apiClient } from '@/lib/api-client'
import type { ServerStatusHistory } from '@/lib/api-types'

type ServerStatusWindow = '1m' | '5m' | '10m' | '1h'

export async function getServerStatusHistory(
  timeWindow: ServerStatusWindow,
): Promise<ServerStatusHistory> {
  const response = await apiClient.get<ServerStatusHistory>(
    `/metric/server-status/${timeWindow}`,
  )
  return response.data
}
