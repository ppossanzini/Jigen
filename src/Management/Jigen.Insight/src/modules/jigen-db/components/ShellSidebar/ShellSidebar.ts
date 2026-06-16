import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { SidebarItem } from '@/modules/jigen-db/types'

export default defineComponent({
  name: 'ShellSidebar',
  props: {
    items: {
      type: Array as PropType<SidebarItem[]>,
      required: true,
    },
    activeKey: {
      type: String,
      required: true,
    },
    collapseLabel: {
      type: String,
      required: true,
    },
    helpLabel: {
      type: String,
      required: true,
    },
  },
  emits: ['navigate', 'collapse', 'help'],
  setup(_, { emit }) {
    const onSelect = (key: string) => emit('navigate', key)
    const onCollapse = () => emit('collapse')
    const onHelp = () => emit('help')

    return {
      onSelect,
      onCollapse,
      onHelp,
    }
  },
})
