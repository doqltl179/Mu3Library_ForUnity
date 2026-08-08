# Lessons

- Keep `ContainerScope.InjectInto` internal across DI and MVP assembly boundaries. Expose only a narrow `IObjectInjector` capability and resolve it as a built-in scope dependency when a container-owned service uses `[Inject]`.
- For one-shot subscriptions, account for synchronous event delivery during registration and clean up failed registration attempts before returning or rethrowing.
- Keep reusable game runtime code in its own package and leave project `Assets` for imported sample content; this prevents duplicate asmdefs when the sample project consumes the package.
- Unity's `Vector2.Lerp` accepts one scalar interpolation value; retain a small component-wise helper when X and Y use separate normalized interpolation values.
- When a fixed default rule retains future catalog data, explicitly gate the default simulation to its supported range so extension-only entries are neither activated nor destroyed prematurely.
- For oversized Unity MonoBehaviours, keep the serialized façade at its existing path and GUID, then group partial responsibility files under named subfolders such as `Controller`, `Area`, and `Item`.
- Keep sample `Prepare` and `GameStart` as distinct lifecycle stages: subscribe before preparation and start only from the prepared event, so failed preparation cannot publish a running board.
- For tile-rendered guide sprites, keep the generated asset to one source segment and let the Unity `SpriteRenderer` repeat it; do not bake the repeated pattern into the image.
- A tiled `SpriteRenderer` draws over `SpriteRenderer.size` and not over the sprite bounds, and assigning a sprite at runtime does not resize it; fit such a renderer through its size and keep its transform scale the same on both axes, which then only decides how big one repeated segment is drawn.
