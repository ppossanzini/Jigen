<template>
  <div class="users-master-detail">
    <div class="master-panel">
      <div class="panel-header">
        <h3>{{ t('security.users.title') }}</h3>
        <el-button type="primary" size="small" @click="onCreateUserClick">{{ t('security.users.actions.create') }}</el-button>
      </div>
      <el-table :data="store.users" stripe v-loading="store.isLoadingUsers" class="users-list" @row-click="onSelectUser" highlight-current-row>
        <el-table-column prop="userName" :label="t('security.users.columns.userName')" min-width="160" />
      </el-table>
    </div>

    <div class="detail-panel" v-if="selectedUser">
      <div class="panel-header">
        <h3>{{ t('security.users.details.title') }}</h3>
        <div class="actions">
          <el-button text type="primary" @click="onEditUserClick" :disabled="!canEdit">{{ t('security.common.edit') }}</el-button>
          <el-button text type="danger" @click="onDeleteUserClick" :disabled="!canEdit">{{ t('security.common.delete') }}</el-button>
        </div>
      </div>
      <div class="detail-content">
        <el-descriptions :column="1" border>
          <el-descriptions-item :label="t('security.users.columns.userName')">{{ selectedUser.userName }}</el-descriptions-item>
          <el-descriptions-item :label="t('security.users.columns.roles')">
            <div class="role-tags">
              <el-tag v-for="roleName in selectedUser.roles" :key="roleName" type="info" size="small">{{ roleName }}</el-tag>
              <span v-if="!selectedUser.roles.length">-</span>
            </div>
          </el-descriptions-item>
        </el-descriptions>
      </div>
    </div>

    <div class="cross-ref-panel" v-if="selectedUser">
      <div class="panel-header">
        <h3>{{ t('security.users.userRoles.title') }}</h3>
        <el-button text type="primary" @click="onManageRolesClick" :disabled="!canEdit" size="small">{{ t('security.common.manage') }}</el-button>
      </div>
      <el-table :data="userRolesData" stripe class="roles-list">
        <el-table-column prop="name" :label="t('security.roles.columns.name')" min-width="200" />
        <el-table-column :label="t('security.common.status')" width="100">
          <template #default="scope">
            <el-icon v-if="isRoleAssigned(scope.row.id)" class="status-assigned"><SuccessFilled /></el-icon>
            <el-icon v-else class="status-unassigned"><CircleCloseFilled /></el-icon>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog :model-value="userDialogVisible" :title="userDialogTitle" width="600px" @close="onUserDialogClose">
      <el-form :model="userForm" label-position="top">
        <el-form-item :label="t('security.users.columns.userName')" required>
          <el-input v-model="userForm.userName" :disabled="!isUserCreateMode" />
        </el-form-item>
        <el-form-item v-if="isUserCreateMode" :label="t('security.users.columns.password')" required>
          <el-input v-model="userForm.password" type="password" show-password />
        </el-form-item>
        <el-form-item :label="t('security.users.columns.roles')">
          <el-select v-model="userForm.roles" multiple filterable>
            <el-option v-for="role in store.roles" :key="role.id" :label="role.name" :value="role.name" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="onUserDialogClose">{{ t('security.common.cancel') }}</el-button>
        <el-button type="primary" :loading="savingUser" @click="onSaveUserClick">{{ t('security.common.save') }}</el-button>
      </template>
    </el-dialog>

    <el-dialog :model-value="rolesDialogVisible" :title="t('security.users.userRoles.manage')" width="600px" @close="onRolesDialogClose">
      <el-tree
        ref="rolesTreeRef"
        :data="rolesTreeData"
        node-key="id"
        :checked-keys="selectedUser?.roles || []"
        :props="{ children: 'children', label: 'name' }"
        show-checkbox
        default-expand-all
      />
      <template #footer>
        <el-button @click="onRolesDialogClose">{{ t('security.common.cancel') }}</el-button>
        <el-button type="primary" :loading="savingRoles" @click="onSaveRolesClick">{{ t('security.common.save') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script lang="ts" src="./UsersMasterDetail.ts"></script>
<style scoped lang="less" src="./UsersMasterDetail.less"></style>
