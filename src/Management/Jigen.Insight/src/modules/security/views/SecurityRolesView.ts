import { computed, defineComponent, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import SecurityRolesToolbar from '@/modules/security/components/SecurityRolesToolbar/SecurityRolesToolbar.vue'
import SecurityRolesTable from '@/modules/security/components/SecurityRolesTable/SecurityRolesTable.vue'
import SecurityRolesDetailPanel from '@/modules/security/components/SecurityRolesDetailPanel/SecurityRolesDetailPanel.vue'
import { useSecurityStore, type SecurityRole } from '@/stores/security'

type RoleDialogMode = 'create' | 'edit'

interface RoleDialogForm {
  name: string
}

interface RoleDialogErrors {
  name: string
}

export default defineComponent({
  name: 'SecurityRolesView',
  components: {
    SecurityRolesToolbar,
    SecurityRolesTable,
    SecurityRolesDetailPanel,
  },
  setup() {
    const { t } = useI18n()
    const securityStore = useSecurityStore()

    const currentPage = ref(1)
    const pageSize = ref(6)
    const roleDialogVisible = ref(false)
    const roleDialogMode = ref<RoleDialogMode>('create')
    const roleDialogSaving = ref(false)

    const roleDialogForm = reactive<RoleDialogForm>({
      name: '',
    })

    const roleDialogErrors = reactive<RoleDialogErrors>({
      name: '',
    })

    const selectedRole = computed(() => securityStore.selectedRole)
    const roles = computed(() => securityStore.roles)
    const visibleRoles = computed(() => {
      const start = (currentPage.value - 1) * pageSize.value
      return roles.value.slice(start, start + pageSize.value)
    })

    const roleDialogTitle = computed(() =>
      roleDialogMode.value === 'create' ? t('security.roles.dialog.createTitle') : t('security.roles.dialog.editTitle'),
    )

    const calculateDynamicPageSize = () => {
      const reservedSpace = 470
      const rowHeight = 54
      const availableHeight = Math.max(window.innerHeight - reservedSpace, rowHeight * 5)
      pageSize.value = Math.max(4, Math.floor(availableHeight / rowHeight))
      const maxPages = Math.max(1, Math.ceil(roles.value.length / pageSize.value))
      if (currentPage.value > maxPages) currentPage.value = maxPages
    }

    const onSelectRole = async (role: SecurityRole) => {
      securityStore.setSelectedRole(role.id)
      await securityStore.loadUsersForRole(role.id)
    }

    const onPageChange = (nextPage: number) => {
      currentPage.value = nextPage
      const fallback = visibleRoles.value[0] ?? null

      if (!fallback) {
        securityStore.setSelectedRole(null)
        return
      }

      void onSelectRole(fallback)
    }

    const refreshData = async () => {
      await securityStore.loadRoles()

      if (!securityStore.selectedRoleId) {
        const first = visibleRoles.value[0] ?? null

        if (first) {
          await onSelectRole(first)
        }

        return
      }

      await securityStore.loadUsersForRole(securityStore.selectedRoleId)
    }

    const onOpenCreateRoleDialog = () => {
      roleDialogMode.value = 'create'
      roleDialogForm.name = ''
      roleDialogErrors.name = ''
      roleDialogVisible.value = true
    }

    const onOpenEditRoleDialog = () => {
      if (!selectedRole.value) {
        ElMessage.warning(t('security.roles.feedback.selectRole'))
        return
      }

      roleDialogMode.value = 'edit'
      roleDialogForm.name = selectedRole.value.name
      roleDialogErrors.name = ''
      roleDialogVisible.value = true
    }

    const onCloseRoleDialog = () => {
      roleDialogVisible.value = false
    }

    const saveDialogRole = async () => {
      roleDialogErrors.name = ''

      const normalizedName = roleDialogForm.name.trim()

      if (!normalizedName) {
        roleDialogErrors.name = t('security.roles.validation.nameRequired')
        return
      }

      roleDialogSaving.value = true

      try {
        if (roleDialogMode.value === 'create') {
          await securityStore.createRole({ name: normalizedName })
          ElMessage.success(t('security.roles.feedback.created'))
        } else if (selectedRole.value) {
          await securityStore.updateRole(selectedRole.value.id, { name: normalizedName })
          ElMessage.success(t('security.roles.feedback.updated'))
        }

        roleDialogVisible.value = false
        await refreshData()
      } catch {
        ElMessage.error(t('security.common.error'))
      } finally {
        roleDialogSaving.value = false
      }
    }

    const onDeleteRole = async () => {
      if (!selectedRole.value) {
        ElMessage.warning(t('security.roles.feedback.selectRole'))
        return
      }

      try {
        await ElMessageBox.confirm(
          t('security.roles.feedback.deleteConfirm'),
          t('security.common.warning'),
          {
            type: 'warning',
            confirmButtonText: t('security.common.delete'),
            cancelButtonText: t('security.common.cancel'),
          },
        )

        await securityStore.deleteRole(selectedRole.value.id)
        const nextSelected = visibleRoles.value[0] ?? null

        if (nextSelected) {
          await onSelectRole(nextSelected)
        }

        ElMessage.success(t('security.roles.feedback.deleted'))
      } catch {
        // Dialog cancellation is expected and should not show feedback.
      }
    }

    const onRefresh = async () => {
      try {
        await refreshData()
        ElMessage.success(t('security.roles.feedback.refreshed'))
      } catch {
        ElMessage.error(t('security.common.error'))
      }
    }

    onMounted(() => {
      void refreshData()
      window.addEventListener('resize', calculateDynamicPageSize)
      calculateDynamicPageSize()
    })

    onBeforeUnmount(() => {
      window.removeEventListener('resize', calculateDynamicPageSize)
    })

    return {
      t,
      securityStore,
      currentPage,
      pageSize,
      selectedRole,
      visibleRoles,
      roleDialogVisible,
      roleDialogTitle,
      roleDialogForm,
      roleDialogErrors,
      roleDialogSaving,
      onSelectRole,
      onPageChange,
      onOpenCreateRoleDialog,
      onOpenEditRoleDialog,
      onCloseRoleDialog,
      saveDialogRole,
      onDeleteRole,
      onRefresh,
    }
  },
})
