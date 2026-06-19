import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { DatabaseRow } from '@/modules/jigen-db/types'

export default defineComponent({
  name: 'DatabaseTable',
  props: {
    rows: {
      type: Array as PropType<DatabaseRow[]>,
      required: true,
    },
    selectedName: {
      type: String,
      default: null,
    },
  },
  emits: ['row-click'],
  setup(props, { emit }) {
    const onRowClick = (row: DatabaseRow) => emit('row-click', row)

    return {
      onRowClick,
    }
  },
})
