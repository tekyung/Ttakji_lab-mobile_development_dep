using System.Collections.Generic;
using System.IO; // 파일 읽기용
using TMPro; // 드롭다운용
using UnityEngine;

public class DeckSelector : MonoBehaviour
{
    public TMP_Dropdown mainDeckDropdown; // 메인 화면에 있는 그 드롭다운 연결

    // ★ 키는 PlayerStorage가 정한다 — 한 PC에서 두 클라이언트를 띄울 때 칸이 갈려야 한다.

    void Start()
    {
        RefreshDropdown();

        if (mainDeckDropdown != null)
        {
            mainDeckDropdown.onValueChanged.RemoveAllListeners(); // 꼬임 방지용 초기화
            mainDeckDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }

    }

    public void RefreshDropdown()
    {
        if (mainDeckDropdown == null) return;

        // 1. 초기화
        mainDeckDropdown.ClearOptions();
        List<string> options = new List<string>();

        // 2. 덱 폴더 경로 (DeckManager랑 똑같은 경로!)
        string folderPath = DeckStorage.EnsureFolder();

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // 3. 파일 목록 가져오기
        string[] filePaths = Directory.GetFiles(folderPath, "*.json");

        foreach (string path in filePaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            options.Add(fileName);
        }

        // 덱이 하나도 없으면?
        if (options.Count == 0)
        {
            options.Add("덱 없음");
            mainDeckDropdown.AddOptions(options);
            mainDeckDropdown.interactable = false;
            return;
        }

        // 4. 드롭다운 채우기
        mainDeckDropdown.interactable = true;
        mainDeckDropdown.AddOptions(options);

        // 5. [중요] 지난번에 골랐던 덱 자동 선택해주기
        string lastSelectedDeck = PlayerStorage.GetSelectedDeck(); // 저장된 거 있니?

        int targetIndex = 0;
        if (!string.IsNullOrEmpty(lastSelectedDeck))
        {
            int findIndex = options.IndexOf(lastSelectedDeck);
            if (findIndex >= 0) targetIndex = findIndex;
        }

        mainDeckDropdown.value = targetIndex;

        // 시작하자마자 현재 선택된 덱 저장해두기 (안전빵)
        SaveSelectedDeck(targetIndex);
    }

    // 드롭다운 값이 바뀔 때 실행할 함수 (인스펙터에서 연결하거나, 아래처럼 코드로 연결)
    public void OnDropdownValueChanged(int index)
    {
        SaveSelectedDeck(index);
    }

    void SaveSelectedDeck(int index)
    {
        string selectedName = mainDeckDropdown.options[index].text;

        if (selectedName == "덱 없음") return;

        // "SelectedDeckName"이라는 이름으로 컴퓨터에 기억시킴!
        PlayerStorage.SetSelectedDeck(selectedName);
    }
}
