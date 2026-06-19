import { defineComponent } from 'vue'
import type { PropType } from 'vue'

export default defineComponent({
  name: 'SemanticSearchControlsPanel',
  props: {
    databaseNames: {
      type: Array as PropType<string[]>,
      required: true,
    },
    selectedDatabaseName: {
      type: String,
      default: null,
    },
    selectedCollections: {
      type: Array as PropType<string[]>,
      required: true,
    },
    searchText: {
      type: String,
      required: true,
    },
    topResults: {
      type: Number,
      required: true,
    },
    topResultsMin: {
      type: Number,
      required: true,
    },
    topResultsMax: {
      type: Number,
      required: true,
    },
    loadingDatabases: {
      type: Boolean,
      default: false,
    },
    collectionsLoading: {
      type: Boolean,
      default: false,
    },
    availableCollections: {
      type: Array as PropType<string[]>,
      required: true,
    },
    searching: {
      type: Boolean,
      default: false,
    },
    canRunSearch: {
      type: Boolean,
      default: false,
    },
  },
  emits: [
    'update:selectedDatabaseName',
    'update:selectedCollections',
    'update:searchText',
    'update:topResults',
    'run-search',
    'clear',
    'search-enter',
  ],
  setup(_, { emit }) {
    const onUpdateDatabase = (value: string | null) => {
      emit('update:selectedDatabaseName', value)
    }

    const onUpdateCollections = (value: string[]) => {
      emit('update:selectedCollections', value)
    }

    const onUpdateSearchText = (value: string) => {
      emit('update:searchText', value)
    }

    const onUpdateTopResults = (value: number | undefined) => {
      if (typeof value !== 'number') {
        return
      }

      emit('update:topResults', value)
    }

    const onRunSearch = () => {
      emit('run-search')
    }

    const onClear = () => {
      emit('clear')
    }

    const onSearchTextEnter = () => {
      emit('search-enter')
    }

    return {
      onUpdateDatabase,
      onUpdateCollections,
      onUpdateSearchText,
      onUpdateTopResults,
      onRunSearch,
      onClear,
      onSearchTextEnter,
    }
  },
})
