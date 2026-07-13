<script setup lang="ts">
import { computed } from 'vue';
import { getPaletteColorByNumber } from '@sa/color';
import { useAppStore } from '@/store/modules/app';
import { useThemeStore } from '@/store/modules/theme';
import { $t } from '@/locales';
import PwdLogin from './modules/pwd-login.vue';

interface Props {
  /** The login module */
  module?: UnionKey.LoginModule;
}

defineProps<Props>();

const appStore = useAppStore();
const themeStore = useThemeStore();

const bgThemeColor = computed(() =>
  themeStore.darkMode ? getPaletteColorByNumber(themeStore.themeColor, 600) : themeStore.themeColor
);

// page background derived from the primary palette: light tint in light mode, deep shade in dark
const bgColor = computed(() => getPaletteColorByNumber(themeStore.themeColor, themeStore.darkMode ? 800 : 200));

// Full brand lockup, swapped for the theme-matching variant (see `public/branding/`)
const logoSrc = computed(() =>
  themeStore.darkMode ? '/branding/jigen-logo-full-dark.png' : '/branding/jigen-logo-full.png'
);
</script>

<template>
  <div class="relative size-full flex-center overflow-hidden" :style="{ backgroundColor: bgColor }">
    <WaveBg :theme-color="bgThemeColor" />
    <NCard :bordered="false" class="relative z-4 w-auto rd-12px">
      <div class="w-400px lt-sm:w-300px">
        <header class="flex-y-center justify-between">
          <img :src="logoSrc" :alt="$t('system.title')" class="h-40px lt-sm:h-32px w-auto" />
          <div class="i-flex-col">
            <ThemeSchemaSwitch
              :theme-schema="themeStore.themeScheme"
              :show-tooltip="false"
              class="text-20px lt-sm:text-18px"
              @switch="themeStore.toggleThemeScheme"
            />
            <LangSwitch
              v-if="themeStore.header.multilingual.visible"
              :lang="appStore.locale"
              :lang-options="appStore.localeOptions"
              :show-tooltip="false"
              @change-lang="appStore.changeLocale"
            />
          </div>
        </header>
        <main class="pt-24px">
          <h3 class="text-18px text-primary font-medium">{{ $t('page.login.pwdLogin.title') }}</h3>
          <div class="pt-24px">
            <PwdLogin />
          </div>
        </main>
      </div>
    </NCard>
  </div>
</template>
