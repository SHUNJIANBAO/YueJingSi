using System;
using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using System.Linq;

public class CreateUITool : EditorWindow
{
    [MenuItem("Tools/创建UI界面")]
    static void CreateUI()
    {
        var window = EditorWindow.CreateWindow<CreateUITool>();
        window.titleContent = new GUIContent("创建UI界面");
        window.Show();
    }


    const string c_ScriptPath = "Assets/Scripts/UI/";
    // 预制体必须位于 Resources 目录下，与 UIManager 的 Resources 相对路径 "Prefabs/UI/{name}/{name}" 对齐
    const string c_PrefabPath = "Assets/GameAssets/Resources/Prefabs/UI/";
    string panelName;
    private bool _isAdd;
    private string _scriptName;

    private void OnGUI()
    {
        panelName = EditorGUILayout.TextField("界面名", panelName);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("创建"))
        {
            _scriptName = $"UI{panelName}Panel";
            CreateScript(_scriptName);
            _isAdd = false;
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }


        EditorGUILayout.EndHorizontal();
        if (!_isAdd && !string.IsNullOrEmpty(_scriptName))
        {
            _isAdd = CreatePrefab(_scriptName);
        }
    }

    void CreateScript(string name)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("using System.Collections;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using UnityEngine;");
        builder.AppendLine("using UnityEngine.UI;");
        builder.AppendLine();
        builder.AppendLine($"public class {name} : UIPanelBase");
        builder.AppendLine("{");
        builder.AppendLine("    protected override void GetUIComponents()");
        builder.AppendLine("    {");
        builder.AppendLine("        ");
        builder.AppendLine("    }");
        builder.AppendLine("    protected override void AddUIListeners()");
        builder.AppendLine("    {");
        builder.AppendLine("        base.AddUIListeners();");
        builder.AppendLine("    }");
        builder.AppendLine("    protected override void RemoveUIListeners()");
        builder.AppendLine("    {");
        builder.AppendLine("        base.RemoveUIListeners();");
        builder.AppendLine("    }");
        builder.AppendLine("    protected override void OnOpen(params object[] args)");
        builder.AppendLine("    {");
        builder.AppendLine("        base.OnOpen(args);");
        builder.AppendLine("        ");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        if (!Directory.Exists(c_ScriptPath + name))
        {
            Directory.CreateDirectory(c_ScriptPath + name);
        }

        File.WriteAllText(c_ScriptPath + name + "/" + name + ".cs", builder.ToString());
    }

    bool CreatePrefab(string name)
    {
        // 加载运行时程序集
        string assemblyName = "Assembly-CSharp";
        System.Reflection.Assembly assembly = System.AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblyName);

        // 获取类型
        Type type = assembly.GetType(name);
        if (type == null)
        {
            return false;
        }


        GameObject go = new GameObject(name);
        go.AddComponent<CanvasRenderer>();
        go.AddComponent<CanvasGroup>();

        go.AddComponent(type);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.layer = LayerMask.NameToLayer("UI");
        GameObject bg = new GameObject("-BG");
        rect = bg.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        bg.layer = go.layer;
        bg.transform.SetParent(go.transform, false);
        GameObject root = new GameObject("-Root");
        rect = root.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.layer = go.layer;
        root.transform.SetParent(go.transform, false);
        if (!Directory.Exists(c_PrefabPath + name))
        {
            Directory.CreateDirectory(c_PrefabPath + name);
        }

        PrefabUtility.SaveAsPrefabAsset(go, c_PrefabPath + name + "/" + name + ".prefab");
        DestroyImmediate(go, true);
        return true;
    }
}