using UnityEngine;

[CreateAssetMenu(fileName = "AvatarConfigData", menuName = "Avatar/Config Data")]
public class AvatarConfigData : ScriptableObject
{
    public string bodyType;      // Male or Female
    public string skinShade;     // Light, Medium, Dark
    public string physique;      // Slim, Medium, Broad
    public string dyadID;
}
