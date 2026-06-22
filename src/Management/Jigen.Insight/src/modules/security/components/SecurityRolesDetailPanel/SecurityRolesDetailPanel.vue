<template>
  <aside class="detail-panel">
    <header class="panel-header">
      <h3>{{ title }}</h3>
      <div class="detail-actions" v-if="role">
        <el-button @click="$emit('edit')">{{ $t('security.common.edit') }}</el-button>
        <el-button type="danger" @click="$emit('delete')">{{ $t('security.common.delete') }}</el-button>
      </div>
    </header>

    <template v-if="role">
      <div class="facts-list" role="group" :aria-label="title">
        <p class="fact-row">
          <span class="fact-label">{{ $t('security.roles.columns.id') }}</span>
          <strong class="fact-value">{{ role.id }}</strong>
        </p>
        <p class="fact-row">
          <span class="fact-label">{{ $t('security.roles.columns.name') }}</span>
          <strong class="fact-value">{{ role.name }}</strong>
        </p>
      </div>

      <div class="users-box">
        <p id="security-role-users-title" class="users-title">{{ $t('security.roles.usersTitle') }}</p>
        <p v-if="loadingUsers" class="empty-note">{{ $t('security.common.loading') }}</p>
        <el-scrollbar v-else max-height="18rem" role="region" aria-labelledby="security-role-users-title">
          <el-empty v-if="users.length === 0" :description="$t('security.roles.noUsers')" />
          <el-space v-else direction="vertical" fill>
            <el-tag v-for="user in users" :key="user.id" size="large">{{ user.userName }}</el-tag>
          </el-space>
        </el-scrollbar>
      </div>
    </template>

    <el-empty v-else :description="$t('security.roles.emptySelection')" />
  </aside>
</template>

<script lang="ts" src="./SecurityRolesDetailPanel.ts"></script>
<style scoped lang="less" src="./SecurityRolesDetailPanel.less"></style>
