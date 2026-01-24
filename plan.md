# v0.1 Release Preparation - High Priority Issues

## Task 1: Fix Bare Catch Blocks

**Files:**
- `OpcUa/OpcUaClientWrapper.cs:323`
- `OpcUa/SubscriptionManager.cs:613`
- `Configuration/RecentFilesManager.cs:119,137`

**Problem:** Empty or bare catch blocks silently swallow exceptions, making debugging difficult and hiding potential bugs.

**Fix:** Add proper exception handling with logging. At minimum, log the exception. Consider whether the exception should be rethrown or handled differently based on context.

**Status:** [x] Complete

---

## Task 2: Fix Async Disposal Deadlock Risk

**Files:**
- `OpcUa/SubscriptionManager.cs:642`

**Problem:** Using `GetAwaiter().GetResult()` in a `Dispose()` method can cause deadlocks, especially when called from a synchronization context.

**Fix:** Implement `IAsyncDisposable` alongside `IDisposable`, or use a fire-and-forget pattern with proper error handling for cleanup. The synchronous Dispose can trigger async cleanup without blocking.

**Status:** [x] Complete

---

## Task 3: Fix Race Condition in CSV Recording

**Files:**
- `Utilities/CsvRecordingManager.cs:268-320`

**Problem:** Race condition between `StopRecording()` and the background write loop. The `_writer` can be disposed while still being accessed, causing `ObjectDisposedException`.

**Fix:** Use proper synchronization (lock or SemaphoreSlim) to ensure the writer is not accessed after disposal. Consider using a cancellation token to signal the write loop to exit cleanly before disposing resources.

**Status:** [x] Complete

---

## Task 4: Pin Wildcard Package Versions

**Files:**
- `Opcilloscope.csproj`
- `tools/Opcilloscope.TestServer/Opcilloscope.TestServer.csproj`

**Problem:** Wildcard versions like `2.*` and `1.5.*` can cause inconsistent builds across different machines or times.

**Fix:** Pin to specific versions. Check the current resolved versions in the lock file or restore output, then update the csproj files with exact versions.

**Status:** [x] Complete

---

## Task 5: Update CHANGELOG to v0.1.0

**Files:**
- `CHANGELOG.md`

**Problem:** CHANGELOG shows v1.0.0 but we're releasing v0.1.0. Version and potentially date need updating.

**Fix:** Update the version header to v0.1.0 and set the release date to today (2026-01-24). Review content to ensure it accurately reflects v0.1 features.

**Status:** [x] Complete

---

## Task 6: Fix Broken README Image Links

**Files:**
- `README.md`

**Problem:** README references images in `docs/assets/` directory that don't exist, resulting in broken image links.

**Fix:** Either create placeholder images, remove the image references, or replace with text descriptions. For v0.1, removing or replacing with descriptive text is acceptable.

**Status:** [x] Complete
