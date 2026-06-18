import { computed, defineComponent, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { useDatabaseStore } from '@/stores/database'

interface SearchResultRow {
  id: string
  collection: string
  attributes: Record<string, string>
  content: string
  score: number
  responseEmbedding: number[]
  latencyMs: number
}

interface SearchPathStep {
  key: string
  title: string
  detail: string
  elapsedMs: number
}

interface AttributeEntry {
  key: string
  value: string
}

const VECTOR_LENGTH = 8

const hashValue = (input: string): number => {
  let hash = 0

  for (let index = 0; index < input.length; index += 1) {
    hash = (hash << 5) - hash + input.charCodeAt(index)
    hash |= 0
  }

  return Math.abs(hash)
}

const toEmbedding = (seed: string, length = VECTOR_LENGTH): number[] =>
  Array.from({ length }, (_, index) => {
    const source = hashValue(`${seed}-${index}`)
    return Number(((source % 1000) / 1000).toFixed(4))
  })

const toRoundedMs = (value: number): number => Number(value.toFixed(1))

export default defineComponent({
  name: 'SemanticSearchView',
  setup() {
    const { t } = useI18n()
    const databaseStore = useDatabaseStore()

    const selectedDatabaseName = ref<string | null>(null)
    const selectedCollections = ref<string[]>([])
    const searchText = ref('')
    const searching = ref(false)

    const queryEmbedding = ref<number[] | null>(null)
    const resultRows = ref<SearchResultRow[]>([])
    const searchPath = ref<SearchPathStep[]>([])

    const queryEmbeddingTimeMs = ref<number | null>(null)
    const globalOperationTimeMs = ref<number | null>(null)

    const databaseNames = computed(() => databaseStore.databases.map((entry) => entry.name))
    const collectionsLoading = computed(() => databaseStore.loadingCollections)

    const availableCollections = computed(() => {
      if (!selectedDatabaseName.value) {
        return []
      }

      return databaseStore.collectionsByDatabase[selectedDatabaseName.value] ?? []
    })

    const queryEmbeddingText = computed(() => {
      if (!queryEmbedding.value) {
        return ''
      }

      return `[${queryEmbedding.value.map((value) => value.toFixed(4)).join(', ')}]`
    })

    const fastestRow = computed<SearchResultRow | null>(() => {
      if (!resultRows.value.length) {
        return null
      }

      return [...resultRows.value].sort((left, right) => left.latencyMs - right.latencyMs)[0] ?? null
    })

    const slowestRow = computed<SearchResultRow | null>(() => {
      if (!resultRows.value.length) {
        return null
      }

      return [...resultRows.value].sort((left, right) => right.latencyMs - left.latencyMs)[0] ?? null
    })

    const highestScoreRow = computed<SearchResultRow | null>(() => {
      if (!resultRows.value.length) {
        return null
      }

      return [...resultRows.value].sort((left, right) => right.score - left.score)[0] ?? null
    })

    const lowestScoreRow = computed<SearchResultRow | null>(() => {
      if (!resultRows.value.length) {
        return null
      }

      return [...resultRows.value].sort((left, right) => left.score - right.score)[0] ?? null
    })

    const closestCollectionLabel = computed(() => {
      if (!resultRows.value.length) {
        return t('semanticSearch.labels.notCalculated')
      }

      const scoreByCollection = resultRows.value.reduce<Record<string, { total: number; count: number }>>(
        (accumulator, row) => {
          let bucket = accumulator[row.collection]

          if (!bucket) {
            bucket = { total: 0, count: 0 }
            accumulator[row.collection] = bucket
          }

          bucket.total += row.score
          bucket.count += 1
          return accumulator
        },
        {},
      )

      const rankedCollection = Object.entries(scoreByCollection)
        .map(([collection, value]) => ({ collection, average: value.total / value.count }))
        .sort((left, right) => right.average - left.average)[0]

      return rankedCollection?.collection ?? t('semanticSearch.labels.notCalculated')
    })

    const formatMs = (value: number | null): string => {
      if (value === null) {
        return t('semanticSearch.labels.notCalculated')
      }

      return `${value.toFixed(1)} ms`
    }

    const formatEmbedding = (embedding: number[]): string =>
      `[${embedding.map((value) => value.toFixed(4)).join(', ')}]`

    const toAttributeEntries = (attributes: Record<string, string>): AttributeEntry[] =>
      Object.entries(attributes).map(([key, value]) => ({ key, value }))

    const toResponseLabel = (row: SearchResultRow | null, byScore = false): string => {
      if (!row) {
        return t('semanticSearch.labels.notCalculated')
      }

      if (byScore) {
        return `${row.id} (${row.score.toFixed(4)})`
      }

      return `${row.id} (${row.latencyMs} ms)`
    }

    const fastestResponseLabel = computed(() => toResponseLabel(fastestRow.value))
    const slowestResponseLabel = computed(() => toResponseLabel(slowestRow.value))
    const highestScoreLabel = computed(() => toResponseLabel(highestScoreRow.value, true))
    const lowestScoreLabel = computed(() => toResponseLabel(lowestScoreRow.value, true))

    const ensureInitialData = async () => {
      await databaseStore.loadDatabases()

      if (!selectedDatabaseName.value) {
        selectedDatabaseName.value = databaseNames.value[0] ?? null
      }
    }

    const generateRows = (query: string, collections: string[]): SearchResultRow[] => {
      const rows: SearchResultRow[] = []

      collections.forEach((collection, collectionIndex) => {
        const rowsInCollection = 2 + (hashValue(`${query}-${collection}`) % 2)

        for (let index = 0; index < rowsInCollection; index += 1) {
          const seed = `${query}-${collection}-${index}`
          const score = Number((0.58 + (hashValue(`${seed}-score`) % 390) / 1000).toFixed(4))
          const latencyMs = 24 + (hashValue(`${seed}-latency`) % 180)
          const hasContent = hashValue(`${seed}-content`) % 4 !== 0
          const idSuffix = hashValue(`${seed}-id`) % 10000

          rows.push({
            id: `${collection}-${collectionIndex + 1}-${idSuffix}`,
            collection,
            attributes: {
              source: collection,
              language: hashValue(`${seed}-lang`) % 2 === 0 ? 'it' : 'en',
              chunk: `${1 + (hashValue(`${seed}-chunk`) % 32)}`,
            },
            content: hasContent
              ? `Excerpt matching "${query}" from ${collection}. Context fragment ${1 + (hashValue(seed) % 120)}.`
              : '',
            score,
            responseEmbedding: toEmbedding(`${seed}-response`),
            latencyMs,
          })
        }
      })

      return rows.sort((left, right) => right.score - left.score)
    }

    const onRunSearch = async () => {
      const normalizedQuery = searchText.value.trim()

      if (!selectedDatabaseName.value) {
        ElMessage.warning(t('semanticSearch.feedback.databaseRequired'))
        return
      }

      if (!normalizedQuery) {
        ElMessage.warning(t('semanticSearch.feedback.queryRequired'))
        return
      }

      if (!selectedCollections.value.length) {
        ElMessage.warning(t('semanticSearch.feedback.collectionsRequired'))
        return
      }

      searching.value = true

      try {
        const embeddingStart = performance.now()
        const generatedEmbedding = toEmbedding(`query-${normalizedQuery}`)
        const syntheticEmbeddingOverhead = 8 + (hashValue(normalizedQuery) % 24)
        const embeddingDuration = toRoundedMs(performance.now() - embeddingStart + syntheticEmbeddingOverhead)

        const generatedRows = generateRows(normalizedQuery, selectedCollections.value)

        queryEmbedding.value = generatedEmbedding
        resultRows.value = generatedRows

        const maxLatency = generatedRows.length
          ? Math.max(...generatedRows.map((entry) => entry.latencyMs))
          : 0
        const rankingDuration = 9 + (hashValue(`${normalizedQuery}-rank`) % 21)
        const dispatchDuration = 10 + Math.round(maxLatency * 0.35)
        const finalizeDuration = 6 + (hashValue(`${normalizedQuery}-finalize`) % 12)

        queryEmbeddingTimeMs.value = embeddingDuration
        globalOperationTimeMs.value = toRoundedMs(
          embeddingDuration + dispatchDuration + rankingDuration + finalizeDuration,
        )

        searchPath.value = [
          {
            key: 'collect-input',
            title: t('semanticSearch.pathSteps.collectInput'),
            detail: `${selectedCollections.value.length} ${t('semanticSearch.labels.collections').toLowerCase()} selected`,
            elapsedMs: 3,
          },
          {
            key: 'build-embedding',
            title: t('semanticSearch.pathSteps.buildEmbedding'),
            detail: t('semanticSearch.labels.queryEmbedding'),
            elapsedMs: embeddingDuration,
          },
          {
            key: 'collections-dispatch',
            title: t('semanticSearch.pathSteps.collectionsDispatch'),
            detail: `${selectedCollections.value.join(', ')}`,
            elapsedMs: dispatchDuration,
          },
          {
            key: 'rank-results',
            title: t('semanticSearch.pathSteps.rankResults'),
            detail: `${generatedRows.length} ${t('semanticSearch.labels.resultCount').toLowerCase()}`,
            elapsedMs: rankingDuration,
          },
          {
            key: 'deliver-output',
            title: t('semanticSearch.pathSteps.deliverOutput'),
            detail: `${t('semanticSearch.labels.globalTime')}: ${formatMs(globalOperationTimeMs.value)}`,
            elapsedMs: finalizeDuration,
          },
        ]

        if (!generatedRows.length) {
          ElMessage.warning(t('semanticSearch.feedback.noResults'))
          return
        }

        ElMessage.success(t('semanticSearch.feedback.searchCompleted'))
      } finally {
        searching.value = false
      }
    }

    const onSelectAllCollections = () => {
      selectedCollections.value = [...availableCollections.value]
    }

    const onClear = () => {
      searchText.value = ''
      queryEmbedding.value = null
      resultRows.value = []
      searchPath.value = []
      queryEmbeddingTimeMs.value = null
      globalOperationTimeMs.value = null
    }

    watch(
      selectedDatabaseName,
      async (databaseName) => {
        selectedCollections.value = []
        onClear()

        if (!databaseName) {
          return
        }

        await databaseStore.loadCollectionsFor(databaseName)
      },
      { immediate: true },
    )

    watch(availableCollections, (collections) => {
      if (!selectedCollections.value.length) {
        return
      }

      selectedCollections.value = selectedCollections.value.filter((entry) => collections.includes(entry))
    })

    onMounted(async () => {
      await ensureInitialData()
    })

    return {
      t,
      databaseStore,
      selectedDatabaseName,
      selectedCollections,
      searchText,
      searching,
      databaseNames,
      collectionsLoading,
      availableCollections,
      queryEmbeddingText,
      resultRows,
      searchPath,
      queryEmbeddingTimeMs,
      globalOperationTimeMs,
      closestCollectionLabel,
      fastestResponseLabel,
      slowestResponseLabel,
      highestScoreLabel,
      lowestScoreLabel,
      onRunSearch,
      onSelectAllCollections,
      onClear,
      formatMs,
      formatEmbedding,
      toAttributeEntries,
    }
  },
})
