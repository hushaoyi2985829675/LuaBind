using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LuaBehaviour))]
public class LuaBindBaseDataDrawer : PropertyDrawer
{
    private const string XLuaRoot = "Assets/LuaScript/Lua/";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineH = EditorGUIUtility.singleLineHeight;
        float top = lineH + 10 + lineH + 15 + lineH; // Lua 脚本 + 间距 + 按钮行 + 间距 + 标题行
        float listH = GetListHeight(property, "luaBindValueListComponent", lineH) + 4
                    + GetListHeight(property, "luaBindValueListGameObject", lineH);
        return top + listH;
    }

    private static float GetListHeight(SerializedProperty property, string listName, float lineH)
    {
        SerializedProperty listProp = property.FindPropertyRelative(listName);
        if (listProp == null) return 0f;
        float h = 0f;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty valueListProp = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("valueList");
            if (valueListProp != null) h += valueListProp.arraySize * lineH;
            h += 2;
        }
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty luaScriptName = property.FindPropertyRelative("luaScript");
        if (luaScriptName == null)
            return;

        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;
        float lineH = EditorGUIUtility.singleLineHeight;

        // Lua 脚本：ObjectField
        Rect scriptRect = new Rect(position.x, y, position.width, lineH);
        DrawLuaScriptField(scriptRect, luaScriptName);
        y += lineH + 10;

        // 绑定按钮（同一行，居中均分）
        const float btnGap = 8f;
        float btnW = (position.width - btnGap) * 0.5f;
        float startX = position.x;
        Rect btnRect1 = new Rect(startX, y, btnW, lineH);
        if (GUI.Button(btnRect1, "绑定Lua变量"))
        {
            Object target = property.serializedObject.targetObject;
            if (target is LuaBindBase luaBindBase)
                luaBindBase.AutoBind();
        }
        Rect btnRect2 = new Rect(startX + btnW + btnGap, y, btnW, lineH);
        if (GUI.Button(btnRect2, "清除Lua变量"))
        {
            Object target = property.serializedObject.targetObject;
            if (target is LuaBindBase luaBindBase)
                luaBindBase.ClearBind();
        }
        y += lineH + 15;

        // 标题行（一行字）
        Rect labelRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.LabelField(labelRect, "                   ------------------Lua 绑定列表----------------------");
        y += lineH;
        // GameObject 列表
        y = DrawLuaBindValueList(position, property, y, "luaBindValueListGameObject", typeof(GameObject));
        // Component 列表
        y = DrawLuaBindValueList(position, property, y, "luaBindValueListComponent", typeof(Component));
        EditorGUI.EndProperty();

        if (GUI.changed)
            property.serializedObject.ApplyModifiedProperties();
    }
    

    private float DrawLuaBindValueList(Rect position, SerializedProperty property, float y, string listName, System.Type objectType)
    {
        SerializedProperty listProp = property.FindPropertyRelative(listName);
        if (listProp == null) return y;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty itemProp = listProp.GetArrayElementAtIndex(i);
            SerializedProperty valueListProp = itemProp.FindPropertyRelative("valueList");
            if (valueListProp == null) continue;
            for (int j = 0; j < valueListProp.arraySize; j++)
            {
                SerializedProperty typeInfo = valueListProp.GetArrayElementAtIndex(j);
                SerializedProperty nameProp = itemProp.FindPropertyRelative("name");
                string nameStr = valueListProp.arraySize > 1 ? nameProp.stringValue + "[" + j + "]" : nameProp.stringValue;
                float rowH = EditorGUIUtility.singleLineHeight;
                Rect lineRect = new Rect(position.x, y, position.width, rowH);
                typeInfo.objectReferenceValue = EditorGUI.ObjectField(lineRect, new GUIContent(nameStr), typeInfo.objectReferenceValue, objectType, true);
                y += rowH + 2;
            }
        }
        return y;
    }
    private void DrawLuaScriptField(Rect rect, SerializedProperty luaScriptName)
    {
        Rect labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
        Rect fieldRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, rect.width - EditorGUIUtility.labelWidth, rect.height);

        EditorGUI.PrefixLabel(labelRect, new GUIContent("Lua 脚本"));

        Object currentAsset = null;
        if (!string.IsNullOrEmpty(luaScriptName.stringValue))
        {
            string path = XLuaRoot + luaScriptName.stringValue.Replace('.', '/') + ".lua";
            currentAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        Object newAsset = EditorGUI.ObjectField(fieldRect, currentAsset, typeof(Object), true);
        if (newAsset != null)
        {
            string scriptPath = AssetDatabase.GetAssetPath(newAsset);
            if (scriptPath.EndsWith(".lua") && scriptPath.StartsWith(XLuaRoot))
            {
                string moduleName = scriptPath
                    .Replace(XLuaRoot, "")
                    .Replace(".lua", "")
                    .Replace("/", ".");
                if (luaScriptName.stringValue != moduleName)
                    luaScriptName.stringValue = moduleName;
            }
        }
    }
}
