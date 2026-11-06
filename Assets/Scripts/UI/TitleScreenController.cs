using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class TitleScreenController : MonoBehaviour
    {
        [Header("Main Menu")]
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private Button startNewGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Options")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Button optionsCloseButton;
        [SerializeField] private Button spanishButton;
        [SerializeField] private Button englishButton;

        [Header("New Game Confirmation")]
        [SerializeField] private GameObject newGameConfirmation;
        [SerializeField] private Button confirmationYesButton;
        [SerializeField] private Button confirmationNoButton;

        [Header("Loading")]
        [SerializeField] private GameObject loadingOverlay;
        [SerializeField] private string gameplaySceneName = "Prototype";

        [Header("UI Input")]
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule uiInputModule;

        private enum ScreenState { MainMenu, Options, Confirmation, Loading }
        private ScreenState state;
        private InputActionAsset originalActions;
        private InputActionAsset titleActions;
        private InputAction cancelAction;
        private Coroutine localeSelection;
        private bool initialized;
        private bool usingGamepad;

        private SaveGameService saveService;
        private bool HasSavedGame => saveService != null && saveService.HasSave();

        private void Awake()
        {
            saveService = new SaveGameService();
            if (mainMenu == null || startNewGameButton == null || continueButton == null
                || optionsButton == null || quitButton == null || optionsPanel == null
                || optionsCloseButton == null || spanishButton == null || englishButton == null
                || newGameConfirmation == null || confirmationYesButton == null
                || confirmationNoButton == null || loadingOverlay == null || eventSystem == null
                || uiInputModule == null || uiInputModule.actionsAsset == null
                || uiInputModule.cancel == null || uiInputModule.cancel.action == null
                || uiInputModule.move == null || uiInputModule.move.action == null
                || uiInputModule.submit == null || uiInputModule.submit.action == null
                || uiInputModule.point == null || uiInputModule.point.action == null
                || uiInputModule.leftClick == null || uiInputModule.leftClick.action == null)
            {
                Debug.LogError("Assign the title panels, buttons, EventSystem and configured UI input module.", this);
                enabled = false;
                return;
            }

            if (transform.IsChildOf(mainMenu.transform) || transform.IsChildOf(optionsPanel.transform)
                || transform.IsChildOf(newGameConfirmation.transform) || transform.IsChildOf(loadingOverlay.transform))
            {
                Debug.LogError("Keep TitleScreenController outside the panels it activates and deactivates.", this);
                enabled = false;
                return;
            }

            // Like Pause, clone UI actions so the shared InputActions asset stays untouched.
            originalActions = uiInputModule.actionsAsset;
            titleActions = Instantiate(originalActions);
            titleActions.Disable();
            InputAction navigation = uiInputModule.move != null && uiInputModule.move.action != null
                ? titleActions.FindAction(uiInputModule.move.action.id) : null;
            if (navigation != null)
            {
                for (int i = 0; i < navigation.bindings.Count; i++)
                {
                    if (navigation.bindings[i].path.Contains("rightStick"))
                        navigation.ApplyBindingOverride(i, string.Empty);
                }
            }
            cancelAction = titleActions.FindAction(uiInputModule.cancel.action.id);
            bool moduleWasEnabled = uiInputModule.enabled;
            uiInputModule.enabled = false;
            uiInputModule.actionsAsset = titleActions;
            uiInputModule.enabled = moduleWasEnabled;

            startNewGameButton.onClick.AddListener(RequestNewGame);
            continueButton.onClick.AddListener(BeginContinue);
            optionsButton.onClick.AddListener(OpenOptions);
            quitButton.onClick.AddListener(QuitGame);
            optionsCloseButton.onClick.AddListener(CloseOptions);
            spanishButton.onClick.AddListener(SetSpanish);
            englishButton.onClick.AddListener(SetEnglish);
            confirmationYesButton.onClick.AddListener(ConfirmNewGame);
            confirmationNoButton.onClick.AddListener(CancelNewGame);

            initialized = true;
            continueButton.gameObject.SetActive(HasSavedGame);
            SetScreen(ScreenState.MainMenu, null);
        }

        private void OnEnable()
        {
            if (!initialized)
                return;
            InputSystem.onActionChange += OnActionChange;
            if (cancelAction != null)
                cancelAction.performed += OnCancel;
            ApplyCursor();
        }

        private void Start()
        {
            if (initialized)
                Select(startNewGameButton);
        }

        public void RequestNewGame()
        {
            if (!CanUse(ScreenState.MainMenu))
                return;
            if (HasSavedGame)
                SetScreen(ScreenState.Confirmation, confirmationNoButton);
            else
                BeginNewGame();
        }

        public void ConfirmNewGame()
        {
            if (!CanUse(ScreenState.Confirmation))
                return;
            BeginLoading(GameLaunchMode.NewGame);
        }

        public void CancelNewGame()
        {
            if (CanUse(ScreenState.Confirmation))
                SetScreen(ScreenState.MainMenu, startNewGameButton);
        }

        public void BeginNewGame()
        {
            if (!initialized || !isActiveAndEnabled
                || (state != ScreenState.MainMenu && state != ScreenState.Confirmation))
                return;
            if (state == ScreenState.MainMenu && HasSavedGame)
                SetScreen(ScreenState.Confirmation, confirmationNoButton);
            else
                BeginLoading(GameLaunchMode.NewGame);
        }

        public void BeginContinue()
        {
            if (!CanUse(ScreenState.MainMenu) || !HasSavedGame)
                return;
            BeginLoading(GameLaunchMode.Continue);
        }

        private void BeginLoading(GameLaunchMode mode)
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName)
                || !Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            {
                Debug.LogError($"Add scene '{gameplaySceneName}' to the active build scene list before starting a game.", this);
                return;
            }

            if (mode == GameLaunchMode.NewGame)
            {
                if (!saveService.DeleteSave())
                    return;
                continueButton.gameObject.SetActive(false);
            }
            else if (saveService.Load() == null)
            {
                // Remain on TitleScreen without overwriting a corrupt or unsupported save.
                return;
            }
            GameLaunchContext.Request(mode);
            SetScreen(ScreenState.Loading, null);
            StartCoroutine(LoadGameplay());
        }

        private IEnumerator LoadGameplay()
        {
            // Give the overlay a rendered frame before scene loading starts.
            yield return null;
            AsyncOperation loading = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);
            if (loading == null)
            {
                GameLaunchContext.Request(GameLaunchMode.NewGame);
                Debug.LogError("The gameplay scene could not be loaded.", this);
                SetScreen(ScreenState.MainMenu, startNewGameButton);
                yield break;
            }
            yield return loading;
        }

        public void OpenOptions()
        {
            if (!CanUse(ScreenState.MainMenu))
                return;
            Locale locale = LocalizationSettings.SelectedLocale;
            bool spanish = locale != null && locale.Identifier.Code == "es-ES";
            SetScreen(ScreenState.Options, spanish ? spanishButton : englishButton);
        }

        public void CloseOptions()
        {
            if (CanUse(ScreenState.Options))
                SetScreen(ScreenState.MainMenu, optionsButton);
        }

        public void SetSpanish() => RequestLocale("es-ES");
        public void SetEnglish() => RequestLocale("en");

        private void RequestLocale(string code)
        {
            if (!CanUse(ScreenState.Options))
                return;
            if (localeSelection != null)
                StopCoroutine(localeSelection);
            localeSelection = StartCoroutine(SelectLocale(code));
        }

        private IEnumerator SelectLocale(string code)
        {
            var initialization = LocalizationSettings.InitializationOperation;
            if (!initialization.IsDone)
                yield return initialization;
            if (initialization.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Localization initialization failed; cannot change locale.", this);
                yield break;
            }
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if (locale == null)
            {
                Debug.LogError($"The locale '{code}' is not available in Localization Settings.", this);
                yield break;
            }
            LocalizationSettings.SelectedLocale = locale;
        }

        public void QuitGame()
        {
            if (!CanUse(ScreenState.MainMenu))
                return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private bool CanUse(ScreenState expected)
        {
            return initialized && isActiveAndEnabled && state == expected;
        }

        private void SetScreen(ScreenState next, Button selection)
        {
            state = next;
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(null);
            mainMenu.SetActive(next == ScreenState.MainMenu || next == ScreenState.Confirmation);
            optionsPanel.SetActive(next == ScreenState.Options);
            newGameConfirmation.SetActive(next == ScreenState.Confirmation);
            loadingOverlay.SetActive(next == ScreenState.Loading);
            bool mainInteractive = next == ScreenState.MainMenu;
            startNewGameButton.interactable = mainInteractive;
            continueButton.interactable = mainInteractive && HasSavedGame;
            optionsButton.interactable = mainInteractive;
            quitButton.interactable = mainInteractive;
            ApplyCursor();
            Select(selection);
        }

        private void Select(Button button)
        {
            if (eventSystem != null && button != null && button.IsActive() && button.IsInteractable())
                eventSystem.SetSelectedGameObject(button.gameObject);
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (state == ScreenState.Options)
                CloseOptions();
            else if (state == ScreenState.Confirmation)
                CancelNewGame();
        }

        private void OnActionChange(object source, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed || !(source is InputAction action)
                || action.actionMap == null || action.actionMap.asset != titleActions
                || action.activeControl == null || !Application.isFocused)
                return;

            InputDevice device = action.activeControl.device;
            if (device is Gamepad && action.activeControl.EvaluateMagnitude() > 0.2f)
            {
                usingGamepad = true;
                if (state != ScreenState.Loading && eventSystem != null
                    && eventSystem.currentSelectedGameObject == null)
                    SelectDefaultForScreen();
            }
            else if (device is Mouse mouse)
            {
                bool moved = mouse.delta.ReadValue().sqrMagnitude > 0.01f;
                if (!moved && action == uiInputModule.point.action)
                    return;
                usingGamepad = false;
                // Let hover control Highlighted rather than leaving an old Selected tint behind.
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(null);
            }
            else if (device is Keyboard)
            {
                usingGamepad = false;
                if (eventSystem != null && eventSystem.currentSelectedGameObject == null)
                    SelectDefaultForScreen();
            }
            ApplyCursor();
        }

        private void SelectDefaultForScreen()
        {
            if (state == ScreenState.MainMenu)
                Select(startNewGameButton);
            else if (state == ScreenState.Options)
                Select(LocalizationSettings.SelectedLocale != null
                    && LocalizationSettings.SelectedLocale.Identifier.Code == "es-ES" ? spanishButton : englishButton);
            else if (state == ScreenState.Confirmation)
                Select(confirmationNoButton);
        }

        private void ApplyCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = !usingGamepad;
            if (optionsCloseButton != null)
                optionsCloseButton.gameObject.SetActive(!usingGamepad);
        }

        private void OnDisable()
        {
            InputSystem.onActionChange -= OnActionChange;
            if (cancelAction != null)
                cancelAction.performed -= OnCancel;
            StopAllCoroutines();
            localeSelection = null;
        }

        private void OnDestroy()
        {
            if (initialized)
            {
                if (startNewGameButton != null) startNewGameButton.onClick.RemoveListener(RequestNewGame);
                if (continueButton != null) continueButton.onClick.RemoveListener(BeginContinue);
                if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
                if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
                if (optionsCloseButton != null) optionsCloseButton.onClick.RemoveListener(CloseOptions);
                if (spanishButton != null) spanishButton.onClick.RemoveListener(SetSpanish);
                if (englishButton != null) englishButton.onClick.RemoveListener(SetEnglish);
                if (confirmationYesButton != null) confirmationYesButton.onClick.RemoveListener(ConfirmNewGame);
                if (confirmationNoButton != null) confirmationNoButton.onClick.RemoveListener(CancelNewGame);
            }
            if (uiInputModule != null && uiInputModule.actionsAsset == titleActions)
                uiInputModule.actionsAsset = originalActions;
            if (titleActions != null)
                Destroy(titleActions);
        }
    }
}
