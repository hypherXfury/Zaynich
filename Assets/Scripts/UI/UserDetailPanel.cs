using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// User details panel — Name, Designation, License Number, Gender.
/// Shown right after the splash screen, but only for first-time users.
/// Details are saved to PlayerPrefs so they survive scene reloads —
/// returning users skip this panel entirely (SplashScreen checks HasSavedData)
/// and go straight to the main menu.
///
/// Flow: Splash → UserDetailsPanel (first time only) → Main Menu → Cutscene → Mission Brief
///
/// HIERARCHY:
///   Canvas
///     └── UserDetails                     ← CanvasGroup + this script
///           ├── Input Box                 ← the form as a whole
///           │     ├── Box                 ← VerticalLayoutGroup, drives the rows
///           │     │     ├── Name          ← TMP_InputField
///           │     │     ├── Designation   ← TMP_Dropdown
///           │     │     ├── LicenseNum    ← TMP_InputField
///           │     │     └── Gender
///           │     │           ├── Male    ← Button
///           │     │           └── Female  ← Button
///           │     ├── Labels              ← "Doctor Name", "Doctor Designation", …
///           │     └── Icons
///           ├── ValidationText            ← TextMeshProUGUI
///           ├── SubmitButton              ← Button "Start"
///           └── BlackOverlay              ← Image (solid black fullscreen)
///
/// Labels and Icons are siblings of Box, positioned by hand to line up with the
/// rows the layout group produces — so anything that moves the form has to move
/// Input Box, not Box, or the rows slide out from under their own labels.
/// </summary>
public class UserDetailsPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup      _canvasGroup;
    [SerializeField] private TMP_InputField   _nameField;
    [SerializeField] private TMP_Dropdown     _designationDropdown;
    [SerializeField] private TMP_InputField   _licenseNumberField;

    [Header("Gender Buttons")]
    [SerializeField] private Button _maleButton;
    [SerializeField] private Button _femaleButton;
    [Tooltip("Swapped onto the Male button's Image depending on whether it's the selected gender.")]
    [SerializeField] private Sprite _maleSelectedSprite;
    [SerializeField] private Sprite _maleUnselectedSprite;
    [Tooltip("Swapped onto the Female button's Image depending on whether it's the selected gender.")]
    [SerializeField] private Sprite _femaleSelectedSprite;
    [SerializeField] private Sprite _femaleUnselectedSprite;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI  _validationText;
    [SerializeField] private Button           _submitButton;
    [SerializeField] private Image            _blackOverlay;

    [Header("Scene")]
    [SerializeField] private string         _gameSceneName  = "Level1";

    [Header("Hand-off")]
    [Tooltip("The main menu shown after the user submits their details.")]
    [SerializeField] private MainMenu _mainMenu;

    [Header("Timing")]
    [SerializeField] private float _fadeInDuration  = 0.35f;
    [SerializeField] private float _fadeOutDuration = 0.6f;

    [Header("Mobile Keyboard")]
    [Tooltip("Rect that slides up when the Android soft keyboard opens. Leave empty to use the " +
             "whole form group (Input Box) — that carries the labels and icons up with the fields, " +
             "while leaving the root's full-screen black overlay covering the screen.")]
    [SerializeField] private RectTransform _keyboardShiftTarget;

    // ── PlayerPrefs keys ──────────────────────────────────────────────────────

    private const string KEY_NAME        = "PlayerName";
    private const string KEY_DESIGNATION = "PlayerDesignation";
    private const string KEY_LICENSE     = "PlayerLicenseNumber";
    private const string KEY_GENDER      = "PlayerGender";
    private const string KEY_SAVED       = "UserDetailsSaved";

    // ── Static accessors ──────────────────────────────────────────────────────

    public static string PlayerName          => PlayerPrefs.GetString(KEY_NAME,        "");
    public static string PlayerDesignation   => PlayerPrefs.GetString(KEY_DESIGNATION, "");
    public static string PlayerLicenseNumber => PlayerPrefs.GetString(KEY_LICENSE,     "");
    public static string PlayerGender        => PlayerPrefs.GetString(KEY_GENDER,      "");
    public static bool   HasSavedData        => PlayerPrefs.HasKey(KEY_SAVED);

    // ── State ─────────────────────────────────────────────────────────────────

    private string _selectedGender = "";

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha          = 0f;
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
        }

        SetOverlayAlpha(0f);
        if (_validationText != null) _validationText.gameObject.SetActive(false);

        SetUpKeyboardHandling();

        gameObject.SetActive(false);

        if (_submitButton  != null) _submitButton.onClick.AddListener(OnSubmitClicked);
        if (_maleButton    != null) _maleButton.onClick.AddListener(() => SelectGender("Male"));
        if (_femaleButton  != null) _femaleButton.onClick.AddListener(() => SelectGender("Female"));
    }

    // ── Mobile keyboard ───────────────────────────────────────────────────────

    /// <summary>
    /// Adds and configures the KeyboardAwarePanel helper so this form behaves
    /// like a normal Android one: no floating native input bar above the
    /// keyboard, and the field being typed into slides clear of it.
    ///
    /// Done in code so the panel needs no extra wiring — an existing helper
    /// added by hand in the inspector is reused rather than duplicated.
    /// </summary>
    private void SetUpKeyboardHandling()
    {
        RectTransform target = _keyboardShiftTarget;
        if (target == null) target = FindFormGroup();

        KeyboardAwarePanel helper = GetComponent<KeyboardAwarePanel>();
        if (helper == null) helper = gameObject.AddComponent<KeyboardAwarePanel>();

        helper.Configure(target, new[] { _nameField, _licenseNumberField });
    }

    /// <summary>
    /// The whole form as one rect: walks up from the Name field to the outermost
    /// group still inside this panel — Name → Box → Input Box.
    ///
    /// Stopping at Box (the field rows' own parent) is what left the labels and
    /// icons behind when the keyboard pushed the form up, since those are Box's
    /// siblings. Stopping one short of this root is equally deliberate: the root
    /// also holds the full-screen black overlay, and lifting that would uncover
    /// the bottom of the screen during the fade-out.
    /// </summary>
    private RectTransform FindFormGroup()
    {
        if (_nameField == null) return transform as RectTransform;

        Transform group = _nameField.transform;
        while (group.parent != null && group.parent != transform) group = group.parent;

        return group as RectTransform ?? transform as RectTransform;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show()
    {
        gameObject.SetActive(true);

        // Pre-fill saved data if it exists
        if (HasSavedData)
        {
            if (_nameField          != null) _nameField.text = PlayerName;
            if (_licenseNumberField != null) _licenseNumberField.text = PlayerLicenseNumber;
            RestoreDesignationDropdown(PlayerDesignation);
            SelectGender(PlayerGender);
        }
        else
        {
            UpdateGenderButtonVisuals();   // neither selected yet
        }

        StartCoroutine(FadeIn());
    }

    // ── Designation dropdown helpers ─────────────────────────────────────────

    private string GetSelectedDesignation()
    {
        if (_designationDropdown == null || _designationDropdown.options.Count == 0) return "";
        return _designationDropdown.options[_designationDropdown.value].text;
    }

    private void RestoreDesignationDropdown(string designation)
    {
        if (_designationDropdown == null || string.IsNullOrEmpty(designation)) return;

        int index = _designationDropdown.options.FindIndex(o => o.text == designation);
        if (index >= 0) _designationDropdown.value = index;
    }

    // ── Gender button helpers ─────────────────────────────────────────────────

    private void SelectGender(string gender)
    {
        if (string.IsNullOrEmpty(gender)) return;
        _selectedGender = gender;
        UpdateGenderButtonVisuals();
    }

    private void UpdateGenderButtonVisuals()
    {
        SetButtonSprite(_maleButton,   _selectedGender == "Male"   ? _maleSelectedSprite   : _maleUnselectedSprite);
        SetButtonSprite(_femaleButton, _selectedGender == "Female" ? _femaleSelectedSprite : _femaleUnselectedSprite);
    }

    private static void SetButtonSprite(Button button, Sprite sprite)
    {
        if (button == null || button.image == null || sprite == null) return;
        button.image.sprite = sprite;
    }

    // ── Validation + Submit ───────────────────────────────────────────────────

    private void OnSubmitClicked()
    {
        if (!Validate()) return;

        // Save to PlayerPrefs
        PlayerPrefs.SetString(KEY_NAME,        _nameField.text.Trim());
        PlayerPrefs.SetString(KEY_DESIGNATION, GetSelectedDesignation());
        PlayerPrefs.SetString(KEY_LICENSE,     _licenseNumberField.text.Trim());
        PlayerPrefs.SetString(KEY_GENDER,      _selectedGender);
        PlayerPrefs.SetInt   (KEY_SAVED,       1);
        PlayerPrefs.Save();

        if (_submitButton != null) _submitButton.interactable = false;
        StartCoroutine(FadeOutThenMainMenu());
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(_nameField?.text))
        { ShowError("Please enter your name."); return false; }

        if (string.IsNullOrEmpty(GetSelectedDesignation()))
        { ShowError("Please select your designation."); return false; }

        if (string.IsNullOrWhiteSpace(_licenseNumberField?.text))
        { ShowError("Please enter your license number."); return false; }

        if (string.IsNullOrEmpty(_selectedGender))
        { ShowError("Please select your gender."); return false; }

        HideError();
        return true;
    }

    private void ShowError(string msg)
    {
        if (_validationText == null) return;
        _validationText.text = msg;
        _validationText.gameObject.SetActive(true);
    }

    private void HideError()
    {
        if (_validationText != null) _validationText.gameObject.SetActive(false);
    }

    // ── Fades ─────────────────────────────────────────────────────────────────

    private IEnumerator FadeIn()
    {
        if (_canvasGroup == null) yield break;
        float elapsed = 0f;
        while (elapsed < _fadeInDuration)
        {
            elapsed            += Time.deltaTime;
            _canvasGroup.alpha  = Mathf.Clamp01(elapsed / _fadeInDuration);
            yield return null;
        }
        _canvasGroup.alpha          = 1f;
        _canvasGroup.interactable   = true;
        _canvasGroup.blocksRaycasts = true;
    }

    private IEnumerator FadeOutThenMainMenu()
    {
        float elapsed = 0f;
        SetOverlayAlpha(0f);
        if (_blackOverlay != null) _blackOverlay.raycastTarget = true;

        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            SetOverlayAlpha(Mathf.Clamp01(elapsed / _fadeOutDuration));
            yield return null;
        }
        SetOverlayAlpha(1f);

        // Hand off to the main menu (it fades in from black itself)
        if (_mainMenu != null)
        {
            gameObject.SetActive(false);
            _mainMenu.Show();
        }
        else
        {
            // Fallback — no main menu assigned
            SceneManager.LoadScene(_gameSceneName);
        }
    }

    private void SetOverlayAlpha(float a)
    {
        if (_blackOverlay == null) return;
        Color c = _blackOverlay.color;
        c.a = a;
        _blackOverlay.color = c;
    }
}
