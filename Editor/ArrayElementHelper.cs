using System;
using System.Text.RegularExpressions;
using UnityEditor;

public static class ArrayElementHelper
{
    static readonly Regex ArrayElementRegex =
    new Regex(@"\.Array\.data\[(\d+)\]$", RegexOptions.Compiled);

    static bool TryGetArrayElementContext(
    SerializedProperty elementProperty,
    out SerializedProperty arrayProperty,
    out int elementIndex)
{
    arrayProperty = null;
    elementIndex = -1;

    var match = ArrayElementRegex.Match(elementProperty.propertyPath);
    if (!match.Success) return false;

    elementIndex = int.Parse(match.Groups[1].Value);

    // everything before ".Array.data[i]"
    string arrayPath = elementProperty.propertyPath.Substring(0, match.Index);
    arrayProperty = elementProperty.serializedObject.FindProperty(arrayPath);
    return arrayProperty != null && arrayProperty.isArray;
}

static object DeepCloneManaged(object src)
{
    if (src == null) return null;

    var t = src.GetType();
    object dst = Activator.CreateInstance(t);

    try
    {
        string json = EditorJsonUtility.ToJson(src);
        EditorJsonUtility.FromJsonOverwrite(json, dst);
    }
    catch
    {
        // Fallback: keep default instance if JSON clone fails
    }

    return dst;
}

public static void EnsureUniqueManagedInstanceInArray(
    SerializedProperty elementProperty,
    SerializedProperty unityObj,
    SerializedProperty managed)
{
    if (managed == null) return;

    object src = managed.managedReferenceValue;
    if (src == null) return;

    // If it's a UnityEngine.Object reference, sharing is expected.
    if (src is UnityEngine.Object) return;

    if (!TryGetArrayElementContext(elementProperty, out var arrayProp, out int index))
        return;

    if (index <= 0) return;

    long id = managed.managedReferenceId;
    if (id == 0) return;

    for (int i = 0; i < index; i++)
    {
        var siblingManaged = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("managed");
        if (siblingManaged == null) continue;

        if (siblingManaged.managedReferenceId == id && siblingManaged.managedReferenceValue != null)
        {
            managed.managedReferenceValue = DeepCloneManaged(src);
            if (unityObj != null) unityObj.objectReferenceValue = null;
            break;
        }
    }
}

}