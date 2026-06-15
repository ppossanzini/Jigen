<template>
  <div class="roles-master-detail">
    <div class="master-panel">
      <div class="panel-header">
        <h3>{{ t('security.roles.title') }}</h3>
        <el-button type="primary" size="small" @click="onCreateRoleClick">{{ t('security.roles.actions.create') }}</el-button>
      </div>
      <el-table :data="store.roles" stripe v-loading="store.isLoadingRoles" class="roles-list" @row-click="onSelectRole" highlight-current-row>
        <el-table-column prop="name" :label="t('security.roles.columns.name')" min-width="160" />
      </el-table>
    </div>

    <div class="cross-ref-panel" v-if="selectedRole">
      <div class="panel-header">
        <h3>{{ t('security.roles.roleUsers.title') }}</h3>
      </div>
      <el-table :data="roleUsersData" stripe class="users-list">
        <el-table-column prop="userName" :label="t('security.users.columns.userName')" min-width="200" />
      </el-table>
    </div>

    <el-dialog :model-value="roleDialogVisible" :title="roleDialogTitle" width="600px" @close="onRoleDialogClose">
      <el-form :model="roleForm" label-position="top">
        <el-form-item :label="t('security.roles.columns.name')" required>
          <el-input v-model="roleForm.name" :disabled="!isRoleCreateMode" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="onRoleDialogClose">{{ t('security.common.cancel') }}</el-button>
        <el-button type="primary" :loading="savingRole" @click="onSaveRoleClick">{{ t('security.common.save') }}</el-button>
      </template>
    </el-dialog>

    <el-dialog :model-value="deleteDialogVisible" :title="t('security.common.warning')" width="500px" @close="onDeleteDialogClose">
      <p>{{ t('security.roles.feedback.confirmDelete') }}</p>
      <template #footer>
        <el-button @click="onDeleteDialogClose">{{ t('security.common.cancel') }}</el-button>
        <el-button type="danger" :loading="deletingRole" @click="onConfirmDeleteRoleClick">{{ t('security.common.delete') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script lang="ts" src="./RolesMasterDetail.ts"></script>
<style scoped lang="less" src="./RolesMasterDetail.less"></style>
