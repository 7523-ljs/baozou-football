using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 替代 StandaloneInputModule，避免 "Input Button Submit is not setup" 报错
/// </summary>
public class SimpleInputModule : StandaloneInputModule
{
    public override bool ShouldActivateModule()
    {
        if (!enabled || !gameObject.activeInHierarchy)
            return false;

        for (int i = 0; i < 3; i++)
            if (Input.GetMouseButtonDown(i))
                return true;

        if (Input.anyKeyDown)
            return true;

        if (Input.touchCount > 0)
            return true;

        return false;
    }

    public override void Process()
    {
        // 只处理鼠标事件，跳过 Submit/Move 键盘导航
        ProcessMouseEvent();
    }
}
