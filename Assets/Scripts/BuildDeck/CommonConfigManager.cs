using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class ConfigItem
{
    public string Name;
    public string Value;
}

[Serializable]
public class RootConfigData
{
    public List<ConfigItem> CommonConfig;
}

public class CommonConfigManager : MonoBehaviour
{
    public static float card_zoom_hold_duration_time = 1.5f;

    // 파일을 읽어오는 함수
    public static void LoadConfig()
    {
        TextAsset jsonText = Resources.Load<TextAsset>("GameData/CommonConfig");

        if (jsonText != null)
        {
            RootConfigData rootData = JsonUtility.FromJson<RootConfigData>(jsonText.text);

            if (rootData != null && rootData.CommonConfig != null)
            {
                // 2. 파싱해온 "5000" 이라는 글자를 숫자로 바꿉니다.
                // (만약 5000이 5초(밀리초)를 의미한다면 1000으로 나눠줍니다!)
                ConfigItem targetItem = rootData.CommonConfig.Find(item => item.Name == "card_zoom_hold_duration_time");
                if (targetItem != null)
                {
                    // 3. 찾은 녀석의 Value(예: "5000")를 숫자로 변환합니다.
                    if (float.TryParse(targetItem.Value, out float parsedValue))
                    {
                        card_zoom_hold_duration_time = parsedValue / 1000f; // 5000 -> 5.0초
                    }
                    else
                    {
                        Debug.LogError($"🚨 Value 값이 숫자가 아닙니다: {targetItem.Value}");
                    }
                }
                else
                {
                    Debug.LogWarning("🚨 JSON 안에 'card_zoom_hold_duration_time' 이라는 Name이 없습니다!");
                }
            }
        }
        else
        {
            Debug.LogError("🚨 파일을 못 찾았습니다! 기본값 1.5초를 사용합니다.");
            card_zoom_hold_duration_time = 1.5f;
        }
    }
}