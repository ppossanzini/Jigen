<template>
  <div class="main-layout">
    <aside class="sidebar">
      <div class="brand-wrap">
        <p class="brand">{{ t('app.brand') }}</p>
        <p class="subtitle">{{ t('app.shell.workspace') }}</p>
      </div>

      <el-menu
        class="menu"
        :default-active="navigationStore.activeMenu"
        background-color="transparent"
        text-color="#9fb1d1"
        active-text-color="#6df6ff"
        @select="onMenuSelect"
      >
        <el-menu-item index="app-home">
          <span class="menu-icon ti ti-dashboard"></span>
          <span>{{ t('app.menu.dashboard') }}</span>
        </el-menu-item>
        <el-menu-item index="app-collections">
          <span class="menu-icon ti ti-database"></span>
          <span>{{ t('app.menu.collections') }}</span>
        </el-menu-item>
        <el-sub-menu index="app-security">
          <template #title>
            <span class="menu-icon ti ti-lock"></span>
            <span>{{ t('app.menu.security') }}</span>
          </template>
          <el-menu-item index="app-security-users">
            {{ t('security.users.title') }}
          </el-menu-item>
          <el-menu-item index="app-security-roles">
            {{ t('security.roles.title') }}
          </el-menu-item>
        </el-sub-menu>
      </el-menu>

      <div class="sidebar-footer">
        <div class="theme-toggle">
          <el-button
            class="theme-icon-btn"
            :class="{ active: isDarkMode === 'light' }"
            text
            circle
            @click="onSetTheme('light')"
          >
            <span class="ti ti-sun"></span>
          </el-button>
          <el-button
            class="theme-icon-btn"
            :class="{ active: isDarkMode === 'dark' }"
            text
            circle
            @click="onSetTheme('dark')"
          >
            <span class="ti ti-moon"></span>
          </el-button>
        </div>
        <el-button class="logout-btn" text type="danger" @click="onLogout">
          {{ t('app.actions.logout') }}
        </el-button>
      </div>
    </aside>

    <main class="content-wrap">
      <header class="topbar">
        <div class="topbar-title">
          <span>{{ authStore.userName || t('app.brand') }}</span>
          <small>{{ t('app.shell.liveOps') }}</small>
        </div>
        <div class="topbar-meta">
          <el-tag size="small" type="success" effect="dark">{{ t('app.shell.connected') }}</el-tag>
          <el-tag size="small" type="info" effect="plain">{{ t('app.shell.readOnly') }}</el-tag>
        </div>
      </header>

      <section class="content-scroll">
        <router-view />
      </section>
    </main>
  </div>
</template>

<script lang="ts" src="./MainLayout.ts"></script>
<style scoped lang="less" src="./MainLayout.less"></style>
