import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { SecurityUser } from '@/stores/security'

export default defineComponent({
  name: 'SecurityUsersTable',
  props: {
    rows: {
      type: Array as PropType<SecurityUser[]>,
      required: true,
    },
    loading: {
      type: Boolean,
      required: true,
    },
  },
  emits: ['row-click', 'open'],
  setup(_, { emit }) {
    const onRowClick = (row: SecurityUser) => emit('row-click', row)

    return {
      onRowClick,
    }
  },
})
