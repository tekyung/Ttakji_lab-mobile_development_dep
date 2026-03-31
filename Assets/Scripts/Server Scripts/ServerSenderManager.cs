using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ServerScripts.EventScripts; // EventDTO.cs 를 사용하기 위해 필요
using TCG_Project.Scripts.Core;   // GameData 를 사용하기 위해 필요

public class ServerSenderManager : MonoBehaviour
{
    [Header("Dependencies")]
    public firebase_network networkService;
    public session_ui uiManager;

    // =======================================================
    // 1. 자원 페이즈 (Resource Phase)
    // =======================================================
    public async void OnClick_GainResource()
    {
        var req = new ResourceActionRequest
        {
            MatchId = GameData.SessionCode,
            PlayerName = GameData.MyRole
        };

        string json = JsonUtility.ToJson(req);
        await networkService.SendAction(GameData.SessionCode, "ResourceActionRequest", json);

        // 중복 클릭 방지
        uiManager.SetActionButtonsState(false);
    } 

    // =======================================================
    // 2. 드로우 페이즈 (Draw Phase)
    // =======================================================
    public async void OnClick_DrawCard()
    {
        var req = new CardDrawRequest
        {
            MatchId = GameData.SessionCode,
            PlayerName = GameData.MyRole,
            DrawCount = 1
        };

        string json = JsonUtility.ToJson(req);
        await networkService.SendAction(GameData.SessionCode, "CardDrawRequest", json);
    }

    // =======================================================
    // 3. 세트 페이즈 (Set Phase)
    // =======================================================
    // (기존 session_game_manage에 있던 함수 이동)
    public async void SendSetPhaseChoice(string selectedCardId, bool isConfirmed)
    {
        var req = new SetPhaseActionRequest
        {
            MatchId = GameData.SessionCode,
            PlayerName = GameData.MyRole,
            PickedCardInstanceId = selectedCardId,
            IsConfirmed = isConfirmed
        };

        await networkService.SendRequestDTO(GameData.SessionCode, "SetPhaseActionRequest", req);
        uiManager.SetActionButtonsState(false);
    }

    // =======================================================
    // 4. 오픈 페이즈 (Open Phase)
    // =======================================================
    // (기존 session_game_manage에 있던 함수 이동)
    public async void SendOpenPhaseChoice(string setCardId, string choiceString)
    {
        var req = new OpenPhaseActionRequest
        {
            MatchId = GameData.SessionCode,
            PlayerName = GameData.MyRole,
            SetCardInstanceId = setCardId,
            Choice = choiceString // "Open" 또는 "Abandon"
        };

        await networkService.SendRequestDTO(GameData.SessionCode, "OpenPhaseActionRequest", req);
    }

    // =======================================================
    // 5. 메인 페이즈 (Main Phase)
    // =======================================================
    public async void OnClick_StartMainBattle()
    {
        var req = new PhaseChangeRequest
        {
            MatchId = GameData.SessionCode,
            PlayerName = GameData.MyRole,
            TargetPhase = "MainPhase"
        };

        string json = JsonUtility.ToJson(req);
        await networkService.SendAction(GameData.SessionCode, "PhaseChangeRequest", json);
    }

    // =======================================================
    // 6. 엔드 페이즈 (End Phase - 턴 종료)
    // =======================================================
    public async void OnClick_TurnEnd()
    {
        var req = new TurnEndRequest
        {
            MatchId = GameData.SessionCode,
            PlayerName = GameData.MyRole
        };

        string json = JsonUtility.ToJson(req);
        await networkService.SendAction(GameData.SessionCode, "TurnEndRequest", json);
    }

    // =======================================================
    // 7. 스택 응답 (상대 공격 시 방어)
    // =======================================================
    public async void SendStackResponse(string myStackCardId, string opponentCardId, bool isUsing)
    {
        var req = new StackResponseRequest
        {
            MatchId = GameData.SessionCode,
            PlayerName = GameData.MyRole,
            StackCardInstanceId = myStackCardId,
            OpponentCardInstanceId = opponentCardId,
            IsUsingStack = isUsing
        };

        await networkService.SendRequestDTO(GameData.SessionCode, "StackResponseRequest", req);
    }
}