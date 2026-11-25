using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BattleTurn.Serializable.Editor;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Tests
{
    public class SerializableTableTests
    {
        [Serializable]
        private class BaseRow
        {
            public int fromBase;
        }

        [Serializable]
        private class Row : BaseRow
        {
            public string visiblePublic;
            [SerializeField] private float visiblePrivate;
            [SerializeField, HideInInspector] private int hidden;
            [NonSerialized] public int notSerialized;
            private int plainPrivate;
            public static int staticField;
            public const int constField = 1;
            public readonly int readOnlyField;

            // Keep the compiler from warning the private fields are never read.
            public float Probe => visiblePrivate + hidden + plainPrivate + readOnlyField;
        }

        private class FooTable : SerializableTable<Row> { }

        [Test]
        public void GetColumns_TakesPublicAndSerializeFieldOnly()
        {
            var names = SerializedFieldScanner.GetColumns(typeof(Row)).Select(f => f.Name).ToList();

            CollectionAssert.AreEqual(
                new[] { "fromBase", "visiblePublic", "visiblePrivate" },
                names,
                "base fields come first, then public, then [SerializeField] private");
        }

        [Test]
        public void GetColumns_SkipsHiddenStaticConstReadonlyAndNonSerialized()
        {
            var names = SerializedFieldScanner.GetColumns(typeof(Row)).Select(f => f.Name).ToList();

            CollectionAssert.DoesNotContain(names, "hidden");
            CollectionAssert.DoesNotContain(names, "notSerialized");
            CollectionAssert.DoesNotContain(names, "plainPrivate");
            CollectionAssert.DoesNotContain(names, "staticField");
            CollectionAssert.DoesNotContain(names, "constField");
            CollectionAssert.DoesNotContain(names, "readOnlyField");
        }

        // These draw as one value, so the table gives them a single column instead of
        // spreading their members out.
        [TestCase(typeof(int))]
        [TestCase(typeof(string))]
        [TestCase(typeof(KeyCode))]
        [TestCase(typeof(Vector3))]
        [TestCase(typeof(GameObject))]
        public void GetColumns_IsEmptyForSingleValueTypes(Type type)
        {
            Assert.IsEmpty(SerializedFieldScanner.GetColumns(type));
            Assert.IsFalse(SerializedFieldScanner.IsRowType(type));
        }

        [Test]
        public void GetColumns_HandlesNull()
        {
            Assert.IsEmpty(SerializedFieldScanner.GetColumns(null));
        }

        [Test]
        public void GetElementType_UnwrapsEveryDeclarationShape()
        {
            Assert.AreEqual(typeof(Row), SerializableTableDrawer.GetElementType(typeof(SerializableTable<Row>)));
            Assert.AreEqual(typeof(Row), SerializableTableDrawer.GetElementType(typeof(List<SerializableTable<Row>>)));
            Assert.AreEqual(typeof(Row), SerializableTableDrawer.GetElementType(typeof(SerializableTable<Row>[])));
            Assert.AreEqual(typeof(Row), SerializableTableDrawer.GetElementType(typeof(FooTable)));
        }

        [Test]
        public void GetElementType_ReturnsNullForUnrelatedTypes()
        {
            Assert.IsNull(SerializableTableDrawer.GetElementType(typeof(List<int>)));
            Assert.IsNull(SerializableTableDrawer.GetElementType(null));
        }

        [Test]
        public void Table_DelegatesToItsRows()
        {
            var table = new SerializableTable<int>();
            table.Add(1);
            table.Add(2);
            table.Insert(0, 0);

            Assert.AreEqual(3, table.Count);
            Assert.AreEqual(0, table[0]);
            Assert.AreEqual(1, table.IndexOf(1));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, table.ToList());
            Assert.AreSame(table.Rows, table.Rows, "Rows exposes the backing list, not a copy");

            table.RemoveAt(0);
            CollectionAssert.AreEqual(new[] { 1, 2 }, table.ToList());

            table.Clear();
            Assert.AreEqual(0, table.Count);
        }

        // Row height is 20 and the viewport shows at most 12 rows, so 240 units.
        [Test]
        public void FirstAndLastVisible_CoversTheViewportAndNothingMore()
        {
            SerializableTableDrawer.FirstAndLastVisible(0f, 240f, 1000, out int first, out int last);
            Assert.AreEqual(0, first);
            Assert.AreEqual(12, last, "one row of overlap is enough to cover a partial row");

            SerializableTableDrawer.FirstAndLastVisible(100f, 240f, 1000, out first, out last);
            Assert.AreEqual(5, first);
            Assert.AreEqual(17, last);
        }

        [Test]
        public void FirstAndLastVisible_StaysInsideTheTable()
        {
            SerializableTableDrawer.FirstAndLastVisible(0f, 240f, 3, out int first, out int last);
            Assert.AreEqual(0, first);
            Assert.AreEqual(2, last, "never past the last row");

            SerializableTableDrawer.FirstAndLastVisible(99999f, 240f, 3, out first, out last);
            Assert.AreEqual(2, first, "a scroll past the end still points at a real row");
            Assert.AreEqual(2, last);
        }

        [Test]
        public void FirstAndLastVisible_EmptyTableYieldsNoRows()
        {
            SerializableTableDrawer.FirstAndLastVisible(0f, 240f, 0, out int first, out int last);

            Assert.Less(last, first, "the draw loop must not run for an empty table");
        }

        [Test]
        public void DataIndexForSlot_IsIdentityWhenNothingIsBeingDragged()
        {
            for (int slot = 0; slot < 5; slot++)
                Assert.AreEqual(slot, SerializableTableDrawer.DataIndexForSlot(slot, -1, -1));
        }

        // Dragging row 3 up to slot 1 of [0 1 2 3 4] must show 0, 3, 1, 2, 4.
        [Test]
        public void DataIndexForSlot_DraggingUpwardsPushesTheRowsBetweenThemDown()
        {
            int[] shown = new int[5];
            for (int slot = 0; slot < 5; slot++)
                shown[slot] = SerializableTableDrawer.DataIndexForSlot(slot, 3, 1);

            CollectionAssert.AreEqual(new[] { 0, 3, 1, 2, 4 }, shown);
        }

        // Dragging row 1 down to slot 3 of [0 1 2 3 4] must show 0, 2, 3, 1, 4.
        [Test]
        public void DataIndexForSlot_DraggingDownwardsPullsTheRowsBetweenThemUp()
        {
            int[] shown = new int[5];
            for (int slot = 0; slot < 5; slot++)
                shown[slot] = SerializableTableDrawer.DataIndexForSlot(slot, 1, 3);

            CollectionAssert.AreEqual(new[] { 0, 2, 3, 1, 4 }, shown);
        }

        [Test]
        public void DataIndexForSlot_ShowsEveryRowExactlyOnce()
        {
            // Whatever the drag, the view must be a permutation - no row lost, none doubled.
            for (int from = 0; from < 6; from++)
                for (int to = 0; to < 6; to++)
                {
                    var seen = new List<int>();
                    for (int slot = 0; slot < 6; slot++)
                        seen.Add(SerializableTableDrawer.DataIndexForSlot(slot, from, to));

                    seen.Sort();
                    CollectionAssert.AreEqual(
                        new[] { 0, 1, 2, 3, 4, 5 }, seen,
                        "drag from " + from + " to " + to);
                }
        }

        private static HashSet<int> Selection(params int[] rows) => new(rows);

        [Test]
        public void Click_SelectsOnlyThatRow()
        {
            var selected = Selection(0, 1, 2);

            int anchor = SerializableTableDrawer.ApplySelectionClick(
                selected, 0, 4, 10, shift: false, toggle: false);

            CollectionAssert.AreEquivalent(new[] { 4 }, selected);
            Assert.AreEqual(4, anchor);
        }

        [Test]
        public void ClickOnTheOnlySelectedRow_ClearsTheSelection()
        {
            var selected = Selection(4);

            int anchor = SerializableTableDrawer.ApplySelectionClick(
                selected, 4, 4, 10, shift: false, toggle: false);

            Assert.IsEmpty(selected);
            Assert.AreEqual(-1, anchor);
        }

        [Test]
        public void ShiftClick_TakesTheWholeRangeAndKeepsTheAnchor()
        {
            var selected = Selection(2);

            int anchor = SerializableTableDrawer.ApplySelectionClick(
                selected, 2, 5, 10, shift: true, toggle: false);

            CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, selected);
            Assert.AreEqual(2, anchor, "the anchor stays put so the range can be stretched again");

            // Stretching back the other way replaces the range rather than adding to it.
            SerializableTableDrawer.ApplySelectionClick(
                selected, anchor, 0, 10, shift: true, toggle: false);

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, selected);
        }

        [Test]
        public void CtrlClick_AddsThenRemovesOneRow()
        {
            var selected = Selection(1);

            SerializableTableDrawer.ApplySelectionClick(selected, 1, 7, 10, false, toggle: true);
            CollectionAssert.AreEquivalent(new[] { 1, 7 }, selected);

            SerializableTableDrawer.ApplySelectionClick(selected, 7, 7, 10, false, toggle: true);
            CollectionAssert.AreEquivalent(new[] { 1 }, selected);
        }

        [Test]
        public void ShiftClick_WithAStaleAnchorFallsBackToASingleRow()
        {
            // Rows were deleted, so the anchor points past the end of the table.
            var selected = Selection(8);

            int anchor = SerializableTableDrawer.ApplySelectionClick(
                selected, 8, 1, 3, shift: true, toggle: false);

            CollectionAssert.AreEquivalent(new[] { 1 }, selected);
            Assert.AreEqual(1, anchor);
        }

        [Test]
        public void Click_OutsideTheTableChangesNothing()
        {
            var selected = Selection(2);

            int anchor = SerializableTableDrawer.ApplySelectionClick(selected, 2, 9, 3, false, false);

            CollectionAssert.AreEquivalent(new[] { 2 }, selected);
            Assert.AreEqual(2, anchor);
        }
    }
}
