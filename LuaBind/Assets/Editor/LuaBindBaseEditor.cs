using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LuaBindBaseData))]
public class LuaBindBaseDataDrawer : PropertyDrawer
{
    private const string XLuaRoot = "Assets/MyLua/";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineH = EditorGUIUtility.singleLineHeight;
        return lineH * 2 + 10 + lineH; // Lua 脚本 + 间距 + 按钮
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty luaScriptName = property.FindPropertyRelative("LuaScriptName");
        if (luaScriptName == null)
            return;

        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;
        float lineH = EditorGUIUtility.singleLineHeight;

        // Lua 脚本：ObjectField
        Rect scriptRect = new Rect(position.x, y, position.width, lineH);
        DrawLuaScriptField(scriptRect, luaScriptName);
        y += lineH + 10;

        // 绑定按钮
        Rect btnRect = new Rect(position.x, y, position.width, lineH);
        if (GUI.Button(btnRect, "绑定Lua变量"))
        {
            Object target = property.serializedObject.targetObject;
            if (target is LuaBindBase luaBindBase)
                luaBindBase.AutoBind();
        }

        EditorGUI.EndProperty();

        if (GUI.changed)
            property.serializedObject.ApplyModifiedProperties();
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
