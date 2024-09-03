using com.perceptlab.armultiplayer;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// ExpController, used to control the behaviour of the shape judgement <br/>
/// Controls taking in user input, sending user input to DataManager, presenting input for trials, etc.
/// </summary>
public class ExpController : MonoBehaviour
{
    /// <summary>
    /// Whether to run the experiment or not
    /// </summary>
    [Header("SCENE VARIABLES")]
    [SerializeField]
    protected bool ToRun = false;
    /// <summary>
    /// DataManager to help monitor and save experiment data
    /// </summary>
    [SerializeField]
    protected DataManager m_DataManager;
    /// <summary>
    /// Player Camera
    /// </summary>
    [SerializeField]
    protected Camera m_PlayerCamera;
    /// <summary>
    /// Experiment set up Transform
    /// </summary>
    [SerializeField]
    protected Transform m_ExpSetUp;
    /// <summary>
    /// The sorting cube in the scene
    /// </summary>
    [SerializeField]
    protected Transform m_SortingCube;
    /// <summary>
    /// The spawn location for the sorting cube
    /// </summary>
    [SerializeField]
    protected Transform m_CubeSpawn;
    /// <summary>
    /// The spawn locations for the shapes
    /// </summary>
    [SerializeField]
    protected Transform[] m_ShapeSpawn = new Transform[] { };
    /// <summary>
    /// Audio to play after player inputs their response
    /// </summary>
    [SerializeField]
    protected AudioSource m_SaveAudio;

    /// <summary>
    /// The regular shapes that we can use, ensure to specify them if you plan on using them
    /// </summary>
    [Header("EXPERIMENT VARIABLES")]
    [SerializeField]
    protected GameObject[] m_RegularShapes = new GameObject[] { };
    /// <summary>
    /// The complex shapes that we can use, ensure to specify them if you plan on using them
    /// </summary>
    [SerializeField]
    protected GameObject[] m_ComplexShapes = new GameObject[] { };
    /// <summary>
    /// Whether the shapes and the box should face towards the player's position or not
    /// </summary>
    [SerializeField]
    protected bool m_FacePlayer = false;
    /// <summary>
    /// Whether to spawn the shapes in random locations or not
    /// </summary>
    [SerializeField]
    protected bool m_RandomShapeLocation = false;
    /// <summary>
    /// Whether we want to be able to grab the box or not
    /// </summary>
    [SerializeField]
    protected bool m_CanGrabBox = false;
    /// <summary>
    /// Whether we want to be able to grab the shapes or not
    /// </summary>
    [SerializeField]
    protected bool m_CanGrabShapes = false;
    /// <summary>
    /// Minimum amount of time to show blank/nothing to the participant
    /// </summary>
    [SerializeField]
    protected float m_MinBlankTimeLimit = 3.0f;
    /// <summary>
    /// Maximum amount of time to show blank/nothing to the participant
    /// </summary>
    [SerializeField]
    protected float m_MaxBlankTimeLimit = 300.0f;
    /// <summary>
    /// Minimum time limit for each trial
    /// </summary>
    [SerializeField]
    protected float m_MinTrialTimeLimit = 5.0f;
    /// <summary>
    /// Maximum time limit for each trial
    /// </summary>
    [SerializeField]
    protected float m_MaxTrialTimeLimit = 10.0f;
    /// <summary>
    /// Number of sizes smaller from the original size for each shape
    /// </summary>
    [SerializeField]
    protected int m_NumberSmaller = 3;
    /// <summary>
    /// Number of sizes larger from the original size for each shape
    /// </summary>
    [SerializeField]
    protected int m_NumberLarger = 3;
    /// <summary>
    /// Particicpant ID
    /// </summary>
    [SerializeField]
    protected string m_PID = "0";
    /// <summary>
    /// What condition are we testing the participants on
    /// </summary>
    [SerializeField]
    protected string m_Condition = "test";
    /// <summary>
    /// Number of repeats for each shape and size
    /// </summary>
    [SerializeField]
    protected int m_Repeats = 1;
    /// <summary>
    /// Represents the difference in range of shapes between trials
    /// </summary>
    [SerializeField]
    protected float m_ScaleDiff = 0.025f;
    /// <summary>
    /// Number of shapes to use, picks random n number of shapes to use
    /// </summary>
    [SerializeField]
    protected int m_NumOfShapes = 4;
    /// <summary>
    /// Player's distance from the experiment set up in meters
    /// </summary>
    [SerializeField]
    protected float m_PlayerDistanceFromSetup = 0.8f;
    /// <summary>
    /// How fast the shape should rotate in DEG/S (Only set this if you're running ExpType 5)
    /// </summary>
    [SerializeField]
    protected float m_ShapeRotSpeed = 1f;
    /// <summary>
    /// Axis the shape should rotate on
    /// </summary>
    [SerializeField]
    protected Vector3 m_ShapeRotAxis = new(0, 1, 0);
    /// <summary>
    /// Amplitude of oscilation of the object in the oscilating version of the experiment
    /// </summary>
    [SerializeField]
    protected Vector3 m_OscilateAmpl = new(0, 90, 0); 

    /// <summary>
    /// Button for larger input response
    /// </summary>
    [Header("INPUT ACTIONS")]
    [SerializeField]
    protected InputAction m_LargerButton;
    /// <summary>
    /// Button for smaller input response
    /// </summary>
    [SerializeField]
    protected InputAction m_SmallerButton;
    /// <summary>
    /// Button to pause rotation if it's rotating
    /// </summary>
    [SerializeField]
    protected InputAction m_PauseRotationButton;

    public enum ExpType
    {
        NonInteractiveSimple,
        InteractiveSimple,
        NonInteractiveComplex,
        InteractiveComplex,
        RotatingComplex
    }

    /// <summary>
    /// Experiment types
    /// </summary>
    private ExpType _expType;
    
    protected ExpType expType{
        get { return _expType; }
        set 
        { 
            _expType = value;
            ExpTypeChanged();
        }
    }
    /// <summary>
    /// List of order of the experiment. Contains string in format "shapeNumber|shapeName|scale|spawnLocation";
    /// </summary>
    protected List<string> _order;
    /// <summary>
    /// Next trial number, where trials go from 0 to n
    /// </summary>
    protected int _nTrialNumber = 0;

    protected Transform _playerTransform;

    /// <summary>
    /// to keep track of the objects being spawned
    /// </summary>
    protected GameObject spawn = null;

    /// <summary>
    /// Time last stimuli is displayed
    /// </summary>
    protected DateTime _dispTime = DateTime.Now;
    /// <summary>
    /// Time last entry was registered
    /// </summary>
    protected DateTime _entryTime = DateTime.Now;

    //static variables
    protected readonly static string LARGER_RESPONSE = "1";//save "1" for larger responses
    protected readonly static string SMALLER_RESPONSE = "0";//save "0" for smaller responses 
    protected readonly static string NO_RESPONSE = "-1";//save "-1" if n response is recorded
    protected readonly static int DEFAULT_NO_SHAPE = 4;//4 shapes by default
    protected readonly static int DEFAULT_NO_LARGER = 3;//3 sizes bigger for each shape
    protected readonly static int DEFAULT_NO_SMALLER = 3;//3 sizes smaler for each shape
    protected readonly static float MAX_SHAPE_ROTATE_TIME = 1;//shape rotates every 1 second
    protected readonly static bool DEFAULT_PAUSE_ROTATION = false;//default value o

    //soooo many flags.........lol
    /// <summary>
    /// when we're ready to start flag
    /// </summary>
    protected bool _ready = false;

    /// <summary>
    /// next trial loading flag
    /// </summary>
    protected bool _nextLoading = false;

    /// <summary>
    /// blank timer, tracking how long we've seen dark/blank/nothing for
    /// </summary>
    protected float _blankTimer = 0;
    /// <summary>
    /// whether the minimum amount of time to see nothing has passed
    /// </summary>
    protected bool _minBlankLoading = false;
    /// <summary>
    /// whether the maximum amount of time to see nothing has passed
    /// after this we move to seeing the next trial/stimuli
    /// </summary>
    protected bool _maxBlankLoading = false;

    /// <summary>
    /// trial timer, tracking how long a trial has been presented to participant for
    /// </summary>
    protected float _trialTimer = 0;
    /// <summary>
    /// whether the minimum amount of time to be presented a trial has passed or not
    /// </summary>
    protected bool _minTrialLoading = true;
    /// <summary>
    /// whether the maximum amount of time to be presented a trial has passed or not <br/>
    /// after this we move to seeing blank, and blank timer starts
    /// </summary>
    protected bool _maxTrialLoading = true;

    /// <summary>
    /// shape rotate timer
    /// </summary>
    protected float _rotateTimer = 0;
    protected bool _pauseRotation = DEFAULT_PAUSE_ROTATION;
    /// <summary>
    /// what direction to rotate the object/shape <br/>
    /// by default, rotate in positive direction on the y-axis
    /// </summary>
    protected Vector3 _rotateDir = new(0, 1, 0);
    /// <summary>
    /// Start rotation of the shape
    /// </summary>
    protected Vector3 _spawnParentStartRot;
    /// <summary>
    /// Current rotation of the shape
    /// </summary>
    protected Vector3 _spawnParentCurrRot = Vector3.zero;
    protected Vector3 _shapeStartRotation = new Vector3(90, -180, -90);

    //saving flags
    protected bool _savingEntry = false;
    protected bool _savedEntry = false;

    /// <summary>
    /// Shape name to the rotation the box should have to see the corresponding slot
    /// </summary>
    protected Dictionary<string, Vector3> _shapeToBoxRotation0;
    /// <summary>
    /// Shape name to the rotation the box should have to see the corresponding slot and have it face the participant
    /// </summary>
    protected Dictionary<string, Vector3[]> _shapeToBoxRotation1;

    /// <summary>
    /// The shapes being used in this run of the experiment
    /// </summary>
    protected GameObject[] m_Shapes;

    virtual protected void OnEnable()
    {
        m_LargerButton.Enable(); m_LargerButton.performed += SaveLargerResponse;
        m_SmallerButton.Enable(); m_SmallerButton.performed += SaveSmallerResponse;
        m_PauseRotationButton.Enable(); m_PauseRotationButton.performed += PauseRotationToggle;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    void FixedUpdate()
    {
        if (_minTrialLoading || _maxTrialLoading)
        {
            _trialTimer += Time.deltaTime;
            if (_trialTimer > m_MinTrialTimeLimit)
            {
                _minTrialLoading = false;
                if (_savedEntry)
                {
                    _maxTrialLoading = false;
                    _trialTimer = 0;
                    //see blank/nothing
                    m_PlayerCamera.cullingMask = 0;
                    _blankTimer = 0;
                    _minBlankLoading = true;
                    _maxBlankLoading = true;
                }
            }

            if(_trialTimer > m_MaxTrialTimeLimit)
            {
                _minTrialLoading = false;
                _maxTrialLoading = false;
                _trialTimer = 0;
                //see blank/nothing
                m_PlayerCamera.cullingMask = 0;
                _blankTimer = 0;
                _minBlankLoading = true;
                _maxBlankLoading = true;
            }
        }

        if (_minBlankLoading || _maxBlankLoading)
        {
            _blankTimer += Time.deltaTime;
            if (_blankTimer > m_MinBlankTimeLimit)
            {
                _minBlankLoading = false;
                if (_savedEntry) NextTrial();
            }

            if (_blankTimer > m_MaxBlankTimeLimit)
            {
                _blankTimer = 0;
                _minBlankLoading = false;
                _maxBlankLoading = false;

                SaveEntry(NO_RESPONSE);
                NextTrial();
            }
        }

        if(spawn != null && expType == ExpType.RotatingComplex && !_pauseRotation)
        {
            Vector3 r = Product(m_ShapeRotAxis, _rotateDir) * m_ShapeRotSpeed * Time.deltaTime;
            spawn.transform.parent.Rotate(r);
            _rotateTimer = 0;

            _spawnParentCurrRot += r;
            if (Math.Abs(_spawnParentCurrRot.y) > m_OscilateAmpl.y)
                _rotateDir *= -1;
        }
    }

    private Vector3 Product(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x*b.x, a.y*b.y, a.z*b.z);
    }

    public void Initialize()
    {
        if (!ToRun || _ready) return;
        //Set the box rotation for the different shapes
        SetBoxRotations();
        //Shuffle the order of the shapes
        ShuffleShapes();
        //Set up experiment order
        CreateExpOrder();
        //Shuffle the order
        ShuffleOrder();
        //Create file path to store entries 
        CreateExpFile();

        _playerTransform = m_PlayerCamera.transform;

        _ready = true;
    }

    /// <summary>
    /// Store the rotation of the box based on the shape and whether it should face the participant or not
    /// </summary>
    protected void SetBoxRotations()
    {
        if (m_FacePlayer)
        {
            _shapeToBoxRotation1 = new Dictionary<string, Vector3[]>();
            foreach (GameObject s in m_Shapes)
            {
                if (s.name.ToLower().Contains("trapezoid") || s.name.ToLower().Contains("oval") || s.name.ToLower().Contains("diamond"))
                {
                    _shapeToBoxRotation1.Add(s.name, new[] { Vector3.up, new Vector3(0, -90, 0) });
                }
                else if (s.name.ToLower().Contains("quatrefoil") || s.name.ToLower().Contains("square") || s.name.ToLower().Contains("triangle"))
                {
                    _shapeToBoxRotation1.Add(s.name, new[] { Vector3.down, new Vector3(90, 0, 0) });
                }
                else if (s.name.ToLower().Contains("octagon") || s.name.ToLower().Contains("parallelogram") || s.name.ToLower().Contains("star"))
                {
                    _shapeToBoxRotation1.Add(s.name, new[] { Vector3.up, new Vector3(90, 0, 0) });
                }
                else if (s.name.ToLower().Contains("hexagon") || s.name.ToLower().Contains("rectangle") || s.name.ToLower().Contains("pentagon"))
                {
                    _shapeToBoxRotation1.Add(s.name, new[] { Vector3.up, new Vector3(0, 90, 0) });
                }
                else
                {
                    Debug.Log("SHAPE NAME NOT RECOGNIZED!!!");
                }
            }
        }
        else
        {
            _shapeToBoxRotation0 = new Dictionary<string, Vector3>();
            foreach (GameObject s in m_Shapes)
            {
                Debug.Log($"Shape Name {s.name}");
                if (s.name.ToLower().Contains("trapezoid") || s.name.ToLower().Contains("oval") || s.name.ToLower().Contains("diamond"))
                {
                    _shapeToBoxRotation0.Add(s.name, new Vector3(0, 180, 0));
                }
                else if (s.name.ToLower().Contains("quatrefoil") || s.name.ToLower().Contains("square") || s.name.ToLower().Contains("triangle"))
                {
                    _shapeToBoxRotation0.Add(s.name, new Vector3(-90, 0, 90));
                }
                else if (s.name.ToLower().Contains("octagon") || s.name.ToLower().Contains("parallelogram") || s.name.ToLower().Contains("star"))
                {
                    _shapeToBoxRotation0.Add(s.name, new Vector3(0, -90, 0));
                }
                else if (s.name.ToLower().Contains("hexagon") || s.name.ToLower().Contains("rectangle") || s.name.ToLower().Contains("pentagon"))
                {
                    _shapeToBoxRotation0.Add(s.name, new Vector3(0, 0, 0));
                }
                else
                {
                    Debug.Log("SHAPE NAME NOT RECOGNIZED!!!");
                }
            }
        }
    }

    /// <summary>
    /// Set up for the next trial
    /// </summary>
    protected void NextSetUp()
    {
        float xDiff = m_PlayerCamera.transform.position.x - m_ExpSetUp.transform.position.x;
        float yDiff = m_PlayerCamera.transform.position.y - m_ExpSetUp.transform.position.y;
        float zDiff = m_PlayerCamera.transform.position.z - m_ExpSetUp.transform.position.z;
        m_ExpSetUp.Translate(new Vector3(xDiff + m_PlayerDistanceFromSetup, yDiff, zDiff));

        string trial = GetNextTrial();
        string shape = trial.Split('|')[1];
        int loc = int.Parse(trial.Split('|')[3]);

        m_SortingCube.position = m_CubeSpawn.position;

        if(m_SortingCube.TryGetComponent<XRGrabInteractable>(out XRGrabInteractable g))
        {
            g.enabled = m_CanGrabBox;
        }
        if(m_SortingCube.TryGetComponent<Rigidbody>(out Rigidbody r)){
            r.constraints = m_CanGrabBox ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeAll;
        }

        if (m_FacePlayer)
        {
            m_SortingCube.LookAt(_playerTransform.position, _shapeToBoxRotation1[shape][0]);
            m_SortingCube.Rotate(_shapeToBoxRotation1[shape][1]);
        }
        else
        {
            m_SortingCube.eulerAngles = _shapeToBoxRotation0[shape];
        }
    }

    /// <summary>
    /// Call to set the player/partcipant ID
    /// </summary>
    /// <param name="id"></param>
    public void SetPID(string id)
    {
        m_PID = id;
        Debug.Log($"set id to {id}");
    }

    /// <summary>
    /// Call to set the experiment type
    /// </summary>
    /// <param name="type"></param>
    public void SetExpType(int type)
    {
        expType = (ExpType)type;
    }

    /// <summary>
    /// Called in the set method or ExpType, so invoked whenever the experiment type changes <br/>
    /// Sets variables based on the experiment type chosen
    /// </summary>
    protected void ExpTypeChanged()
    {
        //Non Interact Simple, Non Interact Complex
        if (expType == ExpType.NonInteractiveSimple || expType == ExpType.NonInteractiveComplex)
        {
            m_FacePlayer = false;
            m_RandomShapeLocation = false;
            m_CanGrabBox = false; m_CanGrabShapes = false;
            m_MinBlankTimeLimit = 2; m_MaxBlankTimeLimit = 300;
            m_MinTrialTimeLimit = 2.5f; m_MaxTrialTimeLimit = 2.5f;
            m_NumberSmaller = DEFAULT_NO_SMALLER; m_NumberLarger = DEFAULT_NO_LARGER; m_NumOfShapes = DEFAULT_NO_SHAPE;

            if (expType == ExpType.NonInteractiveSimple)
                m_Shapes = m_RegularShapes;
            if (expType == ExpType.NonInteractiveComplex)
                m_Shapes = m_ComplexShapes;

            _shapeStartRotation = new Vector3(90, -180, -90);
        }
        //Interact Simple, Interact Complex
        else if (expType == ExpType.InteractiveSimple || expType == ExpType.InteractiveComplex)
        {
            m_FacePlayer = false;
            m_RandomShapeLocation = false;
            m_CanGrabBox = false; m_CanGrabShapes = true;
            m_MinBlankTimeLimit = 2; m_MaxBlankTimeLimit = 300;
            m_MinTrialTimeLimit = 10f; m_MaxTrialTimeLimit = 10f;
            m_NumberSmaller = DEFAULT_NO_SMALLER; m_NumberLarger = DEFAULT_NO_LARGER; m_NumOfShapes = DEFAULT_NO_SHAPE;

            if (expType == ExpType.InteractiveSimple)
                m_Shapes = m_RegularShapes;
            if (expType == ExpType.InteractiveComplex)
                m_Shapes = m_ComplexShapes;

            _shapeStartRotation = new Vector3(90, -180, -180);
        }
        //Rotating Complex
        else if (expType == ExpType.RotatingComplex)
        {
            m_FacePlayer = false;
            m_RandomShapeLocation = false;
            m_CanGrabBox = false; m_CanGrabShapes = false;
            m_MinBlankTimeLimit = 2; m_MaxBlankTimeLimit = 300; 
            m_MinTrialTimeLimit = 5f; m_MaxTrialTimeLimit = 10f;
            m_NumberSmaller = DEFAULT_NO_SMALLER; m_NumberLarger = DEFAULT_NO_LARGER; m_NumOfShapes = DEFAULT_NO_SHAPE;

            m_Shapes = m_ComplexShapes;

            _shapeStartRotation = new Vector3(90, -180, -180);
        }
        else
        {
            RLogger.Log($"ExpType {expType.ToSafeString()} not valid");
        }
    }

    /// <summary>
    /// Sets the number of random shapes to use, caps at the number of shapes we have
    /// </summary>
    /// <param name="num"></param>
    public void SetNumOfShapes(int num = int.MaxValue)
    {
        m_NumOfShapes = num > m_Shapes.Length ? m_Shapes.Length : num;
    }

    /// <summary>
    /// Call to set the number of repeats for the experiment
    /// </summary>
    /// <param name="repeats"></param>
    public void SetRepeats(int repeats)
    {
        m_Repeats = repeats;
    }

    /// <summary>
    /// Call to set the minimum difference in scale between different sizes
    /// </summary>
    /// <param name="diff"></param>
    public void SetScaleDiff(float diff)
    {
        m_ScaleDiff = diff;
    }

    /// <summary>
    /// Call to set the condition being run in this experiment
    /// </summary>
    /// <param name="cond"></param>
    public void SetCondition(string cond)
    {
        m_Condition = cond;
    }

    /// <summary>
    /// Returns a random location to place the shape out of the 5 different tables in the virtual percept room
    /// </summary>
    private int randomShapeLoc
    {
        get
        {
            if (m_RandomShapeLocation)
            {
                return UnityEngine.Random.Range(0, m_ShapeSpawn.Length);
            }
            else
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Call to create the order of the experiment, doesnt create anything if the order already exists
    /// </summary>
    protected void CreateExpOrder()
    {
        if (_order != null) return;

        _order = new List<string>();
        string entry;

        SetNumOfShapes(m_NumOfShapes);

        //for every shape
        for (int i = 0; i < m_NumOfShapes; i++)
        {
            //for every repeat for each shape
            for (int r = 0; r < m_Repeats; r++)
            {
                //smaller sizes
                for (int zS = 1; zS <= m_NumberSmaller; zS++)
                {
                    entry = $"{i}|{m_Shapes[i].name}|{1 - (m_ScaleDiff * zS)}|{randomShapeLoc}";//smaller scales
                    _order.Add(entry);
                }
                //mid size
                entry = $"{i}|{m_Shapes[i].name}|{1}|{randomShapeLoc}";
                _order.Add(entry);
                //larger sizes
                for (int zL = 1; zL <= m_NumberLarger; zL++)
                {
                    entry = $"{i}|{m_Shapes[i].name}|{1 + (m_ScaleDiff * zL)}|{randomShapeLoc}";//larger scales
                    _order.Add(entry);
                }
            }
        }
    }

    public List<string> GetOrder()
    { 
        return _order; 
    }

    /// <summary>
    /// In-place Shuffle based on the Fisher-Yates Algorithm 
    /// </summary>
    /// <remarks>
    /// Fisher-Yates: <see href="https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle">Wikipedia</see>
    /// <seealso cref="https://stackoverflow.com/questions/273313/randomize-a-listt"/>
    /// <see cref="https://docs.unity3d.com/ScriptReference/Random.Range.html">Unity Random</see>
    /// Pseudocode:
    ///     for i from 0 to n−2 do
    ///         j ← random integer such that i ≤ j < n
    ///         exchange a[i] and a[j]
    /// </remarks>
    protected void ShuffleShapes()
    {
        for (int n = 0; n < m_Shapes.Length; n++)
        {
            int i = UnityEngine.Random.Range(n, m_Shapes.Length);

            GameObject temp = m_Shapes[i];
            m_Shapes[i] = m_Shapes[n];
            m_Shapes[n] = temp;
        }
    }

    /// <summary>
    /// Call to shuffle the experiment order
    /// </summary>
    protected void ShuffleOrder()
    {
        for (int n = 0; n < _order.Count; n++)
        {
            int i = UnityEngine.Random.Range(n, _order.Count);

            string temp = _order[i];
            _order[i] = _order[n];
            _order[n] = temp;
        }
    }

    protected void SaveLargerResponse(InputAction.CallbackContext obj)
    {
        if (spawn != null) SaveEntry(LARGER_RESPONSE);
        NextTrial();
    }

    protected void SaveSmallerResponse(InputAction.CallbackContext obj)
    {
        if (spawn != null) SaveEntry(SMALLER_RESPONSE);
        NextTrial();
    }

    protected void PauseRotationToggle(InputAction.CallbackContext obj)
    {
        if (spawn != null)
            _pauseRotation = !_pauseRotation;
    }

    /// <summary>
    /// Creates the experiment file
    /// </summary>
    protected void CreateExpFile()
    {
        m_DataManager.CreateExpFile($"Exp{expType.ToSafeString()}_{m_PID}_{m_Condition}_{m_Repeats}Reps_{m_ScaleDiff}Range");
    }

    /// <summary>
    /// Saves the current/last entry from the user
    /// </summary>
    /// <param name="response"></param>
    protected void SaveEntry(string response)
    {
        if (SaveWait()) return;

        _savingEntry = true;
        m_SaveAudio.Play();
        Debug.Log($"Saving entry {response}");
        string trial = GetCurTrial();
        string shape = trial.Split('|')[1];
        float size = float.Parse(trial.Split('|')[2]);

        _entryTime= DateTime.Now;
        TimeSpan diff = _entryTime - _dispTime;
        m_DataManager.UpdateExpFile(_nTrialNumber, shape, size, response, diff.ToString(), _entryTime.TimeOfDay.ToString());
        
        _savedEntry = true;
        _savingEntry = false;
    }

    /// <summary>
    /// Save the experiment file
    /// </summary>
    protected void SaveExpFile()
    {   
        m_DataManager.SaveExpFile();
    }

    /// <summary>
    /// Sets up the next trial
    /// </summary>
    protected void NextTrial()
    {
        //return if we have to wait
        if (NextWait()) return;

        //close if we've gone through all trials
        if (_nTrialNumber >= _order.Count)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                UnityEditor.EditorApplication.isPlaying = false;
                return;
            }
#endif
            Application.Quit();
            return;
        }

        //set all related flags
        _nextLoading = true;

        _minBlankLoading = false;
        _maxBlankLoading = false;

        _minTrialLoading = false;
        _maxTrialLoading = false;

        _savedEntry = false;

        _pauseRotation = DEFAULT_PAUSE_ROTATION;

        //Set up experiment env
        NextSetUp();

        //delete existing shape if it exists
        if (spawn != null)
        {
            m_DataManager.RemoveObjectTrack(spawn);
            DestroySpawnShape();
        }

        SpawnShape();
            
        _dispTime = DateTime.Now;
        
        _nTrialNumber++;

        //see everything after setting up
        m_PlayerCamera.cullingMask = -1;
        //reset trial timer to go up to the set limit
        _trialTimer = 0;
        //start counting trial loading
        _minTrialLoading = true;
        _maxTrialLoading = true;

        //next trial done loading
        _nextLoading = false;
    }

    /// <summary>
    /// Spawns the next "spawn" shape
    /// </summary>
    protected virtual void SpawnShape()
    {
        string trial = GetNextTrial();

        int index = int.Parse(trial.Split('|')[0]);
        
        int loc = int.Parse(trial.Split('|')[3]);

        spawn = Instantiate(m_Shapes[index], m_ShapeSpawn[loc].transform);
        spawn.name = $"{m_Shapes[index].name}";
        InitShapeSpawn();
    }

    /// <summary>
    /// Initializes values for the current "spawn" shape
    /// </summary>
    protected virtual void InitShapeSpawn()
    {
        string trial = GetNextTrial();
        float size = float.Parse(trial.Split('|')[2]);

        spawn.transform.parent.eulerAngles = Vector3.zero;
        _spawnParentCurrRot = Vector3.zero;

        spawn.transform.localScale = new Vector3(spawn.transform.localScale.x * size,
            spawn.transform.localScale.y * 1,
            spawn.transform.localScale.z * size);

        if (m_FacePlayer)
        {
            spawn.transform.LookAt(_playerTransform.position, Vector3.up);
            spawn.transform.Rotate(new Vector3(90, 0, 0));
        }
        else
        {
            spawn.transform.eulerAngles = _shapeStartRotation;
        }

        if (spawn.TryGetComponent<Rigidbody>(out Rigidbody r))
        {
            r.mass = 1e+09f;
            r.constraints = RigidbodyConstraints.FreezeRotation;
            r.drag = 1000;
            r.angularDrag = 0;
        }

        if (spawn.TryGetComponent<XRGrabInteractable>(out XRGrabInteractable g))
        {
            g.enabled = m_CanGrabShapes;
        }
    }

    /// <summary>
    /// Destroys the current "spawn" shape
    /// </summary>
    protected virtual void DestroySpawnShape()
    {
        Destroy(spawn);
        spawn = null;
    }

    /// <summary>
    /// Returns whether we can move to next trial or not
    /// </summary>
    /// <returns></returns>
    protected bool NextWait()
    {
        Debug.Log($"Unable to proceed to Next.......waiting \n " +
            $"ToRun: {ToRun}; _ready: {_ready}; _nextLoading: {_nextLoading}; _minBlankLoading: {_minBlankLoading}; _minTrialLoading: {_minTrialLoading}; _maxTrialLoading: {_maxTrialLoading};");
        return (!ToRun || !_ready || _nextLoading || _minBlankLoading || _minTrialLoading || _maxTrialLoading);
    }

    /// <summary>
    /// Returns whether we can save now or not
    /// </summary>
    /// <returns></returns>
    protected bool SaveWait()
    {
        Debug.Log("Unable to proceed to Save.......waiting");
        return (!_ready || _savedEntry || _savingEntry || !ToRun || _nextLoading /*|| _trialLoading*/);
    }

    public int GetCurrTrialNumber()
    {
        return _nTrialNumber;
    }

    /// <summary>
    /// Returns string representation of the current trial. In format "shapeNumber|shapeName|scale|spawnLocation";
    /// </summary>
    /// <returns></returns>
    public string GetCurTrial()
    {
        return _order[_nTrialNumber - 1];
    }

    /// <summary>
    /// Returns string representation of the next trial. In format "shapeNumber|shapeName|scale|spawnLocation";
    /// </summary>
    /// <returns></returns>
    public string GetNextTrial()
    {
        return _order[_nTrialNumber];
    }

    /// <summary>
    /// Returns the string representation of the trial based on the given trialNumber. In format "shapeNumber|shapeName|scale|spawnLocation";
    /// </summary>
    /// <param name="trialNumber"></param>
    /// <returns></returns>
    public string GetTrial(int trialNumber)
    {
        return _order[trialNumber];
    }

    protected void OnDisable()
    {
        m_LargerButton.performed -= SaveLargerResponse; m_LargerButton.Disable();
        m_SmallerButton.performed -= SaveSmallerResponse; m_SmallerButton.Disable();
        m_PauseRotationButton.performed -= PauseRotationToggle; m_PauseRotationButton.Disable();
    }
}