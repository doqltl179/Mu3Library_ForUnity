# Lessons

- Keep `ContainerScope.InjectInto` internal across DI and MVP assembly boundaries. Expose only a narrow `IObjectInjector` capability and resolve it as a built-in scope dependency when a container-owned service uses `[Inject]`.
- For one-shot subscriptions, account for synchronous event delivery during registration and clean up failed registration attempts before returning or rethrowing.
- Keep reusable game runtime code in its own package and leave project `Assets` for imported sample content; this prevents duplicate asmdefs when the sample project consumes the package.
- Unity's `Vector2.Lerp` accepts one scalar interpolation value; retain a small component-wise helper when X and Y use separate normalized interpolation values.
