import { defineComponent, type PropType } from 'vue'
import type { SecurityUserApiModel } from '~types/security'

export default defineComponent({
  name: 'UsersMasterTable',
  props: {
    rows: {
      type: Array as PropType<SecurityUserApiModel[]>,
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
    title: {
      type: String,
      required: true,
    },
    userNameLabel: {
      type: String,
      required: true,
    },
    rowsLabel: {
      type: String,
      required: true,
    },
    emptyLabel: {
      type: String,
      required: true,
    },
  },
  emits: ['row-click', 'page-change'],
  setup(_, { emit }) {
    const onRowClick = (row: SecurityUserApiModel) => emit('row-click', row)
    const onPageChange = (page: number) => emit('page-change', page)

    return {
      onRowClick,
      onPageChange,
    }
  },
})
