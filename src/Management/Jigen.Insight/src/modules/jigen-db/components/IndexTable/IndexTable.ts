import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { IndexRow } from '@/modules/jigen-db/types'

export default defineComponent({
  name: 'IndexTable',
  props: {
    rows: {
      type: Array as PropType<IndexRow[]>,
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
    dimensionLabel: {
      type: String,
      required: true,
    },
    metricLabel: {
      type: String,
      required: true,
    },
    shardsLabel: {
      type: String,
      required: true,
    },
    statusLabel: {
      type: String,
      required: true,
    },
    sizeLabel: {
      type: String,
      required: true,
    },
    updatedLabel: {
      type: String,
      required: true,
    },
    actionsLabel: {
      type: String,
      required: true,
    },
    perPageLabel: {
      type: String,
      required: true,
    },
  },
  emits: ['row-click', 'page-change', 'refresh', 'edit', 'delete'],
  setup(_, { emit }) {
    const toStatusType = (status: string) => {
      if (status === 'Healthy') return 'success'
      if (status === 'Warning') return 'warning'
      return 'danger'
    }

    const onRowClick = (row: IndexRow) => emit('row-click', row)
    const onPageChange = (page: number) => emit('page-change', page)

    return {
      toStatusType,
      onRowClick,
      onPageChange,
    }
  },
})
