using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AvatarSync: Middleman used to sync the AvatarSyncModel information using Normcore
/// </summary>
[RequireComponent(typeof(RealtimeAvatarSyncImpl))]
public class AvatarSync : RealtimeComponent<AvatarSyncModel>
{
    [SerializeField]
    private RealtimeAvatarSyncImpl m_AvatarSyncImpl;

    // Start is called before the first frame update
    void Start()
    {
        if (m_AvatarSyncImpl == null)
            m_AvatarSyncImpl = GetComponent<RealtimeAvatarSyncImpl>();
    }

    protected override void OnRealtimeModelReplaced(AvatarSyncModel previousModel, AvatarSyncModel currentModel)
    {
        if (previousModel != null)
        {
            previousModel.avatarDataDidChange -= AvatarDataChanged;
        }

        if(currentModel != null)
        {
            if(model.isFreshModel)
                SetAvatarData("0|");//show nothing

            //then get what to show from Normcore
            UpdateAvatarData();

            currentModel.avatarDataDidChange += AvatarDataChanged;
        }
    }

    private void AvatarDataChanged(AvatarSyncModel model, string value)
    {
        UpdateAvatarData();
    }

    private void UpdateAvatarData()
    {
        if (model == null)
            return;

        if (model.avatarData == "")
            return;

        m_AvatarSyncImpl.UpdateFromNormcore(model.avatarData);
    }

    public void SetAvatarData(string value)
    {
        model.avatarData = value;
    }
}
