---
id: vue
title: Vue
kind: framework
applies_to: ["**/*.vue"]
related: [typescript]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://vuejs.org/guide/introduction.html", "https://vuejs.org/style-guide/", "https://pinia.vuejs.org/", "https://v3-migration.vuejs.org/", "https://blog.vuejs.org/"]
---

# Vue

## Detect
- `vue` major in `package.json`: 2.x or 3.x. Vue 2.7 backports the Composition API and `<script setup>`; 2.6 and older have neither.
- Build and meta-framework: `vite.config.*`, `vue.config.js` (Vue CLI), or `nuxt.config.*`. In Nuxt, its auto-imports, file routing, and data-fetching conventions take precedence.
- API style per component and directory: `<script setup>` or `setup()` (Composition) versus `data`/`methods`/`computed` options (Options). Follow the file being edited and its neighbors.
- `lang="ts"` on script blocks and `vue-tsc` in scripts.
- Store: Pinia (`defineStore`) or Vuex (`createStore`, `new Vuex.Store`). Router: `vue-router` 4 for Vue 3, 3 for Vue 2.
- Tests: Vitest or Jest with `@vue/test-utils`; lint: `eslint-plugin-vue`.

## Conventions
- Name components with multiple words in PascalCase (root `App` excepted); keep one component per SFC and the project's block order.
- Declare props and emits explicitly (`defineProps<T>()`, `defineEmits<T>()`, or typed option objects).
- Props down, events up: never mutate a prop; emit an event or use `v-model` on the child.
- Use `ref` for primitives and values that get reassigned; use `reactive` only for objects never replaced. Access `.value` in script; templates unwrap top-level refs.
- Use `computed` for derived state and keep it free of side effects; use `watch`/`watchEffect` only for side effects, and clean them up.
- Extract reusable stateful logic into `useX` composables that return refs and accept refs or getters as inputs.
- Give every `v-for` a stable `:key` from the data. Never put `v-if` on the same element as `v-for`; filter in a `computed` or move `v-if` to a wrapper `<template>`.
- Style with `<style scoped>` or CSS modules per project convention.
- Pinia: destructure state and getters with `storeToRefs(store)`; destructure actions directly.

## Verify
- Types: `commands.typecheck` (usually `vue-tsc --noEmit`); lint: `commands.lint`.
- Tests: `commands.test`; build: `commands.build`.

## Pitfalls
- Destructuring a `reactive` object or a store loses reactivity; use `toRefs`, `storeToRefs`, or read the property on the object.
- Reassigning a variable that holds `reactive()` leaves the template bound to the old object; use a `ref` for replaceable values.
- `watch(props.id, ...)` receives a plain value and never fires; watch a getter `() => props.id` or the ref itself. Passing `ref.value` into a composable has the same problem.
- Vue 2: adding a new property or assigning an array element by index is not reactive; use `Vue.set`, `splice`, or declare the property up front.
- Arrow functions for Options API `methods`, `computed`, or `data` lose `this`; use method shorthand.
- `v-html` with user content enables XSS; render text with interpolation or sanitize first.

## Version Notes
- Vue 2 reached end of life on 2023-12-31; Pinia is the recommended store and Vuex is in maintenance mode (as of 2026-10, per vuejs.org and pinia.vuejs.org).
- `defineModel` is stable from Vue 3.4. Vue 3.5 made props destructured from `defineProps` reactive in `<script setup>` and added `useTemplateRef`, `useId`, and `onWatcherCleanup` (as of 2026-10, per blog.vuejs.org). Check the installed minor before using them.
