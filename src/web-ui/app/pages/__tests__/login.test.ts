import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import LoginPage from '~/pages/login.vue'

describe('LoginPage', () => {
  it('renders login form with title, inputs, and submit button', async () => {
    const wrapper = await mountSuspended(LoginPage)
    expect(wrapper.find('h1').text()).toBe('HydraForge')
    expect(wrapper.find('input[autocomplete="username"]').exists()).toBe(true)
    expect(wrapper.find('input[type="password"]').exists()).toBe(true)
    expect(wrapper.find('button[type="submit"]').exists()).toBe(true)
  })
})
