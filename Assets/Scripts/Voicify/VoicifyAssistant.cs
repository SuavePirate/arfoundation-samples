using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

public class VoicifyAssistant : MonoBehaviour 
{
    public VoskSpeechToText VoskSpeechToText;
	public VoicifyTTSProvider VoicifyTTSProvider;
    public Text DialogText;
	public string VoicifyAssistantUrl;
	public string SsmlUrl;
	public string AppId;
	public string AppKey;
    private string userId;
	private string sessionId;
	private string deviceId;
	private AudioSource audioSource;
	private AudioClip audioClip;
	
	public Action<string> OnEffect;
    void Awake()
    {
        VoskSpeechToText.OnTranscriptionResult += OnTranscriptionResult;
		VoicifyTTSProvider.OnStartSpeaking += OnStartSpeaking;
		VoicifyTTSProvider.OnStopSpeaking += OnStopSpeaking;
		ResetState();
    }

	void ResetState()
	{
		userId = Guid.NewGuid().ToString();
		sessionId = Guid.NewGuid().ToString();
		deviceId = Guid.NewGuid().ToString();
	}

	void CheckState() {
	
	}

	async Task Say(string ssml)
	{
		await VoicifyTTSProvider.Play(ssml);

	}
	private void OnStartSpeaking(object sender, EventArgs e)
	{
		Debug.Log("Voicify started speaking");
	}
	private void OnStopSpeaking(object sender, EventArgs e)
	{
		Debug.Log("Voicify stopped speaking");
		VoskSpeechToText.Restart();
	}
    private async void OnTranscriptionResult(string obj)
    {
		// once we have a final result - send it to voicify then speak the response
        Debug.Log(obj);
        var result = new RecognitionResult(obj);
		if(!result.Partial && result.Phrases.Length > 0 && !VoskSpeechToText.Paused)
		{
			VoskSpeechToText.Stop();
			using (var client = new System.Net.Http.HttpClient())
			{
				var input = result.Phrases[0].Text;
				Debug.Log(result);
				var device = new CustomAssistantDevice{
					Id= deviceId,
					Name= "Voicify Unity App",
					SupportsDisplayText= true,
					SupportsTextInput= true
				};
				var user = new CustomAssistantUser {
					Id= userId,
					Name= "Unity User"
				};
				var context = new CustomAssistantRequestContext{
					Channel= "Voicify Unity App",
					Locale= "en-US",
					SessionId= sessionId,
					RequestType= "IntentRequest",
					OriginalInput= input,
					RequiresLanguageUnderstanding= true
				};
				var requestBody = new CustomAssistantRequestBody {
					RequestId = Guid.NewGuid().ToString(),
					Device = device,
					User = user,
					Context= context
				};

				if(input.Contains("door") || input.Contains("open")) {
					OnEffect("Door");
				}
				if(input.Contains("color")) {
					OnEffect("Color");
				}
				if(input.Contains("spin") || input.Contains("turn")) {
					OnEffect("Turn");
				}
				if(input.Contains("light")) {
					OnEffect("Lights");
				}

				Debug.Log(requestBody);
				var json = JsonUtility.ToJson(requestBody, true).ToString();
				Debug.Log(json);
				var voicifyResult = await client.PostAsync(VoicifyAssistantUrl, new StringContent(json, Encoding.UTF8, "application/json"));
				Debug.Log(voicifyResult);
				var jsonResponse = await voicifyResult.Content.ReadAsStringAsync();
				Debug.Log(jsonResponse);
				var voicifyResponse = JsonUtility.FromJson<CustomAssistantResponse>(jsonResponse);
				Debug.Log(voicifyResponse.outputSpeech);
				if(!string.IsNullOrEmpty(voicifyResponse.outputSpeech))
				{
					if(DialogText != null)
						DialogText.text = voicifyResponse.outputSpeech;
					await Say(voicifyResponse.ssml);

				}
			}
		}
	}

	[Serializable]
    public class CustomAssistantRequestBody
    {
        public string RequestId;
        public CustomAssistantRequestContext Context;
        public CustomAssistantDevice Device ;
        public CustomAssistantUser User ;
    }

	[Serializable]
	public class CustomAssistantDevice
    {
        public string Id ;
        public string Name ;
        public bool SupportsVideo ;
        public bool SupportsForegroundImage ;
        public bool SupportsBackgroundImage ;
        public bool SupportsAudio ;
        public bool SupportsSsml ;
        public bool SupportsDisplayText ;
        public bool SupportsVoiceInput ;
        public bool SupportsTextInput ;
    }

	[Serializable]
    public class CustomAssistantRequestContext
    {
        public string SessionId ;
        public bool NoTracking ;
        public string RequestType ;
        public string RequestName ;
        public Dictionary<string, string> Slots ;
        public string OriginalInput ;
        public string Channel ;
        public bool RequiresLanguageUnderstanding ;
        public string Locale ;
        public Dictionary<string, object> AdditionalRequestAttributes ;
        public Dictionary<string, object> AdditionalSessionAttributes ;
        public List<string> AdditionalSessionFlags ;
    }

	[Serializable]
    public class CustomAssistantUser
    {
        public string Id ;
        public string Name ;
        public string AccessToken ;
        public Dictionary<string, object> AdditionalUserAttributes ;
        public List<string> AdditionalUserFlags ;
    }

	[Serializable]
    public class CustomAssistantResponse
    {
        public string responseId ;
        public string ssml ;
        public string outputSpeech ;
        public string displayText ;
        public string displayTitle ;
        public string responseTemplate ;
        public List<string> hints ;
        public bool endSession ;
    }
}