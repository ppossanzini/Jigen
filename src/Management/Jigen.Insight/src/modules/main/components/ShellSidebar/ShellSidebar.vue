<template>
  <aside class="shell-sidebar">
    <div class="sidebar-rail">
      <div class="sidebar-logo" aria-hidden="true" />

      <el-menu class="sidebar-menu" :default-active="activeKey" :default-openeds="['security']" @select="onSelect">
        <template v-for="item in items" :key="item.key">
          <el-sub-menu v-if="item.children?.length" :index="item.key">
            <template #title>
              <i :class="item.iconClass" />
              <span>{{ item.label }}</span>
            </template>

            <el-menu-item v-for="child in item.children" :key="child.key" :index="child.key">
              <i :class="child.iconClass" />
              <span>{{ child.label }}</span>
            </el-menu-item>
          </el-sub-menu>

          <el-menu-item v-else :index="item.key">
            <i :class="item.iconClass" />
            <span>{{ item.label }}</span>
          </el-menu-item>
        </template>
      </el-menu>

    </div>
  </aside>
</template>

<script lang="ts" src="./ShellSidebar.ts"></script>
<style scoped lang="less" src="./ShellSidebar.less"></style>
