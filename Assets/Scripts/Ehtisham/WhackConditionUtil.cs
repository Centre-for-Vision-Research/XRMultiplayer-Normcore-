using UnityEngine.SceneManagement;

public static class WhackConditionUtil
{
    public static string GetConditionFromScene()
    {
        string s = SceneManager.GetActiveScene().name.ToLower();

        // Required mapping:
        // HighFid_EXP2 -> HF
        // LowFid_EXP2 -> LF
        // HighFidVisual_EXP2 -> HFV
        // MRFid_Exp2 -> MRF

        if (s.Contains("highfidvisual")) return "HFV";
        if (s.Contains("highfid")) return "HF";
        if (s.Contains("lowfid")) return "LF";
        if (s.Contains("mrfid")) return "MRF";
        return "Unknown";
    }
}
