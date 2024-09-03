using MixedReality.Toolkit;
using MixedReality.Toolkit.Subsystems;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using Handedness = UnityEngine.XR.Hands.Handedness;
using com.perceptlab.armultiplayer;
using Unity.VisualScripting;

/// <summary>
///  AvatarInfoPub: Used to publish player "avatar" information for the relevant parties to subscribe to <br/>
///  Also saves the information to the DataManager in the process. <br/>
///  Note: This can later be changed to make it so DataManager subscribes to AvatarInfoPub and saves the data from that input for better implementation <br/>
///  Used to publish player "avatar" information for the relevant parties to subscribe to
/// </summary>
public class AvatarInfoPub : MonoBehaviour
{
    [SerializeField] private int m_PlayerID = -1;
    [SerializeField] private bool m_SaveToDataManager;
    [SerializeField] private DataManager m_DataManager;
    [Header("ROOTS")]
    [SerializeField] private Transform m_HeadRoot;
    [SerializeField] private Transform m_LeftControllerRoot;
    [SerializeField] private Transform m_RightControllerRoot;
    [SerializeField] private Transform m_CameraOffset;
    /// <summary>
    /// What device is this running on could either be MetaQuest, HoloLens, Other
    /// </summary>
    [Header("ENUMS")]
    [SerializeField] private Devices m_Device;

    //Devices and Subsystems
    private InputDevice _leftControllerInput;
    private InputDevice _rightControllerInput;
    private InputDevice _centerEyeInput;
    private InputDevice _leftEyeInput;
    private InputDevice _rightEyeInput;
    //XRHandSubsystem to get hand data from OpenXR
    private XRHandSubsystem _questHandSubsystem;
    //HandsAggregatorSubsystem to get data from MRTK
    private HandsAggregatorSubsystem _holoHandSubsystem;
    private List<XRHandSubsystem> _subsystems = new();

    //Bool checks
    private bool _initLocalRoot = false;
    private bool _initControllerInput = false;
    private bool _initEyeInput = false;
    private bool _initHandSubsys = false;

    //private bool _controllerTracking = false;
    private bool _leftHandTracking = false;
    private bool _rightHandTracking = false;

    //XR
    private XRHand _leftXRHand;
    private XRHand _rightXRHand;
    private XRNode _leftHandXRNode;
    private XRNode _rightHandXRNode;
    private Handedness _leftHandedness;
    private Handedness _rightHandedness;

    private static readonly string DEFAULTSEND = "0|";

    //Enums
    public enum Devices { MetaQuest, HoloLens, Other }
    public enum Types { Head, LeftHand, RightHand, CenterEye, LeftEye, RightEye, Other }

    public event EventHandler<AvatarInfoEventArgs>
        OnPublishHeadData,
        OnPublishLeftHandControllerData,
        OnPublishRightHandControllerData,
        OnPublishLeftGazeData,
        OnPublishRightGazeData,
        OnPublishCenterGazeData = delegate {};

    // Update is called once per frame
    void Update()
    {
        UpdateCall();
    }

    //removed cuz it might be making it a bit laggy calling the same thing multiple times in one frame
    //will test and find out...same for LateUpdate()
    void FixedUpdate()
    {
        //UpdateCall();
    }

    void LateUpdate()
    {
        //UpdateCall();
    }

    private void UpdateCall()
    {
        Publish();

        LocalRootInit();
        InitControllerInput();
        InitHandSubsystem();
        InitEyeInput();
        //ControllerTracking();
        //HandTracking();
    }

    public void SetPlayerID(int playerID)
    {
        m_PlayerID = playerID;
    }

    private void LocalRootInit()
    {
        _initLocalRoot = m_HeadRoot != null && m_LeftControllerRoot != null 
            && m_RightControllerRoot != null && m_CameraOffset != null;
    }

    private void InitControllerInput()
    {
        if (_leftControllerInput == null || !_leftControllerInput.isValid)
        {
            _leftControllerInput = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _leftHandedness = Handedness.Left;
        }

        if (_rightControllerInput == null || !_rightControllerInput.isValid)
        {
            _rightControllerInput = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            _rightHandedness = Handedness.Right;
        }

        _initControllerInput = _leftControllerInput.isValid && _rightControllerInput.isValid;
    }

    private void InitEyeInput()
    {
        if(_centerEyeInput == null || !_centerEyeInput.isValid)
        {
            _centerEyeInput = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        }

        if(_leftEyeInput == null || !_leftEyeInput.isValid)
        {
            _leftEyeInput = InputDevices.GetDeviceAtXRNode(XRNode.LeftEye);
        }

        if (_rightEyeInput == null || !_rightEyeInput.isValid)
        {
            _rightEyeInput = InputDevices.GetDeviceAtXRNode(XRNode.RightEye);
        }

        _initEyeInput = _centerEyeInput.isValid && _leftEyeInput.isValid && _rightEyeInput.isValid;
    }


    private void InitHandSubsystem()
    {
        if (m_Device == Devices.MetaQuest && _questHandSubsystem != null && _questHandSubsystem.running) return;

        if (m_Device == Devices.HoloLens && _holoHandSubsystem != null && _holoHandSubsystem.running) return;

        if (m_Device == Devices.MetaQuest)
        {
            SubsystemManager.GetSubsystems(_subsystems);
            if (_subsystems.Count == 0)
            {
                _initHandSubsys = false;
                return;
            }

            foreach (XRHandSubsystem s in _subsystems)
            {
                if (s.running)
                {
                    _questHandSubsystem = s;
                    break;
                }
            }

            SubSubsystem();
            InitQuestXRHand();
            _initHandSubsys = true;
        }
        else if (m_Device == Devices.HoloLens)
        {
            _leftHandXRNode = XRNode.LeftHand;
            _rightHandXRNode = XRNode.RightHand;

            _holoHandSubsystem = XRSubsystemHelpers.GetFirstRunningSubsystem<HandsAggregatorSubsystem>();
            _initHandSubsys = _holoHandSubsystem != null && _holoHandSubsystem.running;
        }
        else
        {
            Debug.Log($"{this.gameObject.name}...InitHandSubsystem...device not funcionality supported");
            _initHandSubsys = false;
        }
    }

    private void InitQuestXRHand()
    {
        _leftXRHand = _questHandSubsystem.leftHand;
        _rightXRHand = _questHandSubsystem.rightHand;
    }

    private bool IsControllerTracking(Handedness hand)
    {
        if (!_initControllerInput)
        {
            return false;
        }

        UnityEngine.InputSystem.XR.XRController controllerDevice = null;

        switch (hand)
        {
            case Handedness.Left:
                controllerDevice = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRController>(UnityEngine.InputSystem.CommonUsages.LeftHand);
                break;
            case Handedness.Right:
                controllerDevice = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRController>(UnityEngine.InputSystem.CommonUsages.RightHand);
                break;
            default:
                Debug.Log($"{this.gameObject.name}...ControllerTracking...handedness not set");
                break;
        }

        return (controllerDevice != null);
    }

    private bool IsHandTracking(Handedness hand)
    {
        if (!_initHandSubsys)
        {
            return false;
        }

        if (m_Device == Devices.MetaQuest)
        {
            InitQuestXRHand();
            switch(hand)
            {
                case Handedness.Left:
                    return _leftXRHand.isTracked;
                case Handedness.Right:
                    return _rightXRHand.isTracked;
                default:
                    return false;
            }
        }
        else if (m_Device == Devices.HoloLens)
        {
            return (_holoHandSubsystem != null && _holoHandSubsystem.running);
        }
        else 
            return false;
    }

    private void OnHandTrackingAcquired(XRHand hand)
    {
        if (hand.handedness == _leftXRHand.handedness)
            _leftHandTracking = true;

        if (hand.handedness == _rightXRHand.handedness)
            _rightHandTracking = true;
    }

    private void OnHandTrackingLost(XRHand hand)
    {
        if (hand.handedness == _leftXRHand.handedness)
            _leftHandTracking = false;

        if (hand.handedness == _rightXRHand.handedness)
            _rightHandTracking = false;
    }

    private void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags, XRHandSubsystem.UpdateType updateType)
    {
        if (updateType == XRHandSubsystem.UpdateType.Dynamic)
            return;

        Publish();
    }

    private void Publish()
    {
        //initializing data to send
        string _dataToSend;

        if (!_initLocalRoot)
        {
            //Debug.Log($"{this.gameObject.name}...Local root not initialized....returning");
            //return;
            LocalRootInit();
            return;
        }

        //events......
        if(OnPublishHeadData != null)
        {
            _dataToSend = PublishTranformData(m_HeadRoot);
            OnPublishHeadData(this, new AvatarInfoEventArgs(_dataToSend));
        }

        if(OnPublishLeftHandControllerData != null)
        {
            _dataToSend = PublishHandControllerData(_leftXRHand, _leftHandXRNode, _leftControllerInput, m_LeftControllerRoot, _leftHandedness);
            OnPublishLeftHandControllerData(this, new AvatarInfoEventArgs(_dataToSend));
        }

        if (OnPublishRightHandControllerData != null)
        {
            _dataToSend = PublishHandControllerData(_rightXRHand, _rightHandXRNode, _rightControllerInput, m_RightControllerRoot, _rightHandedness);
            OnPublishRightHandControllerData(this, new AvatarInfoEventArgs(_dataToSend));
        }

        if(OnPublishCenterGazeData != null) 
        {
            _dataToSend = PublishEyeData(XRNode.CenterEye);
            OnPublishCenterGazeData(this, new AvatarInfoEventArgs(_dataToSend));
        }

        if(OnPublishLeftGazeData != null)
        {
            _dataToSend = PublishEyeData(XRNode.LeftEye);
            OnPublishLeftGazeData(this, new AvatarInfoEventArgs(_dataToSend));
        }

        if(OnPublishRightGazeData != null)
        {
            _dataToSend = PublishEyeData(XRNode.RightEye);
            OnPublishRightGazeData(this, new AvatarInfoEventArgs(_dataToSend));
        }
    }

    private string PublishTranformData(Transform transform)
    {
        string send = $"{1}|{transform.position.x}|{transform.position.y}|{transform.position.z}|" +
            $"{transform.eulerAngles.x}|{transform.eulerAngles.y}|{transform.eulerAngles.z}|";

        if (m_SaveToDataManager)
        {
            m_DataManager.UpdatePlayerFile(m_PlayerID, transform);
        }
            
        return send;
    }

    private string PublishHandControllerData(XRHand xrHand, XRNode xrNode, InputDevice controllerInput, Transform controllerRoot, Handedness handedness)
    {
        string send = DEFAULTSEND;
        if (IsHandTracking(handedness))
        {
            if (!_initHandSubsys)
            {
                InitHandSubsystem();
                return send;
            }
            send = $"{2}|";
            var cameraOffsetPose = new Pose(m_CameraOffset.position, m_CameraOffset.rotation);
            if (m_Device == Devices.MetaQuest)
            {
                //loop through and concatenate pose data for the 26 joints
                for (int j = 0; j < 26; j++)
                {
                    if (!xrHand.GetJoint((XRHandJointID)(j + 1)).TryGetPose(out Pose jp))
                        continue;

                    Pose jointPose = jp.GetTransformedBy(cameraOffsetPose);

                    send += $"{jointPose.position.x}|{jointPose.position.y}|{jointPose.position.z}|" +
                        $"{jointPose.rotation.eulerAngles.x}|{jointPose.rotation.eulerAngles.y}|{jointPose.rotation.eulerAngles.z}|";

                    //Save through DataManager if it exists in the scene
                    if(m_DataManager)
                        m_DataManager.UpdatePlayerFile(m_PlayerID, jointPose, string.Concat((XRHandJointID)(j + 1)));
                }
            }
            else if (m_Device == Devices.HoloLens)
            {
                //loop through and concatenate data for the 26 joints
                for (int j = 0; j < 26; j++)
                {
                    if (!_holoHandSubsystem.TryGetJoint((TrackedHandJoint)j, xrNode, out HandJointPose hjp))
                        continue;

                    Pose jointPose = hjp.Pose;

                    send += $"{jointPose.position.x}|{jointPose.position.y}|{jointPose.position.z}|" +
                        $"{jointPose.rotation.eulerAngles.x}|{jointPose.rotation.eulerAngles.y}|{jointPose.rotation.eulerAngles.z}|";

                    //Save through DataManager if it exists in the scene
                    if (m_DataManager)
                        m_DataManager.UpdatePlayerFile(m_PlayerID, jointPose, string.Concat((TrackedHandJoint)j));
                }
            }
            else
            {
                send = DEFAULTSEND;
            }
        }
        else if (IsControllerTracking(handedness))
        {
            if (!_initControllerInput)
            {
                InitControllerInput();
                Debug.Log($"Controller Init Error");
                return send;
            }

            //Concatenate controller data
            send = $"{3}|{controllerRoot.position.x}|{controllerRoot.position.y}|{controllerRoot.position.z}|" +
                $"{controllerRoot.eulerAngles.x}|{controllerRoot.eulerAngles.y}|{controllerRoot.eulerAngles.z}|";

            if (controllerInput.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float triggerValue))
                send += $"{triggerValue}|";
            else
                send += $"{0}|";

            if (controllerInput.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float gripValue))
                send += $"{gripValue}|";
            else
                send += $"{0}|";

            if (m_SaveToDataManager)
                m_DataManager.UpdatePlayerFile(m_PlayerID, controllerRoot);
        }

        return send;
    }

    private string PublishEyeData(XRNode xrEyeNode)
    {
        var inputDeviceList = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, inputDeviceList);

        string send = DEFAULTSEND;

        if (!_initEyeInput)
        {
            InitEyeInput();
            return send;
        }

        switch (xrEyeNode)
        {
            case XRNode.CenterEye:
                if (_centerEyeInput.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyePosition, out Vector3 cPos)
                    && _centerEyeInput.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyeRotation, out Quaternion cRot))
                {
                    send += $"{4}|{cPos.x}|{cPos.y}|{cPos.z}|{cRot.eulerAngles.x}|{cRot.eulerAngles.y}|{cRot.eulerAngles.z}|";
                    if (m_SaveToDataManager)
                        m_DataManager.UpdatePlayerFile(m_PlayerID, "Center Eye", cPos, cRot.eulerAngles);
                }
                else send = DEFAULTSEND;
                break;
            case XRNode.LeftEye:
                if (_leftEyeInput.TryGetFeatureValue(UnityEngine.XR.CommonUsages.leftEyePosition, out Vector3 lPos)
                    && _leftEyeInput.TryGetFeatureValue(UnityEngine.XR.CommonUsages.leftEyeRotation, out Quaternion lRot))
                {
                    send += $"{4}|{lPos.x}|{lPos.y}|{lPos.z}|{lRot.eulerAngles.x}|{lRot.eulerAngles.y}|{lRot.eulerAngles.z}|";
                    if (m_SaveToDataManager)
                        m_DataManager.UpdatePlayerFile(m_PlayerID, "Left Eye", lPos, lRot.eulerAngles);
                }
                else send = DEFAULTSEND;
                break;
            case XRNode.RightEye:
                if (_rightEyeInput.TryGetFeatureValue(UnityEngine.XR.CommonUsages.rightEyePosition, out Vector3 rPos)
                    && _rightEyeInput.TryGetFeatureValue(UnityEngine.XR.CommonUsages.rightEyeRotation, out Quaternion rRot))
                {
                    send += $"{4}|{rPos.x}|{rPos.y}|{rPos.z}|{rRot.eulerAngles.x}|{rRot.eulerAngles.y}|{rRot.eulerAngles.z}|";
                    if (m_SaveToDataManager)
                        m_DataManager.UpdatePlayerFile(m_PlayerID, "Right Eye", rPos, rRot.eulerAngles);
                }
                else send = DEFAULTSEND;
                break;
            default:
                send = DEFAULTSEND;
                break;
        }

        return send;
    }

    private void OnDisable()
    {
        if (m_Device == Devices.MetaQuest)
        {
            UnSubSubsystem();
        }
    }

    private void SubSubsystem()
    {
        if (_questHandSubsystem == null) return;

        _questHandSubsystem.trackingAcquired += OnHandTrackingAcquired;
        _questHandSubsystem.trackingLost += OnHandTrackingLost;
        _questHandSubsystem.updatedHands += OnUpdatedHands;
    }

    private void UnSubSubsystem()
    {
        if (_questHandSubsystem == null) return;

        _questHandSubsystem.trackingAcquired -= OnHandTrackingAcquired;
        _questHandSubsystem.trackingLost -= OnHandTrackingLost;
        _questHandSubsystem.updatedHands -= OnUpdatedHands;
    }
    
}

public class AvatarInfoEventArgs: EventArgs
{
    public string Value { get;}

    public AvatarInfoEventArgs(string value)
    {
        Value = value;
    }
}