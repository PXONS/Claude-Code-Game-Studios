# Unity 6.3 LTS — Breaking Changes

**Last verified:** 2026-05-02

This document tracks breaking API changes and behavioral differences between Unity 2022 LTS
(likely in model training) and Unity 6.3 LTS (current version). Organized by risk level.

## HIGH RISK — Will Break Existing Code

### Entities/DOTS API Complete Overhaul
**Versions:** Entities 1.0+ (Unity 6.0+)

```csharp
// ❌ OLD (pre-Unity 6, GameObjectEntity pattern)
public class HealthComponent : ComponentData {
    public float Value;
}

// ✅ NEW (Unity 6+, IComponentData)
public struct HealthComponent : IComponentData {
    public float Value;
}

// ❌ OLD: ComponentSystem
public class DamageSystem : ComponentSystem { }

// ✅ NEW: ISystem (unmanaged, Burst-compatible)
public partial struct DamageSystem : ISystem {
    public void OnCreate(ref SystemState state) { }
    public void OnUpdate(ref SystemState state) { }
}
```

**Migration:** Follow Unity's ECS migration guide. Major architectural changes required.

---

### Input System — Legacy Input Deprecated
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: Input class (deprecated)
if (Input.GetKeyDown(KeyCode.Space)) { }

// ✅ NEW: Input System package
using UnityEngine.InputSystem;
if (Keyboard.current.spaceKey.wasPressedThisFrame) { }
```

**Migration:** Install Input System package, replace all `Input.*` calls with new API.

---

### URP/HDRP Renderer Feature API Changes
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: ScriptableRenderPass.Execute signature
public override void Execute(ScriptableRenderContext context, ref RenderingData data)

// ✅ NEW: Uses RenderGraph API
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
```

**Migration:** Update custom render passes to use RenderGraph API.

---

## MEDIUM RISK — Behavioral Changes

### Addressables — Asset Loading Returns
**Versions:** Unity 6.2+

Asset loading failures now throw exceptions by default instead of returning null.
Add proper exception handling or use `TryLoad` variants.

```csharp
// ❌ OLD: Silent null on failure
var handle = Addressables.LoadAssetAsync<Sprite>("key");
var sprite = handle.Result; // null if failed

// ✅ NEW: Throws on failure, use try/catch or TryLoad
try {
    var handle = Addressables.LoadAssetAsync<Sprite>("key");
    var sprite = await handle.Task;
} catch (Exception e) {
    Debug.LogError($"Failed to load: {e}");
}
```

---

### Physics — Default Solver Iterations Changed
**Versions:** Unity 6.0+

Default solver iterations increased for better stability.
Check `Physics.defaultSolverIterations` if you rely on old behavior.

---

## LOW RISK — Deprecations (Still Functional)

### UGUI (Legacy UI)
**Status:** Deprecated but supported
**Replacement:** UI Toolkit

UGUI still works but UI Toolkit is recommended for new projects.

---

### Legacy Particle System
**Status:** Deprecated
**Replacement:** Visual Effect Graph (VFX Graph)

---

### Old Animation System
**Status:** Deprecated
**Replacement:** Animator Controller (Mecanim)

---

## Platform-Specific Breaking Changes

### WebGL
- **Unity 6.0+**: WebGPU is now the default (WebGL 2.0 fallback available)
- Update shaders for WebGPU compatibility

### Android
- **Unity 6.0+**: Minimum API level raised to 24 (Android 7.0)

### iOS
- **Unity 6.0+**: Minimum deployment target raised to iOS 13

---

### Object Finding API Renamed (Unity 6.0)
**Risk:** LOW (compile error only — easy fix)

```csharp
// ❌ Obsolete
FindObjectsOfType<T>()
FindObjectOfType<T>()

// ✅ Replacement
FindObjectsByType<T>(FindObjectsSortMode.None)   // unsorted, better perf
FindFirstObjectByType<T>()
FindAnyObjectByType<T>()
```

---

### URP — AfterRendering Injection Timing (Unity 6.2)
**Risk:** LOW — only affects custom ScriptableRendererFeatures

The `AfterRendering` event now consistently fires after the final blit.
If your custom pass must run before the final blit, change its event to
`AfterRenderingPostProcessing`.

---

### URP — SetupRenderPasses Deprecated (Unity 6.2)
**Risk:** MEDIUM — affects all custom Scriptable Renderer Features

```csharp
// ❌ Deprecated
public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData data) { }

// ✅ Replacement — use render graph
public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data) { }
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) { }
```

---

### UI Toolkit — VisualElement.transform Deprecated (Unity 6.2)

```csharp
// ❌ Deprecated
element.transform.position = new Vector3(x, y, 0);

// ✅ Replacement
element.style.translate = new Translate(x, y);
element.style.rotate    = new Rotate(angle);
element.style.scale     = new Scale(new Vector2(sx, sy));

// For reading resolved values:
var pos = element.resolvedStyle.translate;
```

---

### UI Toolkit — Event System Overhaul (Unity 6.0)

```csharp
// ❌ Deprecated
protected override void ExecuteDefaultAction(EventBase evt) { }
protected override void ExecuteDefaultActionAtTarget(EventBase evt) { }
evt.PreventDefault();

// ✅ Replacement
protected override void HandleEventTrickleDown(EventBase evt) { }
protected override void HandleEventBubbleUp(EventBase evt) { }
evt.StopPropagation();
```

Custom UXML attribute declarations also changed:

```csharp
// ❌ Old — UxmlTraits + UxmlFactory boilerplate
public new class UxmlFactory : UxmlFactory<MyElement, UxmlTraits> { }
public new class UxmlTraits : VisualElement.UxmlTraits { }

// ✅ New — attribute-based
[UxmlElement]
public partial class MyElement : VisualElement {
    [UxmlAttribute] public string MyProp { get; set; }
}
```

---

### Accessibility API (Unity 6.3)
**Risk:** LOW — only affects projects using AccessibilityNode

- `AccessibilityRole` and `AccessibilityState` now use `byte` type (was int/flags)
- `AccessibilityRole` converted from flags enum to standard enum
- `AccessibilityNode.selected` deprecated → use `AccessibilityNode.invoked`

---

### Lighting — Auto Generate Removed (Unity 6.0)

The `Auto Generate` lighting checkbox was removed from the Lighting window.
Call `Lightmapping.Bake()` or `Lightmapping.BakeAsync()` explicitly.

```csharp
// ❌ No longer: checkbox in Lighting window
// ✅ Explicit bake
Lightmapping.BakeAsync();
```

Enlighten baked GI backend also removed — projects auto-migrate to Progressive Lightmapper.

---

### GraphicsFormat — Depth/Shadow Formats (Unity 6.0)

```csharp
// ❌ Obsolete — now cause compile errors
GraphicsFormat.DepthAuto
GraphicsFormat.ShadowAuto
GraphicsFormat.VideoAuto

// GraphicsFormatUtility.GetGraphicsFormat returns GraphicsFormat.None for depth/shadow
```

---

## Migration Checklist

When upgrading from 2022 LTS to Unity 6.3 LTS:

- [ ] Audit all DOTS/ECS code (complete rewrite likely needed)
- [ ] Replace `Input` class with Input System package
- [ ] Update custom render passes to RenderGraph API
- [ ] Add exception handling to Addressables calls
- [ ] Test physics behavior (solver iterations changed)
- [ ] Consider migrating UGUI to UI Toolkit for new UI
- [ ] Update WebGL shaders for WebGPU
- [ ] Verify minimum platform versions (Android/iOS)

---

**Sources:**
- https://docs.unity3d.com/6000.0/Documentation/Manual/upgrade-guides.html
- https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/upgrade-guide.html
