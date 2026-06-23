import { computed, defineComponent, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { AxiosError } from 'axios'
import { useI18n } from 'vue-i18n'
import { databaseService } from '@/services/databaseService'
import { useDatabaseStore } from '@/stores/database'
import SemanticSearchControlsPanel from '@/modules/jigen-db/components/SemanticSearchControlsPanel/SemanticSearchControlsPanel.vue'
import SemanticSearchResultsPanel from '@/modules/jigen-db/components/SemanticSearchResultsPanel/SemanticSearchResultsPanel.vue'
import SemanticSearchDiagnosticsPanel from '@/modules/jigen-db/components/SemanticSearchDiagnosticsPanel/SemanticSearchDiagnosticsPanel.vue'
import type {
  PerCollectionMetric,
  SearchPathStep,
  SearchResultRow,
} from '@/modules/jigen-db/types/semanticSearch'

interface ProblemDetailsPayload {
  title?: string
  detail?: string
}

const TOP_RESULTS = 20
const TOP_RESULTS_MIN = 1
const TOP_RESULTS_MAX = 100

const toRoundedMs = (value: number): number => Number(value.toFixed(1))

const decodeBase64Utf8 = (value: string): string => {
  if (!value) {
    return ''
  }

  try {
    const binary = atob(value)
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
    return new TextDecoder().decode(bytes)
  } catch {
    return value
  }
}

const toDisplayId = (value: string): string => decodeBase64Utf8(value) || value

export default defineComponent({
  name: 'SemanticSearchView',
  components: {
    SemanticSearchControlsPanel,
    SemanticSearchResultsPanel,
    SemanticSearchDiagnosticsPanel,
  },
  setup() {
    const { t } = useI18n()
    const databaseStore = useDatabaseStore()

    const selectedDatabaseName = ref<string | null>(null)
    const selectedCollections = ref<string[]>([])
    const searchText = ref('')
    const topResults = ref(TOP_RESULTS)
    const searching = ref(false)

    const queryEmbedding = ref<number[] | null>(null)
    const resultRows = ref<SearchResultRow[]>([])
    const searchPath = ref<SearchPathStep[]>([])

    const queryEmbeddingTimeMs = ref<number | null>(null)
    const globalOperationTimeMs = ref<number | null>(null)
    const perCollectionMetrics = ref<PerCollectionMetric[]>([])

    const databaseNames = computed(() => databaseStore.databases)
    const collectionsLoading = computed(() => databaseStore.loadingCollections)

    const availableCollections = computed(() => {
      if (!selectedDatabaseName.value) {
        return []
      }

      return databaseStore.collectionsByDatabase[selectedDatabaseName.value] ?? []
    })

    const canRunSearch = computed(() => {
      if (searching.value) {
        return false
      }

      if (!selectedDatabaseName.value) {
        return false
      }

      if (!selectedCollections.value.length) {
        return false
      }

      return searchText.value.trim().length > 0
    })

    const queryEmbeddingText = computed(() => {
      if (!queryEmbedding.value) {
        return ''
      }

      return `[${queryEmbedding.value.map((value) => value.toFixed(4)).join(', ')}]`
    })

    const hasSearchResults = computed(() => resultRows.value.length > 0)

    const copyResultContentJsonToClipboard = async (row: SearchResultRow): Promise<void> => {
      if (!row.content) {
        ElMessage.warning(t('semanticSearch.feedback.noContentToCopy'))
        return
      }

      try {
        const parsedContent = JSON.parse(row.content)
        await navigator.clipboard.writeText(JSON.stringify(parsedContent, null, 2))
        ElMessage.success(t('semanticSearch.feedback.contentJsonCopied'))
      } catch {
        try {
          await navigator.clipboard.writeText(JSON.stringify(row.content, null, 2))
          ElMessage.success(t('semanticSearch.feedback.contentJsonCopied'))
        } catch {
          ElMessage.error(t('semanticSearch.feedback.contentCopyFailed'))
        }
      }
    }

    const ensureInitialData = async () => {
      await databaseStore.loadDatabases()

      if (!selectedDatabaseName.value) {
        selectedDatabaseName.value = databaseNames.value[0] ?? null
      }
    }

    const toResultRows = (
      items:
        | server.database.SearchCollectionsResult['mergedResults']
        | server.database.CollectionSearchResult['results']
        | null
        | undefined,
      fallbackSearchTimeMs = 0,
      fallbackCollection = '',
    ): SearchResultRow[] => {
      const source = Array.isArray(items) ? items : []

      return source
        .map((item, index): SearchResultRow => {
          const collection = item.collection || fallbackCollection || t('semanticSearch.labels.notCalculated')
          const rawScore = Number(item.score ?? 0)
          const score = Number.isFinite(rawScore) ? Number(rawScore.toFixed(4)) : 0
          const key = item.key ? toDisplayId(item.key) : ''
          const content = (() => {
            if (item.content === null || item.content === undefined) {
              return ''
            }

            try {
              return JSON.stringify(item.content)
            } catch {
              return String(item.content)
            }
          })()

          return {
            id: key || `${collection}-${index + 1}`,
            collection,
            attributes: {
              searchTime: `${toRoundedMs(fallbackSearchTimeMs)} ms`,
            },
            content,
            score,
            responseEmbedding: [],
            latencyMs: toRoundedMs(fallbackSearchTimeMs),
          }
        })
        .sort((left, right) => right.score - left.score)
    }

    const toErrorMessage = (error: unknown): string => {
      const typedError = error as AxiosError<ProblemDetailsPayload>
      const details = typedError.response?.data

      if (details?.detail && details.detail.length > 0) {
        return details.detail
      }

      if (details?.title && details.title.length > 0) {
        return details.title
      }

      return t('semanticSearch.feedback.searchFailed')
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
        const embeddings = await databaseService.calculateEmbeddings(normalizedQuery)
        const searchResult = await databaseService.searchCollections(selectedDatabaseName.value, {
          collections: selectedCollections.value,
          sentence: normalizedQuery,
          top: topResults.value,
        })

        queryEmbedding.value = embeddings
        const mergedResults = Array.isArray(searchResult.mergedResults) ? searchResult.mergedResults : []
        const collectionsResults: server.database.CollectionSearchResult[] = Array.isArray(searchResult.collectionsResults)
          ? searchResult.collectionsResults
          : []

        const mappedRows = mergedResults.length > 0
          ? toResultRows(mergedResults, Number(searchResult.searchTime ?? 0))
          : collectionsResults.flatMap((entry) => {
              const results = Array.isArray(entry.results) ? entry.results : []
              return toResultRows(results, Number(entry.searchTime ?? 0), entry.collection ?? '')
            })

        resultRows.value = mappedRows

        const embeddingsCalculationTimeMs = Number(searchResult.embeddingsCalculationTime ?? 0)
        const searchTimeMs = Number(searchResult.searchTime ?? 0)
        const mergeTimeMs = Number(searchResult.mergeTime ?? 0)
        const sortingTimeMs = Number(searchResult.sortingTime ?? 0)

        queryEmbeddingTimeMs.value = toRoundedMs(embeddingsCalculationTimeMs)
        globalOperationTimeMs.value = toRoundedMs(
          embeddingsCalculationTimeMs + searchTimeMs + mergeTimeMs + sortingTimeMs,
        )
        perCollectionMetrics.value = collectionsResults
          .map((entry: server.database.CollectionSearchResult) => {
            const searchTime = Number(entry.searchTime ?? 0)
            const resultsCount = Array.isArray(entry.results) ? entry.results.length : 0

            return {
              collection: entry.collection ?? '',
              searchTimeMs: Number.isFinite(searchTime) ? searchTime : 0,
              resultsCount,
            }
          })
          .sort(
            (left: { searchTimeMs: number }, right: { searchTimeMs: number }) => right.searchTimeMs - left.searchTimeMs,
          )

        searchPath.value = [
          {
            key: 'collect-input',
            title: t('semanticSearch.pathSteps.collectInput'),
            detail: `${selectedCollections.value.length} ${t('semanticSearch.labels.collections').toLowerCase()} selected`,
            elapsedMs: 1,
          },
          {
            key: 'build-embedding',
            title: t('semanticSearch.pathSteps.buildEmbedding'),
            detail: t('semanticSearch.labels.queryEmbedding'),
            elapsedMs: toRoundedMs(embeddingsCalculationTimeMs),
          },
          {
            key: 'collections-dispatch',
            title: t('semanticSearch.pathSteps.collectionsDispatch'),
            detail: `${selectedCollections.value.join(', ')}`,
            elapsedMs: toRoundedMs(searchTimeMs),
          },
          {
            key: 'rank-results',
            title: t('semanticSearch.pathSteps.rankResults'),
            detail: `${resultRows.value.length} ${t('semanticSearch.labels.resultCount').toLowerCase()}`,
            elapsedMs: toRoundedMs(sortingTimeMs),
          },
          {
            key: 'deliver-output',
            title: t('semanticSearch.pathSteps.deliverOutput'),
            detail: `${t('semanticSearch.labels.globalTime')}: ${globalOperationTimeMs.value.toFixed(1)} ms`,
            elapsedMs: toRoundedMs(mergeTimeMs),
          },
        ]

        if (!resultRows.value.length) {
          ElMessage.warning(t('semanticSearch.feedback.noResults'))
          return
        }

        ElMessage.success(t('semanticSearch.feedback.searchCompleted'))
      } catch (error) {
        perCollectionMetrics.value = []
        ElMessage.error(toErrorMessage(error))
      } finally {
        searching.value = false
      }
    }

    const onSearchTextEnter = async () => {
      if (!canRunSearch.value) {
        return
      }

      await onRunSearch()
    }

    const onClear = () => {
      searchText.value = ''
      queryEmbedding.value = null
      resultRows.value = []
      searchPath.value = []
      queryEmbeddingTimeMs.value = null
      globalOperationTimeMs.value = null
      perCollectionMetrics.value = []
    }

    const onUpdateSelectedDatabaseName = (value: string | null) => {
      selectedDatabaseName.value = value
    }

    const onUpdateSelectedCollections = (value: string[]) => {
      selectedCollections.value = value
    }

    const onUpdateSearchText = (value: string) => {
      searchText.value = value
    }

    const onUpdateTopResults = (value: number) => {
      topResults.value = value
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
      topResults,
      topResultsMin: TOP_RESULTS_MIN,
      topResultsMax: TOP_RESULTS_MAX,
      searching,
      databaseNames,
      collectionsLoading,
      availableCollections,
      queryEmbeddingText,
      hasSearchResults,
      resultRows,
      searchPath,
      queryEmbeddingTimeMs,
      globalOperationTimeMs,
      perCollectionMetrics,
      canRunSearch,
      onRunSearch,
      onSearchTextEnter,
      onClear,
      copyResultContentJsonToClipboard,
      onUpdateSelectedDatabaseName,
      onUpdateSelectedCollections,
      onUpdateSearchText,
      onUpdateTopResults,
    }
  },
})
