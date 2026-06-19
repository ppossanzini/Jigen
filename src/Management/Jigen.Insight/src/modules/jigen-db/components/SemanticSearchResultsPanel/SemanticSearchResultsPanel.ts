import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { SearchResultRow } from '@/modules/jigen-db/types/semanticSearch'

interface AttributeEntry {
  key: string
  value: string
}

export default defineComponent({
  name: 'SemanticSearchResultsPanel',
  props: {
    queryEmbeddingText: {
      type: String,
      required: true,
    },
    resultRows: {
      type: Array as PropType<SearchResultRow[]>,
      required: true,
    },
  },
  emits: ['copy-query-embedding', 'copy-embedding', 'copy-result-json'],
  setup(_, { emit }) {
    const toAttributeEntries = (attributes: Record<string, string>): AttributeEntry[] =>
      Object.entries(attributes).map(([key, value]) => ({ key, value }))

    const hasAttributes = (attributes: Record<string, string>): boolean =>
      toAttributeEntries(attributes).length > 0

    const formatEmbedding = (embedding: number[]): string =>
      `[${embedding.map((value) => value.toFixed(4)).join(', ')}]`

    const onCopyQueryEmbedding = () => {
      emit('copy-query-embedding')
    }

    const onCopyEmbedding = (embedding: number[]) => {
      emit('copy-embedding', embedding)
    }

    const onCopyResultJson = (row: SearchResultRow) => {
      emit('copy-result-json', row)
    }

    return {
      toAttributeEntries,
      hasAttributes,
      formatEmbedding,
      onCopyQueryEmbedding,
      onCopyEmbedding,
      onCopyResultJson,
    }
  },
})
