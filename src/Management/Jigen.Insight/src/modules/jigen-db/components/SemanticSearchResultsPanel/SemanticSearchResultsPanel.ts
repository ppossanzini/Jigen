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
  emits: ['copy-result-content-json'],
  setup(_, { emit }) {
    const toAttributeEntries = (attributes: Record<string, string>): AttributeEntry[] =>
      Object.entries(attributes).map(([key, value]) => ({ key, value }))

    const hasAttributes = (attributes: Record<string, string>): boolean =>
      toAttributeEntries(attributes).length > 0

    const onCopyResultContentJson = (row: SearchResultRow) => {
      emit('copy-result-content-json', row)
    }

    return {
      toAttributeEntries,
      hasAttributes,
      onCopyResultContentJson,
    }
  },
})
