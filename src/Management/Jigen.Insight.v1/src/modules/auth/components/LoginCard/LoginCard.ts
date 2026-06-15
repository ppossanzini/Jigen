import { defineComponent } from 'vue'
import { computed, ref, watch } from 'vue'

interface SubmitPayload {
  userName: string
  password: string
}

interface Props {
  title: string
  tagline: string
  usernameLabel: string
  passwordLabel: string
  submitLabel: string
  loading: boolean
  errorText: string
}

export default defineComponent({
  name: 'LoginCard',
  props: {
    title: {
      type: String,
      required: true,
    },
    tagline: {
      type: String,
      required: true,
    },
    usernameLabel: {
      type: String,
      required: true,
    },
    passwordLabel: {
      type: String,
      required: true,
    },
    submitLabel: {
      type: String,
      required: true,
    },
    loading: {
      type: Boolean,
      required: true,
    },
    errorText: {
      type: String,
      required: true,
    },
  },
  emits: {
    submit: (_payload: SubmitPayload) => true,
  },
  setup(props: Props, { emit }) {
    const localUserName = ref('')
    const localPassword = ref('')

    const errorText = computed(() => props.errorText)

    watch(
      () => props.loading,
      (isLoading) => {
        if (!isLoading && !props.errorText) {
          localPassword.value = ''
        }
      },
    )

    function onSubmitInternal() {
      emit('submit', {
        userName: localUserName.value.trim(),
        password: localPassword.value,
      })
    }

    return {
      localUserName,
      localPassword,
      errorText,
      onSubmitInternal,
    }
  },
})
