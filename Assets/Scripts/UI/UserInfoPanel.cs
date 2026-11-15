using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

/// <summary>
/// Read-only panel that displays the saved user details (Name, Designation,
/// License Number, Gender) entered on the UserDetailsPanel. Opened from the
/// main menu via a button.
///
/// Reads the values from PlayerPrefs through UserDetailsPanel's static accessors,
/// so it always reflects whatever the player submitted.
///
/// SETUP:
///   1. Put this on the info panel GameObject.
///   2. Assign the four TextMeshPro labels for Name, Designation, License Number, Gender.
///      (_designationText carries over the old _ageText scene reference automatically —
///      only _licenseText needs a fresh label wired up.)
///   3. Wire the open button's onClick → Open(), and the close button → Close().
/// </summary>
public class UserInfoPanel : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The panel root toggled by Open/Close. Defaults to this GameObject.")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Value Labels")]
    [SerializeField] private TextMeshProUGUI _nameText;
    [FormerlySerializedAs("_ageText")]
    [SerializeField] private TextMeshProUGUI _designationText;
    [SerializeField] private TextMeshProUGUI _licenseText;
    [SerializeField] private TextMeshProUGUI _genderText;

    [Header("Label Prefixes (optional)")]
    [SerializeField] private string _namePrefix        = "Name: ";
    [FormerlySerializedAs("_agePrefix")]
    [SerializeField] private string _designationPrefix = "Designation: ";
    [SerializeField] private string _licensePrefix     = "License No: ";
    [SerializeField] private string _genderPrefix      = "Gender: ";

    private void Awake()
    {
        if (_panelRoot == null) _panelRoot = gameObject;
        _panelRoot.SetActive(false);
    }

    /// <summary>Populate the labels from saved data and show the panel.</summary>
    public void Open()
    {
        Refresh();
        _panelRoot.SetActive(true);
    }

    /// <summary>Hide the panel.</summary>
    public void Close()
    {
        _panelRoot.SetActive(false);
    }

    private void Refresh()
    {
        if (_nameText        != null) _nameText.SetText(_namePrefix + UserDetailsPanel.PlayerName);
        if (_designationText != null) _designationText.SetText(_designationPrefix + UserDetailsPanel.PlayerDesignation);
        if (_licenseText     != null) _licenseText.SetText(_licensePrefix + UserDetailsPanel.PlayerLicenseNumber);
        if (_genderText      != null) _genderText.SetText(_genderPrefix + UserDetailsPanel.PlayerGender);
    }
}
