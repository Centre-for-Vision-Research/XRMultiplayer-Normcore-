/************************************************************************************
Copyright : Copyright 2019 (c) Speak Geek (PTY), LTD and its affiliates. All rights reserved.

Developer : Dylan Holshausen

Script Description : Helper for Normcore Realtime Component

************************************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using Types = AvatarInfoPub.Types;

public class SceneHelper : MonoBehaviour
{
    [SerializeField]
    protected GameObject m_LocalPlayer;
    [SerializeField]
    protected GameObject m_Room; //Percept lab in the second scene
    [SerializeField]
    protected DataManager m_DataManager;
    [Header("OPTIONAL")]
    [Header("AVATAR PUBLISH INFO")]
    /// <summary>
    /// Check this if you want to automatically to AvatarInfoPub present in the scene
    /// This is best if you don't have any implementation already subscribed to AvatarInfoPub or if you want to add more
    /// </summary>
    [SerializeField]
    private bool m_AutoSubToAvatarInfoPub;
    [SerializeField] AvatarInfoPub m_AvatarInfo;
    /// <summary>
    /// What we want to subscribe to, if we checked the box above
    /// </summary>
    [SerializeField]
    private Types[] m_AvatarSubTo;

    protected Transform _spawnTransform;

    public virtual void Start()
    {
        SetUp();
    }

    public virtual void SetUp()
    {
        if (m_AutoSubToAvatarInfoPub && m_AvatarInfo != null)
        {
            //ensure no duplicates
            HashSet<Types> types = new HashSet<Types>(m_AvatarSubTo);
            foreach (var type in types)
            {
                switch (type)
                {
                    case Types.Head:
                        m_AvatarInfo.OnPublishHeadData += AutoSub;
                        break;
                    case Types.LeftHand:
                        m_AvatarInfo.OnPublishLeftHandControllerData += AutoSub;
                        break;
                    case Types.RightHand:
                        m_AvatarInfo.OnPublishRightHandControllerData += AutoSub;
                        break;
                    case Types.CenterEye:
                        m_AvatarInfo.OnPublishCenterGazeData += AutoSub;
                        break;
                    case Types.RightEye:
                        m_AvatarInfo.OnPublishRightGazeData += AutoSub;
                        break;
                    case Types.LeftEye:
                        m_AvatarInfo.OnPublishLeftGazeData += AutoSub;
                        break;
                    default: break;
                }
            }
            m_AvatarInfo.SetPlayerID(0);
            if (!m_DataManager) m_DataManager = FindAnyObjectByType<DataManager>();
            m_DataManager.CreatePlayerFile(0);
        }
    }

    private void AutoSub(object sender, EventArgs args)
    {
        //currently being used to automatically subscribe to avatar info publisher
        //to ensure data is saved to the data manager in non realtime/multiplayer
    }

    //Realtime Event when Connecting to a Room
    public virtual void InitializePlayer()
    {
        m_LocalPlayer.transform.SetPositionAndRotation(_spawnTransform.position, _spawnTransform.rotation);
    }

    public void SetPlayerOffset(MyTransform offset)
    {
        _spawnTransform = new GameObject().transform;
        _spawnTransform.position = m_Room.transform.position + offset.position;//player related to the room's position
        _spawnTransform.eulerAngles = offset.eulerAngles; //tansform is the player's transform exactly where it was in the previous scene
        _spawnTransform.RotateAround(m_Room.transform.position, Vector3.up, -offset.originRot.y);
        Debug.Log($"PlayerCenterReference {_spawnTransform.transform.position.ToString()} Room {m_Room.transform.position}");
    }

    public void SetPlayerOffset(Transform offset)
    {
        SetPlayerOffset(new MyTransform(offset.position, offset.eulerAngles));
    }

    public void SetPlayerOffset(Vector3 pos, Quaternion rot)
    {
        SetPlayerOffset(new MyTransform(pos, rot.eulerAngles));
    }

    public virtual void JoinRoom(Transform transform)
    {
        SetPlayerOffset(transform);
        InitializePlayer();
    }

    public virtual void JoinRoom(MyTransform transform)
    {
        SetPlayerOffset(transform);
        InitializePlayer();
    }

    public virtual void JoinRoom(Vector3 pos, Quaternion rot)
    {
        SetPlayerOffset(pos, rot);
        InitializePlayer();
    }
}