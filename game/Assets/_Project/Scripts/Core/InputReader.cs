using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SalvageRun.Core
{
    /// <summary>
    /// 입력 한 곳. Input System(신) / Input Manager(구) 어느 설정이든 동작하게 전처리기로 분기한다.
    ///
    /// 🔴 **키보드뿐이다** (2026-08-27 사장님 지시: *"마우스 조작은 없애고 키보드로만"*).
    ///    마우스 배관(커서 좌표·좌클릭·조작방식 저장)은 2026-09-02에 전부 걷어냈다 —
    ///    아무도 안 읽는데 남아 있으면 **"마우스도 되나"** 하고 오해를 부른다.
    ///    (메뉴 클릭은 여기가 아니라 `OnGUI`의 `Event.current`가 받는다. 그건 살아 있다)
    /// </summary>
    public static class InputReader
    {
        /// <summary>
        /// 키보드 이동 입력. WASD와 방향키를 함께 받는다.
        /// 대각선이 빨라지지 않도록 정규화한다.
        /// </summary>
        public static Vector2 MoveAxis
        {
            get
            {
                float x = 0f, y = 0f;

#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  y -= 1f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    y += 1f;
                    var v = new Vector2(x, y);
                    return v.sqrMagnitude > 1f ? v.normalized : v;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  y -= 1f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    y += 1f;
                var lv = new Vector2(x, y);
                return lv.sqrMagnitude > 1f ? lv.normalized : lv;
#else
                return Vector2.zero;
#endif
            }
        }

        /// <summary>대시. 마우스 조작이라 왼손은 Shift 하나만 쓰고, 우클릭도 같이 받는다.</summary>
        public static bool DashPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                var m = Mouse.current;
                if (kb != null || m != null)
                {
                    bool shift = kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame);
                    bool right = m != null && m.rightButton.wasPressedThisFrame;
                    return shift || right;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
                    || Input.GetMouseButtonDown(1);
#else
                return false;
#endif
            }
        }

        /// <summary>다음 층으로 내려간다. HUD 버튼으로도 누를 수 있다.</summary>
        public static bool DescendPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                var m = Mouse.current;
                if (kb != null || m != null)
                {
                    bool space = kb != null && kb.spaceKey.wasPressedThisFrame;
                    bool wheel = m != null && m.middleButton.wasPressedThisFrame;
                    return space || wheel;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(2);
#else
                return false;
#endif
            }
        }

        /// <summary>레벨업 카드를 숫자키로 고른다. 안 눌렀으면 0.</summary>
        public static int NumberPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) return 1;
                if (kb.digit2Key.wasPressedThisFrame) return 2;
                if (kb.digit3Key.wasPressedThisFrame) return 3;
                if (kb.digit4Key.wasPressedThisFrame) return 4;
                if (kb.digit5Key.wasPressedThisFrame) return 5;
                return 0;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 1; i <= 5; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i)) return i;
#endif
            return 0;
        }

        // ================================================================ 메뉴 조작
        //
        // 🔴 **이동이 키보드 전용이 된 뒤로 메뉴만 마우스인 것은 앞뒤가 안 맞는다**
        //    (2026-08-27 사장님: *"어색한 UI가 많다. UX도 구조적으로 챙겨 달라"*).
        //    손을 WASD에 올려 두고 버튼만 마우스로 누르게 하면 **손이 계속 오간다.**
        //    ⚠️ 마우스 클릭도 그대로 둔다 — 빼앗을 이유는 없다. **더하는** 것이다.

        /// <summary>메뉴에서 위로. W·↑</summary>
        public static bool MenuUpPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && (Keyboard.current.wKey.wasPressedThisFrame
                                      || Keyboard.current.upArrowKey.wasPressedThisFrame),
#endif
            KeyCode.W, KeyCode.UpArrow);

        /// <summary>메뉴에서 아래로. S·↓</summary>
        public static bool MenuDownPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && (Keyboard.current.sKey.wasPressedThisFrame
                                      || Keyboard.current.downArrowKey.wasPressedThisFrame),
#endif
            KeyCode.S, KeyCode.DownArrow);

        /// <summary>메뉴에서 왼쪽. A·←</summary>
        public static bool MenuLeftPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && (Keyboard.current.aKey.wasPressedThisFrame
                                      || Keyboard.current.leftArrowKey.wasPressedThisFrame),
#endif
            KeyCode.A, KeyCode.LeftArrow);

        /// <summary>메뉴에서 오른쪽. D·→</summary>
        public static bool MenuRightPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && (Keyboard.current.dKey.wasPressedThisFrame
                                      || Keyboard.current.rightArrowKey.wasPressedThisFrame),
#endif
            KeyCode.D, KeyCode.RightArrow);

        /// <summary>
        /// 메뉴에서 고른다. **Enter와 Space 둘 다** 받는다 —
        /// 판에서 Space가 "줍기"라 손이 이미 거기 있고, Enter는 메뉴의 표준이다.
        /// </summary>
        public static bool MenuConfirmPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame
                                      || Keyboard.current.numpadEnterKey.wasPressedThisFrame
                                      || Keyboard.current.spaceKey.wasPressedThisFrame),
#endif
            KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space);

        public static bool EscapePressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame,
#endif
            KeyCode.Escape);

        /// <summary>디버그: 크레딧 지급. ⚠️ F키는 브라우저가 가로챈다 — 글자 키를 쓴다</summary>
        public static bool CheatCreditsPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame,
#endif
            KeyCode.G);

        /// <summary>봇에게 조종을 넘긴다 — 시뮬이 왜 그런 값을 냈는지 눈으로 보라고</summary>
        public static bool ToggleBotPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame,
#endif
            KeyCode.B);

        /// <summary>
        /// 다음 지역으로 출발. ⚠️ 브라우저가 가로채지 않는 **글자 키**를 쓴다 (F-키 금지).
        /// </summary>
        public static bool TravelPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame,
#endif
            KeyCode.E);

        /// <summary>
        /// 조절 패널 열기/닫기. ⚠️ 브라우저가 안 가로채는 글자 키를 쓴다.
        /// </summary>
        public static bool ToggleTunePressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame,
#endif
            KeyCode.K);

        /// <summary>
        /// 🔴 **한 번 누르면 하나** (2026-08-26).
        ///
        ///    처음엔 홀드로 했는데, 홀드는 **매 프레임 하나씩** 물어서
        ///    한 곳에 여러 개가 있으면 **누르는 순간 전부 빨려 들어갔다** —
        ///    그러면 청소기와 다를 게 없고 "고른다"가 다시 사라진다.
        ///
        ///    ⚠️ 연타 걱정을 했었지만 아니다. 칸이 여섯이고 덩어리가 크므로
        ///       한 번 나갔다 오는 동안 **여섯 번쯤** 누른다. 그건 연타가 아니라 조작이다.
        /// </summary>
        public static bool CollectPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame,
#endif
            KeyCode.Space);

        /// <summary>정비소(영구 강화) 열기/닫기</summary>
        public static bool ToggleTechPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame,
#endif
            KeyCode.T);

        /// <summary>
        /// 화면 흔들림 켜기/끄기. 🔴 멀미는 취향이 아니라 접근성 문제다.
        ///
        /// ⚠️ **F 키를 쓰지 않는다.** 브라우저가 F3(찾기) 같은 걸 가로채서
        ///    WebGL에서는 눌리지 않는다 — 2026-08-22 웹 빌드에서 그대로 겪었다.
        ///    게임 키는 **브라우저가 안 쓰는 글자 키**로 잡는다.
        /// </summary>
        public static bool ToggleShakePressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame,
#endif
            KeyCode.K);

        /// <summary>디버그: 런 즉시 종료. ⚠️ F키는 브라우저가 가로챈다 — 글자 키를 쓴다</summary>
        public static bool ForceReturnPressed => KeyOnce(
#if ENABLE_INPUT_SYSTEM
            Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame,
#endif
            KeyCode.X);

        static bool KeyOnce(
#if ENABLE_INPUT_SYSTEM
            bool newSystemResult,
#endif
            params KeyCode[] legacyKeys)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) return newSystemResult;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < legacyKeys.Length; i++)
                if (Input.GetKeyDown(legacyKeys[i])) return true;
#endif
            return false;
        }
    }
}
