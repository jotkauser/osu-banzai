import { createInertiaApp } from '@inertiajs/svelte'
import { mount } from 'svelte'
import { resolvePageComponent } from 'laravel-vite-plugin/inertia-helpers'
import Main from './Layouts/Main.svelte'

createInertiaApp({
    resolve: async (name) => {
        const page = await resolvePageComponent(
            `./Pages/${name}.svelte`,
            import.meta.glob('./Pages/**/*.svelte'),
        )
        page.default.layout = page.default.layout ?? Main
        return page
    },
    setup({ el, App, props }) {
        mount(App, { target: el, props })
    },
})
