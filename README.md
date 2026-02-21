# 🩹 Patchly

**The `null` vs "I didn't send this" problem — solved.**

```json
{ "firstName": "Mark", "age": null }
```

☝️ `firstName` → update it. `age` → clear it. `lastName` → **not sent, don't touch it.**

Every PATCH endpoint needs this. Nullable DTOs can't do it. Patchly can.

```bash
dotnet add package Patchly
```

## ⚡ 30-Second Overview

**1. Define your patch DTO:**

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? Age { get; set; }
}
```

**2. Use it in your endpoint:**

```csharp
[HttpPatch("{id}")]
public IActionResult Patch(int id, CustomerPatch patch)
{
    var customer = _repo.Get(id);

    if (patch.Provided.FirstName)
        customer.FirstName = patch.FirstName;

    if (patch.Provided.Age)
        customer.Age = patch.Age;  // could be null — that's intentional

    _repo.Save(customer);
    return Ok(customer);
}
```

**3. That's it.** No configuration. No middleware. No reflection. It just works.

## 🤔 Why Does This Exist?

Every .NET developer building PATCH endpoints hits the same wall: **how do you tell the difference between "the client sent `null`" and "the client didn't send this field at all"?**

The existing options all suck:

| Approach | What's wrong with it |
|---|---|
| 🚫 **Nullable DTOs** | Can't distinguish `null` from absent — was that `null` intentional or just a default? |
| 🚫 **JsonPatchDocument** | Awkward operation-array format (`[{ "op": "replace", "path": "/name" }]`) — terrible generated clients |
| 🚫 **OData Delta\<T\>** | Pulls in the entire OData stack for one feature |
| 🚫 **Wrapper types** | `Patchable<T>`, `JsonMergePatchDocument<T>` — leak into your OpenAPI schema, ugly clients |

Patchly gives you **null vs absent tracking** with a **clean OpenAPI schema** and **zero ceremony**.

## ✅ What You Get

| Feature | |
|---|---|
| 🔍 **Null vs absent** | Distinguishes "set to null" from "not provided" |
| 📄 **Clean OpenAPI** | No wrapper types in the schema — NSwag, Kiota, etc. just work |
| ⚙️ **System.Text.Json native** | Respects `[JsonPropertyName]`, `[JsonIgnore]`, naming policies, and more |
| 🏎️ **Source-generated** | AOT and trimming friendly — no runtime reflection |
| 🪄 **Zero ceremony** | Just add `[PatchDocument]` to a partial class |
| 🏗️ **Nested tracking** | `[PatchDocument]` properties track independently per level |
| 🛡️ **Compile-time diagnostics** | Catches mistakes before you run |

## 📊 How It Compares

| | Null vs absent | Clean OpenAPI | System.Text.Json | No heavy deps | Source-generated | Native AOT |
|---|---|---|---|---|---|---|
| **🩹 Patchly** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (.NET 8+) |
| JsonPatchDocument | ✅ | ❌ | .NET 10+ only | ✅ | ❌ | ❌ |
| OData Delta\<T\> | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| JsonMergePatch | ✅ | ❌ | Separate pkg | ✅ | ❌ | ❌ |
| Nullable DTO | ❌ | ✅ | ✅ | ✅ | N/A | ✅ |

## 📦 Installation

```bash
dotnet add package Patchly
```

**Requirements:** .NET 6+ · C# 9+ (C# 12+ for `required` keyword)

Patchly uses System.Text.Json, which ships in-box with .NET 6+. No additional dependencies.

## 🔧 How It Works Under the Hood

Patchly source-generates three things for each `[PatchDocument]` class:

1. 🔄 A **`JsonConverter`** that tracks which properties were present in the JSON during deserialization
2. ✅ A **`Provided` accessor** with per-property booleans (`patch.Provided.FirstName`)
3. 🔗 A **`WasProvided(string)`** method for generic/dynamic scenarios

### 📡 On the Wire

The client sends plain JSON — only the fields it wants to change:

```json
{ "firstName": "Mark", "age": null }
```

- `firstName` → updated to `"Mark"`
- `age` → explicitly set to `null`
- `lastName` → not sent, **left unchanged**

### 🖥️ In Your Endpoint

Works with controllers:

```csharp
[HttpPatch("{id}")]
public IActionResult Patch(int id, CustomerPatch patch)
{
    var customer = _repo.Get(id);

    if (patch.Provided.FirstName)
        customer.FirstName = patch.FirstName;

    if (patch.Provided.Age)
        customer.Age = patch.Age;  // could be null — that's intentional

    _repo.Save(customer);
    return Ok(customer);
}
```

And minimal APIs — no configuration required:

```csharp
app.MapPatch("/customers/{id}", (int id, CustomerPatch patch) =>
{
    var customer = repo.Get(id);

    if (patch.Provided.FirstName)
        customer.FirstName = patch.FirstName;

    if (patch.Provided.Age)
        customer.Age = patch.Age;

    repo.Save(customer);
    return Results.Ok(customer);
});
```

> 💡 The `Provided` accessor is IntelliSense-discoverable — no magic strings. For generic/dynamic scenarios, `WasProvided(string)` and `ProvidedProperties` are also available via the [`IPatchDocument` interface](#-ipatchdocument-interface).

### 📋 In Your OpenAPI Schema

Just a plain object with nullable properties — no wrapper types, no special formats:

```json
{
  "CustomerPatch": {
    "type": "object",
    "properties": {
      "firstName": { "type": "string", "nullable": true },
      "lastName": { "type": "string", "nullable": true },
      "age": { "type": "integer", "format": "int32", "nullable": true }
    }
  }
}
```

NSwag, Kiota, and other generators produce a clean, idiomatic client.

## 📲 Client-Side Behaviour

How the client sends partial updates depends on which client generator you use.

### Kiota (recommended)

Kiota's [backing store](https://learn.microsoft.com/en-us/openapi/kiota/backing-store) tracks which properties your code actually sets and only serializes those — including explicit nulls. Works perfectly with Patchly out of the box:

```csharp
var patch = new CustomerPatch();
patch.FirstName = "Mark";
patch.Age = null;  // explicitly clear age
await client.Customers[id].PatchAsync(patch);
// Sends: { "firstName": "Mark", "age": null }
// lastName is NOT sent — Kiota knows it was never touched
```

### NSwag

NSwag generates plain DTOs with no change tracking, so by default `System.Text.Json` serializes all properties. Configure your serializer to skip nulls:

```csharp
var options = new JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
```

This covers most cases. The trade-off is you can't explicitly send `null` to clear a field. For that edge case, construct the request manually:

```csharp
var body = new JsonObject
{
    ["firstName"] = "Mark",
    ["age"] = null
};
```

## 🏗️ Nested Patch Documents

When a property's type is itself a `[PatchDocument]`, tracking works independently at each level:

```csharp
[PatchDocument]
public partial class AddressPatch
{
    public string? Line1 { get; set; }
    public string? City { get; set; }
}

[PatchDocument]
public partial class CustomerPatch
{
    public string? FirstName { get; set; }
    public AddressPatch? Address { get; set; }
}
```

```json
{ "address": { "line1": "123 Main St" } }
```

```csharp
patch.Provided.Address       // ✅ true — address object was sent
patch.Address.Provided.Line1 // ✅ true — line1 was sent within address
patch.Address.Provided.City  // ❌ false — city was not sent
patch.Provided.FirstName     // ❌ false — not sent at all
```

## 🗺️ Patch Mapping

For projects with many patch endpoints, Patchly provides a structured mapping pattern that centralizes patch-to-entity logic with DI integration.

**1. Define a map:**

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

**2. Register all maps at startup:**

```csharp
builder.Services.AddPatchlyMaps();
```

**3. Inject `IPatchApplier` in your endpoints:**

```csharp
[HttpPatch("{id}")]
public IActionResult Patch(int id, CustomerPatch patch, [FromServices] IPatchApplier patchApplier)
{
    var customer = _repo.Get(id);
    patchApplier.Apply(patch, customer);
    _repo.Save(customer);
    return Ok(customer);
}
```

The source generator discovers all `PatchMap<,>` subclasses and generates:
- **`PatchApplier`** — the `IPatchApplier` implementation that dispatches to the correct map
- **`AddPatchlyMaps()`** — registers all maps and the applier with DI

Maps can take constructor dependencies (loggers, services, etc.) since they're resolved from the container. One map per `(TPatch, TTarget)` pair — the generator emits a compile error (`PATCH020`) if duplicates are found.

## 🚀 Native AOT Support

Patchly works with Native AOT (`PublishAot=true`) on .NET 8+. Add `PatchlyJsonTypeInfoResolver` to your resolver chain **before** your `JsonSerializerContext`:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, PatchlyJsonTypeInfoResolver.Default);
});
```

Or with manual `JsonSerializerOptions`:

```csharp
var options = new JsonSerializerOptions
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine(
        PatchlyJsonTypeInfoResolver.Default,
        AppJsonContext.Default)
};
```

**Important:** The Patchly resolver handles `[PatchDocument]` types only. Property-level types (e.g., `string`, `int?`, `List<string>`) must be covered by your `JsonSerializerContext`. Non-AOT apps don't need any of this — the existing `[JsonConverter]` attribute continues to work automatically.

## 🎛️ Supported JSON Attributes

Patchly respects standard System.Text.Json attributes on your properties:

| Attribute | Effect |
|---|---|
| `[JsonPropertyName("name")]` | Overrides the JSON property name used during deserialization |
| `[JsonIgnore]` | Excludes the property from serialization, deserialization, and tracking |
| `[JsonInclude]` | Includes non-public properties in serialization and tracking |
| `[JsonNumberHandling(...)]` | Applies per-property number handling (e.g., `AllowReadingFromString`) |
| `required` keyword | Supported — the generated converter applies `[SetsRequiredMembers]` internally |

The generated converter also works with all `JsonSerializerOptions` configuration: naming policies, `PropertyNameCaseInsensitive`, `DefaultIgnoreCondition`, and `JsonSerializerDefaults.Web`.

## 🔗 IPatchDocument Interface

All generated patch documents implement `IPatchDocument`, which exposes:

- `bool WasProvided(string propertyName)` — check by C# property name (case-insensitive)
- `IReadOnlySet<string> ProvidedProperties` — the set of C# property names that were present in the JSON

Use `IPatchDocument` for generic constraints:

```csharp
public void ApplyPatch<T>(T patch, Action<T> apply) where T : IPatchDocument
{
    if (patch.ProvidedProperties.Any())
        apply(patch);
}
```

## 🛡️ Diagnostics

The source generator catches problems at compile time so you don't have to debug them at runtime.

### Errors

| Code | Description |
|---|---|
| PATCH001 | Class must be declared as `partial` |
| PATCH002 | `[PatchDocument]` cannot be applied to structs |
| PATCH003 | `[PatchDocument]` is not supported on record types |
| PATCH004 | `[PatchDocument]` cannot be applied to abstract classes |
| PATCH005 | `[PatchDocument]` does not support generic type parameters |
| PATCH006 | Class must have an accessible parameterless constructor |
| PATCH013 | `init`-only properties are not supported |
| PATCH014 | `[JsonExtensionData]` is not supported |
| PATCH020 | Duplicate `PatchMap` for the same `(TPatch, TTarget)` pair |

### Warnings

| Code | Description |
|---|---|
| PATCH010 | Non-nullable value type property cannot distinguish "not provided" from "default value" |
| PATCH011 | Patch document has no public properties to track |
| PATCH012 | Read-only property will be excluded from deserialization and tracking |
| PATCH015 | `[JsonConstructor]` is ignored by the generated converter |

## 📄 License

MIT
