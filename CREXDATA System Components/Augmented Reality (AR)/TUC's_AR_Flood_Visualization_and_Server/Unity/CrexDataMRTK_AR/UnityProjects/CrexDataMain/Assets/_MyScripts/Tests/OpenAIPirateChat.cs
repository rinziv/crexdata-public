using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class OpenAIPirateChat : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField questionInput;
    public Button askButton;
    public TMP_Text answerText;

    [Header("OpenAI Parameters")]
    [Tooltip("Your OpenAI API key here")]
    public string openAIKey;

    private const string apiUrl = "https://api.openai.com/v1/chat/completions";

    private void Start()
    {
        askButton.onClick.AddListener(OnAskButtonClicked);
        answerText.text = "";
    }

    void OnAskButtonClicked()
    {
        string question = questionInput.text;
        if (!string.IsNullOrEmpty(question))
        {
            answerText.text = "Thinking like a pirate... Arrr!";
            StartCoroutine(SendOpenAIRequest(question));
        }
    }

    IEnumerator SendOpenAIRequest(string question)
    {
        // Define the pirate prompt and the chat message
        var messages = new object[]
        {
            new { role = "system", content = "You are a greek lover" },
            new { role = "user", content = question }
        };

        // Create request body
        var requestBody = new
        {
            model = "gpt-4.1",
            messages = messages,
            temperature = 0.8,
            max_tokens = 150
        };

        string jsonBody = JsonUtility.ToJson(new JsonHelper(requestBody));
        jsonBody = JsonHelper.ToJson(requestBody); // Use helper because Unity JsonUtility struggles with anonymous types

        // Actually use Newtonsoft.Json for better serialization:
        jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + openAIKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                string errorMessage = $"Error: {request.error}";
                if (!string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    errorMessage += $"\nResponse: {request.downloadHandler.text}";
                }
                answerText.text = errorMessage;
                Debug.LogError(errorMessage); // Log to console for more details
            }
            else
            {
                // Parse response
                var jsonResponse = request.downloadHandler.text;
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<OpenAIResponse>(jsonResponse);
                if (data != null && data.choices.Length > 0)
                {
                    string pirateAnswer = data.choices[0].message.content.Trim();
                    answerText.text = pirateAnswer;
                }
                else
                {
                    answerText.text = "Arr! I got no answer for ye.";
                }
            }
        }
    }

    // Classes for JSON parse
    [System.Serializable]
    public class OpenAIResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    public class Choice
    {
        public Message message;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

    // JSON utility helper to wrap anonymous object if needed
    public class JsonHelper
    {
        private object obj;
        public JsonHelper(object o) { obj = o; }
        public override string ToString() { return JsonUtility.ToJson(obj); }
        public static string ToJson(object o) { return JsonUtility.ToJson(o); }
    }
}