import { computed, defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { SecurityUser } from '@/stores/security'

export default defineComponent({
  name: 'SecurityUsersTable',
  props: {
    rows: {
      type: Array as PropType<SecurityUser[]>,
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
    loading: {
      type: Boolean,
      required: true,
    },
    userNameLabel: {
      type: String,
      required: true,
    },
    idLabel: {
      type: String,
      required: true,
    },
    actionsLabel: {
      type: String,
      required: true,
    },
    openLabel: {
      type: String,
      required: true,
    },
    perPageLabel: {
      type: String,
      required: true,
    },
    emptyLabel: {
      type: String,
      required: true,
    },
  },
  emits: ['row-click', 'page-change', 'open'],
  setup(props, { emit }) {
    const onRowClick = (row: SecurityUser) => emit('row-click', row)
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
