import { defineComponent, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox } from 'element-plus'

import UsersPanel from '@/modules/users/components/UsersPanel/UsersPanel.vue'
import RolesPanel from '@/modules/users/components/RolesPanel/RolesPanel.vue'
import type {
  CreateRolePayload,
  CreateUserPayload,
  UpdateRolePayload,
  UpdateUserPayload,
} from '@/modules/users/types'
import { useUsersRolesStore } from '@/stores/usersRoles'
import { useNavigationStore } from '@/stores/navigation'
import { API_BASE_URL } from '@/services/baseRestService'

export default defineComponent({
  name: 'UsersRolesView',
  components: {
    UsersPanel,
    RolesPanel,
  },
  setup() {
    const { t } = useI18n()
    const store = useUsersRolesStore()
    const navigationStore = useNavigationStore()

    const isSavingUser = ref(false)
    const isSavingRole = ref(false)
    const apiBaseUrl = API_BASE_URL

    onMounted(async () => {
      navigationStore.setCurrentFeature('users')
      navigationStore.setActiveMenu('app-users')
      try {
        await store.loadAll()
      } catch (error) {
        const message = error instanceof Error ? error.message : t('usersRoles.feedback.genericError')
        ElMessage.error(message)
      }
    })

    async function onCreateUser(payload: CreateUserPayload) {
      isSavingUser.value = true
      try {
        await store.createUser({
          userName: payload.userName?.trim() || '',
          password: payload.password || '',
          roles: Array.isArray(payload.roles) ? payload.roles : [],
        })
        ElMessage.success(t('usersRoles.feedback.userCreated'))
      } catch (error) {
        const message = error instanceof Error ? error.message : t('usersRoles.feedback.genericError')
        ElMessage.error(message)
      } finally {
        isSavingUser.value = false
      }
    }

    async function onUpdateUser(payload: { id: string } & UpdateUserPayload) {
      isSavingUser.value = true
      try {
        await store.updateUser(payload.id, {
          userName: payload.userName?.trim() || '',
          roles: Array.isArray(payload.roles) ? payload.roles : [],
        })
        ElMessage.success(t('usersRoles.feedback.userUpdated'))
      } catch (error) {
        const message = error instanceof Error ? error.message : t('usersRoles.feedback.genericError')
        ElMessage.error(message)
      } finally {
        isSavingUser.value = false
      }
    }

    async function onDeleteUser(payload: { id: string }) {
      try {
        await ElMessageBox.confirm(t('usersRoles.feedback.confirmDeleteUser'), t('usersRoles.feedback.warningTitle'), {
          type: 'warning',
          confirmButtonText: t('usersRoles.actions.delete'),
          cancelButtonText: t('usersRoles.actions.cancel'),
        })
        await store.deleteUser(payload.id)
        ElMessage.success(t('usersRoles.feedback.userDeleted'))
      } catch (error) {
        if (error === 'cancel') {
          return
        }

        const message = error instanceof Error ? error.message : t('usersRoles.feedback.genericError')
        ElMessage.error(message)
      }
    }

    async function onCreateRole(payload: CreateRolePayload) {
      isSavingRole.value = true
      try {
        await store.createRole({
          name: payload.name?.trim() || '',
        })
        ElMessage.success(t('usersRoles.feedback.roleCreated'))
      } catch (error) {
        const message = error instanceof Error ? error.message : t('usersRoles.feedback.genericError')
        ElMessage.error(message)
      } finally {
        isSavingRole.value = false
      }
    }

    async function onUpdateRole(payload: { id: string } & UpdateRolePayload) {
      isSavingRole.value = true
      try {
        await store.updateRole(payload.id, {
          name: payload.name?.trim() || '',
        })
        ElMessage.success(t('usersRoles.feedback.roleUpdated'))
      } catch (error) {
        const message = error instanceof Error ? error.message : t('usersRoles.feedback.genericError')
        ElMessage.error(message)
      } finally {
        isSavingRole.value = false
      }
    }

    async function onDeleteRole(payload: { id: string }) {
      try {
        await ElMessageBox.confirm(t('usersRoles.feedback.confirmDeleteRole'), t('usersRoles.feedback.warningTitle'), {
          type: 'warning',
          confirmButtonText: t('usersRoles.actions.delete'),
          cancelButtonText: t('usersRoles.actions.cancel'),
        })
        await store.deleteRole(payload.id)
        ElMessage.success(t('usersRoles.feedback.roleDeleted'))
      } catch (error) {
        if (error === 'cancel') {
          return
        }

        const message = error instanceof Error ? error.message : t('usersRoles.feedback.genericError')
        ElMessage.error(message)
      }
    }

    return {
      t,
      store,
      apiBaseUrl,
      isSavingUser,
      isSavingRole,
      onCreateUser,
      onUpdateUser,
      onDeleteUser,
      onCreateRole,
      onUpdateRole,
      onDeleteRole,
    }
  },
})
