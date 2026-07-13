<script setup lang="ts">
import { $t } from '@/locales';

defineOptions({
  name: 'GlobalFooter'
});

const year = new Date().getFullYear();
</script>

<template>
  <DarkModeContainer class="h-full flex-center">
    <span>{{ $t('system.copyright', { year }) }}</span>
  </DarkModeContainer>
</template>

