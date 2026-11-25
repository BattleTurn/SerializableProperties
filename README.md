# SerializableProperties

Helper types for things Unity cannot serialize or show in the Inspector on its own.
Requires Unity 2022.1 or newer.

| Type | What it gives you |
|---|---|
| `Serializable2DArray<T>` | A 2D grid, drawn as a resizable table |
| `SerializableQueue<T>` | FIFO queue that saves and shows in the Inspector |
| `SerializableStack<T>` | LIFO stack that saves and shows in the Inspector |
| `SerializableTable<T>` | A list drawn as rows x columns, one column per field of `T` |
| `SerializedGeneric<T>` | An interface/abstract field with a type picker |

```csharp
[SerializeField] private SerializableQueue<int> spawnOrder;
[SerializeField] private SerializableStack<string> undoHistory;
[SerializeField] private SerializableTable<EnemyRow> enemies;

spawnOrder.Enqueue(5);
int next = spawnOrder.Dequeue();   // 5
enemies.Add(new EnemyRow { id = "slime", health = 10 });
```

## Queue and stack

Both store their data in one serialized `List<T>` and nothing else, so the Inspector and
the runtime object can never drift apart. `Dequeue()` is O(n) by design; `Push`/`Pop` are
O(1). Enumeration matches the BCL types: queue front-first, stack top-first. Use
`ToQueue()` / `ToStack()` for APIs that need the exact BCL type - those are copies and do
not stay in sync.

## Table

`SerializableTable<T>` draws one row per element and one column per serialized field of
`T`, with add/remove buttons and a sticky header. Drawn with IMGUI so it works in the default
Inspector; a UI Toolkit only drawer renders "No GUI Implemented" there.
Columns come from the fields Unity itself would show: public fields and `[SerializeField]`
private ones, base-class fields first, skipping `[HideInInspector]`, `[NonSerialized]`,
static, `const` and `readonly`. A `T` that Unity draws as a single value - a primitive,
`string`, enum, `UnityEngine.Object` reference or a built-in struct like `Vector3` - gets
one `Value` column instead.

It is a type rather than a `[TableView]` attribute because Unity dispatches an attribute
drawer **per array element**, so an attribute drawer can never see the whole collection.

### Working in it like a spreadsheet

- **Row numbers** in the first column, and a sticky header row of column names. Click a row
  number to select it, shift-click to take the range from the last click, and ctrl-click
  (cmd on a Mac) to add or drop a single row. Copy takes the selected rows, paste starts at
  the first of them, `+` inserts after the last of them, and `-` deletes all of them.
- **Drag the handle** (the three bars at the far left) to move a row: a gap opens where it
  will land and the row itself follows the pointer. The view scrolls on its own when the
  pointer passes the top or bottom edge. Dragging moves only the row you grabbed, whatever
  else is selected.
- **Keyboard**: `Tab` / `Shift+Tab` move across a row, `Enter` and the up/down arrows move
  between rows in the same column. Left and right arrows are left to the text caret.
- **Copy / Paste** as tab separated text, through the two buttons above the table:
  - *Copy TSV* copies the selected rows, or every row when nothing is selected, with a
    header line. Paste it straight into Excel or Sheets.
  - *Paste TSV* writes the clipboard into the table from the first selected row on, adding
    rows when the clipboard holds more than the table does. A first line matching the
    column names is recognised as a header and skipped.
  - Values holding tabs, newlines or quotes are double-quoted the way Excel does it, so a
    multi-line string survives the round trip.
  - Supported cell types: integers, floats, booleans, strings, enums (by name or index),
    `Color` (hex), `Vector2`, `Vector3`, and asset references (by asset path). Anything
    else is left untouched, and the Console reports how many cells were skipped.
  - Scene object references cannot be pasted: they have no asset path.

Sorting, filtering and column resizing are not wired up; columns share the width evenly
and the table scrolls horizontally once they hit their minimum.

**Dictionary is out of scope.** Unity 6.6 serializes `Dictionary<K,V>` natively.

## Running the tests

Package tests need the project `Packages/manifest.json` to list this package under
`"testables"`:

```json
"testables": [ "battleturn.serializable_properties" ]
```
