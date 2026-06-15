<template>
  <el-card class="panel-card">
    <template #header>
      <div class="panel-header">
        <span>{{ title }}</span>
        <el-button type="primary" @click="onCreateClick">{{ createLabel }}</el-button>
      </div>
    </template>

    <el-table :data="users" stripe v-loading="loading" class="users-table" height="100%">
      <el-table-column prop="userName" :label="usernameLabel" min-width="180" />
      <el-table-column :label="rolesLabel" min-width="220">
        <template #default="scope">
          <div class="role-tags">
            <el-tag v-for="roleName in getRoleNames(scope.row.roles)" :key="roleName" size="small" type="info" effect="plain">
              {{ roleName }}
            </el-tag>
          </div>
        </template>
      </el-table-column>
      <el-table-column :label="actionsLabel" fixed="right" width="180">
        <template #default="scope">
          <div class="actions-wrap">
            <el-button text type="primary" :disabled="!canMutate(scope.row)" @click="onEditClick(scope.row)">{{ editLabel }}</el-button>
            <el-button text type="danger" :disabled="!canMutate(scope.row)" @click="onDeleteClick(scope.row)">{{ deleteLabel }}</el-button>
          </div>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog :model-value="dialogVisible" :title="dialogTitle" width="560px" @close="onDialogClose">
      <el-form :model="form" label-position="top">
        <el-form-item :label="usernameLabel" required>
          <el-input v-model="form.userName" />
        </el-form-item>

        <el-form-item v-if="isCreateMode" :label="passwordLabel" required>
          <el-input v-model="form.password" type="password" show-password />
        </el-form-item>

        <el-form-item :label="rolesLabel">
          <el-select v-model="form.roles" multiple filterable>
            <el-option v-for="role in roles" :key="role.id" :label="role.name" :value="role.name" />
          </el-select>
        </el-form-item>
      </el-form>

      <template #footer>
        <div class="dialog-footer">
          <el-button @click="onDialogClose">{{ cancelLabel }}</el-button>
          <el-button type="primary" :loading="saving" @click="onSaveClick">{{ saveLabel }}</el-button>
        </div>
      </template>
    </el-dialog>
  </el-card>
</template>

<script lang="ts" src="./UsersPanel.ts"></script>
<style scoped lang="less" src="./UsersPanel.less"></style>
