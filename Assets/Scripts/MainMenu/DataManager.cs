using System.Collections.Generic;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance;

    // 메인 화면에서 선택한 덱의 '카드 번호(ID)'들을 담아둘 리스트입니다.
    public List<int> selectedDeckList = new List<int>();

    private void Awake()
    {
        // 싱글톤 & 씬이 넘어가도 파괴되지 않도록 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 이 스크립트가 붙은 오브젝트는 절대 파괴되지 않음!
        }
        else
        {
            Destroy(gameObject);
        }
    }
}