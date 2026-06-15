import { computed, defineComponent, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import IndexToolbar from '@/modules/jigen-db/components/IndexToolbar/IndexToolbar.vue'
import IndexTable from '@/modules/jigen-db/components/IndexTable/IndexTable.vue'
import IndexDetailPanel from '@/modules/jigen-db/components/IndexDetailPanel/IndexDetailPanel.vue'
import type { IndexRow } from '@/modules/jigen-db/types'

const allRows: IndexRow[] = [
  {
    id: 'idx-001',
    name: 'semantic-products-v1',
    description: 'Product catalog embeddings for e-commerce recommendations',
    dimension: 512,
    metric: 'cosine',
    shardsReplicas: '8 / 2',
    status: 'Healthy',
    size: '32.4 GB',
    updatedAt: '2026-06-14 11:22',
    namespace: 'env:production',
    owner: 'owner:ml-team',
  },
  {
    id: 'idx-002',
    name: 'news-embeddings-v3',
    description: 'Multilingual news article vectors for search and clustering',
    dimension: 256,
    metric: 'dot',
    shardsReplicas: '4 / 1',
    status: 'Degraded',
    size: '4.8 GB',
    updatedAt: '2026-06-13 08:07',
    namespace: 'env:staging',
    owner: 'owner:nlp-team',
  },
  {
    id: 'idx-003',
    name: 'vision-descriptors-2026',
    description: 'Image descriptors for visual similarity and duplicate detection',
    dimension: 1024,
    metric: 'euclidean',
    shardsReplicas: '12 / 3',
    status: 'Healthy',
    size: '128.1 GB',
    updatedAt: '2026-06-14 22:45',
    namespace: 'env:production',
    owner: 'owner:cv-team',
  },
  {
    id: 'idx-004',
    name: 'support-chats-v2',
    description: 'Customer support conversation embeddings for rerank and routing',
    dimension: 384,
    metric: 'cosine',
    shardsReplicas: '6 / 1',
    status: 'Warning',
    size: '22.0 GB',
    updatedAt: '2026-06-14 06:33',
    namespace: 'env:prod',
    owner: 'owner:support-ai',
  },
  {
    id: 'idx-005',
    name: 'audio-embeddings-matching',
    description: 'Audio vectors for similarity search and recommendation',
    dimension: 768,
    metric: 'cosine',
    shardsReplicas: '10 / 2',
    status: 'Warning',
    size: '56.7 GB',
    updatedAt: '2026-06-12 17:10',
    namespace: 'env:prod',
    owner: 'owner:audio-team',
  },
  {
    id: 'idx-006',
    name: 'catalog-images-v4',
    description: 'Product image vectors for recommendation cards',
    dimension: 640,
    metric: 'cosine',
    shardsReplicas: '6 / 2',
    status: 'Healthy',
    size: '18.2 GB',
    updatedAt: '2026-06-15 01:25',
    namespace: 'env:production',
    owner: 'owner:ml-team',
  },
  {
    id: 'idx-007',
    name: 'catalog-text-v2',
    description: 'Textual embeddings for product metadata matching',
    dimension: 768,
    metric: 'dot',
    shardsReplicas: '6 / 2',
    status: 'Healthy',
    size: '26.1 GB',
    updatedAt: '2026-06-15 03:17',
    namespace: 'env:production',
    owner: 'owner:search-team',
  },
  {
    id: 'idx-008',
    name: 'returns-anomaly-v1',
    description: 'Anomaly index for return pattern detection',
    dimension: 320,
    metric: 'cosine',
    shardsReplicas: '3 / 1',
    status: 'Warning',
    size: '9.5 GB',
    updatedAt: '2026-06-14 13:49',
    namespace: 'env:ops',
    owner: 'owner:risk-team',
  },
]

export default defineComponent({
  name: 'IndexManagementView',
  components: {
    IndexToolbar,
    IndexTable,
    IndexDetailPanel,
  },
  setup() {
    const { t } = useI18n()
    const rows = ref<IndexRow[]>(allRows)
    const selectedRow = ref<IndexRow | null>(rows.value[0] ?? null)
    const currentPage = ref(1)
    const pageSize = ref(6)

    const calculateDynamicPageSize = () => {
      const reservedSpace = 470
      const rowHeight = 54
      const availableHeight = Math.max(window.innerHeight - reservedSpace, rowHeight * 4)
      pageSize.value = Math.max(4, Math.floor(availableHeight / rowHeight))
      const maxPages = Math.max(1, Math.ceil(rows.value.length / pageSize.value))
      if (currentPage.value > maxPages) currentPage.value = maxPages
    }

    const visibleRows = computed(() => {
      const start = (currentPage.value - 1) * pageSize.value
      return rows.value.slice(start, start + pageSize.value)
    })

    const onRowClick = (row: IndexRow) => {
      selectedRow.value = row
    }

    const onPageChange = (nextPage: number) => {
      currentPage.value = nextPage
      selectedRow.value = visibleRows.value[0] ?? null
    }

    const onPlaceholderAction = () => {
      ElMessage.info(t('common.customOperationsPlaceholder'))
    }

    onMounted(() => {
      calculateDynamicPageSize()
      window.addEventListener('resize', calculateDynamicPageSize)
    })

    onBeforeUnmount(() => {
      window.removeEventListener('resize', calculateDynamicPageSize)
    })

    return {
      t,
      rows,
      selectedRow,
      currentPage,
      pageSize,
      visibleRows,
      onRowClick,
      onPageChange,
      onPlaceholderAction,
    }
  },
})
