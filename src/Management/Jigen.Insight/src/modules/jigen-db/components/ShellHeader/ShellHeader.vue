<template>
  <header class="shell-header">
    <div class="left-group">
      <div class="brand-mark" aria-hidden="true">
        <img src="/assets/logo.svg" />
      </div>
      <h1 class="brand-name">{{ appName }}</h1>
      <button class="workspace-chip" type="button" @click="onSwitchWorkspace">
        <i class="ti ti-stack" />
        <span>{{ workspace }}</span>
        <i class="ti ti-chevron-down" />
      </button>
    </div>

    <div class="center-group">
      <el-input
        v-model="searchText"
        class="search-input"
        :placeholder="searchPlaceholder"
        @keyup.enter="onSearch"
      >
        <template #prefix>
          <i class="ti ti-search" />
        </template>
      </el-input>
    </div>

    <div class="right-group">
      <button class="notification-btn" type="button" @click="onNotifications">
        <i class="ti ti-bell" />
        <span>{{ notificationLabel }}</span>
      </button>
      <button class="user-pill" type="button">
        <i class="ti ti-user-circle" />
        <span>{{ userName }}</span>
      </button>
    </div>
  </header>
</template>

<script lang="ts" src="./ShellHeader.ts"></script>
<style scoped lang="less" src="./ShellHeader.less"></style>
