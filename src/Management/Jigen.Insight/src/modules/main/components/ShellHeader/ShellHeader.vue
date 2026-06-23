<template>
  <header class="shell-header">
    <div class="left-group">
      <div class="brand-mark" aria-hidden="true">
        <img src="/assets/logo.svg" alt="" />
      </div>
      <h1 class="brand-name">{{ appName }}</h1>
      <el-button class="workspace-chip" @click="onSwitchWorkspace">
        <i class="ti ti-stack" />
        <span>{{ workspace }}</span>
        <i class="ti ti-chevron-down" />
      </el-button>
    </div>

    <div class="center-group">
      <span class="sr-only">{{ searchLabel }}</span>
      <el-input
        v-model="searchText"
        class="search-input"
        :placeholder="searchPlaceholder"
        :aria-label="searchLabel"
        @keyup.enter="onSearch"
      >
        <template #prefix>
          <i class="ti ti-search" />
        </template>
      </el-input>
    </div>

    <div class="right-group">
      <el-button class="notification-btn" @click="onNotifications">
        <i class="ti ti-bell" />
        <span>{{ notificationLabel }}</span>
      </el-button>
      <el-dropdown class="user-menu" trigger="click" @command="onUserMenuCommand">
        <el-button class="user-pill" role="status" aria-live="polite">
          <i class="ti ti-user-circle" />
          <span>{{ userName }}</span>
          <i class="ti ti-chevron-down" />
        </el-button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item command="logout">
              <i class="ti ti-logout"></i>
              <span>{{ logoutLabel }}</span>
            </el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>
  </header>
</template>

<script lang="ts" src="./ShellHeader.ts"></script>
<style scoped lang="less" src="./ShellHeader.less"></style>
