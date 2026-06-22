<template>
  <header class="database-toolbar">
    <div class="title-group">
      <h2>{{ $t('databaseManagement.title') }}</h2>
      <p>{{ $t('databaseManagement.subtitle') }}</p>
    </div>

    <div class="actions">
      <el-tooltip :disabled="!createDisabled" :content="$t('databaseManagement.feedback.adminOnly')" placement="top">
        <el-button type="success" :disabled="createDisabled" @click="$emit('create')">{{ $t('databaseManagement.createDatabase') }}</el-button>
      </el-tooltip>
      <el-button @click="$emit('refresh')">{{ $t('databaseManagement.refresh') }}</el-button>
      <el-tooltip :disabled="!deleteDisabled" :content="$t('databaseManagement.feedback.adminOnly')" placement="top">
        <el-button type="danger"  :disabled="deleteDisabled" @click="$emit('delete')">{{ $t('databaseManagement.deleteDatabase') }}</el-button>
      </el-tooltip>
    </div>
  </header>
</template>

<script lang="ts" src="./DatabaseToolbar.ts"></script>
<style scoped lang="less" src="./DatabaseToolbar.less"></style>
