import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import '@tabler/icons-webfont/dist/tabler-icons.min.css'

import App from './App.vue'
import router from './router'
import pinia from '@/store'
import i18n from '@/lang'
import '@/assets/styles/global/base.less'
import { initializeTheme } from '@/stores/theme'

initializeTheme()

const app = createApp(App)

app.use(pinia)
app.use(i18n)
app.use(ElementPlus)
app.use(router)

app.mount('#app')
