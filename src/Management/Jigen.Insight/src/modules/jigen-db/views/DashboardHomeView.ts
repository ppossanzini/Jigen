import { computed, defineComponent, onMounted, onUnmounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import DashboardMetricCard from '@/modules/jigen-db/components/DashboardMetricCard/DashboardMetricCard.vue'
import type { DashboardMetric } from '@/modules/jigen-db/types'
import { databaseService } from '@/services/databaseService'

interface WindowOption {
  label: string
  value: string
}

const WINDOW_OPTIONS: WindowOption[] = [
  { label: '1m', value: '1m' },
  { label: '5m', value: '5m' },
  { label: '10m', value: '10m' },
  { label: '1h', value: '1h' },
]

const POLLING_INTERVAL_MS = 5000
const GLOBAL_CHART_WIDTH = 760
const GLOBAL_CHART_HEIGHT = 200
const GLOBAL_CHART_PADDING_X = 20
const GLOBAL_CHART_PADDING_Y = 18

const toNumber = (value: number | null | undefined): number => {
  if (typeof value !== 'number' || Number.isNaN(value)) {
    return 0
  }

  return value
}

const formatBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return '0 B'
  }

  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const exponent = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  const value = bytes / 1024 ** exponent
  return `${value.toFixed(exponent === 0 ? 0 : 2)} ${units[exponent]}`
}

const formatDateTime = (value: string | null | undefined): string => {
  if (!value) {
    return '-'
  }

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}

const toPercentageTone = (cpuUsagePercent: number): DashboardMetric['tone'] => {
  if (cpuUsagePercent >= 85) {
    return 'magenta'
  }

  if (cpuUsagePercent >= 65) {
    return 'cyan'
  }

  return 'green'
}

const toLinePoints = (values: number[], maxValue: number): string => {
  if (!values.length) {
    return ''
  }

  const plotWidth = GLOBAL_CHART_WIDTH - GLOBAL_CHART_PADDING_X * 2
  const plotHeight = GLOBAL_CHART_HEIGHT - GLOBAL_CHART_PADDING_Y * 2
  const safeMax = maxValue > 0 ? maxValue : 1

  return values
    .map((value, index) => {
      const x = values.length === 1
        ? GLOBAL_CHART_PADDING_X
        : GLOBAL_CHART_PADDING_X + (plotWidth * index) / (values.length - 1)
      const y = GLOBAL_CHART_PADDING_Y + plotHeight - (Math.min(value, safeMax) / safeMax) * plotHeight
      return `${x.toFixed(2)},${y.toFixed(2)}`
    })
    .join(' ')
}

export default defineComponent({
  name: 'DashboardHomeView',
  components: {
    DashboardMetricCard,
  },
  setup() {
    const { t } = useI18n()

    const loading = ref(false)
    const refreshInProgress = ref(false)
    const selectedWindow = ref<string>('1h')
    const serverStatusHistory = ref<server.metrics.ServerStatusHistory | null>(null)
    let pollingTimer: ReturnType<typeof setInterval> | null = null

    const windowOptions = WINDOW_OPTIONS

    const samples = computed(() => {
      const source = serverStatusHistory.value?.samples
      return Array.isArray(source) ? source : []
    })

    const latestSample = computed<server.metrics.ServerStatusSample | null>(() => {
      if (!samples.value.length) {
        return null
      }

      return samples.value[samples.value.length - 1] ?? null
    })

    const latestDatabases = computed<server.metrics.DatabaseStatus[]>(() => {
      const databases = latestSample.value?.databases
      return Array.isArray(databases) ? databases : []
    })

    const recentSamples = computed(() => [...samples.value].reverse().slice(0, 10))
    const trendSamples = computed(() => samples.value.slice(-40))

    const lastUpdatedLabel = computed(() => formatDateTime(latestSample.value?.timestampUtc))

    const globalTrendChart = computed(() => {
      const source = trendSamples.value

      if (!source.length) {
        return {
          cpuLinePoints: '',
          memoryLinePoints: '',
          cpuMax: 0,
          memoryMaxMb: 0,
        }
      }

      const cpuSeries = source.map((entry) => toNumber(entry.cpuUsagePercent))
      const memorySeriesMb = source.map((entry) => toNumber(entry.memoryUsageBytes) / (1024 * 1024))

      const cpuMax = Math.max(...cpuSeries, 100)
      const memoryMaxMb = Math.max(...memorySeriesMb, 1)

      return {
        cpuLinePoints: toLinePoints(cpuSeries, cpuMax),
        memoryLinePoints: toLinePoints(memorySeriesMb, memoryMaxMb),
        cpuMax,
        memoryMaxMb,
      }
    })

    const dbStatusChartRows = computed(() => {
      const source = latestDatabases.value

      if (!source.length) {
        return []
      }

      const maxQueue = Math.max(...source.map((entry) => toNumber(entry.ingestionQueueLength)), 1)

      return source.map((entry) => {
        const queueValue = toNumber(entry.ingestionQueueLength)
        const queuePercent = Math.max((queueValue / maxQueue) * 100, queueValue > 0 ? 2 : 0)

        return {
          name: entry.name,
          queueValue,
          queuePercent,
          collectionsCount: toNumber(entry.collectionsCount),
          totalElementsCount: toNumber(entry.totalElementsCount),
          contentSizeBytes: toNumber(entry.contentSizeBytes),
          vectorSizeBytes: toNumber(entry.vectorSizeBytes),
        }
      })
    })

    const statusMetrics = computed<DashboardMetric[]>(() => {
      if (!latestSample.value) {
        return []
      }

      const cpuUsage = toNumber(latestSample.value.cpuUsagePercent)
      const memoryUsageBytes = toNumber(latestSample.value.memoryUsageBytes)
      const ingestionQueueLength = latestDatabases.value.reduce(
        (acc, entry) => acc + toNumber(entry.ingestionQueueLength),
        0,
      )

      return [
        {
          title: t('dashboard.cpuUsage'),
          value: `${cpuUsage.toFixed(1)}%`,
          hint: t('dashboard.latestSample'),
          tone: toPercentageTone(cpuUsage),
        },
        {
          title: t('dashboard.memoryUsage'),
          value: formatBytes(memoryUsageBytes),
          hint: t('dashboard.latestSample'),
          tone: 'cyan',
        },
        {
          title: t('dashboard.monitoredDatabases'),
          value: String(latestDatabases.value.length),
          hint: t('dashboard.latestSample'),
          tone: 'green',
        },
        {
          title: t('dashboard.indexingBacklog'),
          value: String(ingestionQueueLength),
          hint: t('dashboard.latestSample'),
          tone: ingestionQueueLength > 0 ? 'magenta' : 'green',
        },
      ]
    })

    const refreshServerStatus = async (showLoading = true) => {
      if (refreshInProgress.value) {
        return
      }

      refreshInProgress.value = true

      if (showLoading) {
        loading.value = true
      }

      try {
        serverStatusHistory.value = await databaseService.getServerStatusHistory(selectedWindow.value)
      } catch {
        ElMessage.error(t('dashboard.feedback.loadFailed'))
        serverStatusHistory.value = null
      } finally {
        if (showLoading) {
          loading.value = false
        }

        refreshInProgress.value = false
      }
    }

    const onWindowChange = async () => {
      await refreshServerStatus()
    }

    const onRefresh = async () => {
      await refreshServerStatus()
    }

    const startPolling = () => {
      if (pollingTimer) {
        clearInterval(pollingTimer)
      }

      pollingTimer = setInterval(() => {
        void refreshServerStatus(false)
      }, POLLING_INTERVAL_MS)
    }

    const stopPolling = () => {
      if (!pollingTimer) {
        return
      }

      clearInterval(pollingTimer)
      pollingTimer = null
    }

    onMounted(async () => {
      await refreshServerStatus()
      startPolling()
    })

    onUnmounted(() => {
      stopPolling()
    })

    return {
      t,
      loading,
      selectedWindow,
      windowOptions,
      statusMetrics,
      latestDatabases,
      recentSamples,
      trendSamples,
      globalTrendChart,
      dbStatusChartRows,
      lastUpdatedLabel,
      serverStatusHistory,
      onRefresh,
      onWindowChange,
      formatBytes,
      formatDateTime,
      toNumber,
      globalChartWidth: GLOBAL_CHART_WIDTH,
      globalChartHeight: GLOBAL_CHART_HEIGHT,
    }
  },
})
