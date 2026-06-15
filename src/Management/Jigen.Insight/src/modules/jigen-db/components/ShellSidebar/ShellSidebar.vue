<template>
  <aside class="shell-sidebar">
    <div class="sidebar-rail">
      <div class="sidebar-logo" aria-hidden="true" />

      <el-menu class="sidebar-menu" :default-active="activeKey" @select="onSelect">
        <el-menu-item v-for="item in items" :key="item.key" :index="item.key">
          <i :class="item.iconClass" />
          <span>{{ item.label }}</span>
        </el-menu-item>
      </el-menu>

      <div class="sidebar-bottom">
        <button type="button" class="collapse-btn" @click="onCollapse">
          <span>{{ collapseLabel }}</span>
        </button>
        <button type="button" class="help-btn" @click="onHelp">
          <i class="ti ti-help" />
        </button>
      </div>
    </div>
  </aside>
</template>

<script lang="ts" src="./ShellSidebar.ts"></script>
<style scoped lang="less" src="./ShellSidebar.less"></style>
