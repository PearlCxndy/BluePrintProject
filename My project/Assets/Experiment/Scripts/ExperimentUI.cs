using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class ExperimentUI : MonoBehaviour
{
    public Canvas Canvas { get; private set; }
    public Font Font { get; private set; }

    readonly System.Collections.Generic.List<GameObject> _pauseHidden = new System.Collections.Generic.List<GameObject>();
    GameObject _pause, _pausedScreen;
    Button _resume;
    RectTransform _root;
    GameObject _screen;
    Sprite _circleSprite;
    Texture2D[] _faceTextures;
    Texture2D[] _valenceSamFigures;
    Texture2D[] _arousalSamFigures;
    GameObject _background;
    Sprite _roundedSprite;
    Button[] _responseButtons;
    bool _committing;
    float _screenOpenedAt;
    string _progress = "TECH PORTO  /  EXPERIENCE STUDY";

    static readonly Color Bg = Hex("2B2B2B");
    static readonly Color Panel = Hex("252729");
    static readonly Color HeaderBar = Hex("303335");
    static readonly Color Text = Hex("FFFFFF");
    static readonly Color Muted = Hex("C8C8C8");
    static readonly Color Button = Hex("363A3D");
    static readonly Color Accent = Hex("E8B84A");
    static readonly Color NatureBg = Hex("163026");
    static readonly Color CityBg = Hex("262830");
    static readonly Color RedCross = Hex("E31C23");
    static readonly Color FaceIdle = Hex("B8B8B8");

    public static readonly string[] BeautyOptions =
    {
        "Not at all", "Slightly", "Somewhat", "Neutral", "Moderately", "Very", "Extremely"
    };

    public static readonly string[] LikingOptions =
    {
        "Not at all", "Slightly", "Somewhat", "Neutral", "Moderately", "Very much", "Extremely"
    };

    public static readonly string[] WantingOptions =
    {
        "Not at all", "Slightly", "Somewhat", "Neutral", "Moderately", "Very much", "Extremely"
    };

    public void Build()
    {
        Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _circleSprite = Sprite.Create(MakeCircleTexture(64), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        _roundedSprite = MakeRoundedSprite();
        _faceTextures = new[]
        {
            MakeFaceTexture(0),
            MakeFaceTexture(1),
            MakeFaceTexture(2),
            MakeFaceTexture(3),
            MakeFaceTexture(4)
        };
        // Individually cropped, transparent versions of the validated drawings
        // supplied in the project brief.  Their white line art is legible on the
        // application's dark background.
        _valenceSamFigures = LoadSamFigures("Valence");
        _arousalSamFigures = LoadSamFigures("Arousal");

        EnsureEventSystem();
        Canvas = CreateCanvas();
        _root = Canvas.transform as RectTransform;
        _background = CreateFullRect("Background", _root, Bg);
    }

    public void ClearScreen()
    {
        if (_screen != null)
        {
            _screen.SetActive(false);
            Destroy(_screen);
        }

        _background.SetActive(true);
        _responseButtons = null;
        _committing = false;
        _screenOpenedAt = Time.unscaledTime;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        _screen = new GameObject("Screen", typeof(RectTransform));
        var rt = _screen.GetComponent<RectTransform>();
        rt.SetParent(_root, false);
        rt.gameObject.layer = 5;
        Stretch(rt);
        if (ExperimentVR.Instance != null) ExperimentVR.Instance.ScreenChanged();
        if (_pause != null) { _screen.SetActive(false); _pause.transform.SetAsLastSibling(); }
    }

    public void ShowSetup(
        string defaultId,
        bool testMode,
        ConditionOrder order,
        Action<string, bool, ConditionOrder> onStart,
        Action<ConditionType> onPreview = null)
    {
        ClearScreen();
        var page = CreateSurveyPage("Experiment setup");
        CreateText(page, "For the experimenter. Participants will see the welcome screen next.", 18, FontStyle.Italic, Muted, new Vector2(0, 210), 900, 40);

        CreateText(page, "Participant ID", 18, FontStyle.Bold, Muted, new Vector2(0, 150), 560, 28);
        var idInput = CreateInput(page, defaultId, new Vector2(0, 102), 560, 48);
        if (ExperimentVR.Requested)
        {
            idInput.readOnly = true;
            idInput.interactable = false;
            CreateButton(page, "Edit ID", new Vector2(410, 102), 180, 54, () => ShowIDKeyboard(idInput));
        }

        var testToggle = CreateToggle(page, "Test mode (short timers)", testMode, new Vector2(0, 38));
        CreateText(page, "Condition order", 18, FontStyle.Bold, Muted, new Vector2(0, -26), 560, 28);

        var selected = order;
        var orderLabel = CreateText(page, OrderLabel(selected), 20, FontStyle.Normal, Text, new Vector2(0, -70), 560, 32);

        CreateButton(page, "Random", new Vector2(-190, -130), 170, 48, () =>
        {
            selected = ConditionOrder.Random;
            orderLabel.text = OrderLabel(selected);
        });
        CreateButton(page, "Nature first", new Vector2(0, -130), 170, 48, () =>
        {
            selected = ConditionOrder.NatureThenCity;
            orderLabel.text = OrderLabel(selected);
        });
        CreateButton(page, "City first", new Vector2(190, -130), 170, 48, () =>
        {
            selected = ConditionOrder.CityThenNature;
            orderLabel.text = OrderLabel(selected);
        });

        if (onPreview != null)
        {
            CreateText(page, "Preview environments · no participant data recorded", 17, FontStyle.Normal, Muted, new Vector2(0, -208), 900, 32);
            CreateButton(page, "Preview Nature", new Vector2(-150, -266), 270, 54, () => onPreview(ConditionType.Nature));
            CreateButton(page, "Preview Urban", new Vector2(150, -266), 270, 54, () => onPreview(ConditionType.City));
        }
        CreateText(page, ExperimentVR.Requested ? "Point + trigger, or thumbstick + A / X to choose" : "Researcher shortcut: Right Shift + N skips a timer", 15, FontStyle.Normal, Muted, new Vector2(-210, -350), 650, 30);
        CreateNavButton(page, "Start session", () =>
        {
            var id = string.IsNullOrWhiteSpace(idInput.text) ? defaultId : idInput.text.Trim();
            onStart(id, testToggle.isOn, selected);
        });
    }

    public void ShowWelcome(string heading, string body, Action onContinue, string buttonLabel = "Next")
    {
        ClearScreen();
        var page = CreateSurveyPage(heading);
        CreateText(page, body, 22, FontStyle.Normal, Text, new Vector2(0, 20), 900, 280);
        CreateNavButton(page, buttonLabel, onContinue);
    }

    public void ShowCountdown(string heading, string instruction, string number)
    {
        ClearScreen();
        CreateText(_screen.transform, heading, 30, FontStyle.Bold, Text, new Vector2(0, 220), 1100, 50);
        CreateText(_screen.transform, instruction, 22, FontStyle.Normal, Muted, new Vector2(0, 140), 1100, 52);
        CreateText(_screen.transform, number, 100, FontStyle.Bold, number == "Begin" ? Accent : Text, Vector2.zero, 600, 130);
    }

    public void ShowSixPointRating(string heading, string question, string low, string high, Action<int> onComplete, string scaleKind = null)
    {
        ClearScreen();
        var page = CreateSurveyPage(heading);
        CreateText(page, question, 32, FontStyle.Bold, Text, new Vector2(0, 220), 1220, 70);
        CreateText(page, "Choose the number that best describes your experience.", 20, FontStyle.Normal, Muted, new Vector2(0, 154), 1160, 36);
        CreateText(page, low, 22, FontStyle.Normal, Text, new Vector2(-490, 96), 220, 34).alignment = TextAnchor.MiddleLeft;
        CreateText(page, high, 22, FontStyle.Normal, Text, new Vector2(490, 96), 220, 34).alignment = TextAnchor.MiddleRight;

        var isValence = (scaleKind ?? heading).StartsWith("Valence", StringComparison.OrdinalIgnoreCase);
        var isArousal = (scaleKind ?? heading).StartsWith("Arousal", StringComparison.OrdinalIgnoreCase);
        var figures = isValence ? _valenceSamFigures : _arousalSamFigures;
        if ((isValence || isArousal) && figures != null && Array.TrueForAll(figures, f => f != null))
            CreateSamSixPointScale(page, figures, onComplete);
        else
            CreateDirectSixPointScale(page, onComplete);
        CreateText(page, ExperimentVR.Requested ? "Point + trigger, or thumbstick + A / X to choose" : "Click a number to continue  ·  Keys 1–6 also work", 18, FontStyle.Normal, Muted, new Vector2(0, -302), 1120, 40);
    }

    public void SetProgress(string value) { _progress = value; }

    public void ShowLoading(string heading, string body)
    {
        ClearScreen();
        var page = CreateSurveyPage(heading);
        CreateText(page, body, 24, FontStyle.Normal, Muted, Vector2.zero, 1100, 140);
    }

    public void ShowEnvironmentOverlay(string instruction, string detail = "", string countdown = "", Action onBack = null)
    {
        ClearScreen();
        _background.SetActive(false);
        if (!string.IsNullOrEmpty(instruction))
        {
            var panel = CreatePanel("Instruction", _screen.transform, new Color(.10f, .12f, .13f, .93f), new Vector2(1240, 128), new Vector2(0, 342));
            CreateText(panel, instruction, 25, FontStyle.Bold, Text, new Vector2(0, 22), 1150, 62);
            CreateText(panel, detail, 18, FontStyle.Normal, Muted, new Vector2(0, -32), 1150, 35);
        }
        else if (!string.IsNullOrEmpty(detail))
            CreateText(_screen.transform, detail, 18, FontStyle.Normal, Color.white, new Vector2(0, -410), 1240, 36);
        if (!string.IsNullOrEmpty(countdown))
        {
            var badge = CreatePanel("Countdown", _screen.transform, new Color(.10f, .12f, .13f, .9f), new Vector2(180, 150), Vector2.zero);
            CreateText(badge, countdown, 72, FontStyle.Bold, Text, Vector2.zero, 170, 140);
        }
        if (onBack != null) CreateButton(_screen.transform, "Back to setup", new Vector2(0, -372), 280, 60, onBack);
    }

    void Update()
    {
        if (ExperimentVR.Instance != null && ExperimentVR.Instance.Suspended) return;
        if (_responseButtons == null || _committing || Time.unscaledTime - _screenOpenedAt < .2f) return;
        for (var i = 0; i < _responseButtons.Length; i++)
            if (ExperimentInput.Pressed((Key)((int)Key.Digit1 + i)) || ExperimentInput.Pressed((Key)((int)Key.Numpad1 + i)))
                _responseButtons[i].onClick.Invoke();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null &&
            (ExperimentInput.Pressed(Key.Tab) || ExperimentInput.Pressed(Key.RightArrow) || ExperimentInput.Pressed(Key.LeftArrow)))
            _responseButtons[0].Select();
    }

    public Text ShowTimedTask(string heading, string instruction, Color? background = null)
    {
        ClearScreen();
        if (background.HasValue)
            CreateFullRect("Tint", _screen.transform, background.Value);

        if (!string.IsNullOrEmpty(heading))
            CreateText(_screen.transform, heading, 28, FontStyle.Bold, Text, new Vector2(0, 360), 900, 40);
        if (!string.IsNullOrEmpty(instruction))
            CreateText(_screen.transform, instruction, 20, FontStyle.Normal, Muted, new Vector2(0, 310), 900, 40);

        return CreateText(_screen.transform, "", 22, FontStyle.Normal, Muted, new Vector2(0, -360), 400, 36);
    }

    public Text ShowRedCrossBaseline()
    {
        var timer = ShowTimedTask("", "");
        CreateCross(_screen.transform, 96, 14);
        return timer;
    }

    public Text ShowEyesClosedBaseline()
    {
        var timer = ShowTimedTask("", "");
        var go = new GameObject("Closed eyes cue", typeof(RectTransform), typeof(ClosedEyesGraphic));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(_screen.transform, false);
        go.layer = 5;
        rect.sizeDelta = new Vector2(800, 260);
        rect.anchoredPosition = new Vector2(0, 10);
        var eyes = go.GetComponent<ClosedEyesGraphic>();
        eyes.color = Color.white;
        eyes.raycastTarget = false;
        return timer;
    }

    public Text ShowCondition(ConditionType condition, string instruction, Texture2D[] slides)
    {
        var tint = condition == ConditionType.Nature ? NatureBg : CityBg;
        var conditionName = condition == ConditionType.Nature ? "Nature condition" : "Urban condition";
        var timer = ShowTimedTask("", instruction, tint);

        var imageGo = new GameObject("Slide", typeof(RectTransform), typeof(RawImage));
        var imageRt = imageGo.GetComponent<RectTransform>();
        imageRt.SetParent(_screen.transform, false);
        imageRt.gameObject.layer = 5;
        Stretch(imageRt);
        imageGo.transform.SetSiblingIndex(0);
        var image = imageGo.GetComponent<RawImage>();
        image.color = Color.white;
        image.texture = slides != null && slides.Length > 0 ? slides[0] : null;

        if (slides == null || slides.Length == 0)
        {
            image.color = tint;
            CreateText(_screen.transform, conditionName, 48, FontStyle.Bold, Text, new Vector2(0, 20), 800, 70);
            CreateText(
                _screen.transform,
                "Add images to Assets/Experiment/Resources/Experiment/" + condition,
                18,
                FontStyle.Italic,
                Muted,
                new Vector2(0, -40),
                900,
                40);
        }

        var holder = _screen.AddComponent<SlideHolder>();
        holder.Image = image;
        holder.Slides = slides;
        return timer;
    }

    public void SetSlide(int index)
    {
        var holder = _screen != null ? _screen.GetComponent<SlideHolder>() : null;
        if (holder == null || holder.Slides == null || holder.Slides.Length == 0)
            return;

        holder.Image.texture = holder.Slides[Mathf.Abs(index) % holder.Slides.Length];
        holder.Image.color = Color.white;
    }

    public void ShowSam(string heading, SamRatings current, Action<SamRatings> onComplete)
    {
        ClearScreen();
        var page = CreateSurveyPage("SAM");
        CreateText(page, heading, 26, FontStyle.Bold, Text, new Vector2(0, 250), 1000, 40);

        var valence = CreateFaceScale(
            page,
            "How pleasant do you feel?",
            new[] { "Very Unhappy", "Unhappy", "Neutral", "Happy", "Very Happy" },
            current.valence,
            new Vector2(0, 110));

        var arousal = CreateRadioScale(
            page,
            "How calm or excited do you feel?",
            new[] { "Very calm", "Calm", "Somewhat\ncalm", "Neutral", "Somewhat\nexcited", "Excited", "Very\nexcited" },
            current.arousal,
            new Vector2(0, -70));

        var dominance = CreateRadioScale(
            page,
            "How in control do you feel?",
            new[] { "Very\ncontrolled", "Controlled", "Somewhat\ncontrolled", "Neutral", "Somewhat\nin control", "In control", "Very\nin control" },
            current.dominance,
            new Vector2(0, -250));

        var next = CreateNavButton(page, "Next", () =>
        {
            current.valence = valence();
            current.arousal = arousal();
            current.dominance = dominance();
            current.timestampIso = ExperimentSession.NowIso();
            onComplete(current);
        });
        next.interactable = valence() >= 1 && arousal() >= 1 && dominance() >= 1;

        BindScaleRefresh(page, () =>
        {
            next.interactable = valence() >= 1 && arousal() >= 1 && dominance() >= 1;
        });
    }

    public void ShowLikert(string heading, string question, string[] options, int current, Action<int> onComplete)
    {
        ClearScreen();
        var page = CreateSurveyPage(heading);
        var getValue = CreateRadioScale(page, question, options, current, new Vector2(0, 40));
        var next = CreateNavButton(page, "Next", () => onComplete(getValue()));
        next.interactable = current >= 1;
        BindScaleRefresh(page, () => next.interactable = getValue() >= 1);
    }

    public void ShowEnd(string participantId, string savePath, Action onRestart)
    {
        ClearScreen();
        var page = CreateSurveyPage("Finished");
        CreateText(page, "Thank you", 40, FontStyle.Bold, Text, new Vector2(0, 80), 800, 50);
        CreateText(page, "You have finished the experiment.\nParticipant ID: " + participantId, 22, FontStyle.Normal, Text, new Vector2(0, -10), 900, 80);
        CreateText(page, "Your responses have been saved. You may let the researcher know you have finished.", 20, FontStyle.Normal, Muted, new Vector2(0, -110), 1000, 70);
        CreateNavButton(page, "New session", onRestart);
    }

    public static string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        var m = Mathf.FloorToInt(seconds / 60f);
        var s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    static string OrderLabel(ConditionOrder order)
    {
        switch (order)
        {
            case ConditionOrder.NatureThenCity: return "Selected: Nature then City";
            case ConditionOrder.CityThenNature: return "Selected: City then Nature";
            default: return "Selected: Random order";
        }
    }

    Canvas CreateCanvas()
    {
        var go = new GameObject("ExperimentCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 1000);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return canvas;
    }

    static void EnsureEventSystem()
    {
        var system = FindFirstObjectByType<EventSystem>();
        if (system == null) system = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        foreach (var old in system.GetComponents<BaseInputModule>()) old.enabled = false;
        if (!ExperimentVR.Requested)
        {
            var desktop = system.GetComponent<InputSystemUIInputModule>() ?? system.gameObject.AddComponent<InputSystemUIInputModule>();
            desktop.AssignDefaultActions();
            desktop.enabled = true;
            return;
        }
        var module = system.GetComponent<XRUIInputModule>() ?? system.gameObject.AddComponent<XRUIInputModule>();
        module.enabled = true;
        module.enableXRInput = ExperimentVR.Requested;
        module.enableMouseInput = !ExperimentVR.Requested;
        module.enableTouchInput = false;
        module.enableGamepadInput = false;
        module.enableJoystickInput = false;
    }

    void ShowIDKeyboard(InputField field)
    {
        var previous = _screen;
        previous.SetActive(false);
        var keyboard = CreatePanel("Participant keyboard", _root, Panel, new Vector2(1400, 860), Vector2.zero);
        var value = field.text;
        CreateText(keyboard, "Participant ID", 32, FontStyle.Bold, Text, new Vector2(0, 300), 1000, 60);
        var label = CreateText(keyboard, value, 32, FontStyle.Bold, Accent, new Vector2(0, 205), 1100, 60);
        const string keys = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        for (var i = 0; i < keys.Length; i++)
        {
            var letter = keys[i].ToString();
            CreateButton(keyboard, letter, new Vector2((i % 12 - 5.5f) * 100, 85 - (i / 12) * 90), 86, 70,
                () => { if (value.Length < 32) value += letter; label.text = value; });
        }
        CreateButton(keyboard, "Delete", new Vector2(-350, -240), 240, 65,
            () => { if (value.Length > 0) value = value.Substring(0, value.Length - 1); label.text = value; });
        CreateButton(keyboard, "Cancel", new Vector2(0, -240), 240, 65,
            () => { Destroy(keyboard.gameObject); previous.SetActive(true); EventSystem.current.SetSelectedGameObject(null); });
        CreateButton(keyboard, "Done", new Vector2(350, -240), 240, 65,
            () => { if (!string.IsNullOrWhiteSpace(value)) field.text = value; Destroy(keyboard.gameObject); previous.SetActive(true); EventSystem.current.SetSelectedGameObject(null); });
        EventSystem.current.SetSelectedGameObject(null);
    }

    public void ShowVRPause(Action resume)
    {
        if (_pause != null) return;
        _pausedScreen = _screen;
        _pauseHidden.Clear();
        foreach (Transform child in _root)
            if (child.gameObject.activeSelf) { _pauseHidden.Add(child.gameObject); child.gameObject.SetActive(false); }
        var panel = CreatePanel("Session paused", _root, Panel, new Vector2(1400, 860), Vector2.zero);
        _pause = panel.gameObject;
        CreateText(panel, "Session paused", 42, FontStyle.Bold, Text, new Vector2(0, 160), 1200, 80);
        CreateText(panel, "Put the headset on and let the researcher know.\nThe interruption has been recorded. Resume when ready.", 28, FontStyle.Normal, Text, Vector2.zero, 1100, 180);
        _resume = CreateButton(panel, "Resume", new Vector2(0, -230), 340, 80, resume);
        _resume.interactable = false;
        EventSystem.current.SetSelectedGameObject(null);
    }

    public void SetResumeAvailable(bool available) { if (_resume != null) _resume.interactable = available; }
    public void HideVRPause()
    {
        if (_pause != null) { _pause.SetActive(false); Destroy(_pause); }
        _pause = null;
        foreach (var hidden in _pauseHidden) if (hidden != null) hidden.SetActive(true);
        _pauseHidden.Clear();
        if (_screen != null && _screen != _pausedScreen) _screen.SetActive(true);
        _pausedScreen = null;
        EventSystem.current.SetSelectedGameObject(null);
    }

    RectTransform CreateSurveyPage(string header)
    {
        var page = CreatePanel("SurveyPage", _screen.transform, Panel, new Vector2(1400, 860), Vector2.zero);
        CreateText(page, "TECH PORTO  /  EXPERIENCE STUDY", 16, FontStyle.Bold, Muted, new Vector2(-295, 375), 650, 30).alignment = TextAnchor.MiddleLeft;
        CreateText(page, _progress, 16, FontStyle.Normal, Accent, new Vector2(350, 375), 530, 30).alignment = TextAnchor.MiddleRight;
        CreateText(page, header.Replace('_', ' '), 28, FontStyle.Bold, Text, new Vector2(0, 311), 1250, 55);
        CreatePanel("Divider", page, HeaderBar, new Vector2(1240, 2), new Vector2(0, 270));
        return page;
    }

    RectTransform CreatePanel(string name, Transform parent, Color color, Vector2 size, Vector2 anchored)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchored;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.sprite = _roundedSprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return rt;
    }

    GameObject CreateFullRect(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        Stretch(rt);
        go.GetComponent<Image>().color = color;
        return go;
    }

    Text CreateText(Transform parent, string value, int size, FontStyle style, Color color, Vector2 pos, float width, float height)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = pos;

        var text = go.GetComponent<Text>();
        text.font = Font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    InputField CreateInput(Transform parent, string value, Vector2 pos, float width, float height)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = Hex("1F1F1F");

        var text = CreateText(go.transform, "", 22, FontStyle.Normal, Text, Vector2.zero, width - 24, height);
        var placeholder = CreateText(go.transform, "e.g. P001", 22, FontStyle.Italic, Muted, Vector2.zero, width - 24, height);
        var field = go.GetComponent<InputField>();
        field.textComponent = text;
        field.placeholder = placeholder;
        field.text = value;
        field.caretColor = Text;
        return field;
    }

    Toggle CreateToggle(Transform parent, string label, bool on, Vector2 pos)
    {
        var go = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = new Vector2(560, 40);
        rt.anchoredPosition = pos;

        var box = CreatePanel("Box", go.transform, Hex("1F1F1F"), new Vector2(28, 28), new Vector2(-250, 0));
        var check = CreatePanel("Check", box, Accent, new Vector2(18, 18), Vector2.zero);
        CreateText(go.transform, label, 20, FontStyle.Normal, Text, new Vector2(24, 0), 480, 36);

        var toggle = go.GetComponent<Toggle>();
        box.GetComponent<Image>().raycastTarget = true;
        toggle.graphic = check.GetComponent<Image>();
        toggle.targetGraphic = box.GetComponent<Image>();
        toggle.isOn = on;
        return toggle;
    }

    Button CreateButton(Transform parent, string label, Vector2 pos, float width, float height, Action onClick)
    {
        return CreateButtonInternal(parent, label, pos, width, height, Button, onClick);
    }

    Button CreateNavButton(Transform parent, string label, Action onClick)
    {
        return CreateButtonInternal(parent, label, new Vector2(430, -350), 320, 64, Accent, onClick);
    }

    Button CreateButtonInternal(Transform parent, string label, Vector2 pos, float width, float height, Color color, Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = pos;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.sprite = _roundedSprite;
        image.type = Image.Type.Sliced;
        CreateText(go.transform, label, 21, FontStyle.Bold, color == Accent ? Hex("242321") : Text, Vector2.zero, width - 24, height);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        var feedback = go.AddComponent<ExperimentButtonFeedback>();
        feedback.idle = color;
        feedback.focus = color == Accent ? Color.Lerp(Accent, Color.white, .22f) : Hex("645333");
        button.onClick.AddListener(() => onClick());
        return button;
    }

    Func<int> CreateRadioScale(Transform parent, string question, string[] options, int selected, Vector2 pos)
    {
        var root = new GameObject("RadioScale", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = new Vector2(1200, 170);
        rt.anchoredPosition = pos;

        CreateText(root.transform, question, 22, FontStyle.Bold, Text, new Vector2(0, 60), 1100, 36);

        var state = root.AddComponent<ScaleState>();
        state.Value = selected;
        var fills = new Image[options.Length];
        var count = options.Length;
        var spacing = count <= 5 ? 180f : 150f;
        var startX = -(count - 1) * spacing * 0.5f;

        for (var i = 0; i < count; i++)
        {
            var value = i + 1;
            var x = startX + i * spacing;
            var item = new GameObject("Option" + value, typeof(RectTransform), typeof(Image), typeof(Button));
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.SetParent(root.transform, false);
            itemRt.gameObject.layer = 5;
            itemRt.sizeDelta = new Vector2(140, 110);
            itemRt.anchoredPosition = new Vector2(x, -20);

            var hit = item.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            var outline = CreateCircle(item.transform, 36, Color.white, new Vector2(0, 22));
            outline.raycastTarget = true;
            var fill = CreateCircle(item.transform, 22, Accent, new Vector2(0, 22));
            fill.raycastTarget = false;
            fills[i] = fill;
            fill.enabled = value == selected;

            var label = CreateText(item.transform, options[i], 14, FontStyle.Normal, Text, new Vector2(0, -28), 136, 40);
            label.raycastTarget = false;

            var button = item.GetComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(() =>
            {
                state.Value = value;
                for (var f = 0; f < fills.Length; f++)
                    fills[f].enabled = f + 1 == state.Value;
                root.SendMessage("Notify", SendMessageOptions.DontRequireReceiver);
            });
        }

        return () => state.Value;
    }

    // Five supplied visual anchors above an independent six-point response row.
    void CreateSamSixPointScale(Transform parent, Texture2D[] figures, Action<int> onSelected)
    {
        var root = new GameObject("SAMSixPointScale", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = new Vector2(1230, 290);
        rt.anchoredPosition = new Vector2(0, -48);

        for (var i = 0; i < figures.Length; i++)
        {
            var figure = new GameObject("SAM figure", typeof(RectTransform), typeof(RawImage));
            var figureRt = figure.GetComponent<RectTransform>();
            figureRt.SetParent(root.transform, false);
            figureRt.gameObject.layer = 5;
            var source = figures[i];
            figureRt.sizeDelta = new Vector2(176f * source.width / source.height, 176);
            figureRt.anchoredPosition = new Vector2(-480 + i * 240, 30);
            var figureImage = figure.GetComponent<RawImage>();
            figureImage.texture = source;
            figureImage.color = Color.white;
            figureImage.raycastTarget = false;
        }
        CreateResponseRow(parent, -195, onSelected);
    }

    static Texture2D[] LoadSamFigures(string scale)
    {
        var figures = new Texture2D[5];
        for (var i = 0; i < figures.Length; i++)
            figures[i] = Resources.Load<Texture2D>("Experiment/SAM/" + scale + "/" + (i + 1));
        return figures;
    }

    void CreateDirectSixPointScale(Transform parent, Action<int> onSelected)
    {
        CreateResponseRow(parent, -65, onSelected);
    }

    void CreateResponseRow(Transform parent, float y, Action<int> onSelected)
    {
        _responseButtons = new Button[6];
        for (var i = 0; i < 6; i++)
        {
            var value = i + 1;
            var button = CreateButtonInternal(parent, value.ToString(), new Vector2(-500 + i * 200, y), 176, 86, Button, () =>
            {
                if (!_committing) StartCoroutine(CommitResponse(value, onSelected));
            });
            button.name = "Response " + value;
            button.GetComponentInChildren<Text>().fontSize = 32;
            _responseButtons[i] = button;
        }
        for (var i = 0; i < 6; i++)
            _responseButtons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = _responseButtons[Mathf.Max(0, i - 1)],
                selectOnRight = _responseButtons[Mathf.Min(5, i + 1)]
            };
    }

    IEnumerator CommitResponse(int value, Action<int> onSelected)
    {
        _committing = true;
        var screen = _screen;
        foreach (var button in _responseButtons) button.interactable = false;
        var chosen = _responseButtons[value - 1];
        chosen.GetComponent<ExperimentButtonFeedback>().idle = Accent;
        chosen.GetComponent<Image>().color = Accent;
        chosen.GetComponentInChildren<Text>().color = Hex("242321");
        yield return new WaitForSecondsRealtime(.18f);
        if (_screen == screen) onSelected(value);
    }

    static Sprite MakeRoundedSprite()
    {
        const int size = 48;
        const float radius = 12;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var p = new Vector2(x + .5f, y + .5f);
                var center = new Vector2(Mathf.Clamp(p.x, radius, size - radius), Mathf.Clamp(p.y, radius, size - radius));
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - Vector2.Distance(p, center))));
            }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(13, 13, 13, 13));
    }

    Func<int> CreateFaceScale(Transform parent, string question, string[] options, int selected, Vector2 pos)
    {
        var root = new GameObject("FaceScale", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = new Vector2(1200, 180);
        rt.anchoredPosition = pos;

        CreateText(root.transform, question, 22, FontStyle.Bold, Text, new Vector2(0, 68), 1100, 36);

        var state = root.AddComponent<ScaleState>();
        state.Value = selected;
        var images = new Image[5];
        var startX = -360f;

        for (var i = 0; i < 5; i++)
        {
            var value = i + 1;
            var item = new GameObject("Face" + value, typeof(RectTransform), typeof(Button));
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.SetParent(root.transform, false);
            itemRt.gameObject.layer = 5;
            itemRt.sizeDelta = new Vector2(150, 130);
            itemRt.anchoredPosition = new Vector2(startX + i * 180f, -16);

            var face = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var faceRt = face.GetComponent<RectTransform>();
            faceRt.SetParent(item.transform, false);
            faceRt.gameObject.layer = 5;
            faceRt.sizeDelta = new Vector2(72, 72);
            faceRt.anchoredPosition = new Vector2(0, 18);
            var image = face.GetComponent<Image>();
            image.sprite = Sprite.Create(_faceTextures[i], new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            image.color = value == selected ? Accent : FaceIdle;
            images[i] = image;

            CreateText(item.transform, options[i], 14, FontStyle.Normal, Text, new Vector2(0, -42), 140, 36);

            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                state.Value = value;
                for (var f = 0; f < images.Length; f++)
                    images[f].color = f + 1 == state.Value ? Accent : FaceIdle;
                root.SendMessage("Notify", SendMessageOptions.DontRequireReceiver);
            });
        }

        return () => state.Value;
    }

    Image CreateCircle(Transform parent, float size, Color color, Vector2 pos)
    {
        var go = new GameObject("Circle", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;
        var image = go.GetComponent<Image>();
        image.sprite = _circleSprite;
        image.color = color;
        return image;
    }

    static void BindScaleRefresh(Transform page, Action refresh)
    {
        var binder = page.gameObject.GetComponent<ScaleRefreshBinder>();
        if (binder == null)
            binder = page.gameObject.AddComponent<ScaleRefreshBinder>();
        binder.Callback = refresh;
    }

    void CreateCross(Transform parent, float arm, float thickness)
    {
        var root = new GameObject("RedCross", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = 5;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        CreatePanel("H", root.transform, RedCross, new Vector2(arm, thickness), Vector2.zero);
        CreatePanel("V", root.transform, RedCross, new Vector2(thickness, arm), Vector2.zero);
    }

    static Texture2D MakeCircleTexture(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var center = (size - 1) * 0.5f;
        var radius = center - 1f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                var a = Mathf.Clamp01(radius - d + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        texture.Apply();
        return texture;
    }

    static Texture2D MakeFaceTexture(int mood)
    {
        var size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var center = (size - 1) * 0.5f;
        var radius = 28f;
        var smile = (mood - 2) / 2f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var p = new Vector2(x, y);
                var d = Vector2.Distance(p, new Vector2(center, center));
                var color = Color.clear;
                if (d <= radius)
                    color = Color.white;

                var leftEye = Vector2.Distance(p, new Vector2(center - 10f, center + 6f));
                var rightEye = Vector2.Distance(p, new Vector2(center + 10f, center + 6f));
                if (leftEye < 3.2f || rightEye < 3.2f)
                    color = Color.black;

                var mouthX = (x - center) / 12f;
                var mouthY = center - 8f + smile * 6f * (mouthX * mouthX);
                if (Mathf.Abs(x - center) < 14f && Mathf.Abs(y - mouthY) < 1.8f && d < radius - 2f)
                    color = Color.black;

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var color);
        return color;
    }

    class ScaleState : MonoBehaviour
    {
        public int Value = -1;

        public void Notify()
        {
            var binder = GetComponentInParent<ScaleRefreshBinder>();
            binder?.Callback?.Invoke();
        }
    }

    class ScaleRefreshBinder : MonoBehaviour
    {
        public Action Callback;
    }

    class SlideHolder : MonoBehaviour
    {
        public RawImage Image;
        public Texture2D[] Slides;
    }
}
