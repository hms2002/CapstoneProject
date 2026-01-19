using UnityEngine;
using Ink.Runtime;
using System.Collections.Generic;

public class DialogueVariables
{
    public Dictionary<string, Ink.Runtime.Object> variables { get; private set; }
    private Story globalVariablesStory;

    public DialogueVariables(TextAsset loadGlobalsJSON)
    {
        variables = new Dictionary<string, Ink.Runtime.Object>();

        // 파일이 할당되지 않았으면 로드를 건너뜁니다.
        if (loadGlobalsJSON == null)
        {
            Debug.Log("전역 변수 JSON이 할당되지 않았습니다. (데이터 동기화 생략)");
            return;
        }

        try
        {
            globalVariablesStory = new Story(loadGlobalsJSON.text);
            foreach (string name in globalVariablesStory.variablesState)
            {
                Ink.Runtime.Object value = globalVariablesStory.variablesState.GetVariableWithName(name);
                variables.Add(name, value);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ink 전역 변수 로드 중 오류 발생: {e.Message}");
        }
    }

    public void StartListening(Story story)
    {
        if (story == null) return;
        VariablesToStory(story);
        story.variablesState.variableChangedEvent += VariableChanged;
    }

    public void StopListening(Story story)
    {
        if (story == null) return;
        story.variablesState.variableChangedEvent -= VariableChanged;
    }

    private void VariableChanged(string name, Ink.Runtime.Object value)
    {
        if (variables.ContainsKey(name)) variables[name] = value;
    }

    private void VariablesToStory(Story story)
    {
        foreach (KeyValuePair<string, Ink.Runtime.Object> variable in variables)
        {
            story.variablesState.SetGlobal(variable.Key, variable.Value);
        }
    }

    public void SaveVariables()
    {
        // 추후 세이브 시스템 구현 시 사용
    }
}