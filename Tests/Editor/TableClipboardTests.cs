using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BattleTurn.Serializable.Editor;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Tests
{
    public class TableClipboardTests
    {
        [Serializable]
        public class Row
        {
            public string id;
            public int hp;
            public float speed;
            public KeyCode key;
            public Vector3 position;
            public Color tint;
            public bool alive;
        }

        private class Holder : ScriptableObject
        {
            public SerializableTable<Row> table = new();
        }

        private static readonly string[] Paths =
            { "id", "hp", "speed", "key", "position", "tint", "alive" };
        private static readonly string[] Headers =
            { "Id", "Hp", "Speed", "Key", "Position", "Tint", "Alive" };

        private Holder _holder;
        private SerializedObject _serialized;

        [SetUp]
        public void SetUp()
        {
            _holder = ScriptableObject.CreateInstance<Holder>();
            _serialized = new SerializedObject(_holder);
        }

        [TearDown]
        public void TearDown()
        {
            _serialized?.Dispose();
            if (_holder != null) UnityEngine.Object.DestroyImmediate(_holder);
        }

        private SerializedProperty Rows()
        {
            _serialized.Update();
            return _serialized.FindProperty("table").FindPropertyRelative("rows");
        }

        [Test]
        public void ParseTsv_SplitsRowsAndCells()
        {
            List<List<string>> table = TableClipboard.ParseTsv("a\tb\nc\td\n");

            Assert.AreEqual(2, table.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, table[0]);
            CollectionAssert.AreEqual(new[] { "c", "d" }, table[1]);
        }

        [Test]
        public void ParseTsv_KeepsTabsAndNewlinesInsideQuotes()
        {
            List<List<string>> table = TableClipboard.ParseTsv("\"a\tb\"\t\"line1\nline2\"\nx\ty");

            Assert.AreEqual(2, table.Count);
            CollectionAssert.AreEqual(new[] { "a\tb", "line1\nline2" }, table[0]);
            CollectionAssert.AreEqual(new[] { "x", "y" }, table[1]);
        }

        [Test]
        public void ParseTsv_UnescapesDoubledQuotes()
        {
            List<List<string>> table = TableClipboard.ParseTsv("\"say \"\"hi\"\"\"\tb");

            CollectionAssert.AreEqual(new[] { "say \"hi\"", "b" }, table[0]);
        }

        [Test]
        public void ParseTsv_HandlesEmptyInput()
        {
            Assert.IsEmpty(TableClipboard.ParseTsv(""));
            Assert.IsEmpty(TableClipboard.ParseTsv(null));
        }

        [Test]
        public void CopyThenPaste_RoundTripsEverySupportedType()
        {
            _holder.table.Add(new Row
            {
                id = "slime",
                hp = 10,
                speed = 3.5f,
                key = KeyCode.Space,
                position = new Vector3(1f, 2f, 3f),
                tint = new Color(0.25f, 0.5f, 0.75f, 1f),
                alive = true
            });

            SerializedProperty rows = Rows();
            string tsv = TableClipboard.ToTsv(rows, new[] { 0 }, Paths, Headers);

            // Paste the copied row underneath itself.
            int written = TableClipboard.Paste(rows, 1, Paths, tsv, Headers, out int skipped);
            _serialized.ApplyModifiedProperties();

            Assert.AreEqual(0, skipped, "every column should have been usable");
            Assert.AreEqual(Paths.Length, written);
            Assert.AreEqual(2, _holder.table.Count);

            Row copy = _holder.table[1];
            Assert.AreEqual("slime", copy.id);
            Assert.AreEqual(10, copy.hp);
            Assert.AreEqual(3.5f, copy.speed, 1e-6f);
            Assert.AreEqual(KeyCode.Space, copy.key);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), copy.position);
            Assert.AreEqual(0.25f, copy.tint.r, 0.01f);
            Assert.IsTrue(copy.alive);
        }

        [Test]
        public void Paste_SkipsTheHeaderLine()
        {
            SerializedProperty rows = Rows();
            string tsv = "Id\tHp\nslime\t10\n";

            TableClipboard.Paste(rows, 0, new[] { "id", "hp" }, tsv, new[] { "Id", "Hp" }, out _);
            _serialized.ApplyModifiedProperties();

            Assert.AreEqual(1, _holder.table.Count, "the header must not become a row");
            Assert.AreEqual("slime", _holder.table[0].id);
        }

        [Test]
        public void Paste_WithoutHeaderTreatsTheFirstLineAsData()
        {
            SerializedProperty rows = Rows();

            TableClipboard.Paste(rows, 0, new[] { "id", "hp" }, "slime\t10\nbat\t6\n", null, out _);
            _serialized.ApplyModifiedProperties();

            Assert.AreEqual(2, _holder.table.Count);
            Assert.AreEqual("bat", _holder.table[1].id);
        }

        [Test]
        public void Paste_GrowsTheTable()
        {
            SerializedProperty rows = Rows();

            TableClipboard.Paste(rows, 3, new[] { "id" }, "late\n", null, out _);
            _serialized.ApplyModifiedProperties();

            Assert.AreEqual(4, _holder.table.Count, "rows before the paste point are created empty");
            Assert.AreEqual("late", _holder.table[3].id);
        }

        [Test]
        public void ToTsv_QuotesValuesHoldingTabsOrNewlines()
        {
            _holder.table.Add(new Row { id = "a\tb" });
            _holder.table.Add(new Row { id = "line1\nline2" });

            string tsv = TableClipboard.ToTsv(Rows(), new[] { 0, 1 }, new[] { "id" }, null);

            Assert.AreEqual("\"a\tb\"\n\"line1\nline2\"\n", tsv);

            // And it survives the trip back.
            List<List<string>> parsed = TableClipboard.ParseTsv(tsv);
            Assert.AreEqual("a\tb", parsed[0][0]);
            Assert.AreEqual("line1\nline2", parsed[1][0]);
        }

        [Test]
        public void WriteCell_AcceptsEnumByNameAndByIndex()
        {
            _holder.table.Add(new Row());
            SerializedProperty key = Rows().GetArrayElementAtIndex(0).FindPropertyRelative("key");

            Assert.IsTrue(TableClipboard.WriteCell(key, "Escape"));
            Assert.AreEqual((int)KeyCode.Escape, key.intValue);

            int index = key.enumNames.Length - 1;
            Assert.IsTrue(TableClipboard.WriteCell(key, index.ToString()));
            Assert.AreEqual(index, key.enumValueIndex);
        }

        [Test]
        public void WriteCell_RefusesTextThatDoesNotFitAndLeavesTheValue()
        {
            _holder.table.Add(new Row { hp = 42 });
            SerializedProperty hp = Rows().GetArrayElementAtIndex(0).FindPropertyRelative("hp");

            Assert.IsFalse(TableClipboard.WriteCell(hp, "not a number"));
            Assert.AreEqual(42, hp.intValue, "a refused write must not clear the cell");

            SerializedProperty position = Rows().GetArrayElementAtIndex(0).FindPropertyRelative("position");
            Assert.IsFalse(TableClipboard.WriteCell(position, "1,2"), "a Vector3 needs three parts");
        }

        [Test]
        public void ReadCell_ReturnsNullForTypesItCannotRepresent()
        {
            _holder.table.Add(new Row());
            SerializedProperty row = Rows().GetArrayElementAtIndex(0);

            Assert.IsNull(TableClipboard.ReadCell(row), "a whole row is not a single value");
            Assert.IsNull(TableClipboard.ReadCell(null));
        }
    }
}
