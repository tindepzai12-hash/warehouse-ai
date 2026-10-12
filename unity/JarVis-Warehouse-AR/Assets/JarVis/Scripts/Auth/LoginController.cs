using System.Collections;
using System.Collections.Generic;
using JarVis.Warehouse.App;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

namespace JarVis.Warehouse.Auth
{
    public sealed class LoginController : MonoBehaviour
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text subtitleLabel;
        [SerializeField] private Text employeeLabel;
        [SerializeField] private Text accessCodeLabel;
        [SerializeField] private Text roleLabel;
        [SerializeField] private Text languageLabel;
        [SerializeField] private InputField employeeInput;
        [SerializeField] private InputField accessCodeInput;
        [SerializeField] private Dropdown roleDropdown;
        [SerializeField] private Dropdown languageDropdown;
        [SerializeField] private Button[] roleButtons;
        [SerializeField] private Text[] roleButtonLabels;
        [SerializeField] private Image[] roleButtonBackgrounds;
        [SerializeField] private Button[] languageButtons;
        [SerializeField] private Text[] languageButtonLabels;
        [SerializeField] private Image[] languageButtonBackgrounds;
        [SerializeField] private Button loginButton;
        [SerializeField] private Text loginButtonLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private string nextScene = "WarehouseScene";

        private IAuthenticationService authenticationService;
        private bool usingOmsAuthentication;
        private bool isSubmitting;
        private UnityAction[] roleButtonActions;
        private UnityAction[] languageButtonActions;

        private void Awake()
        {
            usingOmsAuthentication = OmsMobileSession.TryLoadEndpoint(out string backendUrl);
            authenticationService = usingOmsAuthentication
                ? new OmsAuthenticationService(backendUrl)
                : new MockAuthenticationService();
            ConfigureInputs();
            MobileTextStyle.Apply(transform.Find("Safe Area"));
            AddListeners();
            RefreshLocalization();
            SetLoading(false);
            if (!usingOmsAuthentication && statusLabel != null)
                statusLabel.text = "DEMO OFFLINE — chưa cấu hình backend OMS";
        }

        private void OnDestroy()
        {
            RemoveListeners();
        }

        private void ConfigureInputs()
        {
            if (accessCodeInput != null)
                accessCodeInput.contentType = InputField.ContentType.Password;
            if (employeeInput != null)
                employeeInput.characterLimit = 32;
            if (accessCodeInput != null)
                accessCodeInput.characterLimit = 64;
        }

        private void AddListeners()
        {
            if (loginButton != null)
                loginButton.onClick.AddListener(BeginLogin);
            if (languageDropdown != null)
                languageDropdown.onValueChanged.AddListener(ChangeLanguage);

            roleButtonActions = AddIndexedListeners(roleButtons, SelectRole);
            languageButtonActions = AddIndexedListeners(languageButtons, SelectLanguage);
            JarvisLocalization.LanguageChanged += RefreshLocalization;
        }

        private void RemoveListeners()
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(BeginLogin);
            if (languageDropdown != null)
                languageDropdown.onValueChanged.RemoveListener(ChangeLanguage);
            RemoveIndexedListeners(roleButtons, roleButtonActions);
            RemoveIndexedListeners(languageButtons, languageButtonActions);
            JarvisLocalization.LanguageChanged -= RefreshLocalization;
        }

        private void ChangeLanguage(int index)
        {
            JarvisLocalization.SetLanguage(index == 0 ? SupportedLanguage.Vi : SupportedLanguage.En);
        }

        private void SelectRole(int index)
        {
            if (roleDropdown != null)
                roleDropdown.SetValueWithoutNotify(Mathf.Clamp(index, 0, 2));
            RefreshChoiceVisuals();
        }

        private void SelectLanguage(int index)
        {
            SupportedLanguage language = index == 0 ? SupportedLanguage.En : SupportedLanguage.Vi;
            if (languageDropdown != null)
                languageDropdown.SetValueWithoutNotify(language == SupportedLanguage.Vi ? 0 : 1);
            JarvisLocalization.SetLanguage(language);
            RefreshChoiceVisuals();
        }

        private void BeginLogin()
        {
            if (isSubmitting)
                return;

            string employeeId = employeeInput != null ? employeeInput.text : string.Empty;
            string accessCode = accessCodeInput != null ? accessCodeInput.text : string.Empty;
            WarehouseRole role = (WarehouseRole)Mathf.Clamp(
                roleDropdown != null ? roleDropdown.value : 0,
                0,
                2
            );
            StartCoroutine(Authenticate(new AuthenticationRequest(employeeId, accessCode, role)));
        }

        private IEnumerator Authenticate(AuthenticationRequest request)
        {
            isSubmitting = true;
            SetLoading(true);
            AuthenticationResult result = null;
            yield return authenticationService.Authenticate(request, value => result = value);

            if (result != null && result.Success)
            {
                if (accessCodeInput != null)
                    accessCodeInput.text = string.Empty;

                JarvisAppSession.SetAuthenticated(result.EmployeeCode ?? request.EmployeeId,
                    usingOmsAuthentication ? WarehouseRole.Associate : request.Role);
                if (!string.IsNullOrWhiteSpace(nextScene)
                    && Application.CanStreamedLevelBeLoaded(nextScene))
                {
                    SceneManager.LoadScene(nextScene);
                    yield break;
                }

                ShowError("network_error");
            }
            else
            {
                ShowError(result?.ErrorKey ?? "network_error");
            }

            isSubmitting = false;
            SetLoading(false);
        }

        private void SetLoading(bool loading)
        {
            if (loadingIndicator != null)
                loadingIndicator.SetActive(loading);
            if (loginButton != null)
                loginButton.interactable = !loading;
            if (employeeInput != null)
                employeeInput.interactable = !loading;
            if (accessCodeInput != null)
                accessCodeInput.interactable = !loading;
            if (roleDropdown != null)
                roleDropdown.interactable = !loading;
            if (languageDropdown != null)
                languageDropdown.interactable = !loading;
            SetButtonsInteractable(roleButtons, !loading);
            SetButtonsInteractable(languageButtons, !loading);

            if (loading && statusLabel != null)
                statusLabel.text = JarvisLocalization.Get("loading");
        }

        private void ShowError(string errorKey)
        {
            if (statusLabel != null)
                statusLabel.text = JarvisLocalization.Get(errorKey);
        }

        private void RefreshLocalization()
        {
            SetText(titleLabel, "login_title");
            SetText(subtitleLabel, "login_subtitle");
            SetText(employeeLabel, "employee_id");
            SetText(accessCodeLabel, "access_code");
            SetText(roleLabel, "role");
            SetText(languageLabel, "language");
            SetText(loginButtonLabel, "login");

            if (employeeInput != null && employeeInput.placeholder is Text employeePlaceholder)
                employeePlaceholder.text = JarvisLocalization.Get("employee_placeholder");
            if (accessCodeInput != null && accessCodeInput.placeholder is Text codePlaceholder)
                codePlaceholder.text = JarvisLocalization.Get("access_code_placeholder");

            RefreshRoleOptions();
            RefreshLanguageOptions();
            RefreshChoiceLabels();
            RefreshChoiceVisuals();
        }

        private void RefreshChoiceLabels()
        {
            string[] roleKeys = { "associate", "supervisor", "inventory" };
            if (roleButtonLabels != null)
                for (int i = 0; i < roleButtonLabels.Length && i < roleKeys.Length; i++)
                    SetText(roleButtonLabels[i], roleKeys[i]);

            if (languageButtonLabels != null && languageButtonLabels.Length > 0)
                languageButtonLabels[0].text = "English";
            if (languageButtonLabels != null && languageButtonLabels.Length > 1)
                languageButtonLabels[1].text = "Tiếng Việt";
        }

        private void RefreshChoiceVisuals()
        {
            int selectedRole = roleDropdown != null ? roleDropdown.value : 0;
            SetChoiceColors(roleButtonBackgrounds, selectedRole, true);

            int selectedLanguage = JarvisLocalization.CurrentLanguage == SupportedLanguage.En ? 0 : 1;
            SetChoiceColors(languageButtonBackgrounds, selectedLanguage, false);
        }

        private static void SetChoiceColors(Image[] backgrounds, int selectedIndex, bool filled)
        {
            if (backgrounds == null)
                return;

            Color teal = new Color32(8, 186, 208, 255);
            Color selected = filled ? new Color32(0, 151, 174, 255) : teal;
            Color idle = filled ? teal : new Color32(247, 250, 252, 255);
            for (int i = 0; i < backgrounds.Length; i++)
                if (backgrounds[i] != null)
                    backgrounds[i].color = i == selectedIndex ? selected : idle;
        }

        private static UnityAction[] AddIndexedListeners(Button[] buttons, System.Action<int> handler)
        {
            if (buttons == null)
                return null;

            UnityAction[] actions = new UnityAction[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                actions[i] = () => handler(index);
                if (buttons[i] != null)
                    buttons[i].onClick.AddListener(actions[i]);
            }
            return actions;
        }

        private static void RemoveIndexedListeners(Button[] buttons, UnityAction[] actions)
        {
            if (buttons == null || actions == null)
                return;

            int count = Mathf.Min(buttons.Length, actions.Length);
            for (int i = 0; i < count; i++)
                if (buttons[i] != null && actions[i] != null)
                    buttons[i].onClick.RemoveListener(actions[i]);
        }

        private static void SetButtonsInteractable(Button[] buttons, bool interactable)
        {
            if (buttons == null)
                return;
            foreach (Button button in buttons)
                if (button != null)
                    button.interactable = interactable;
        }

        private void RefreshRoleOptions()
        {
            if (roleDropdown == null)
                return;

            int selected = roleDropdown.value;
            roleDropdown.ClearOptions();
            roleDropdown.AddOptions(new List<string>
            {
                JarvisLocalization.Get("associate"),
                JarvisLocalization.Get("supervisor"),
                JarvisLocalization.Get("inventory")
            });
            roleDropdown.value = Mathf.Clamp(selected, 0, 2);
            roleDropdown.RefreshShownValue();
        }

        private void RefreshLanguageOptions()
        {
            if (languageDropdown == null)
                return;

            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new List<string>
            {
                JarvisLocalization.Get("vietnamese"),
                JarvisLocalization.Get("english")
            });
            languageDropdown.SetValueWithoutNotify(
                JarvisLocalization.CurrentLanguage == SupportedLanguage.Vi ? 0 : 1
            );
            languageDropdown.RefreshShownValue();
        }

        private static void SetText(Text label, string key)
        {
            if (label != null)
                label.text = JarvisLocalization.Get(key);
        }
    }
}
