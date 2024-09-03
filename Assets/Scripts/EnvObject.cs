using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// EnvObject, used to control the behaviour of importaitn objects placed in the scene <br/>
/// Attached to shapes in the shape judgement/sorting tasks
/// </summary>
public class EnvObject : MonoBehaviour
{
    /// <summary>
    /// Whether to track this object or not
    /// </summary>
    [SerializeField]
    protected bool tracking = false;
    /// <summary>
    /// Audio to play when object is grabbed
    /// </summary>
    [SerializeField]
    protected AudioSource _audioSource;
    /// <summary>
    /// ID of player who owns this object, set at runtime <br/>
    /// Set to 0 for non multiplayer. <br/>
    /// Set to 0......n based on number of players in the room for multiplayer
    /// </summary>
    [SerializeField]
    protected int _ownerID = 0;

    /// <summary>
    /// Initial position of the object at start
    /// </summary>
    private Vector3 _initPosition;
    /// <summary>
    /// Initial rotation of the object at start
    /// </summary>
    private Quaternion _initRotation;

    /// <summary>
    /// Set to true if to stop traking the object, whether multiplayer or not
    /// </summary>
    private bool _stopTracking;
    /// <summary>
    /// How many how long stop tracking has been set to true
    /// </summary>
    private float _stopTrackCount = 0;
    /// <summary>
    /// Limit for stopTrackCount, object will no longer be tracked after stopTrackCount reaches this limit
    /// </summary>
    private float _stopTrackLimit = 1.5f;

    /// <summary>
    /// Whether the EnvObject is colliding with a the posting box or not
    /// </summary>
    private bool _boxColliding = false;
    /// <summary>
    /// Time when the EnvObject collides with the box in total
    /// </summary>
    private float _boxColTime = 0;
    /// <summary>
    /// Time when the EnvObject collides with the box while entering or being posted into the box
    /// </summary>
    private float _boxColTimeWhilePosting = 0;
    /// <summary>
    /// Triggers in the Shape Sorting Cube
    /// </summary>
    private HashSet<GameObject> _boxTriggers;

    public enum StatusWRTBox
    {
        OutsideBox,
        EnteringBoxRight,
        EnteringBoxWrong,
        InsideBox
    }

    /// <summary>
    /// Status of the object with reference to the box. <br/>
    /// Either "Outside Box" or "Inside Box" or "Entering {hole name}"
    /// </summary>
    private StatusWRTBox _statusWRTBox = StatusWRTBox.OutsideBox;

    private Rigidbody _rigidbody;
    private XRGrabInteractable _grabInteractable;

    /// <summary>
    /// DataManager in the scene
    /// </summary>
    private DataManager _dataManager;
    //add a listener to the selectEntered so it requests ownership when the object is grabbed
    public virtual void OnEnable()
    {
        _dataManager = FindAnyObjectByType<DataManager>();
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (_grabInteractable != null)
        {
            _grabInteractable.selectEntered.AddListener(OnGrab);
            _grabInteractable.selectExited.AddListener(OnRelease);
        }
        SetUp();
    }

    private void FixedUpdate()
    {
        //don't update stuff for the box for now
        if (CompareTag("Box") || !tracking) return;

        if (_stopTrackCount >= _stopTrackLimit)
        {
            _dataManager.RemoveObjectTrack(gameObject);
            tracking = false;
            gameObject.SetActive(false);
        }

        if (_stopTracking)
            _stopTrackCount += Time.deltaTime;

        if (_boxColliding)
        {
            _boxColTime += Time.deltaTime;
            if (_statusWRTBox == StatusWRTBox.EnteringBoxRight)
                _boxColTimeWhilePosting += Time.deltaTime;
        }
    }

    /// <summary>
    /// Called to set up the environment object at the start
    /// </summary>
    public virtual void SetUp()
    {
        SetUpColliders(transform); //set up colliders
        _dataManager.AddObjectTrack(gameObject); //add to list of tracked objects by data manager
        _initPosition = transform.position; //store the initial position
        _initRotation = transform.rotation; //store the initial rotation

        _boxTriggers = new HashSet<GameObject>(); //initialize the list of triggers on the shape sorting cube
    }

    /// <summary>
    /// Make all the colliders grabbable
    /// </summary>
    /// <param name="o"></param>
    private void SetUpColliders(Transform o)
    {
        if(o.TryGetComponent<Collider>(out Collider col) && !_grabInteractable.colliders.Contains(col))
            _grabInteractable.colliders.Add(col);

        foreach(Transform child in o)
            SetUpColliders(child);

        return;
    }

    /// <summary>
    /// Get the status of the object with respect to the box/ShapeSortingCube
    /// </summary>
    /// <returns>The status of the object with respect to the box</returns>
    public StatusWRTBox GetStatus()
    {
        return _statusWRTBox;
    }

    /// <summary>
    /// Get owner of the object
    /// </summary>
    /// <returns>ID of the owner of the object</returns>
    public int GetOwnerID()
    {
        return _ownerID;
    }

    /// <summary>
    /// What happens when the object is grabbed (XR)
    /// </summary>
    /// <param name="args"></param>
    public virtual void OnGrab(SelectEnterEventArgs args)
    {
        _rigidbody.constraints = RigidbodyConstraints.None;
        _audioSource.Play();
        _rigidbody.drag = 0;
        _rigidbody.angularDrag = 0.05f;
    }

    /// <summary>
    /// What happens when the object is released (XR)
    /// </summary>
    /// <param name="args"></param>
    public virtual void OnRelease(SelectExitEventArgs args)
    {
        _rigidbody.constraints = RigidbodyConstraints.None;
        _rigidbody.drag = 0;
        _rigidbody.angularDrag = 0.05f;
    }

    /// <summary>
    /// Call to reset the objects position and rotation to it's initial
    /// </summary>
    private void ResetToStartPos()
    {
        //return to initial position and rotation
        transform.SetPositionAndRotation(_initPosition, _initRotation);
        
        _rigidbody.velocity = Vector3.zero; //so it doesn't keep moving with it's current velocity
        _rigidbody.angularVelocity = Vector3.zero;

        //so we release the grab even after reseting position
        if(_grabInteractable.isSelected)
            _grabInteractable.interactionManager.CancelInteractorSelection(_grabInteractable.firstInteractorSelecting);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Floor"))
        {
            ResetToStartPos();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.collider.CompareTag("Floor"))
        {
            ResetToStartPos();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Trigger"))
        {
            if (other.name.ToLower().Contains("reset")) ResetToStartPos();
            if (other.name.ToLower().Contains("box")) _statusWRTBox = StatusWRTBox.InsideBox;
            else if (other.name.ToLower().Contains("hole")) _statusWRTBox = StatusWRTBox.EnteringBoxRight;

            if (other.name.ToLower().Contains("bottom"))
            {
                Debug.Log("Stopping Tracking True");
                _stopTracking = true;
            }
        }
        else if (other.CompareTag("BoxTrigger"))
        {
            _boxColliding = true;
            _boxTriggers.Add(other.gameObject);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Trigger"))
        {
            if (other.name.ToLower().Contains("box")) _statusWRTBox = StatusWRTBox.InsideBox;
            else if (other.name.ToLower().Contains("hole")) _statusWRTBox = StatusWRTBox.EnteringBoxRight;

            if (other.name.ToLower().Contains("bottom"))
            {
                _stopTracking = true;
            }
                
        }
        else if (other.CompareTag("BoxTrigger"))
        {
            _boxColliding = true;
            _boxTriggers.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Trigger"))
        {
            _statusWRTBox = StatusWRTBox.OutsideBox;

            if (other.name.ToLower().Contains("bottom"))
            {
                _stopTracking = false;
            }
        }
        if (other.CompareTag("BoxTrigger"))
        {
            _boxTriggers.Remove(other.gameObject);
            if (_boxTriggers.Count <= 0)
                _boxColliding = false;
        }
    }

    private void OnDestroy()
    {
        tracking = false;
    }
}
