import { defineComponent, type PropType } from 'vue'
import type { SecurityRoleApiModel } from '~types/security'

export default defineComponent({
  name: 'RolesMasterTable',
  props: {
    rows: {
      type: Array as PropType<SecurityRoleApiModel[]>,
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
    roleNameLabel: {
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
    const onRowClick = (row: SecurityRoleApiModel) => emit('row-click', row)
    const onPageChange = (page: number) => emit('page-change', page)

    return {
      onRowClick,
      onPageChange,
    }
  },
})
