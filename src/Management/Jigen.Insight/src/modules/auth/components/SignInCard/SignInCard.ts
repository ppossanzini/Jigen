import { computed, defineComponent, ref, watch, type PropType } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import type { SignInFormModel } from '@/modules/auth/types'

export default defineComponent({
  name: 'SignInCard',
  props: {
    title: {
      type: String,
      required: true,
    },
    subtitle: {
      type: String,
      required: true,
    },
    modelValue: {
      type: Object as PropType<SignInFormModel>,
      required: true,
    },
    emailLabel: {
      type: String,
      required: true,
    },
    emailPlaceholder: {
      type: String,
      required: true,
    },
    passwordLabel: {
      type: String,
      required: true,
    },
    passwordPlaceholder: {
      type: String,
      required: true,
    },
    emailRequiredMessage: {
      type: String,
      required: true,
    },
    passwordRequiredMessage: {
      type: String,
      required: true,
    },
    rememberLabel: {
      type: String,
      required: true,
    },
    forgotPasswordLabel: {
      type: String,
      required: true,
    },
    submitLabel: {
      type: String,
      required: true,
    },
    continueWithLabel: {
      type: String,
      required: true,
    },
    googleLabel: {
      type: String,
      required: true,
    },
    ssoLabel: {
      type: String,
      required: true,
    },
    lastWorkspaceLabel: {
      type: String,
      required: true,
    },
    lastWorkspaceName: {
      type: String,
      required: true,
    },
    lastWorkspaceSeen: {
      type: String,
      required: true,
    },
    quickDemoLabel: {
      type: String,
      required: true,
    },
    infoTooltip: {
      type: String,
      required: true,
    },
    loading: {
      type: Boolean,
      required: true,
    },
  },
  emits: [
    'update:modelValue',
    'submit',
    'forgot-password',
    'oauth-google',
    'oauth-sso',
    'quick-demo',
  ],
  setup(props, { emit }) {
    const formRef = ref<FormInstance>()
    const localModel = ref<SignInFormModel>({ ...props.modelValue })

    watch(
      () => props.modelValue,
      (value) => {
        localModel.value = { ...value }
      },
      { deep: true },
    )

    const rules = computed<FormRules<SignInFormModel>>(() => ({
      email: [{ required: true, message: props.emailRequiredMessage, trigger: 'blur' }],
      password: [{ required: true, message: props.passwordRequiredMessage, trigger: 'blur' }],
    }))

    const syncModel = () => emit('update:modelValue', { ...localModel.value })

    const onEmailChange = (value: string) => {
      localModel.value.email = value
      syncModel()
    }

    const onPasswordChange = (value: string) => {
      localModel.value.password = value
      syncModel()
    }

    const onRememberChange = (value: string | number | boolean) => {
      localModel.value.rememberMe = Boolean(value)
      syncModel()
    }

    const onSubmit = async () => {
      if (!formRef.value) return

      const isValid = await formRef.value.validate().catch(() => false)

      if (!isValid) return

      emit('submit')
    }

    return {
      formRef,
      rules,
      onEmailChange,
      onPasswordChange,
      onRememberChange,
      onSubmit,
    }
  },
})
