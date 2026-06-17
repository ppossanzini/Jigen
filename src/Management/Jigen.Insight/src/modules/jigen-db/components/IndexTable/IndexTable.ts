import { computed, defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { DatabaseRow } from '@/modules/jigen-db/types'

export default defineComponent({
  name: 'IndexTable',
  props: {
    rows: {
      type: Array as PropType<DatabaseRow[]>,
      required: true,
    },
    currentPage: {
      type: Number,
      required: true,
    },
    pageSize: {
      type: Number,
      required: true,
    },
    total: {
      type: Number,
      required: true,
    },
    nameLabel: {
      type: String,
      required: true,
    },
    collectionsLabel: {
      type: String,
      required: true,
    },
    actionsLabel: {
      type: String,
      required: true,
    },
    readActionLabel: {
      type: String,
      required: true,
    },
    deleteActionLabel: {
      type: String,
      required: true,
    },
    deleteDisabled: {
      type: Boolean,
      required: true,
    },
    adminOnlyHint: {
      type: String,
      required: true,
    },
    perPageLabel: {
      type: String,
      required: true,
    },
  },
  emits: ['row-click', 'page-change', 'read-collections', 'delete'],
  setup(props, { emit }) {
    const onRowClick = (row: DatabaseRow) => emit('row-click', row)
    const onPageChange = (page: number) => emit('page-change', page)

    const hasReliableCount = computed(
      () => Number.isFinite(props.total) && props.total >= props.rows.length,
    )

    const visibleRowsCount = computed(() => props.rows.length)

    return {
      onRowClick,
      onPageChange,
      hasReliableCount,
      visibleRowsCount,
    }
  },
})
