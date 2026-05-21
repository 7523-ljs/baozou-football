using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// 主菜单UI一键修复工具
/// 在 Unity 顶部菜单栏点击 "Tools/Fix MainMenu UI" 即可运行
/// </summary>
public class MainMenuUIFixer : EditorWindow
{
    [MenuItem("Tools/Fix MainMenu UI")]
    static void FixMainMenuUI()
    {
        // 1. 验证场景对象
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("❌ 未找到 Canvas 对象，请确保 MainMenu 场景已打开");
            return;
        }

        MainMenuController controller = canvas.GetComponent<MainMenuController>();
        if (controller == null)
        {
            Debug.LogError("❌ 未找到 MainMenuController 脚本，请确保它已挂载到 Canvas 上");
            return;
        }

        int fixedCount = 0;

        // 2. 修复 QuitBtn 的文字偏移
        if (controller.quitButton != null)
        {
            TextMeshProUGUI quitText = controller.quitButton.GetComponentInChildren<TextMeshProUGUI>();
            if (quitText != null)
            {
                RectTransform rt = quitText.GetComponent<RectTransform>();
                if (rt != null && rt.anchoredPosition.y != 0)
                {
                    Undo.RecordObject(rt, "Fix QuitBtn text position");
                    rt.anchoredPosition = Vector2.zero;
                    fixedCount++;
                    Debug.Log("✅ 修复 QuitBtn 文字偏移：anchoredPosition 已重置为 (0,0)");
                }

                if (quitText.text == "Quit")
                {
                    Undo.RecordObject(quitText, "Change Quit text to Exit Game");
                    quitText.text = "Exit Game";
                    fixedCount++;
                    Debug.Log("✅ 更新 QuitBtn 文字为 'Exit Game'");
                }

                // 增大字号
                if (quitText.fontSize < 30)
                {
                    Undo.RecordObject(quitText, "Increase QuitBtn font size");
                    quitText.fontSize = 32;
                    fixedCount++;
                }
            }
        }

        // 3. 修复 StartBtn 文字
        if (controller.startButton != null)
        {
            TextMeshProUGUI startText = controller.startButton.GetComponentInChildren<TextMeshProUGUI>();
            if (startText != null)
            {
                if (startText.fontSize < 30)
                {
                    Undo.RecordObject(startText, "Increase StartBtn font size");
                    startText.fontSize = 32;
                    fixedCount++;
                }
            }
        }

        // 4. 检查所有 TMP 文字的重叠问题
        // 调整 MainMenuPanel 内各元素的间距
        if (controller.mainMenuPanel != null)
        {
            AdjustTextSpacing(controller.mainMenuPanel.transform);
        }

        // 5. 增加 Controls 文字的对比度
        TextMeshProUGUI controlsText = null;
        if (controller.mainMenuPanel != null)
        {
            Transform controls = controller.mainMenuPanel.transform.Find("Controls");
            if (controls != null)
                controlsText = controls.GetComponent<TextMeshProUGUI>();
        }
        if (controlsText != null)
        {
            Undo.RecordObject(controlsText, "Fix Controls text contrast");
            controlsText.fontSize = 22;
            controlsText.color = new Color(1, 1, 1, 0.9f);
            fixedCount++;
            Debug.Log("✅ 修复 Controls 文字对比度：字号 22，透明度 0.9");
        }

        Debug.Log($"\n🎯 修复完成！共修复 {fixedCount} 个问题。\n建议：如果仍有文字乱码，请运行 Tools/Regenerate TMP Font Atlas");
    }

    static void AdjustTextSpacing(Transform panel)
    {
        // 调整 MainMenuPanel 下的子对象位置
        foreach (Transform child in panel)
        {
            RectTransform rt = child.GetComponent<RectTransform>();
            if (rt == null) continue;

            switch (child.name)
            {
                case "Title":
                    rt.anchoredPosition = new Vector2(0, 240);
                    rt.sizeDelta = new Vector2(600, 80);
                    break;
                case "Subtitle":
                    rt.anchoredPosition = new Vector2(0, 150);
                    rt.sizeDelta = new Vector2(500, 60);
                    break;
                case "Rules":
                    rt.anchoredPosition = new Vector2(0, 95);
                    rt.sizeDelta = new Vector2(500, 50);
                    break;
                case "StartBtn":
                    rt.anchoredPosition = new Vector2(0, -15);
                    rt.sizeDelta = new Vector2(260, 60);
                    break;
                case "QuitBtn":
                    rt.anchoredPosition = new Vector2(0, -95);
                    rt.sizeDelta = new Vector2(260, 60);
                    break;
                case "Controls":
                    rt.anchoredPosition = new Vector2(0, -270);
                    rt.sizeDelta = new Vector2(500, 120);
                    break;
            }
        }
    }

    [MenuItem("Tools/Regenerate TMP Font Atlas")]
    static void RegenerateTMPFont()
    {
        // 查找场景中所有 TMP 字体
        TMP_FontAsset[] fonts = FindObjectsOfType<TMP_FontAsset>();
        if (fonts.Length == 0)
        {
            // 尝试从资源中查找
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                {
                    Debug.Log($"找到字体资源: {path}");
                    Debug.Log("请手动在 Inspector 中点击 'Update Atlas Texture' 按钮重新生成图集");
                }
            }
            else
            {
                Debug.LogWarning("未找到 TMP_FontAsset，请导入 TextMeshPro 并创建字体资源");
            }
        }
        else
        {
            Debug.Log($"找到 {fonts.Length} 个 TMP_FontAsset");
            foreach (var f in fonts)
            {
                string path = AssetDatabase.GetAssetPath(f);
                Debug.Log($"字体: {f.name} 路径: {path}");
                Debug.Log("请手动选中该字体，在 Inspector 中点击 'Update Atlas Texture' 按钮");
            }
        }

        Debug.Log("\n💡 提示：如果找不到字体设置面板的 Update Atlas Texture 按钮，");
        Debug.Log("可以尝试：Window > TextMeshPro > Font Asset Creator，选择字体后重新生成。");
        Debug.Log("或者直接删除 Library 文件夹后重新打开 Unity 以强制重建资源缓存。");
    }
}
