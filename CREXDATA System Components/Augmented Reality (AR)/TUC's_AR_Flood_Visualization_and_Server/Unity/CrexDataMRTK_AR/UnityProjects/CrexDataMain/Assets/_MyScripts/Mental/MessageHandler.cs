using GLTFast;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class UserMessage
{
    protected string userId { get; set; }
    protected string message { get; set; }
    protected string timestamp { get; set; }

    public UserMessage(string userId, string message, string timestamp)
    {
        this.userId = userId;
        this.message = message;
        this.timestamp = timestamp;
    }
    public string GetUserId() => userId;
    public string GetMessage() => message;
    public string GetTimestamp() => timestamp;
}


public class MessageHandler : MonoBehaviour
{
    private List<UserMessage> lst = new();
    bool use = false;
    GameObject sceneObject;

    [SerializeField] private float deleteAfterSeconds = 10;
    [SerializeField] private float expireAfterSeconds = 5;
    [SerializeField] private float checkIntervalSeconds = 0.1f;
    public GameObject parentObject;  // Assign your inactive parent in the Inspector
    public GameObject prefabToSpawn; // Assign the prefab you want to spawn
    private List<GameObject> buttonList = new();
    // per-Info running color coroutine so we can stop it when a new color is requested
    private readonly Dictionary<Info, Coroutine> colorCoroutines = new();
    // remember the last intended target color per Info so new lerps start from the previous target
    private readonly Dictionary<Info, Color> lastTargetColors = new();
    // per-Info running label color coroutine (stateText only)
    private readonly Dictionary<Info, Coroutine> labelCoroutines = new();
    // remember last label target color per Info
    private readonly Dictionary<Info, Color> lastLabelTargetColors = new();
    // track message count per userId to only process every Nth message
    private readonly Dictionary<string, int> userMessageCounts = new();
    private string currentState = "";


    private void Start()
    {
        StartCoroutine(timeCalculation());
    }


    public void recieveMessage(UserMessage userMessage)
    {
        // track and increment message count for this user
        string userId = userMessage.GetUserId();
        if (!userMessageCounts.ContainsKey(userId))
        {
            userMessageCounts[userId] = 0;
        }
        userMessageCounts[userId]++;

        // only process every 2nd message (skip odd numbered messages)
        if (userMessageCounts[userId] % 2 != 0)
        {
            Debug.Log($"Skipping message {userMessageCounts[userId]} from {userId}");
            return;
        }

        use = lst.Any(x => x.GetUserId() == userMessage.GetUserId());

        // determine textual state and target color once
        string stateText = "";
        Color computedTargetColor = Color.white;
        switch (userMessage.GetMessage())
        {
            case "1":
                stateText = "LOW";
                computedTargetColor = Color.green;
                break;
            case "2":
                stateText = "MILD";
                computedTargetColor = Color.yellow;
                break;
            case "3":
                stateText = "MODERATE";
                computedTargetColor = new Color(1f, 0.5f, 0f); // Orange
                break;
            case "4":
                stateText = "SEVERE";
                computedTargetColor = Color.red;
                break;
            default:
                stateText = userMessage.GetMessage();
                computedTargetColor = Color.white;
                break;
        }

        if (use)
        {
            var index = lst.FindIndex(x => x.GetUserId() == userMessage.GetUserId());
            lst[index] = userMessage;
            sceneObject = buttonList.Find(x => x.GetComponent<Info>().GetUserId() == userMessage.GetUserId());

            // Map userId to role name
            string displayId = userMessage.GetUserId();
            if (displayId == "RPi_00ea781d")
                displayId = "DRONE OPERATOR";
            else if (displayId == "RPi_68b44a38")
                displayId = "LEADER";

            sceneObject.GetComponent<Info>().label.text = "Mental State: " + stateText + "\nID: " + displayId;
            var info = sceneObject.GetComponent<Info>();
            if (info != null)
            {
                info.SetDisconnectedIconActive(false);

                // stop any running icon color coroutine for this Info
                if (colorCoroutines.TryGetValue(info, out var runningIcon) && runningIcon != null)
                {
                    StopCoroutine(runningIcon);
                    colorCoroutines.Remove(info);
                }

                // start icon color coroutine (keep existing icon behavior)
                Color startColor = info.icon.color;
                if (lastTargetColors.TryGetValue(info, out var prevIconTarget))
                {
                    startColor = prevIconTarget;
                }
                float duration = 0.2f;
                lastTargetColors[info] = computedTargetColor;
                var iconCoro = StartCoroutine(ColorCoroutine(info, startColor, computedTargetColor, duration));
                colorCoroutines[info] = iconCoro;

                // stop any running label coroutine for this Info
                if (labelCoroutines.TryGetValue(info, out var runningLabel) && runningLabel != null)
                {
                    StopCoroutine(runningLabel);
                    labelCoroutines.Remove(info);
                }

                // choose start color for label text: prefer last label target if present, otherwise default to white
                Color startLabelColor = Color.white;
                if (lastLabelTargetColors.TryGetValue(info, out var prevLabelTarget))
                {
                    startLabelColor = prevLabelTarget;
                }

                // start label-color coroutine which updates only the stateText substring
                var labelCoro = StartCoroutine(LabelColorCoroutine(info, stateText, displayId, startLabelColor, computedTargetColor, duration));
                labelCoroutines[info] = labelCoro;
            }
            use = false;
        }
        else
        {
            Debug.Log("Added: " + userMessage.GetMessage());
            lst.Add(userMessage);
            GameObject newObj = Instantiate(prefabToSpawn, parentObject.transform);

            // Optionally set position/rotation relative to parent
            newObj.transform.localPosition = Vector3.zero;
            newObj.transform.localRotation = Quaternion.identity;
            var newInfo = newObj.GetComponent<Info>();
            if (newInfo != null)
            {
                newInfo.SetUserId(userMessage.GetUserId());

                // Map userId to role name
                string displayId = userMessage.GetUserId();
                if (displayId == "RPi_00ea781d")
                    displayId = "Drone Operator";
                else if (displayId == "RPi_68b44a38")
                    displayId = "Leader";

                // initialize label with white prefix/suffix and colored stateText
                string hex = ColorUtility.ToHtmlStringRGB(computedTargetColor);
                newInfo.label.text = $"<color=#FFFFFF>Mental State: </color><color=#{hex}>{stateText}</color><color=#FFFFFF>\nID: {displayId}</color>";
                newInfo.SetIconColor(computedTargetColor);
                // ensure the TMP component's base color is white so non-colored parts remain white
                newInfo.label.color = Color.white;
                // record initial last label target
                lastLabelTargetColors[newInfo] = computedTargetColor;
            }
            buttonList.Add(newObj);
        }
    }

    private IEnumerator timeCalculation()
    {
        DateTime parsedTime;
        var format = "yyyy-MM-dd HH:mm:ss.ffffff";
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        while (true)
        {
            var wait = new WaitForSeconds(checkIntervalSeconds);

            // iterate backwards so we can safely remove items while iterating
            for (int i = lst.Count - 1; i >= 0; i--)
            {
                var message = lst[i];

                if (!DateTime.TryParseExact(message.GetTimestamp(), format, culture, System.Globalization.DateTimeStyles.None, out parsedTime))
                {
                    Debug.LogWarning($"Could not parse timestamp '{message.GetTimestamp()}' for user {message.GetUserId()}");
                    continue;
                }

                var age = DateTime.Now - parsedTime;

                // Delete when older than deleteAfterSeconds
                if (age > TimeSpan.FromSeconds(deleteAfterSeconds))
                {
                    var objToRemove = buttonList.Find(x => x.GetComponent<Info>().GetUserId() == message.GetUserId());
                    if (objToRemove != null)
                    {
                        Debug.Log("Deleted: " + message.GetMessage());
                        buttonList.Remove(objToRemove);
                        Destroy(objToRemove);
                    }

                    // remove message from list
                    lst.RemoveAt(i);
                    // continue to next message (we already removed this one)
                    continue;
                }

                // Mark as expired (disconnected) when older than expireAfterSeconds
                if (age > TimeSpan.FromSeconds(expireAfterSeconds))
                {
                    var objToExpire = buttonList.Find(x => x.GetComponent<Info>().GetUserId() == message.GetUserId());
                    if (objToExpire != null)
                    {
                        var info = objToExpire.GetComponent<Info>();
                        if (info != null)
                        {
                            Debug.Log("Expired: " + message.GetMessage());
                            info.SetDisconnectedIconActive(true);
                        }
                    }

                    // do not remove the message here; allow it to be deleted later when past deleteAfterSeconds
                    continue;
                }
            }
            yield return wait;
        }
    }

    private IEnumerator ColorCoroutine(Info info, Color startColor, Color targetColor, float duration)
    {
        if (info == null)
            yield break;

        // perform interpolation in linear color space to preserve perceived brightness
        Color startLinear = startColor.linear;
        Color targetLinear = targetColor.linear;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            // lerp in linear space, then convert back to gamma for display
            Color lerpedLinear = Color.Lerp(startLinear, targetLinear, t);
            Color result = lerpedLinear.gamma;

            // lerp alpha separately in gamma/normal space
            result.a = Mathf.Lerp(startColor.a, targetColor.a, t);

            info.SetIconColor(result);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ensure final color is set exactly (use targetColor as provided)
        info.SetIconColor(targetColor);

        // cleanup tracking
        if (colorCoroutines.TryGetValue(info, out var running) && running != null)
        {
            colorCoroutines.Remove(info);
        }

        // set last target explicitly in case of float rounding
        lastTargetColors[info] = targetColor;
    }

    // Animate only the stateText substring color using TMP rich text color tags
    private IEnumerator LabelColorCoroutine(Info info, string stateText, string id, Color startColor, Color targetColor, float duration)
    {
        if (info == null)
            yield break;

        // ensure base label format without colored state
        string prefix = "Mental State: ";
        string suffix = "\nID: " + id;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            Color c = Color.Lerp(startColor, targetColor, t);
            string hex = ColorUtility.ToHtmlStringRGB(c);
            // keep prefix and suffix white explicitly
            info.label.text = $"<color=#FFFFFF>{prefix}</color><color=#{hex}>{stateText}</color><color=#FFFFFF>{suffix}</color>";

            elapsed += Time.deltaTime;
            yield return null;
        }

        // final set
        string finalHex = ColorUtility.ToHtmlStringRGB(targetColor);
        info.label.text = $"<color=#FFFFFF>{prefix}</color><color=#{finalHex}>{stateText}</color><color=#FFFFFF>{suffix}</color>";

        // cleanup tracking
        if (labelCoroutines.TryGetValue(info, out var running) && running != null)
        {
            labelCoroutines.Remove(info);
        }

        lastLabelTargetColors[info] = targetColor;
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}


