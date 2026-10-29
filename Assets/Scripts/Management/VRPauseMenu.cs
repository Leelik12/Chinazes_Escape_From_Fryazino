using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using RacingProject.Network;

namespace RacingProject.Management
{
    // Пауза в VR: кнопка меню левого контроллера (или Esc) открывает надпись перед глазами, края обзора темнеют
    // (VRComfortRig). Сетевая игра не останавливается — напарник продолжает, а пулемёт на паузе не стреляет.
    // Короткое нажатие меню или курок — продолжить, удержание меню holdToExit секунд — выйти в меню
    public class VRPauseMenu : MonoBehaviour
    {
        [Tooltip("Камера шлема на этом риге: пауза работает, пока она включена")]
        [SerializeField] private Camera head;
        [SerializeField] private float distance = 1.2f;
        [SerializeField] private float holdToExit = 2f;

        public static bool IsOpen { get; private set; }

        private InputAction menuAction;
        private InputAction resumeAction;
        private TextMeshPro panel;
        private float pressStart = -1f;
        private bool exiting;

        private void Awake()
        {
            menuAction = new InputAction("VRPause", InputActionType.Button);
            menuAction.AddBinding("<XRController>{LeftHand}/menu");
            menuAction.AddBinding("<XRController>{LeftHand}/menuButton");
            menuAction.AddBinding("<Keyboard>/escape");
            resumeAction = new InputAction("VRResume", InputActionType.Button);
            resumeAction.AddBinding("<XRController>{RightHand}/triggerPressed");
        }

        private void OnEnable()
        {
            menuAction.Enable();
            resumeAction.Enable();
        }

        private void OnDisable()
        {
            menuAction.Disable();
            resumeAction.Disable();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            menuAction.Dispose();
            resumeAction.Dispose();
            if (IsOpen) IsOpen = false;
        }

        private void Update()
        {
            bool active = ViewModeService.IsVR && LocalPlayerRole.Current != PlayerRole.None
                && head != null && head.isActiveAndEnabled;
            if (!active)
            {
                if (panel != null && panel.gameObject.activeSelf) SetOpen(false);
                return;
            }

            if (!IsOpen)
            {
                if (menuAction.WasPressedThisFrame())
                {
                    SetOpen(true);
                    // Это нажатие открыло паузу и не должно сразу считаться удержанием на выход
                    pressStart = -1f;
                }
                return;
            }

            if (menuAction.WasPressedThisFrame())
                pressStart = Time.unscaledTime;
            if (pressStart >= 0f && menuAction.IsPressed() && Time.unscaledTime - pressStart >= holdToExit && !exiting)
            {
                exiting = true;
                SetOpen(false);
                LanLobby lobby = FindFirstObjectByType<LanLobby>();
                if (lobby != null) lobby.OnLeavePressed();
                return;
            }
            if (pressStart >= 0f && menuAction.WasReleasedThisFrame())
            {
                pressStart = -1f;
                SetOpen(false);
                return;
            }
            if (resumeAction.WasPressedThisFrame())
            {
                SetOpen(false);
                return;
            }
            UpdateText();
        }

        private void SetOpen(bool open)
        {
            if (IsOpen == open && (panel == null || panel.gameObject.activeSelf == open)) return;
            IsOpen = open;
            if (open && panel == null) CreatePanel();
            if (panel != null) panel.gameObject.SetActive(open);
            if (open) UpdateText();
        }

        private void UpdateText()
        {
            string exit = "Удерживайте кнопку меню " + holdToExit.ToString("0") + " с — выйти в меню";
            if (pressStart >= 0f)
            {
                float left = Mathf.Max(0f, holdToExit - (Time.unscaledTime - pressStart));
                exit = "<color=#ffcc55>Выход в меню через " + left.ToString("0.0") + " с</color>";
            }
            panel.text = "<size=140%><b>ПАУЗА</b></size>\n" +
                         (LocalPlayerRole.IsSolo ? "" : "<color=#9a9a9a>Сетевая игра не останавливается</color>\n") +
                         "Курок или кнопка меню — продолжить\n" + exit;
        }

        // Надпись перед камерой шлема, поверх всего (TMP overlay), чтобы её не закрывала кабина
        private void CreatePanel()
        {
            var go = new GameObject("VR Pause");
            go.transform.SetParent(head.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, distance);
            go.transform.localRotation = Quaternion.identity;
            panel = go.AddComponent<TextMeshPro>();
            panel.rectTransform.sizeDelta = new Vector2(1.4f, 0.6f);
            panel.fontSize = 0.7f;
            panel.alignment = TextAlignmentOptions.Center;
            panel.enableWordWrapping = true;
            panel.color = Color.white;
            panel.outlineWidth = 0.2f;
            panel.outlineColor = Color.black;
            panel.isOverlay = true;
        }
    }
}
