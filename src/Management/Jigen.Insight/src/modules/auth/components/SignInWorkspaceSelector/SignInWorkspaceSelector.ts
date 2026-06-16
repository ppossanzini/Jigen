import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { WorkspaceOption } from '~types/auth'

export default defineComponent({
  name: 'SignInWorkspaceSelector',
  props: {
    label: {
      type: String,
      required: true,
    },
    modelValue: {
      type: String,
      required: true,
    },
    options: {
      type: Array as PropType<WorkspaceOption[]>,
      required: true,
    },
  },
  emits: ['update:modelValue'],
  setup(_, { emit }) {
    const onChange = (value: string) => emit('update:modelValue', value)

    return {
      onChange,
    }
  },
})
