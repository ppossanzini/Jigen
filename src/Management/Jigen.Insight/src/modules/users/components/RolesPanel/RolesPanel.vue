<template>
  <el-card class="panel-card">
    <template #header>
      <div class="panel-header">
        <span>{{ title }}</span>
        <el-button type="primary" @click="onCreateClick">{{ createLabel }}</el-button>
      </div>
    </template>

    <el-table :data="roles" stripe v-loading="loading" class="roles-table" height="100%">
      <el-table-column prop="name" :label="nameLabel" min-width="180" />
      <el-table-column :label="actionsLabel" fixed="right" width="180">
        <template #default="scope">
          <div class="actions-wrap">
            <el-button text type="primary" :disabled="!scope.row.id" @click="onEditClick(scope.row)">{{ editLabel }}</el-button>
            <el-button text type="danger" :disabled="!scope.row.id" @click="onDeleteClick(scope.row)">{{ deleteLabel }}</el-button>
          </div>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog :model-value="dialogVisible" :title="dialogTitle" width="500px" @close="onDialogClose">
      <el-form :model="form" label-position="top">
        <el-form-item :label="nameLabel" required>
          <el-input v-model="form.name" />
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

<script lang="ts" src="./RolesPanel.ts"></script>
<style scoped lang="less" src="./RolesPanel.less"></style>
