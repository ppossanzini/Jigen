<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import { getPaletteColorByNumber } from '@sa/color';
import { useThemeStore } from '@/store/modules/theme';
import { useAuthStore } from '@/store/modules/auth';
import { useRouterPush } from '@/hooks/common/router';
import { $t } from '@/locales';

defineOptions({
  name: 'AuthCallbackPage'
});

const route = useRoute();
const themeStore = useThemeStore();
const authStore = useAuthStore();
const { toLogin } = useRouterPush();

const bgThemeColor = computed(() =>
  themeStore.darkMode ? getPaletteColorByNumber(themeStore.themeColor, 600) : themeStore.themeColor
);

// page background derived from the primary palette: light tint in light mode, deep shade in dark
const bgColor = computed(() => getPaletteColorByNumber(themeStore.themeColor, themeStore.darkMode ? 800 : 200));

const logoSrc = computed(() =>
  themeStore.darkMode ? '/branding/jigen-logo-full-dark.png' : '/branding/jigen-logo-full.png'
);

onMounted(() => {
  const code = typeof route.query.code === 'string' ? route.query.code : '';
  const state = typeof route.query.state === 'string' ? route.query.state : '';

  // on success this navigates away on its own (to the originally intended page, or home); on
  // failure it sets authStore.callbackError and this page falls through to the error state below
  authStore.completeOAuthCallback(code, state);
});
</script>

<template>
  <div class="relative size-full flex-center overflow-hidden" :style="{ backgroundColor: bgColor }">
    <WaveBg :theme-color="bgThemeColor" />
    <NCard :bordered="false" class="relative z-4 w-auto rd-12px">
      <div class="w-400px flex-col items-center gap-16px lt-sm:w-300px">
        <img :src="logoSrc" :alt="$t('system.title')" class="h-40px w-auto" />

        <template v-if="!authStore.callbackError">
          <NSpin size="large" />
          <span class="text-14px text-gray-500">{{ $t('page.login.callback.signingIn') }}</span>
        </template>
        <template v-else>
          <NResult status="error" :title="$t('page.login.callback.title')" :description="authStore.callbackError" />
          <NButton type="primary" @click="toLogin()">{{ $t('page.login.callback.backToLogin') }}</NButton>
        </template>
      </div>
    </NCard>
  </div>
</template>
