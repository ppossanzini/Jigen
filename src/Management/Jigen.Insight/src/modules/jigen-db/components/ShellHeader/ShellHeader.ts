import { defineComponent, ref } from 'vue'

export default defineComponent({
  name: 'ShellHeader',
  props: {
    appName: {
      type: String,
      required: true,
    },
    workspace: {
      type: String,
      required: true,
    },
    userName: {
      type: String,
      required: true,
    },
    searchPlaceholder: {
      type: String,
      required: true,
    },
    notificationLabel: {
      type: String,
      required: true,
    },
  },
  emits: ['search', 'switch-workspace', 'notifications'],
  setup(_, { emit }) {
    const searchText = ref('')

    const onSearch = () => emit('search', searchText.value)
    const onSwitchWorkspace = () => emit('switch-workspace')
    const onNotifications = () => emit('notifications')

    return {
      searchText,
      onSearch,
      onSwitchWorkspace,
      onNotifications,
    }
  },
})
