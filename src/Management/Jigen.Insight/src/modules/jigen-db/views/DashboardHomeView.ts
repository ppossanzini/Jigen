import { computed, defineComponent, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { BarChart, LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { init, use } from 'echarts/core'
import type { ECharts, EChartsCoreOption } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
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
const CHART_SLOTS = 40
const MAX_CHART_SLOTS = 240

const getWindowDurationMs = (windowValue: string): number => {
  switch (windowValue) {
    case '1m':
      return 60 * 1000
    case '5m':
      return 5 * 60 * 1000
    case '10m':
      return 10 * 60 * 1000
    case '1h':
    default:
      return 60 * 60 * 1000
  }
}

use([BarChart, LineChart, GridComponent, TooltipComponent, CanvasRenderer])

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

const formatChartTime = (value: string | null | undefined): string => {
  if (!value) {
    return '-'
  }

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return '-'
  }

  return date.toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
    hourCycle: 'h23',
  })
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
    const globalTrendChartRef = ref<HTMLElement | null>(null)
    let globalTrendChartInstance: ECharts | null = null
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
      const source = samples.value

      if (!source.length) {
        return {
          labels: [] as string[],
          cpuSeries: [] as Array<number | null>,
          memorySeriesMb: [] as Array<number | null>,
          cpuMax: 0,
          memoryMaxMb: 0,
        }
      }

      const windowMs = getWindowDurationMs(selectedWindow.value)
      const historySampleIntervalMs =
        (serverStatusHistory.value?.sampleIntervalSeconds ?? 0) > 0
          ? Number(serverStatusHistory.value?.sampleIntervalSeconds) * 1000
          : 0
      const latestSampleTimestamp = source
        .map((entry) => new Date(entry.timestampUtc ?? '').getTime())
        .filter((timestamp) => Number.isFinite(timestamp))
        .reduce((max, timestamp) => Math.max(max, timestamp), 0)

      const windowEndTimestamp = latestSampleTimestamp > 0 ? latestSampleTimestamp : Date.now()
      const windowStartTimestamp = windowEndTimestamp - windowMs

      const slotCount = historySampleIntervalMs > 0
        ? Math.min(MAX_CHART_SLOTS, Math.max(CHART_SLOTS, Math.floor(windowMs / historySampleIntervalMs) + 1))
        : CHART_SLOTS
      const slotStepMs = windowMs / Math.max(slotCount - 1, 1)

      const labels: string[] = []
      const cpuSeries: Array<number | null> = Array.from({ length: slotCount }, () => null)
      const memorySeriesMb: Array<number | null> = Array.from({ length: slotCount }, () => null)

      for (let index = 0; index < slotCount; index += 1) {
        labels.push(formatChartTime(new Date(windowStartTimestamp + index * slotStepMs).toISOString()))
      }

      for (const entry of source) {
        const timestamp = new Date(entry.timestampUtc ?? '').getTime()

        if (!Number.isFinite(timestamp) || timestamp < windowStartTimestamp || timestamp > windowEndTimestamp) {
          continue
        }

        const slotIndex = Math.min(
          slotCount - 1,
          Math.max(0, Math.round((timestamp - windowStartTimestamp) / slotStepMs)),
        )

        cpuSeries[slotIndex] = toNumber(entry.cpuUsagePercent)
        memorySeriesMb[slotIndex] = toNumber(entry.memoryUsageBytes) / (1024 * 1024)
      }

      const availableCpuValues = cpuSeries.filter((value): value is number => value !== null)
      const availableMemoryValues = memorySeriesMb.filter((value): value is number => value !== null)

      const cpuMax = availableCpuValues.length ? Math.max(...availableCpuValues) : 0
      const memoryMaxMb = availableMemoryValues.length ? Math.max(...availableMemoryValues) : 0

      return {
        labels,
        cpuSeries,
        memorySeriesMb,
        cpuMax,
        memoryMaxMb,
      }
    })

    const globalTrendChartOption = computed<EChartsCoreOption | null>(() => {
      const chartData = globalTrendChart.value

      if (!chartData.labels.length) {
        return null
      }

      const cpuAxisMax = Math.max(
        10,
        chartData.cpuMax > 0 ? Number((chartData.cpuMax * 1.1).toFixed(2)) : 10,
      )
      const memoryAxisMax = chartData.memoryMaxMb > 0 ? Number((chartData.memoryMaxMb * 1.1).toFixed(2)) : 1

      return {
        animation: false,
        grid: {
          top: 18,
          right: 18,
          bottom: 24,
          left: 18,
          containLabel: true,
        },
        tooltip: {
          trigger: 'axis',
          axisPointer: {
            type: 'cross',
          },
          formatter: (params: unknown) => {
            const entries = Array.isArray(params) ? params : [params]

            if (!entries.length) {
              return ''
            }

            const firstEntry = entries[0] as { axisValueLabel?: string }
            const lines: string[] = [firstEntry.axisValueLabel ?? '']

            for (const rawEntry of entries) {
              const entry = rawEntry as {
                marker?: string
                seriesName?: string
                seriesIndex?: number
                value?: number | string | null
              }

              const numericValue = typeof entry.value === 'number' ? entry.value : Number(entry.value)
              const formattedValue = Number.isFinite(numericValue) ? numericValue.toFixed(2) : '-'
              const unit = entry.seriesIndex === 0 ? '%' : ' MB'

              lines.push(`${entry.marker ?? ''}${entry.seriesName ?? ''}: ${formattedValue}${unit}`)
            }

            return lines.join('<br/>')
          },
        },
        xAxis: {
          type: 'category',
          boundaryGap: true,
          data: chartData.labels,
          axisLabel: {
            color: '#a8b6c7',
            hideOverlap: true,
          },
          axisLine: {
            lineStyle: {
              color: 'rgba(255, 255, 255, 0.2)',
            },
          },
        },
        yAxis: [
          {
            type: 'value',
            min: 10,
            max: cpuAxisMax,
            axisLabel: {
              color: '#a8b6c7',
              formatter: '{value}%',
            },
            splitLine: {
              lineStyle: {
                color: 'rgba(255, 255, 255, 0.08)',
              },
            },
          },
          {
            type: 'value',
            min: 0,
            max: memoryAxisMax,
            axisLabel: {
              color: '#a8b6c7',
              formatter: '{value} MB',
            },
            splitLine: {
              show: false,
            },
          },
        ],
        series: [
          {
            name: t('dashboard.cpuUsage'),
            type: 'bar',
            yAxisIndex: 0,
            barMaxWidth: 12,
            itemStyle: {
              color: '#7fcf4b',
              borderRadius: [4, 4, 0, 0],
            },
            data: chartData.cpuSeries,
          },
          {
            name: t('dashboard.memoryUsage'),
            type: 'line',
            yAxisIndex: 1,
            smooth: true,
            symbol: 'none',
            showSymbol: false,
            lineStyle: {
              color: '#4da5db',
              width: 2,
            },
            itemStyle: {
              color: '#4da5db',
            },
            data: chartData.memorySeriesMb,
          },
        ],
      }
    })

    const disposeGlobalTrendChart = () => {
      if (!globalTrendChartInstance) {
        return
      }

      globalTrendChartInstance.dispose()
      globalTrendChartInstance = null
    }

    const ensureGlobalTrendChart = (): ECharts | null => {
      const container = globalTrendChartRef.value

      if (!container) {
        return null
      }

      if (!globalTrendChartInstance) {
        globalTrendChartInstance = init(container)
      }

      return globalTrendChartInstance
    }

    const renderGlobalTrendChart = () => {
      const option = globalTrendChartOption.value

      if (!option) {
        disposeGlobalTrendChart()
        return
      }

      const chart = ensureGlobalTrendChart()

      if (!chart) {
        return
      }

      chart.setOption(option, true)
      chart.resize()
    }

    const onWindowResize = () => {
      globalTrendChartInstance?.resize()
    }

    watch(
      globalTrendChartOption,
      async () => {
        await nextTick()
        renderGlobalTrendChart()
      },
      { deep: true },
    )

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
          indexSizeBytes: toNumber(entry.indexSizeBytes),
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
      await nextTick()
      renderGlobalTrendChart()
      window.addEventListener('resize', onWindowResize)
      startPolling()
    })

    onUnmounted(() => {
      stopPolling()
      window.removeEventListener('resize', onWindowResize)
      disposeGlobalTrendChart()
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
      globalTrendChartRef,
      dbStatusChartRows,
      lastUpdatedLabel,
      serverStatusHistory,
      onRefresh,
      onWindowChange,
      formatBytes,
      formatDateTime,
      toNumber,
    }
  },
})
