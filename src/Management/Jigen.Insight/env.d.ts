/// <reference types="vite/client" />

interface ImportMetaEnv {
	readonly VITE_OIDC_CLIENT_ID?: string
	readonly VITE_OIDC_CLIENT_SECRET?: string
	readonly VITE_OIDC_REDIRECT_URI?: string
	readonly VITE_OIDC_SCOPE?: string
}

interface ImportMeta {
	readonly env: ImportMetaEnv
}
