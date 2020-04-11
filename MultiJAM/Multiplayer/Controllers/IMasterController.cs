﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public abstract class IMasterController<PSM,IM> : MonoBehaviour
{
    public IPlayerController<PSM, IM> liveController;
    public IPlayerController<PSM, IM> projectionController;
    public IInputController<IM> inputController;
    public InputReceiverController<PSM,IM> inputReceiverController;
    public InputSenderController<PSM,IM> inputSenderController;
    public IPlayerView<PSM> ghostPlayer;
    public IPlayerView<PSM> mirrorPlayer;
    public IPlayerView<PSM> projectedPlayer;
    public IPlayerView<PSM> pastPlayer;
    public TransportController signalRController;

    public GameObject liveControllerObj;
    public GameObject projectionControllerObj;
    public GameObject inputControllerObj;
    public GameObject ghostPlayerObj;
    public GameObject mirrorPlayerObj;
    public GameObject projectedPlayerObj;
    public GameObject pastPlayerObj;

    public bool isOwner;
    public string connectionId;

    public abstract void CorrectPlayerState(PSM serverState);
    public abstract bool CheckForCorrection(PSM serverState, PSM localState);

    public void Initialize(TransportController _signalRController,PlayerStatePack<PSM> psp, bool _isOwner)
    {
        signalRController = _signalRController;
        liveController = liveControllerObj.GetComponent<IPlayerController<PSM, IM>>();
        projectionController = projectionControllerObj.GetComponent<IPlayerController<PSM,IM>> ();
        inputController = inputControllerObj.GetComponent<IInputController<IM>>();
        ghostPlayer = ghostPlayerObj.GetComponent<IPlayerView<PSM>>();
        mirrorPlayer = mirrorPlayerObj.GetComponent<IPlayerView<PSM>>();
        projectedPlayer = projectedPlayerObj.GetComponent<IPlayerView<PSM>>();
        pastPlayer = pastPlayerObj.GetComponent<IPlayerView<PSM>>();

        isOwner = _isOwner;
        connectionId = psp.conId;
        liveController.Initialize(psp.playerState, _isOwner, connectionId, this);

        if (isOwner)
        {
            inputSenderController = new InputSenderController<PSM, IM>(this);
            inputSenderController.StartStream(connectionId);
            inputController.enabled = true;
        }
        else
        {
            inputReceiverController = new InputReceiverController<PSM, IM>(this);
            inputReceiverController.StartReception(connectionId);
            inputController.enabled = false;
            projectionController.gameObject.SetActive(false);
            pastPlayer.gameObject.SetActive(false);
            ghostPlayer.gameObject.SetActive(false);
        }
    }

    public void SetMirrorState(PSM psp)
    {
        mirrorPlayer.SetFromModel(psp);
        OnMirrorStateSet(psp);
    }

    public void SetGhostState(PlayerStatePack<PSM> psp)
    {
        ghostPlayer.SetFromModel(psp.playerState);
        OnGhostStateSet(psp.playerState);
        TickModel<PSM, IM> pastTick = new TickModel<PSM, IM>();

        if (inputSenderController.tickHistory.TryGetValue(psp.tick, out pastTick))
        {
            pastPlayer.SetFromModel(pastTick.state);

            if (CheckForCorrection(psp.playerState, pastTick.state))
            {
                PSM projectedState = ProjectState(psp);
                projectedPlayer.SetFromModel(projectedState);
                CorrectPlayerState(projectedState);
            }
        }
    }

    public abstract void OnMirrorStateSet(PSM playerState);
    public abstract void OnGhostStateSet(PSM playerState);

    public PSM ProjectState(PlayerStatePack<PSM> psp)
    {
        TickModel<PSM, IM> pastTick = new TickModel<PSM, IM>();
        if (inputSenderController.tickHistory.TryGetValue(psp.tick, out pastTick))
        {
            projectionController.SetState(psp.playerState);
            pastPlayer.SetFromModel(psp.playerState);
            for (int i = psp.tick+1; i < inputSenderController.tickTrack; i++)
            {
                projectionController.ProcessInput(inputSenderController.tickHistory[i].input);
            }
        }
        return projectionController.currentPlayerState;
    }

    public void ProcessServerRequests(Dictionary<int, ServerEventRequest> requests)
    {
        foreach (KeyValuePair<int, ServerEventRequest> kvp in requests)
        {
            for (int i = 0; i < kvp.Value.requestInstances.Count; i++)
            {
                liveController.ProcessServerEvents(kvp.Key, kvp.Value.requestInstances[i]);
            }
        }
    }
}
