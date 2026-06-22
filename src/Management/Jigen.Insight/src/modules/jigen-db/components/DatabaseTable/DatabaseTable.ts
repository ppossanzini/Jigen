import { defineComponent } from 'vue'
import type { PropType } from 'vue'

export default defineComponent({
  name: 'DatabaseTable',
  props: {
    rows: {
      type: Array as PropType<server.database.DatabaseName[]>,
      required: true,
    },
    collectionsCountByDatabase: {
      type: Object as PropType<Record<string, number>>,
      required: true,
    },
    selectedName: {
      type: String,
      default: null,
    },
  },
  emits: ['row-click'],
  setup(props, { emit }) {
    const onRowClick = (row: server.database.DatabaseName) => emit('row-click', row)

    return {
      onRowClick,
    }
  },
})
