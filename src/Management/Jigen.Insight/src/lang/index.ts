import { createI18n } from 'vue-i18n'

import it from '@/lang/locales/it'
import en from '@/lang/locales/en'

const i18n = createI18n({
  legacy: false,
  locale: 'it',
  fallbackLocale: 'en',
  messages: {
    it,
    en,
  },
})

export default i18n
