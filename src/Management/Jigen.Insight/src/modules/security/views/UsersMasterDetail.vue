<template>
  <section class="users-master-detail">
    <header class="toolbar">
      <h3>{{ t('security.users.title') }}</h3>
      <el-button type="primary" @click="onOpenCreateUserDialog">
        {{ t('security.users.actions.create') }}
      </el-button>
    </header>

    <div class="layout-grid">
      <article class="panel">
        <UsersMasterTable
          :rows="visibleUsers"
          :current-page="currentPage"
          :page-size="pageSize"
          :total="securityStore.users.length"
          :loading="securityStore.loadingUsers"
          :title="t('security.users.masterTitle')"
          :user-name-label="t('security.users.columns.userName')"
          :rows-label="t('security.common.rows')"
          :empty-label="t('security.common.empty')"
          @row-click="onSelectUser"
          @page-change="onUsersPageChange"
        />
      </article>

      <article class="panel detail-panel">
        <template v-if="selectedUser">
          <div class="detail-header">
            <h3>{{ t('security.users.detailTitle') }}</h3>
            <div class="detail-actions">
              <el-button @click="onOpenEditUserDialog">{{ t('security.common.edit') }}</el-button>
              <el-button type="danger" @click="onDeleteUser">{{ t('security.common.delete') }}</el-button>
            </div>
          </div>

          <el-form label-position="top" class="detail-form">
            <el-form-item :label="t('security.users.columns.id')">
              <el-input :model-value="selectedUser.id" disabled />
            </el-form-item>
            <el-form-item :label="t('security.users.columns.userName')">
              <el-input :model-value="selectedUser.userName ?? '-'" disabled />
            </el-form-item>
            <el-form-item :label="t('security.users.columns.roles')">
              <el-checkbox-group v-model="selectedRoles" class="roles-group">
                <el-checkbox v-for="roleName in roleOptions" :key="roleName" :value="roleName">
                  {{ roleName }}
                </el-checkbox>
              </el-checkbox-group>
            </el-form-item>
            <el-button type="primary" :loading="applyingRoles" @click="onSaveRoles">
              {{ t('security.users.actions.saveRoles') }}
            </el-button>
          </el-form>
        </template>

        <el-empty v-else :description="t('security.users.emptySelection')" />
      </article>
    </div>

    <el-dialog
      :model-value="userDialogVisible"
      :title="userDialogTitle"
      width="560px"
      :teleported="false"
      class="security-user-dialog"
      @close="onCloseUserDialog"
    >
      <el-form label-position="top">
        <el-form-item :label="t('security.users.columns.userName')" required>
          <el-input v-model="userDialogForm.userName" />
        </el-form-item>

        <el-form-item :label="t('security.users.columns.password')">
          <el-input v-model="userDialogForm.password" type="password" show-password />
        </el-form-item>

        <el-form-item :label="t('security.users.columns.roles')">
          <el-select v-model="userDialogForm.roles" multiple filterable>
            <el-option v-for="roleName in roleOptions" :key="roleName" :label="roleName" :value="roleName" />
          </el-select>
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="onCloseUserDialog">{{ t('security.common.cancel') }}</el-button>
        <el-button type="primary" :loading="userDialogSaving" @click="saveDialogUser">
          {{ t('security.common.save') }}
        </el-button>
      </template>
    </el-dialog>
  </section>
</template>

<script lang="ts" src="./UsersMasterDetail.ts"></script>
<style scoped lang="less" src="./UsersMasterDetail.less"></style>
