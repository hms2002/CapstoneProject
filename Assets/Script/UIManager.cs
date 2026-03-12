using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private static UIManager instance;
    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<UIManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("UIManager");
                    instance = go.AddComponent<UIManager>();
                }
            }
            return instance;
        }
    }

    // 현재 열려 있는 UI들의 이름을 저장하는 리스트
    private HashSet<string> openUIs = new HashSet<string>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// UI가 열릴 때 호출하여 등록합니다.
    /// </summary>
    public void RegisterUI(string uiName)
    {
        if (!openUIs.Contains(uiName))
        {
            openUIs.Add(uiName);
            // 필요 시 여기서 Player 입력을 끄는 등의 처리를 할 수 있습니다.
            // Debug.Log($"[UIManager] UI Registered: {uiName}");
        }
    }

    /// <summary>
    /// UI가 닫힐 때 호출하여 해제합니다.
    /// </summary>
    public void UnregisterUI(string uiName)
    {
        if (openUIs.Contains(uiName))
        {
            openUIs.Remove(uiName);
            // Debug.Log($"[UIManager] UI Unregistered: {uiName}");
        }
    }

    /// <summary>
    /// 현재 화면을 가로막는 UI가 하나라도 열려 있는지 확인합니다.
    /// DialogueManager에서 스킵 방지용으로 참조합니다.
    /// </summary>
    public bool IsAnyBlockingUIOpen => openUIs.Count > 0;

    /// <summary>
    /// 특정 UI가 열려 있는지 확인합니다.
    /// </summary>
    public bool IsUIOpen(string uiName) => openUIs.Contains(uiName);

    /// <summary>
    /// 모든 UI 등록을 강제로 초기화합니다. (씬 전환 시 등)
    /// </summary>
    public void ClearAllRegisteredUIs()
    {
        openUIs.Clear();
    }
}