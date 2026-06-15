<template>
  <header class="index-toolbar">
    <div class="title-group">
      <h2>{{ title }}</h2>
      <p>{{ subtitle }}</p>
    </div>

    <div class="actions">
      <el-button type="success" @click="$emit('create')">{{ createLabel }}</el-button>
      <el-button @click="$emit('import')">{{ importLabel }}</el-button>
      <el-button @click="$emit('tune')">{{ tuneLabel }}</el-button>
      <el-button @click="$emit('filter')">{{ filterLabel }}</el-button>
      <el-button @click="$emit('compact')">{{ compactLabel }}</el-button>
      <el-button @click="$emit('export')">{{ exportLabel }}</el-button>
    </div>
  </header>
</template>

<script lang="ts" src="./IndexToolbar.ts"></script>
<style scoped lang="less" src="./IndexToolbar.less"></style>
