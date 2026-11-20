# SerializableProperties

Helper types for things Unity cannot serialize or show in the Inspector on its own.

| Type | What it gives you |
|---|---|
| `Serializable2DArray<T>` | A 2D grid, drawn as a resizable table |
| `SerializableQueue<T>` | FIFO queue that saves and shows in the Inspector |
| `SerializableStack<T>` | LIFO stack that saves and shows in the Inspector |
| `SerializedGeneric<T>` | An interface/abstract field with a type picker |

```csharp
[SerializeField] private SerializableQueue<int> spawnOrder;
[SerializeField] private SerializableStack<string> undoHistory;

spawnOrder.Enqueue(5);
int next = spawnOrder.Dequeue();   // 5
```

`SerializableQueue<T>` and `SerializableStack<T>` store their data in one serialized
`List<T>` and nothing else, so the Inspector and the runtime object can never drift
apart. `Dequeue()` is O(n) by design; `Push`/`Pop` are O(1). Enumeration matches the
BCL types: queue front-first, stack top-first. Use `ToQueue()` / `ToStack()` for APIs
that need the exact BCL type — those are copies and do not stay in sync.

**Dictionary is out of scope.** Unity 6.6 serializes `Dictionary<K,V>` natively.

## Running the tests

Package tests need the project `Packages/manifest.json` to list this package under
`"testables"`:

```json
"testables": [ "battleturn.serializable_properties" ]
```
