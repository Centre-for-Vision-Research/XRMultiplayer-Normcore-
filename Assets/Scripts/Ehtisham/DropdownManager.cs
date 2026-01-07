using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class DropdownManager : MonoBehaviour {
    public TMP_Dropdown dyadDropdown;
    public AvatarConfigData avatarConfig;

    private List<string> dyadOptions = new List<string>();

    private void Start() {
        PopulateDropdown();
        dyadDropdown.onValueChanged.AddListener(OnDropdownChanged);

        // Restore saved selection
        if (!string.IsNullOrEmpty(avatarConfig.dyadID)) {
            int savedIndex = dyadOptions.IndexOf(avatarConfig.dyadID);
            if (savedIndex >= 0)
                dyadDropdown.value = savedIndex;
        }
    }

    private void PopulateDropdown() {
        dyadOptions.Clear();
        for (int i = 1; i <= 100; i++) {
            dyadOptions.Add(i.ToString());
        }
        dyadDropdown.ClearOptions();
        dyadDropdown.AddOptions(dyadOptions);
    }

    private void OnDropdownChanged(int selectedIndex) {
        if (selectedIndex >= 0 && selectedIndex < dyadOptions.Count) {
            avatarConfig.dyadID = dyadOptions[selectedIndex];
        }
    }
}