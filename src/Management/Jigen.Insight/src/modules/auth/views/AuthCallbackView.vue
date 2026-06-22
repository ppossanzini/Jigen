<template>
  <section class="auth-callback-view">
    <el-card class="auth-callback-view__card" shadow="never">
      <el-result
        icon="info"
        :title="t('auth.oauthCallbackTitle')"
        :sub-title="t('auth.oauthCallbackSubtitle')"
      >
        <template #extra>
          <el-button type="primary" :loading="loading" :disabled="loading" @click="goToSignIn">
            {{ t('auth.signIn') }}
          </el-button>
        </template>
      </el-result>
    </el-card>
  </section>
</template>

<script lang="ts" src="./AuthCallbackView.ts"></script>
<style scoped lang="less" src="./AuthCallbackView.less"></style>
