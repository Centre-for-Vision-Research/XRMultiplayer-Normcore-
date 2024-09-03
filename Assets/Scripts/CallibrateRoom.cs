using System;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;

/// <summary>
///  CalibrateRoom: Contains functionality to calibrate the virtual room to match the real room <br/>
///  Allows to for vertical move, horizontal move and y-axis rotation of the room
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CallibrateRoom : MonoBehaviour
{
    /// <summary>
    /// Room we're calibrating
    /// </summary>
    [SerializeField]
    private GameObject _room;
    [SerializeField]
    private XROrigin _player;
    [SerializeField]
    private Transform _playerSpawn;
    /// <summary>
    /// What the room shouuld rotate about
    /// </summary>
    [SerializeField]
    private GameObject _rotationReference;
    /// <summary>
    /// Reference to the center of the player in the room
    /// </summary>
    [SerializeField]
    private GameObject _playerCenterReference;
    /// <summary>
    /// ARSession to manage and toggle ar in the scene
    /// </summary>
    [SerializeField]
    private ARSession _ARSession;
    /// <summary>
    /// Event to be triggered after we're done calibrating the room
    /// </summary>
    [SerializeField]
    private MyLoadSceneEvent _doneEvent;
    [SerializeField]
    private bool _doneDebug;
    /// <summary>
    /// We turn this off when we're done calibrating
    /// </summary>
    [SerializeField]
    private bool _canCalibrate = true;//we turn this off after we're done calibrating
    /// <summary>
    /// Texts to provide ui feedback to the user
    /// 0 - Standby, 1 - Position, 2 - Rotation
    /// </summary>
    [Header("MODES")]
    [SerializeField]
    private TMP_Text[] _modes = new TMP_Text[3];
    /// <summary>
    /// Passthrough text to provide visual feedback
    /// </summary>
    [SerializeField]
    private TMP_Text passThrough;
    [SerializeField]
    private Material[] _seeThroughInPassThroughMaterials = new Material[0];
    [SerializeField]
    private Material[] _partialSeeThroughInPassThroughMaterials = new Material[0];
    [SerializeField]
    private Color UI_NOT_SELECTED = Color.white;
    [SerializeField]
    private Color UI_SELECTED = Color.blue;

    [Header("INPUT ACTIONS")]
    [SerializeField]
    private InputAction menuButton;
    [SerializeField]
    private InputAction leftHandPB;
    [SerializeField]
    private InputAction leftHandSB;
    [SerializeField]
    private InputAction leftJS;
    [SerializeField]
    private InputAction rightHandPB;
    [SerializeField]
    private InputAction rightHandSB;
    [SerializeField]
    private InputAction rightJS;

    enum Vision
    {
        Normal,
        Passthrough
    }
    private Vision _vision;
    Vision vision
    {
        get { return _vision; }
        set
        {
            _vision = value;

            VisionChanged();
        }
    }

    public enum Mode
    {
        Standby,
        CalibratingPos,
        CalibratingRot,
        Done
    }
    private Mode _mode;
    Mode mode
    {
        get { return _mode; }
        set
        {
            _mode = value;

            ToggleModeUI();
            ModeChanged();
        }
    }

    /// <summary>
    /// What we send to the SceneLoader as we move to the next scene
    /// </summary>
    private MyTransform _send;
    private GameObject _rotRef;
    private Rigidbody _roomRB;

    private float _rotDirection = 0.0f;
    public readonly float _rotFactor = 0.05f;
    public readonly float _posFactor = 0.00625f;

    void OnEnable()
    {
        menuButton.Enable(); menuButton.performed += DoneAction;
        //leftHandPB.Enable(); leftHandPB.performed += VisionToggleAction;
        //leftHandSB.Enable(); leftHandSB.performed += LeftRotateAction;
        leftJS.Enable(); leftJS.performed += VPositioningAction;
        rightHandPB.Enable(); rightHandPB.performed += TeleportAction;
        //rightHandSB.Enable(); rightHandSB.performed += RightRotateAction;
        rightJS.Enable(); rightJS.performed += HPositioningAction;
    }

    void Start()
    {
        if (!_rotationReference)
            _rotationReference = GameObject.FindWithTag("MainCamera");

        _roomRB = _room.GetComponent<Rigidbody>();
        mode = Mode.CalibratingPos;
        vision = Vision.Passthrough;

        //saving the starting transform
        _send = new MyTransform(transform.position, transform.eulerAngles);
        _rotRef = Instantiate(new GameObject("_rotRef"), _rotationReference.transform);

        Debug.Log("Strt Done");
    }

    // Update is called once per frame
    void Update()
    {
        //only if we can can calibrate and are holding the controllers
        if (_canCalibrate)
        {
            //if  "done" is toggled in inspector
            if (_doneDebug)
            {
                mode = Mode.Done;
            }

            if (mode == Mode.CalibratingRot)
                _roomRB.transform.RotateAround(_rotationReference.transform.position, Vector3.up, _rotFactor * _rotDirection);
        }
    }

    public void SetMode(Mode m)
    {
        mode = m;
    }

    public void SetMode(int m)
    {
        mode = (Mode)m;
    }

    private void TeleportAction(InputAction.CallbackContext obj)
    {
        float yPos = _player.transform.position.y + _player.CameraInOriginSpaceHeight;
        _player.MoveCameraToWorldLocation(new Vector3(_playerSpawn.position.x, yPos, _playerSpawn.position.z));
        _player.MatchOriginUpCameraForward(_playerSpawn.up, _playerSpawn.forward);
    }

    private void PositionToggleAction(InputAction.CallbackContext obj)
    {
        Debug.Log("A was pressed");

        switch (mode)
        {
            case Mode.Standby:
                mode = Mode.CalibratingPos;
                break;
            default:
                Debug.Log($"Switch from {mode.ToString()} to standby");
                mode = Mode.Standby;
                return;
        }
    }
    
    /// <summary>
    /// Vertical Positioning Action 
    /// </summary>
    /// <param name="obj"></param>
    private void VPositioningAction(InputAction.CallbackContext obj)
    {
        if(mode == Mode.CalibratingPos) {
            Vector2 val = leftJS.ReadValue<Vector2>();

            _rotRef.transform.eulerAngles = new Vector3(0, _rotationReference.transform.eulerAngles.y, 0);
            _room.transform.Translate(new Vector3(0, val.y, 0) * _posFactor, _rotRef.transform);
        }
    }

    /// <summary>
    /// Horizontal Positioning Action
    /// </summary>
    /// <param name="obj"></param>
    private void HPositioningAction(InputAction.CallbackContext obj)
    {
        if (mode == Mode.CalibratingPos) {
            Vector2 val = rightJS.ReadValue<Vector2>();

            _rotRef.transform.eulerAngles = new Vector3(0, _rotationReference.transform.eulerAngles.y, 0);
            _room.transform.Translate(new Vector3(val.x, 0, val.y) * _posFactor, _rotRef.transform);
        }
    }

    private void LeftRotateAction(InputAction.CallbackContext obj)
    {
        Rotate(-1);
    }

    private void RightRotateAction(InputAction.CallbackContext obj)
    {
        Rotate(1);
    }

    /// <summary>
    /// Rotate the "Room" in the direction "dir" provided
    /// </summary>
    /// <param name="dir"></param>
    private void Rotate(int dir)
    {
        switch (mode)
        {
            case Mode.Standby:
                mode = Mode.CalibratingRot;
                _rotDirection = dir;
                break;
            default:
                Debug.Log($"Switch from {mode.ToString()} to standby");
                mode = Mode.Standby;
                return;
        }
    }
    
    /// <summary>
    /// Method to toggle between passthrough and VR view
    /// </summary>
    /// <param name="obj"></param>
    private void VisionToggleAction(InputAction.CallbackContext obj)
    {
        switch (vision)
        {
            case Vision.Normal:
                vision = Vision.Passthrough;
                break;
            case Vision.Passthrough:
                vision = Vision.Normal;
                break;
            default:
                vision = Vision.Normal;
                return;
        }
    }

    /// <summary>
    /// Method called when the "Done" button is pushed
    /// </summary>
    /// <param name="obj"></param>
    private void DoneAction(InputAction.CallbackContext obj)
    {
        mode = Mode.Done;
    }

    /// <summary>
    /// Method called everytime the calibration mode is changed
    /// </summary>
    private void ModeChanged()
    {
        if (mode == Mode.Standby)
        {
            //freeze everything, inlcuding position and rotation
            //hide the passthrough layer
            //stop all room rotations
            //set the material colors right
            _roomRB.constraints = RigidbodyConstraints.FreezeAll;
            _rotDirection = 0;
        }
        else if (mode == Mode.CalibratingPos)
        {
            //only freeze the room rotating
            _roomRB.constraints = RigidbodyConstraints.FreezeRotation;
        }
        else if (mode == Mode.CalibratingRot)
        {
            //only freeze the x, y and z rotations
            _roomRB.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePosition;
        }
        else if (mode == Mode.Done)
        {
            //done, so send the player's information relative to the room to the next scene
            // R: where the player is relative to the room's centre (which is not 0 anymore),
            // how much the player is rotated relative to Quaternion.Identity
            // how much the room is rotated relative to Quaternion.Identity
            _send = new MyTransform(_playerCenterReference.transform.position - _room.transform.position,
            _playerCenterReference.transform.eulerAngles, _room.transform.eulerAngles);
            vision = Vision.Normal;

            //if doneEvent is not null, invoke doneEvent
            _doneEvent?.Invoke(_send);

            //stop ability to calibrate
            _canCalibrate = false;
        }
        else
        {
            Debug.Log("Error with ModeChanged(), mode not set properly");
        }
    }

    /// <summary>
    /// Method called everytime the Vision changes between Normal and Passthrough view
    /// </summary>
    private void VisionChanged()
    {
        if (vision == Vision.Normal)
        {
            foreach (Material m in _seeThroughInPassThroughMaterials)
            {
                Color c = m.color;
                c.a = 1;
                m.color = c;
            }
            foreach (Material m in _partialSeeThroughInPassThroughMaterials)
            {
                Color c = m.color;
                c.a = 1;
                m.color = c;
            }
            passThrough.color = UI_NOT_SELECTED;
            _ARSession.enabled = false;
        }
        else if (vision == Vision.Passthrough)
        {
            //passthrough layer is always active

            foreach (Material m in _seeThroughInPassThroughMaterials)
            {
                Color c = m.color;
                c.a = 0;
                m.color = c;
            }
            foreach (Material m in _partialSeeThroughInPassThroughMaterials)
            {
                Color c = m.color;
                c.a = 0.5f;
                m.color = c;
            }
            passThrough.color = UI_SELECTED;
            _ARSession.enabled = true;
        }
        else
        {
            Debug.Log("Problem with setting up vision change");
        }
    }

    private void ToggleModeUI()
    {
        foreach (TMP_Text mode in _modes)
        {
            mode.color = UI_NOT_SELECTED;
        }
            
        if ((int)mode < _modes.Length)
        {
            _modes[(int)mode].color = UI_SELECTED;
        }
            
    }

    private void OnDisable()
    {
        menuButton.performed -= DoneAction; menuButton.Disable();
        leftHandPB.performed -= VisionToggleAction; leftHandPB.Disable();
        leftHandSB.performed -= LeftRotateAction; leftHandSB.Disable();
        leftJS.performed -= VPositioningAction; leftJS.Disable();
        rightHandPB.performed -= PositionToggleAction; rightHandPB.Disable();
        rightHandSB.performed -= RightRotateAction; rightHandSB.Disable();
        rightJS.performed -= HPositioningAction; rightJS.Disable();

        Debug.Log("OnDisable");
    }
}

/// <summary>
/// UnityEvent to take player's position relative to the room (MyTransform) as an input
/// </summary>
[System.Serializable]
public class MyLoadSceneEvent : UnityEvent<MyTransform>
{
}

/// <summary>
/// struct to store: <br/>
/// the positon of the player relative to it's origin <br/>
/// rotation of the player <br/>
/// the rotation of the player's origin
/// </summary>
public struct MyTransform
{
    public MyTransform(Vector3 pos, Vector3 eul)
    {
        position = pos;
        eulerAngles = eul;
        originRot = Vector3.zero;
    }

    public MyTransform(Vector3 pos, Vector3 eul, Vector3 oRot)
    {
        position = pos;
        eulerAngles = eul;
        originRot = oRot;
    }

    public Vector3 position { get; }
    public Vector3 eulerAngles { get; }
    public Vector3 originRot { get; }
    public override String ToString()
    {
        return $"Position: {position} EulerAngles: {eulerAngles} OriginRotation: {originRot}";
    }
}
