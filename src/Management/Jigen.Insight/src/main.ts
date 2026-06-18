import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import '@tabler/icons-webfont/dist/tabler-icons.min.css'
import '@fontsource/jetbrains-mono/index.css'

import App from './App.vue'
import router from './router'
import i18n from './i18n'
import { loadSettings } from './settings'
import './assets/styles/global/base.less'

const bootstrap = async (): Promise<void> => {
	await loadSettings()

	const app = createApp(App)

	app.use(createPinia())
	app.use(router)
	app.use(i18n)
	app.use(ElementPlus)

	app.mount('#app')
}

void bootstrap()
