# Lessons

- Keep `ContainerScope.InjectInto` internal across DI and MVP assembly boundaries. Expose only a narrow `IObjectInjector` capability and resolve it as a built-in scope dependency when a container-owned service uses `[Inject]`.
- For one-shot subscriptions, account for synchronous event delivery during registration and clean up failed registration attempts before returning or rethrowing.
