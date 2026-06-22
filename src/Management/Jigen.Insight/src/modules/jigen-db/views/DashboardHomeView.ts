import { defineComponent } from 'vue'
import { useI18n } from 'vue-i18n'
import DashboardMetricCard from '@/modules/jigen-db/components/DashboardMetricCard/DashboardMetricCard.vue'
import type { DashboardMetric } from '@/modules/jigen-db/types'

export default defineComponent({
  name: 'DashboardHomeView',
  components: {
    DashboardMetricCard,
  },
  setup() {
    const { t } = useI18n()

    const statusMetrics: DashboardMetric[] = [
      { title: t('dashboard.indexingBacklog'), value: '12,430', hint: 'docs', tone: 'green' },
      { title: t('dashboard.cpuUsage'), value: '68%', hint: t('dashboard.healthy'), tone: 'green' },
      { title: t('dashboard.gpuUsage'), value: '41%', hint: t('dashboard.mid'), tone: 'magenta' },
      { title: t('dashboard.activeNodes'), value: '8', hint: t('dashboard.stable'), tone: 'cyan' },
    ]

    const searchScores = [
      { name: 'Image similarity', level: 'High', type: 'success' },
      { name: 'Semantic search', level: 'Mid', type: 'danger' },
      { name: 'Nearest neighbors', level: 'Stable', type: 'info' },
    ]

    const logs = [
      { time: '09:42:11', message: 'Indexing worker accepted batch 45321 for ImageNet Extended.' },
      { time: '09:41:02', message: 'Pipeline preprocess warning: missing metadata for 14 documents.' },
      { time: '09:39:55', message: 'Search node latency spike detected: 312ms median over 60s.' },
      { time: '09:38:10', message: 'Alert fired: GPU memory usage on node-7 reached 82%.' },
    ]

    const topQueries = [
      { query: 'sunset beach image similar', volume: '8,420' },
      { query: 'modern chair design nearest', volume: '5,903' },
      { query: 'synthetic texture match', volume: '4,112' },
      { query: 'vintage poster visual similarity', volume: '2,995' },
    ]

    const datasets = [
      { name: 'ImageNet Extended', size: '120GB - 8.4M vectors', updated: '12m ago' },
      { name: 'Product Catalog v5', size: '42GB - 2.1M vectors', updated: '1h ago' },
      { name: 'Benchmark Suite 2026', size: '9GB - 400k vectors', updated: '6h ago' },
    ]

    const alerts = [
      {
        title: 'GPU memory threshold exceeded on node-7',
        detail: 'Triggered 09:38 - action: scale-up recommended',
      },
      {
        title: 'Indexer backlog cleared for ProdSet',
        detail: 'Resolved 08:55 - no pending batches',
      },
      {
        title: 'Failed pipeline run: normalize-v2 (retry scheduled)',
        detail: 'Triggered 07:22 - 14 docs missing metadata',
      },
    ]

    return {
      t,
      statusMetrics,
      searchScores,
      logs,
      topQueries,
      datasets,
      alerts,
    }
  },
})
