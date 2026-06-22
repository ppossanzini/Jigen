<template>
  <div class="app-shell">
    <ShellHeader
      :app-name="t('app.name')"
      :workspace="workspaceName"
      :user-name="userName"
      :search-placeholder="t('app.searchPlaceholder')"
      :search-label="t('app.searchLabel')"
      :notification-label="t('app.notifications')"
      @search="onSearch"
      @switch-workspace="onSwitchWorkspace"
      @notifications="onNotifications"
    />

    <div class="shell-body">
      <ShellSidebar
        :items="navItems"
        :active-key="navigationStore.activeNavKey"
        :collapse-label="t('nav.collapse')"
        :help-label="t('nav.help')"
        @navigate="onNavigate"
      />

      <main class="main-content">
        <div class="main-scroll">
          <RouterView />
        </div>
      </main>
    </div>
  </div>
</template>

<script lang="ts" src="./AppShellView.ts"></script>
<style scoped lang="less" src="./AppShellView.less"></style>
