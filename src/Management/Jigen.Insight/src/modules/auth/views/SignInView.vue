<template>
  <section class="sign-in-view">
    <!-- <header class="sign-in-view__topbar">
      <el-button circle class="sign-in-view__brand" @click="onBrandAction">
        <i class="ti ti-leaf" />
      </el-button>

      <SignInWorkspaceSelector
        :label="t('auth.workspaceLabel')"
        :model-value="selectedWorkspace"
        :options="workspaceOptions"
        @update:model-value="onWorkspaceChange"
      />
    </header> -->

    <div class="sign-in-view__content">
      <SignInHeroPanel
        image-src="/assets/logo.svg"
        :image-alt="t('auth.heroAlt')"
      />

      <SignInCard
        v-model="form"
        :title="t('auth.title')"
        :subtitle="t('auth.subtitle')"
        :email-label="t('auth.emailLabel')"
        :email-placeholder="t('auth.emailPlaceholder')"
        :password-label="t('auth.passwordLabel')"
        :password-placeholder="t('auth.passwordPlaceholder')"
        :email-required-message="t('auth.validation.emailRequired')"
        :password-required-message="t('auth.validation.passwordRequired')"
        :remember-label="t('auth.rememberMe')"
        :forgot-password-label="t('auth.forgotPassword')"
        :submit-label="t('auth.signIn')"
        :continue-with-label="t('auth.continueWith')"
        :google-label="t('auth.signInWithGoogle')"
        :sso-label="t('auth.signInWithSso')"
        :last-workspace-label="t('auth.lastWorkspace')"
        :last-workspace-name="lastWorkspace.name"
        :last-workspace-seen="t('auth.lastWorkspaceActivity')"
        :quick-demo-label="t('auth.quickDemo')"
        :info-tooltip="t('auth.lastWorkspaceInfoTooltip')"
        :loading="loading"
        @submit="onSubmit"
        @forgot-password="onForgotPassword"
        @oauth-google="onOauthSignIn"
        @oauth-sso="onOauthSignIn"
        @quick-demo="onQuickDemo"
      />
    </div>

    <footer class="sign-in-view__footer">
      <p>{{ t('auth.supportText') }}</p>
      <el-link href="https://github.com/ppossanzini/Jigen/issues" type="primary">https://github.com/ppossanzini/Jigen/issues</el-link>
      <el-link href="https://raw.githubusercontent.com/ppossanzini/Jigen/refs/heads/main/LICENSE.txt">{{ t('auth.termsAndPrivacy') }}</el-link>
    </footer>
  </section>
</template>

<script lang="ts" src="./SignInView.ts"></script>
<style scoped lang="less" src="./SignInView.less"></style>
