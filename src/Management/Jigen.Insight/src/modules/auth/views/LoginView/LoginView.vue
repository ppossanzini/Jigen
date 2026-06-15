<template>
  <section class="login-view">
    <el-image class="login-logo" :src="logoSrc" fit="contain" height="10%" />
    <div class="login-stack">
      <LoginCard
        :title="t('auth.title')"
        :tagline="t('auth.tagline')"
        :username-label="t('auth.username')"
        :password-label="t('auth.password')"
        :submit-label="t('app.actions.enter')"
        :loading="authStore.isLoading"
        :error-text="errorText"
        @submit="onSubmit"
      />
    </div>
  </section>
</template>

<script lang="ts" src="./LoginView.ts"></script>
<style scoped lang="less" src="./LoginView.less"></style>
