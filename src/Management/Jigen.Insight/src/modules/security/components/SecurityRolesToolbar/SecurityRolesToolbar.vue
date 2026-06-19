<template>
  <header class="security-toolbar">
    <div class="title-group">
      <h2>{{ $t('security.roles.title') }}</h2>
      <p>{{ $t('security.roles.subtitle') }}</p>
    </div>

    <div class="actions">
      <el-button type="success" @click="$emit('create')">{{ $t('security.roles.actions.create') }}</el-button>
      <el-button @click="$emit('refresh')">{{ $t('security.roles.actions.refresh') }}</el-button>
      <el-button type="danger"  :disabled="deleteDisabled" @click="$emit('delete')">{{ $t('security.roles.actions.delete') }}</el-button>
    </div>
  </header>
</template>

<script lang="ts" src="./SecurityRolesToolbar.ts"></script>
<style scoped lang="less" src="./SecurityRolesToolbar.less"></style>
