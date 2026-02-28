# Diagnostics Reference

Patchly's source generator emits compile-time diagnostics to catch problems before they become runtime surprises. This document explains each diagnostic: what triggers it, why it exists, and what to do instead.

For background on how the generated converter works, see [How It Works](#how-the-generated-converter-works) at the bottom of this page.

## Errors

Errors prevent code generation entirely for the affected type. You must fix these before the generated code will be emitted.

---

### PATCH001 — Class must be declared as `partial`

**Message:** `Class '{0}' must be declared as partial to use [PatchDocument]`

**Rationale:** Patchly works by generating a companion partial class file that adds the `JsonConverter`, `Provided` accessor, and `IPatchDocument` implementation. Without the `partial` keyword, the compiler won't merge the generated code with your class.

**Triggers:**

```csharp
[PatchDocument]
public class CustomerPatch
{
    public string? FirstName { get; set; }
}
```

**Fix:** Add the `partial` keyword.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? FirstName { get; set; }
}
```

---

### PATCH002 — Cannot be applied to a struct

**Message:** `'{0}' is a struct; [PatchDocument] can only be applied to a partial class`

**Rationale:** The generated converter creates the instance with `new T()` and then mutates it by setting properties in a loop. Structs are value types — passing them around copies them, which means the converter would populate a copy that gets discarded. The `_providedProperties` tracking set also requires reference semantics to work correctly.

**Triggers:**

```csharp
[PatchDocument]
public partial struct CustomerPatch
{
    public string? FirstName { get; set; }
}
```

**Fix:** Use a `partial class` instead.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? FirstName { get; set; }
}
```

---

### PATCH003 — Cannot be applied to a record

**Message:** `'{0}' is a record; [PatchDocument] can only be applied to a partial class`

**Rationale:** Records are designed around immutability and value semantics. Their compiler-generated `Equals`, `GetHashCode`, and `with` expressions assume a fixed shape at construction time. Patchly's converter mutates properties after construction, which conflicts with record design patterns. Records also generate init-only properties by default (via positional parameters), which the converter cannot set.

**Triggers:**

```csharp
[PatchDocument]
public partial record CustomerPatch
{
    public string? FirstName { get; set; }
}
```

**Fix:** Use a `partial class` instead.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? FirstName { get; set; }
}
```

---

### PATCH004 — Cannot be applied to an abstract class

**Message:** `'{0}' is abstract; [PatchDocument] classes must be concrete`

**Rationale:** The generated converter calls `new T()` to create an instance during deserialization. Abstract classes cannot be instantiated.

**Triggers:**

```csharp
[PatchDocument]
public abstract partial class BasePatch
{
    public string? Name { get; set; }
}
```

**Fix:** Apply `[PatchDocument]` to the concrete subclass instead.

```csharp
public abstract partial class BasePatch
{
    public string? Name { get; set; }
}

[PatchDocument]
public partial class CustomerPatch : BasePatch
{
    public string? Email { get; set; }
}
```

---

### PATCH005 — Cannot be applied to a generic class

**Message:** `'{0}' is generic; [PatchDocument] classes must not have type parameters`

**Rationale:** The source generator emits a concrete `JsonConverter` with specific property matching logic at compile time. Open generic types don't have a fixed set of property types, so the generator cannot produce a type-safe converter. The generated `ProvidedSet` struct and property-name matching code also require knowing the exact properties at generation time.

**Triggers:**

```csharp
[PatchDocument]
public partial class PatchDocument<T>
{
    public T? Value { get; set; }
}
```

**Fix:** Create concrete patch classes for each use case.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? Name { get; set; }
}

[PatchDocument]
public partial class OrderPatch
{
    public decimal? Total { get; set; }
}
```

---

### PATCH006 — Must have an accessible constructor

**Message:** `'{0}' has no accessible parameterless constructor or [JsonConstructor] constructor; [PatchDocument] classes require one for deserialization`

**Rationale:** The generated `JsonConverter.Read()` method needs a way to create an instance during deserialization. This can be either a parameterless constructor (for the streaming path) or a `[JsonConstructor]`-annotated constructor (for the buffered path). Without either, the converter has no way to create the instance.

**Triggers:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public CustomerPatch(string id) { }
    public string? FirstName { get; set; }
}
```

**Fix:** Add a parameterless constructor, or annotate the parameterized constructor with `[JsonConstructor]`.

```csharp
// Option 1: Parameterless constructor
[PatchDocument]
public partial class CustomerPatch
{
    public CustomerPatch() { }
    public CustomerPatch(string id) { }
    public string? FirstName { get; set; }
}

// Option 2: [JsonConstructor]
[PatchDocument]
public partial class CustomerPatch
{
    [JsonConstructor]
    public CustomerPatch(string? firstName) { FirstName = firstName; }
    public string? FirstName { get; set; }
}
```

---

### PATCH014 — `[JsonExtensionData]` is not supported

**Message:** `Property '{0}' on '{1}' has [JsonExtensionData] which is not supported on [PatchDocument] classes`

**Rationale:** The generated converter handles unrecognized JSON properties by skipping them — it reads past the property name and value and moves to the next token. `[JsonExtensionData]` expects unrecognized properties to be captured into a dictionary, but the converter's skip behavior means they're silently discarded. Rather than produce surprising behavior, the generator rejects this combination.

**Triggers:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}
```

**Fix:** Remove the `[JsonExtensionData]` property. If you need to accept arbitrary fields, deserialize to a `JsonDocument` or `JsonElement` separately.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? FirstName { get; set; }
}
```

---

### PATCH018 — Multiple `[JsonConstructor]` constructors

**Message:** `'{0}' has multiple constructors with [JsonConstructor]; only one is allowed`

**Rationale:** The generator uses the `[JsonConstructor]` constructor to determine how to construct the instance during deserialization. If multiple constructors have the attribute, the generator cannot determine which one to use.

**Triggers:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    [JsonConstructor]
    public CustomerPatch() { }
    [JsonConstructor]
    public CustomerPatch(string? name) { }
    public string? Name { get; set; }
}
```

**Fix:** Apply `[JsonConstructor]` to only one constructor.

---

### PATCH019 — Init-only property not covered by `[JsonConstructor]` parameter

**Message:** `Property '{0}' on '{1}' is init-only but is not covered by any [JsonConstructor] parameter and cannot be set after construction`

**Rationale:** When a `[JsonConstructor]` constructor is present, `init`-only properties that aren't matched by a constructor parameter cannot be set — they can't be passed through the constructor and can't be assigned after construction. The generator requires all `init`-only properties to be covered by a constructor parameter.

**Triggers:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    [JsonConstructor]
    public CustomerPatch(string? name) { Name = name; }
    public string? Name { get; init; }
    public int? Age { get; init; }  // not covered by constructor
}
```

**Fix:** Add the missing property to the constructor parameters.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    [JsonConstructor]
    public CustomerPatch(string? name, int? age) { Name = name; Age = age; }
    public string? Name { get; init; }
    public int? Age { get; init; }
}
```

---

### PATCH020 — Duplicate PatchMap for the same pair

**Message:** `Multiple PatchMap classes ({0}) map the same pair {1}; only one map per (TPatch, TTarget) pair is allowed`

**Rationale:** The generated `PatchApplier` uses a compile-time type switch to dispatch `Apply<TPatch, TTarget>()` calls to the correct `PatchMap`. If two maps register for the same `(TPatch, TTarget)` pair, the generator cannot determine which one to use. Rather than pick one arbitrarily, it reports an error.

**Triggers:**

```csharp
public class CustomerPatchMapA : PatchMap<CustomerPatch, Customer>
{
    public override void Apply(CustomerPatch patch, Customer target) { }
}

public class CustomerPatchMapB : PatchMap<CustomerPatch, Customer>
{
    public override void Apply(CustomerPatch patch, Customer target) { }
}
```

**Fix:** Keep only one map per `(TPatch, TTarget)` pair. If you need different mapping behaviors, use conditional logic within a single map, or use different patch/target types.

```csharp
public class CustomerPatchMap : PatchMap<CustomerPatch, Customer>
{
    public override void Apply(CustomerPatch patch, Customer target)
    {
        if (patch.Provided.FirstName) target.GivenName = patch.FirstName;
        if (patch.Provided.Age)       target.Age = patch.Age ?? 0;
    }
}
```

---

## Warnings

Warnings indicate potential issues but don't prevent code generation. The generator still produces output, but you should review these to avoid runtime surprises.

---

### PATCH010 — Non-nullable value type property

**Message:** `Property '{0}' on '{1}' is a non-nullable value type; check state/Provided semantics because it cannot distinguish between 'not provided' and 'default value' from value alone`

**Rationale:** Patchly's core purpose is distinguishing "not provided" from "provided, even if null." For non-nullable value types like `int`, the default value is `0`. If the client doesn't send the property, the value is `0`. If the client sends `0`, the value is also `0`. The `Provided` accessor still works correctly — `patch.Provided.Count` tells you whether the property was in the JSON — but the property value alone is ambiguous.

**Triggers:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public int Count { get; set; }
}
```

**Fix:** Use a nullable value type so the default is `null` rather than `0`.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public int? Count { get; set; }
}
```

> **Note:** If you intentionally want a non-nullable value type and will always check `patch.Provided.Count` before reading the value, you can suppress this warning with `#pragma warning disable PATCH010`.

---

### PATCH011 — No public properties to track

**Message:** `'{0}' has no public properties to track`

**Rationale:** A `[PatchDocument]` class with no trackable properties serves no purpose — there's nothing to deserialize or track. This can happen when the class is empty, when all properties are marked `[JsonIgnore]`, or when all public properties are read-only (and thus excluded).

**Triggers:**

```csharp
[PatchDocument]
public partial class EmptyPatch
{
}
```

**Fix:** Add at least one public property with a getter and setter.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? FirstName { get; set; }
}
```

---

### PATCH012 — Read-only property excluded

**Message:** `Property '{0}' on '{1}' is read-only and will be excluded from deserialization and the Provided accessor`

**Rationale:** The generated converter sets properties in a loop after construction. Read-only properties (no setter) cannot be assigned to, so the converter cannot populate them from JSON. They are excluded from both the deserialization logic and the `Provided` accessor. This warning lets you know the property won't participate in patch tracking.

**Triggers:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? FullName { get; }
    public string? FirstName { get; set; }
}
```

**Fix:** If the property should be part of the patch, add a setter. If it's intentionally computed or derived, you can suppress the warning.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? FullName { get; set; }
    public string? FirstName { get; set; }
}
```

---

### PATCH017 — `[JsonConstructor]` parameter does not match any property

**Message:** `Constructor parameter '{0}' on '{1}' does not match any tracked property`

**Rationale:** The generator matches `[JsonConstructor]` parameters to tracked properties by name (case-insensitive). Unmatched parameters receive their declared default value (or `default` if none is declared). This warning alerts you to parameters that will never receive a value from the JSON payload.

**Triggers:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    [JsonConstructor]
    public CustomerPatch(string? name, string? role = "user") { Name = name; }
    public string? Name { get; set; }
}
```

**Fix:** Either add a matching property or remove the unmatched parameter.

---

### PATCH030 — Non-nullable collection property in deterministic mode

**Message:** `Property '{0}' on '{1}' is a non-nullable collection; nullable collection types are recommended in deterministic mode to model clear-vs-replace semantics`

**Rationale:** In deterministic mode, collections use explicit states: omitted (no-op), null (clear), and value (replace). A non-nullable collection shape makes clear-vs-replace intent less explicit and easier to misuse in handler code.

**Triggers:**

```csharp
[PatchDocument(SemanticsMode = PatchSemanticsMode.DeterministicV1)]
public partial class CustomerPatch
{
    public List<string> Tags { get; set; } = new();
}
```

**Fix:** Use a nullable collection type.

```csharp
[PatchDocument(SemanticsMode = PatchSemanticsMode.DeterministicV1)]
public partial class CustomerPatch
{
    public List<string>? Tags { get; set; }
}
```

---

### PATCH099 — Type was skipped

**Message:** `'{0}' was skipped due to unresolvable errors`

**Rationale:** This is a fallback diagnostic that indicates the generator encountered an unexpected situation while processing your `[PatchDocument]` class and could not produce generated code. This should be rare — it typically means the type has compilation errors that prevent the generator from analyzing it (e.g., unresolvable types in property declarations).

**Fix:** Check for other compilation errors in the class and fix them first. The generator will retry on the next compilation.

---

## Info

Informational diagnostics provide visibility into generator decisions. They don't indicate a problem.

---

### PATCH016 — Buffered deserialization path used

**Message:** `'{0}' uses buffered deserialization because it has {1}`

**Rationale:** The generator has two codegen paths for `JsonConverter.Read()`: a streaming path (construct first, then set properties) and a buffered path (read all properties into local variables, then construct). The buffered path is selected when the class has `init`-only properties or a `[JsonConstructor]` constructor. This info diagnostic makes the selection visible.

**Triggers:** Any `[PatchDocument]` class with `init`-only properties or a `[JsonConstructor]` constructor.

---

### PATCH021 — Constructor parameter type does not match property type

**Message:** `Constructor parameter '{0}' on '{1}' has type '{2}' but matched property has type '{3}'`

**Rationale:** The generator matches `[JsonConstructor]` parameters to properties by name. When a match is found, the types must also match — the generator passes the buffered property value directly as the constructor argument.

**Triggers:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    [JsonConstructor]
    public CustomerPatch(int? name) { }
    public string? Name { get; set; }
}
```

**Fix:** Ensure the constructor parameter type matches the property type.

---

### PATCH022 — `[JsonConstructor]` missing `[SetsRequiredMembers]`

**Message:** `[JsonConstructor] on '{0}' must have [SetsRequiredMembers] because the class has required members`

**Rationale:** When a class has `required` members and a `[JsonConstructor]` constructor, the generated code constructs the instance via the `[JsonConstructor]` constructor. Without `[SetsRequiredMembers]`, the C# compiler would require `required` members to be set in an object initializer, but the constructor invocation path doesn't use one.

**Triggers:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    [JsonConstructor]
    public CustomerPatch(string? name) { Name = name; }
    public required string? Name { get; set; }
}
```

**Fix:** Add `[SetsRequiredMembers]` to the `[JsonConstructor]` constructor.

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    [SetsRequiredMembers]
    [JsonConstructor]
    public CustomerPatch(string? name) { Name = name; }
    public required string? Name { get; set; }
}
```

---

## How the generated converter works

Understanding the converter's architecture explains why most of these restrictions exist.

The generated `JsonConverter<T>.Read()` method uses one of two paths, selected at compile time:

### Streaming path (default)

Used when the class has a parameterless constructor and all tracked properties have `set` accessors.

1. **Creates an empty instance** via `new T()` (parameterless constructor)
2. **Reads JSON token-by-token** in a `while` loop over the `Utf8JsonReader`
3. **Matches each JSON property name** to a C# property using compile-time name comparisons
4. **Sets the property value** on the instance using the property setter
5. **Records the property name** in a `_providedProperties` hash set
6. **Returns the populated instance** when it reaches the end of the JSON object

### Buffered path

Used when any tracked property is `init`-only or a `[JsonConstructor]` constructor is present.

1. **Declares local variables** for each tracked property and a `bool` flag per property
2. **Reads JSON token-by-token** into the local variables, setting flags for provided properties
3. **Constructs the instance** after the read loop via object initializer (for `init` properties) or constructor invocation (for `[JsonConstructor]`)
4. **Populates `_providedProperties`** by calling `.Add()` for each provided property
5. **Returns the constructed instance**

Both paths produce identical observable behavior. The streaming path avoids extra allocations and is used for the common case. The buffered path enables `init`-only properties and `[JsonConstructor]` support.

Common restrictions across both paths:
- Structs don't work (mutations would be lost on copy)
- `[JsonExtensionData]` is unsupported (unrecognized properties are skipped, not captured)
- Records are blocked (value-based equality and compiler-generated members conflict with tracking)
