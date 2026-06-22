<template>
  <header class="security-toolbar">
    <div class="title-group">
      <h2>{{ $t('security.users.title') }}</h2>
      <p>{{ $t('security.users.subtitle') }}</p>
    </div>

    <div class="actions">
      <el-button type="success" @click="$emit('create')">{{ $t('security.users.actions.create') }}</el-button>
      <el-button @click="$emit('refresh')">{{ $t('security.users.actions.refresh') }}</el-button>
      <el-button type="danger"  :disabled="deleteDisabled" @click="$emit('delete')">{{ $t('security.users.actions.delete') }}</el-button>
    </div>
  </header>
</template>

<script lang="ts" src="./SecurityUsersToolbar.ts"></script>
<style scoped lang="less" src="./SecurityUsersToolbar.less"></style>
