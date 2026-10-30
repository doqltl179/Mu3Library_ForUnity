---
id: pytorch
title: PyTorch
kind: framework
applies_to: []
related: [python]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.pytorch.org/docs/stable/index.html", "https://docs.pytorch.org/docs/stable/notes/randomness.html", "https://docs.pytorch.org/docs/stable/amp.html", "https://docs.pytorch.org/docs/stable/data.html", "https://docs.pytorch.org/docs/stable/user_guide/torch_compiler/torch.compiler.html", "https://docs.pytorch.org/tutorials/unstable/inductor_windows.html", "https://docs.pytorch.org/tutorials/beginner/saving_loading_models.html", "https://github.com/pytorch/pytorch/releases"]
---

# PyTorch

## Detect
- `torch` version and build in dependency files (`pyproject.toml`, `requirements*.txt`, lockfile) and the wheel index (`download.pytorch.org/whl/<cpu|cuXXX|rocmX.Y>`); at runtime `torch.__version__` and `torch.version.cuda`.
- Accelerators: `torch.cuda.is_available()`, `torch.backends.mps.is_available()`. Do not assume a GPU in tests or CI.
- Training layer on top (for example Lightning or Hugging Face `transformers`/`accelerate`) or a custom loop. When a framework owns device placement, precision, or the optimizer step, configure it instead of adding manual calls.
- Distributed setup: `torchrun` launch scripts, `DistributedDataParallel`, FSDP.
- Config system (for example Hydra or argparse) and where seeds, paths, and hyperparameters are set.

## Conventions
- Resolve one `device` from config; move the model and every batch with `.to(device)`; create new tensors on an existing tensor's device and dtype (`torch.zeros_like(x)`, `x.new_zeros(...)`, `device=x.device`). Never hard-code `.cuda()` or `"cuda"`.
- Call `model.train()` before training steps and `model.eval()` before validation or inference; they switch dropout and batch-norm behavior.
- Run inference under `torch.inference_mode()`; use `torch.no_grad()` when outputs may later enter autograd.
- Step order: `optimizer.zero_grad()` → forward → loss → `loss.backward()` → `optimizer.step()` → `scheduler.step()` at the scheduler's step unit.
- Log scalars with `loss.item()` or `.detach()`; accumulating tensors that carry a graph leaks memory.
- Reproducibility: seed `random`, `numpy`, and `torch.manual_seed`; seed DataLoader workers through `generator` and `worker_init_fn`; for determinism set `torch.use_deterministic_algorithms(True)` (older versions on CUDA also need an environment variable; see «Version Notes»).
- DataLoader: use `num_workers > 0` and `pin_memory=True` for GPU training; keep the dataset picklable, define `collate_fn` and `worker_init_fn` at module top level, and guard entry points with `if __name__ == "__main__":`, because workers are not forked on every platform (see «Version Notes»).
- Mixed precision: wrap forward and loss in `torch.autocast(device_type=..., dtype=torch.bfloat16 | torch.float16)`; float16 also needs `torch.amp.GradScaler`. Never call `.half()` on a model being trained.
- Checkpoints: save a dict of `state_dict()`s (model, optimizer, scheduler, scaler) plus step and config; load with `torch.load(path, map_location=..., weights_only=True)` and `load_state_dict`. Never pickle whole modules.
- Unwrap before saving: `model.module` under DDP; `torch.compile` wrappers prefix keys with `_orig_mod.`.

## Verify
- Prefer `commands.test`; keep unit tests on CPU with tiny shapes and fixed seeds.
- Overfit test: train on one small batch for a few hundred steps and assert the loss approaches zero; failure means a wiring bug (labels, loss, `zero_grad`, frozen parameters, eval mode).
- After one backward pass, assert every parameter with `requires_grad` has a finite, non-`None` `.grad`.
- Shape tests: run a forward pass on a dummy batch and assert output shapes and dtypes.
- Compare tensors with `torch.testing.assert_close`, not `==`.
- Debug NaNs with `torch.autograd.set_detect_anomaly(True)` temporarily; it is slow.

## Pitfalls
- Silent broadcasting: `(N,)` against `(N, 1)` yields `(N, N)` in a loss; assert shapes and `squeeze`/`unsqueeze` explicitly.
- `nn.CrossEntropyLoss` takes raw logits and `long` class indices; applying `softmax` first is wrong.
- `view` fails on non-contiguous tensors; use `reshape` or `.contiguous()`.
- In-place ops (`x += ...`, `relu_()`) on tensors saved for backward raise autograd errors; use out-of-place ops.
- `torch.cuda.empty_cache()` does not free tensors still referenced; drop references, lower batch size, or use gradient accumulation or activation checkpointing. Measure with `torch.cuda.max_memory_allocated()`.
- `.item()`, `.cpu()`, or printing tensors every step forces device synchronization; log every N steps.
- `model.eval()` alone still records gradients; combine it with `inference_mode` or `no_grad`.

## Version Notes
- Latest release is PyTorch 2.14 (2.14.1, 2026-09-30); check the project's pinned version before using any API below (as of 2026-10, per PyTorch GitHub releases).
- `torch.load` defaults to `weights_only=True` from 2.6; loading arbitrary pickled objects needs an explicit `weights_only=False` and trusted files (as of 2026-10, per PyTorch 2.6 release notes).
- `torch.cuda.amp.autocast`/`torch.cuda.amp.GradScaler` are deprecated; use `torch.autocast(device_type="cuda")` (or `torch.amp.autocast("cuda")`) and `torch.amp.GradScaler("cuda")` (as of 2026-10, per PyTorch AMP docs).
- `optimizer.zero_grad()` sets gradients to `None` by default from 2.0 (as of 2026-10, per PyTorch 2.0 release notes and optimizer docs).
- TorchScript (`torch.jit.script`, `trace`, `save`, `load`) is deprecated: warnings from 2.10, visible `FutureWarning` from 2.14, and no guaranteed support on Python 3.14. Use `torch.export.export`/`torch.export.save` to serialize and `torch.compile` to optimize (as of 2026-10, per PyTorch 2.10 and 2.14 release notes).
- 2.14 removed `torch.cholesky` and `torch.qr` (use `torch.linalg.cholesky` and `torch.linalg.qr`); 2.13 removed named tensors (as of 2026-10, per PyTorch release notes).
- `torch.compile` generates C++ for CPU, so it needs a working C++ compiler, and uses Triton for CUDA, ROCm, and XPU devices. On Windows the official guide covers CPU from 2.5 (MSVC installed) and XPU from 2.7. Confirm the project's version, OS, and accelerator before enabling it (as of 2026-10, per docs.pytorch.org torch.compiler docs and Windows tutorial).
- On CUDA with PyTorch 2.9 or older, deterministic mode also needs `CUBLAS_WORKSPACE_CONFIG=:4096:8`; 2.10 removed that requirement (as of 2026-10, per PyTorch 2.9 and 2.10 reproducibility docs and 2.10 release notes).
- DataLoader workers start with `spawn` on Windows and macOS, `forkserver` on Linux with Python 3.14+, and `fork` on Linux below 3.14 (as of 2026-10, per docs.pytorch.org data docs).
- `torch.accelerator.current_accelerator(check_available=True)` (2.7+) returns the usable accelerator device or `None`; use it with a `"cpu"` fallback for device-agnostic selection (as of 2026-10, per PyTorch accelerator docs).
- From 2.11, `pip install torch` from PyPI installs CUDA 13.0 wheels on Linux, which need a CUDA 13 driver and a GPU of compute capability 7.5 or newer; pin `--index-url https://download.pytorch.org/whl/cu126` (or `cu128`) for older drivers or GPUs (as of 2026-10, per PyTorch 2.11 release notes).
- The `pytorch` conda channel (`-c pytorch`) stops at 2.5; install 2.6+ from PyPI or `download.pytorch.org` wheels, or from conda-forge in conda environments (as of 2026-10, per PyTorch 2.6 release notes).
