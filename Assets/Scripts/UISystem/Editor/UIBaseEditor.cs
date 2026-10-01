using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(UIBase), true)]
public class UIBaseEditor : Editor
{
    private UIBase m_Target => target as UIBase;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty panelTypeProp = serializedObject.FindProperty("_panelType");
        if (panelTypeProp != null)
            EditorGUILayout.PropertyField(panelTypeProp);
        SerializedProperty panelMemoryTypeProp = serializedObject.FindProperty("_panelMemoryType");
        if (panelMemoryTypeProp != null)
            EditorGUILayout.PropertyField(panelMemoryTypeProp);

        EditorGUILayout.Space();
        SerializedProperty uiListProp = serializedObject.FindProperty("_uiList");
        EditorGUILayout.PropertyField(uiListProp, true);

        if (GUILayout.Button("刷新节点"))
        {
            RefreshNodes(uiListProp);
        }

        if (GUILayout.Button("自动生成 UI 引用"))
        {
            RefreshNodes(uiListProp);
            GenerateUIReferences();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void RefreshNodes(SerializedProperty uiListProp)
    {
        var uiList = new List<Transform>(m_Target.transform.GetComponentsInChildren<Transform>(true));
        uiList.RemoveAll(ui => ui.name.StartsWith("-"));
        uiListProp.arraySize = uiList.Count;
        for (int i = 0; i < uiList.Count; i++)
        {
            var element = uiListProp.GetArrayElementAtIndex(i);
            element.objectReferenceValue = uiList[i];
        }
    }

    private void GenerateUIReferences()
    {
        if (m_Target == null)
            return;

        var script = MonoScript.FromMonoBehaviour(m_Target);
        if (script == null)
        {
            EditorUtility.DisplayDialog("生成 UI 引用", "找不到对应的 MonoScript。", "确定");
            return;
        }

        string scriptPath = AssetDatabase.GetAssetPath(script);
        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
        {
            EditorUtility.DisplayDialog("生成 UI 引用", "找不到对应的脚本文件。", "确定");
            return;
        }

        string source = File.ReadAllText(scriptPath);
        var fields = ParseFieldNames(source);
        var bindings = new List<UIBinding>();
        var usedFieldNames = new HashSet<string>(fields, StringComparer.Ordinal);
        bool needsTMPro = false;
        string className = m_Target.GetType().Name;

        // 跳过根节点：根节点本身就是当前 UIBase，不应为自身生成引用。
        var nodes = m_Target.transform.GetComponentsInChildren<Transform>(true)
            .Where(node => node != m_Target.transform && !node.name.StartsWith("-"));

        foreach (var node in nodes)
        {
            string baseFieldName = SanitizeIdentifier(node.name);
            // 约定：节点名就是字段名。已有同名字段视为已接入，不重复生成。
            if (fields.Contains(baseFieldName))
                continue;

            var binding = TryCreateBinding(node, usedFieldNames, baseFieldName);
            if (binding == null)
                continue;

            fields.Add(binding.FieldName);
            usedFieldNames.Add(binding.FieldName);
            bindings.Add(binding);
            needsTMPro |= binding.RequiresTMPro;
        }

        if (bindings.Count == 0)
        {
            EditorUtility.DisplayDialog("生成 UI 引用", "没有发现需要新增的 UI 引用。\n已有字段不会被覆盖。", "确定");
            return;
        }

        if (needsTMPro && !HasUsing(source, "TMPro"))
            source = AddUsing(source, "TMPro");

        string newline = DetectNewLine(source);
        string indent = DetectIndent(source);
        string fieldText = BuildFieldText(bindings, newline, indent);
        source = InsertFields(source, fieldText, newline, className);

        string componentText = BuildComponentText(bindings, newline, indent);
        source = InsertComponentBindings(source, componentText, newline, indent, className);

        File.WriteAllText(scriptPath, source, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceUpdate);
        EditorUtility.SetDirty(script);
        AssetDatabase.SaveAssets();

        Debug.Log($"[UIBaseEditor] 已为 {m_Target.GetType().Name} 生成 {bindings.Count} 个 UI 引用：{scriptPath}");
        EditorUtility.DisplayDialog("生成 UI 引用", $"已生成 {bindings.Count} 个字段和 GetUIComponents 绑定。\n已有代码未覆盖。", "确定");
    }

    private static UIBinding TryCreateBinding(Transform node, HashSet<string> usedFieldNames, string baseFieldName)
    {
        Type componentType = ResolveComponentType(node, out bool requiresTMPro);
        if (componentType == null)
            return null;

        string fieldName = MakeUniqueFieldName(baseFieldName, usedFieldNames);
        return new UIBinding
        {
            FieldName = fieldName,
            NodeName = node.name,
            ComponentType = componentType,
            RequiresTMPro = requiresTMPro
        };
    }

    private static Type ResolveComponentType(Transform node, out bool requiresTMPro)
    {
        requiresTMPro = false;
        string nodeName = node.name;
        int underscore = nodeName.IndexOf('_');
        string prefix = underscore > 0 ? nodeName.Substring(0, underscore) : string.Empty;

        if (string.Equals(prefix, "Image", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "Img", StringComparison.OrdinalIgnoreCase))
            return node.GetComponent<Image>() != null ? typeof(Image) : null;

        if (string.Equals(prefix, "Rect", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "Root", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "Panel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nodeName, "Root", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nodeName, "BG", StringComparison.OrdinalIgnoreCase))
            return node.GetComponent<RectTransform>().GetType();

        if (string.Equals(prefix, "Text", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "Txt", StringComparison.OrdinalIgnoreCase))
        {
            var tmp = node.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                requiresTMPro = true;
                return tmp.GetType();
            }
            return node.GetComponent<Text>() != null ? typeof(Text) : null;
        }

        if (string.Equals(prefix, "Btn", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "Button", StringComparison.OrdinalIgnoreCase))
            return node.GetComponent<Button>() != null ? typeof(Button) : null;

        if (string.Equals(prefix, "Toggle", StringComparison.OrdinalIgnoreCase))
            return node.GetComponent<Toggle>() != null ? typeof(Toggle) : null;

        if (string.Equals(prefix, "Input", StringComparison.OrdinalIgnoreCase))
        {
            var tmpInput = node.GetComponent<TMP_InputField>();
            if (tmpInput != null)
            {
                requiresTMPro = true;
                return tmpInput.GetType();
            }
            return node.GetComponent<InputField>() != null ? typeof(InputField) : null;
        }

        if (string.Equals(prefix, "Slider", StringComparison.OrdinalIgnoreCase))
            return node.GetComponent<Slider>() != null ? typeof(Slider) : null;

        if (string.Equals(prefix, "Scroll", StringComparison.OrdinalIgnoreCase))
            return node.GetComponent<ScrollRect>() != null ? typeof(ScrollRect) : null;

        if (string.Equals(prefix, "RawImage", StringComparison.OrdinalIgnoreCase))
            return node.GetComponent<RawImage>() != null ? typeof(RawImage) : null;

        if (string.Equals(prefix, "Canvas", StringComparison.OrdinalIgnoreCase))
            return node.GetComponent<Canvas>() != null ? typeof(Canvas) : null;

        // 无前缀：只绑定与物体同名的自定义脚本。
        return node.GetComponents<Component>()
            .Where(component => component != null)
            .Select(component => component.GetType())
            .FirstOrDefault(type => type.Name == nodeName && IsCustomComponent(type));
    }

    private static bool IsCustomComponent(Type type)
    {
        if (type == typeof(Transform) || type == typeof(RectTransform) || type == typeof(GameObject))
            return false;
        string assemblyName = type.Assembly.GetName().Name;
        return !assemblyName.StartsWith("UnityEngine", StringComparison.Ordinal) &&
               !assemblyName.StartsWith("UnityEditor", StringComparison.Ordinal);
    }

    private static string BuildFieldText(List<UIBinding> bindings, string newline, string indent)
    {
        var builder = new StringBuilder();
        builder.Append(newline);
        builder.Append(indent).Append("// 自动生成的 UI 引用，请勿重复生成同名字段").Append(newline);
        foreach (var binding in bindings)
        {
            builder.Append(indent).Append("[SerializeField] private ")
                .Append(GetTypeCodeName(binding.ComponentType)).Append(" ")
                .Append(binding.FieldName).Append(";").Append(newline);
        }
        return builder.ToString();
    }

    private static string BuildComponentText(List<UIBinding> bindings, string newline, string indent)
    {
        var builder = new StringBuilder();
        foreach (var binding in bindings)
        {
            builder.Append(indent).Append(indent)
                .Append(binding.FieldName).Append(" = GetUI<")
                .Append(GetTypeCodeName(binding.ComponentType)).Append(">(")
                .Append("\"").Append(binding.NodeName).Append("\"");
            builder.Append(");").Append(newline);
        }
        return builder.ToString();
    }

    private static string InsertFields(string source, string fieldText, string newline, string className)
    {
        int classOpen = FindClassOpeningBrace(source, className);
        if (classOpen < 0)
            throw new InvalidOperationException($"无法定位 UI 类 {className} 的定义。");
        return source.Insert(classOpen + 1, fieldText);
    }

    private static string InsertComponentBindings(string source, string componentText, string newline, string indent, string className)
    {
        int classOpen = FindClassOpeningBrace(source, className);
        int classClose = FindMatchingBrace(source, classOpen);
        if (classOpen < 0 || classClose < 0)
            throw new InvalidOperationException($"无法定位 UI 类 {className} 的结束位置。");

        const string methodSignature = "protected override void GetUIComponents";
        int methodStart = source.IndexOf(methodSignature, classOpen, classClose - classOpen, StringComparison.Ordinal);
        if (methodStart >= 0)
        {
            int openBrace = source.IndexOf('{', methodStart);
            int closeBrace = FindMatchingBrace(source, openBrace);
            if (openBrace >= 0 && closeBrace > openBrace && closeBrace < classClose)
                return source.Insert(openBrace + 1, newline + componentText);
        }

        string method = newline + indent + "protected override void GetUIComponents()" + newline + indent + "{" + newline
            + componentText + indent + "}" + newline;
        return source.Insert(classClose, method);
    }

    private static HashSet<string> ParseFieldNames(string source)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var matches = Regex.Matches(source,
            @"(?:(?:public|private|protected|internal|static|readonly|const|\s)+)\s*[\w\.<>\[\],?]+\s+(\w+)\s*(?:=|;)");
        foreach (Match match in matches)
            result.Add(match.Groups[1].Value);
        return result;
    }

    private static int FindClassOpeningBrace(string source, string className)
    {
        var match = Regex.Match(source, @"\bclass\s+" + Regex.Escape(className) + @"\b");
        if (!match.Success)
            return -1;
        return source.IndexOf('{', match.Index);
    }

    private static int GetClassCloseBrace(string source, string className)
    {
        int openBrace = FindClassOpeningBrace(source, className);
        return FindMatchingBrace(source, openBrace);
    }

    private static int FindMatchingBrace(string source, int openBrace)
    {
        if (openBrace < 0)
            return -1;
        int depth = 0;
        for (int i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static string GetTypeCodeName(Type type)
    {
        // 输出简单类型名（如 Image / TextMeshProUGUI / RectTransform）。
        // 依赖脚本文件已有的 using：UnityEngine.UI 为面板脚本惯例引入，
        // TMPro 命名空间已在生成时按需自动补充 using。
        return type.Name;
    }

    private static string AddUsing(string source, string namespaceName)
    {
        MatchCollection matches = Regex.Matches(source, @"^using\s+[^;]+;\s*$", RegexOptions.Multiline);
        if (matches.Count == 0)
            return "using " + namespaceName + ";\n" + source;
        Match last = matches[matches.Count - 1];
        return source.Insert(last.Index + last.Length, "\nusing " + namespaceName + ";");
    }

    private static bool HasUsing(string source, string namespaceName)
    {
        return Regex.IsMatch(source, @"^using\s+" + Regex.Escape(namespaceName) + @"\s*;", RegexOptions.Multiline);
    }

    private static string DetectNewLine(string source)
    {
        return source.Contains("\r\n") ? "\r\n" : "\n";
    }

    private static string DetectIndent(string source)
    {
        Match match = Regex.Match(source, @"^([ \t]+)\w", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : "    ";
    }

    private static string SanitizeIdentifier(string value)
    {
        string result = Regex.Replace(value, @"[^a-zA-Z0-9_]", "_");
        if (string.IsNullOrEmpty(result)) result = "UIReference";
        if (char.IsDigit(result[0])) result = "_" + result;
        return result;
    }

    private static string MakeUniqueFieldName(string name, HashSet<string> usedNames)
    {
        string result = name;
        int suffix = 1;
        while (usedNames.Contains(result))
            result = name + "_" + suffix++;
        return result;
    }

    private sealed class UIBinding
    {
        public string FieldName;
        public string NodeName;
        public Type ComponentType;
        public bool RequiresTMPro;
    }
}