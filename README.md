# Capsicum

[![CI](https://github.com/LambdaSix/Capsicum/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/LambdaSix/Capsicum/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A small, allocation-conscious Entity Component System for .NET.

Capsicum stores each component type in its own sparse set, so component lookups, queries and iteration
allocate nothing in steady state. It's meant for game loops and simulation ticks, where garbage-collection
pauses matter.

## Table of Contents

- [Features](#features)
- [Getting Started](#getting-started)
- [Usage](#usage)
- [Design Notes](#design-notes)
- [Building and Testing](#building-and-testing)
- [Contributing](#contributing)
- [License](#license)

## Features

- **O(1) component access.** Each `Pool` keeps one sparse-set store per component type. Get, has, add and remove are array lookups.
- **Allocation-free queries.** `pool.Query().With<A>().Without<B>()` is a struct with a struct enumerator. It walks the smallest store it requires.
- **Deterministic ordering when you need it.** `Pool.QueryInto` fills a reused list sorted by entity index.
- **Generational handles.** An `EntityHandle` names a slot plus the generation it was created on, so a stale handle never resolves to the slot's new occupant.
- **Safe iteration.** Adding or removing a component type a query depends on while it runs throws, instead of skipping or repeating entities.
- **Typed events.** Each component type has its own `Added`, `Removed` and `Replaced` events, with no per-entity subscriptions.
- **Binary serialization** for components that implement `ISerializableComponent`.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Installation

Capsicum isn't published to NuGet. Add it as a git submodule and reference the project:

```sh
git submodule add https://github.com/LambdaSix/Capsicum lib/Capsicum
dotnet add reference lib/Capsicum/Capsicum/Capsicum.csproj
```

## Usage

### Components

A component is any class that implements the `IComponent` marker interface:

```csharp
using Capsicum;
using Capsicum.Interfaces;

public sealed class Position : IComponent {
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class Velocity : IComponent {
    public int Dx { get; set; }
    public int Dy { get; set; }
}

public sealed class Frozen : IComponent {}
```

### Entities

```csharp
var pool = new Pool();

var player = pool.CreateEntity()
    .AddComponent(new Position { X = 1, Y = 2 })
    .AddComponent(new Velocity { Dx = 1 });

var rock = pool.CreateEntity(new IComponent[] { new Position { X = 5, Y = 5 } });

var position = player.GetComponent<Position>();             // throws if missing
var velocity = rock.GetComponentOf<Velocity>();             // null if missing
if (player.TryGetComponent<Velocity>(out var v)) { /* ... */ }
bool moving = player.HasComponent<Velocity>();

player.ReplaceComponent(new Velocity { Dx = 0, Dy = 1 });
player.RemoveComponent<Velocity>();

pool.RemoveEntity(rock);                                    // releases its slot and components
```

Lookups match the exact runtime type a component was added as, not base classes or interfaces.

### Queries

```csharp
foreach (var entity in pool.Query().With<Position>().With<Velocity>().Without<Frozen>()) {
    var p = entity.GetComponent<Position>();
    var v = entity.GetComponent<Velocity>();
    p.X += v.Dx;
    p.Y += v.Dy;
}
```

A query takes up to four `With` and four `Without` clauses. Direct enumeration follows each store's
internal order, which depends on add/remove history. If order matters, or you need to change the stores
you're querying, collect the matches first:

```csharp
private readonly List<Entity> _buffer = new();

void Tick(Pool pool) {
    pool.QueryInto(pool.Query().With<Velocity>(), _buffer);    // sorted by handle index
    foreach (var entity in _buffer)
        entity.AddComponent(new Frozen());                     // safe: not enumerating a store
}
```

Changing a store the running query names, or creating or releasing entities during a query with no
`With` clause, throws `InvalidOperationException`. Changing unrelated component types is allowed.

### Handles

```csharp
EntityHandle handle = player.Handle;            // { Index, Generation }

if (pool.TryGetEntity(handle, out var live)) {
    // still the same entity
}

pool.IsAlive(handle);                           // false once the entity is released
```

Released slot indices are reused first-in first-out with their generation incremented. `Entity` objects
are never reused: a released entity reports no components and refuses new ones.

### Events

```csharp
var positions = pool.Components<Position>();

positions.Added    += (entity, component) => Console.WriteLine($"{entity} placed");
positions.Removed  += (entity, component) => Console.WriteLine($"{entity} lifted");
positions.Replaced += (entity, previous, current) => Console.WriteLine($"{entity} moved");

pool.OnEntityCreated += (sender, e) => Console.WriteLine($"created {e.Entity}");
```

Components added through `CreateEntity(components)` don't raise `Added`. Releasing an entity raises
`Removed` only when you ask: `pool.RemoveEntity(entity, notifyComponents: true)`.

### Aspects

An `IEntityAspect` packages a reusable composition:

```csharp
public sealed class Projectile : IEntityAspect {
    public void Setup(Entity entity) =>
        entity.AddComponent(new Position()).AddComponent(new Velocity { Dx = 4 });
}

var bullet = pool.CreateEntity(new Projectile());
```

### Serialization

Components that implement `ISerializableComponent` can be written and read with the binary
`EntityWriter` and `EntityReader` in `Capsicum.Serialization.Binary`:

```csharp
byte[] data = new EntityWriter().Serialize(pool);
var restored = new EntityReader().ReadFrom(data, components => newPool.CreateEntity(components)).ToList();
```

The format names components by assembly-qualified type name. That makes it useful for tooling and tests, but
not for durable saves that must survive refactoring.

## Design Notes

| Concern | Approach |
|---|---|
| Storage | One sparse set per component type in each pool: a sparse index array, a dense component array and a dense index array. Removal swaps the last entry into the hole. |
| Type lookup | A static generic cache (`ComponentType<T>.Id`) indexes the pool's store array, with no dictionary on the hot path. |
| Identity | `EntityHandle` = slot index + generation. Index 0 is never issued. |
| Hashing | `Entity` hashes by reference. `EntityHandle` uses a fixed combination of index and generation, never a per-process seed. |
| Ordering | Stores iterate in insertion/removal order. `QueryInto` and `Pool` enumeration are in handle index order. |
| Threading | A pool isn't thread-safe. Use it from one thread, or put your own synchronisation around it. |

Component type ids are assigned in first-use order within a process, so `Entity.GetComponents()` is not
ordered consistently across runs.

## Building and Testing

```sh
dotnet restore Capsicum.sln
dotnet build Capsicum.sln --configuration Release --no-restore
dotnet test Capsicum.sln --configuration Release --no-build
```

The test suite includes allocation tests that assert zero bytes allocated (via
`GC.GetAllocatedBytesForCurrentThread`) for lookups, queries, `QueryInto`, pool enumeration and component churn.

## Contributing

1. Fork the repository and create a branch from `develop`.
2. Add tests for any behaviour you change. Hot-path changes need an allocation test.
3. Make sure `dotnet test Capsicum.sln` passes.
4. Open a pull request against `develop`.

## License

Distributed under the MIT License. See [`LICENSE`](LICENSE) for details.
