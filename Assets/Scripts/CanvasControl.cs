using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// CanvasControl, used to control the behaviour of a specific canvas it's attached to
/// </summary>
public class CanvasControl : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI m_Range;
    [SerializeField] private TextMeshProUGUI m_pID;
    [Header("UI INTERACTABILITY")]
    [SerializeField] private CallibrateRoom _room;
    [SerializeField] private CallibrateRoom.Mode defaultRoomMode;
    [SerializeField] private CallibrateRoom.Mode pointerRoomMode;

    private float _range = 0;

    private void Start()
    {
        UpdateRange();
    }

    /// <summary>
    /// Action to complete when pointing to the Canvas
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        //set to standby mode so we can't "Calibrate" the room when interacting with Canvas interface
        _room.SetMode(pointerRoomMode);
    }

    /// <summary>
    /// Action to complete on pointer exit with the canvas
    /// </summary>
    /// <param name="eventData"></param>
    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        //set to default mode
        _room.SetMode(defaultRoomMode);
    }

    /// <summary>
    /// Increase range value
    /// </summary>
    public void OnIncreaseRange()
    {
        _range += 0.5f;
        UpdateRange();
    }

    /// <summary>
    /// Decrease range value
    /// </summary>
    public void OnDecreaseRange()
    {
        _range = (_range == 0) ? 0 : (_range - 0.5f);
        UpdateRange();
    }

    /// <summary>
    /// Add text to the end of a string on the interface <br/>
    /// NOTE: Used for typing out the participant ID in the Shape Judgement Experiment
    /// </summary>
    /// <param name="v"></param>
    public void OnConcatText(string v)
    {
        string val = m_pID.text;
        if(val.Length < 3) val += v;

        m_pID.text = val;
    }

    /// <summary>
    /// Remove from the end of a string on the interface <br/>
    /// NOTE: Used for typing out the participant ID in the Shape Judgement Experiment
    /// </summary>
    public void OnDeconcatText()
    {
        string val = m_pID.text;
        if(val.Length == 1 || val.Length == 0)
        {
            m_pID.text = "";
        }
        else
        {
            m_pID.text = val.Substring(0, val.Length - 1);
        }

    }

    /// <summary>
    /// Change "Range" text on the interface
    /// </summary>
    private void UpdateRange()
    {
        m_Range.text = _range.ToString();
    }
}
