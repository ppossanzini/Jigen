import { computed, defineComponent } from 'vue'
import type { PropType } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  PerCollectionMetric,
  SearchPathStep,
  SearchResultRow,
} from '@/modules/jigen-db/types/semanticSearch'

export default defineComponent({
  name: 'SemanticSearchDiagnosticsPanel',
  props: {
    searchPath: {
      type: Array as PropType<SearchPathStep[]>,
      required: true,
    },
    queryEmbeddingTimeMs: {
      type: Number as PropType<number | null>,
      default: null,
    },
    globalOperationTimeMs: {
      type: Number as PropType<number | null>,
      default: null,
    },
    resultRows: {
      type: Array as PropType<SearchResultRow[]>,
      required: true,
    },
    perCollectionMetrics: {
      type: Array as PropType<PerCollectionMetric[]>,
      required: true,
    },
  },
  setup(props) {
    const { t } = useI18n()

    const formatMs = (value: number | null): string => {
      if (value === null) {
        return t('semanticSearch.labels.notCalculated')
      }

      return `${value.toFixed(1)} ms`
    }

    const queryEmbeddingTimeLabel = computed(() => formatMs(props.queryEmbeddingTimeMs))
    const globalTimeLabel = computed(() => formatMs(props.globalOperationTimeMs))

    const fastestRow = computed<SearchResultRow | null>(() => {
      if (!props.resultRows.length) {
        return null
      }

      return [...props.resultRows].sort((left, right) => left.latencyMs - right.latencyMs)[0] ?? null
    })

    const slowestRow = computed<SearchResultRow | null>(() => {
      if (!props.resultRows.length) {
        return null
      }

      return [...props.resultRows].sort((left, right) => right.latencyMs - left.latencyMs)[0] ?? null
    })

    const highestScoreRow = computed<SearchResultRow | null>(() => {
      if (!props.resultRows.length) {
        return null
      }

      return [...props.resultRows].sort((left, right) => right.score - left.score)[0] ?? null
    })

    const lowestScoreRow = computed<SearchResultRow | null>(() => {
      if (!props.resultRows.length) {
        return null
      }

      return [...props.resultRows].sort((left, right) => left.score - right.score)[0] ?? null
    })

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

    const closestCollectionLabel = computed(() => {
      if (!props.resultRows.length) {
        return t('semanticSearch.labels.notCalculated')
      }

      const scoreByCollection = props.resultRows.reduce<Record<string, { total: number; count: number }>>(
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

    return {
      queryEmbeddingTimeLabel,
      fastestResponseLabel,
      slowestResponseLabel,
      highestScoreLabel,
      lowestScoreLabel,
      closestCollectionLabel,
      globalTimeLabel,
    }
  },
})
