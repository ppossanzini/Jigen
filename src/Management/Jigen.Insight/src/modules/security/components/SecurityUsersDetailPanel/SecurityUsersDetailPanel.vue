<template>
  <aside class="detail-panel">
    <header class="panel-header">
      <h3>{{ title }}</h3>
      <div class="detail-actions" v-if="user">
        <el-button @click="$emit('edit')">{{ $t('security.common.edit') }}</el-button>
        <el-button type="danger" @click="$emit('delete')">{{ $t('security.common.delete') }}</el-button>
      </div>
    </header>

    <template v-if="user">
      <div class="facts-list" role="group" :aria-label="title">
        <p class="fact-row">
          <span class="fact-label">{{ $t('security.users.columns.id') }}</span>
          <strong class="fact-value">{{ user.id }}</strong>
        </p>
        <p class="fact-row">
          <span class="fact-label">{{ $t('security.users.columns.userName') }}</span>
          <strong class="fact-value">{{ user.userName }}</strong>
        </p>
      </div>

      <div class="roles-box">
        <p id="security-user-roles-title" class="roles-title">{{ $t('security.users.columns.roles') }}</p>
        <p v-if="loading" class="empty-note">{{ $t('security.common.loading') }}</p>
        <el-checkbox-group
          v-else
          v-model="editableRoles"
          class="chip-checkbox-group"
          aria-labelledby="security-user-roles-title"
        >
          <el-checkbox v-for="roleName in roleOptions" :key="roleName" :value="roleName">
            {{ roleName }}
          </el-checkbox>
        </el-checkbox-group>
        <p v-if="!loading && roleOptions.length === 0" class="empty-note">{{ $t('security.users.noRoles') }}</p>
      </div>

      <el-button type="primary" :loading="saving" @click="$emit('save-roles')">{{ $t('security.users.actions.saveRoles') }}</el-button>
    </template>

    <el-empty v-else :description="$t('security.users.emptySelection')" />
  </aside>
</template>

<script lang="ts" src="./SecurityUsersDetailPanel.ts"></script>
<style scoped lang="less" src="./SecurityUsersDetailPanel.less"></style>
