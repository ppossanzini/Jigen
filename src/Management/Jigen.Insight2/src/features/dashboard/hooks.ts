import { useQuery } from '@tanstack/react-query'
import { getServerStatusHistory } from './api'

type ServerStatusWindow = '1m' | '5m' | '10m' | '1h'

export function useServerStatusHistory(timeWindow: ServerStatusWindow) {
  return useQuery({
    queryKey: ['metrics', 'server-status', timeWindow],
    queryFn: () => getServerStatusHistory(timeWindow),
  })
}
