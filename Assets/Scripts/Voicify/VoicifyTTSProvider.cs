using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;


[RequireComponent(typeof(AudioSource))]
public class VoicifyTTSProvider : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private AudioSource source;

    [Header("Settings")]
    public string ApplicationId;
    public string ApplicationKey;

    private string[] externalAudio;

    [Header("Debugging")]
    [SerializeField] private List<AudioClip> musicClips = new List<AudioClip>();
    [SerializeField] private int currentTrack;
    [SerializeField] private bool isInitialized;
    private Coroutine currentPlayTrack;
    public event EventHandler OnStartSpeaking;
    public event EventHandler OnStopSpeaking;

    private bool wasPlaying;

    public async Task Play(string ssml)
    {
        OnStartSpeaking?.Invoke(this, EventArgs.Empty);
        var client = new HttpClient();
        var req = new SsmlRequestObject
        {
            ApplicationId = ApplicationId,
            ApplicationSecret = ApplicationKey,
            SsmlRequest = new InnerSsmlRequest
            {
                Ssml = ssml,
                Locale = "en-US"
            }
        };

        var json = JsonUtility.ToJson(req, true).ToString();
        var response = await client.PostAsync("https://assistant.voicify.com/api/ssml/tospeech/google", new StringContent(json, Encoding.UTF8, "application/json"));
        var jsonResponse = await response.Content.ReadAsStringAsync();
        Debug.Log(jsonResponse);
        var voicifyResponse = FromJsonArray<SsmlResponse>(jsonResponse);
        //Debug.Log(JsonUtility.ToJson(voicifyResponse, true));
        externalAudio = voicifyResponse.Select(x => x.url).ToArray();
        foreach (var audio in externalAudio)
        {
            Debug.Log($"parsed audio url {audio}");
        }
        Debug.Log(externalAudio);
        StartCoroutine(Run());
    }

    // Yes, if you make Start return IEnumerator then Units
    // automatically runs it as a Coroutine
    private void Start()
    {
        // block input from the outside until this controller is finished with the downloads
        isInitialized = false;

        if (!source) source = GetComponent<AudioSource>();
        
    }

    private void Update()
    {
        if(!source.isPlaying && wasPlaying)
        {
            wasPlaying = false;
            OnStopSpeaking?.Invoke(this, EventArgs.Empty);
        }
    }


    private IEnumerator Run()
    {
        yield return GetAudioClipsParallel();
    }

    // This version starts all downloads at once and waits until they are all done
    // probably faster than the sequencial version
    private IEnumerator GetAudioClipsParallel()
    {
        musicClips.Clear();
        var requests = new List<UnityWebRequest>();

        foreach (var url in externalAudio)
        {
            Debug.Log($"loading audio from {url}");

            var www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);

            // Start the request without waiting
            www.SendWebRequest();
            requests.Add(www);
        }

        // Wait for all requests to finish
        yield return new WaitWhile(() => requests.Any(r => !r.isDone));

        // Now examine and use all results
        foreach (var www in requests)
        {
            switch (www.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError($"Could not get clip from \"{www.url}\"! Error: {www.error}", this);

                    continue;
            }

            musicClips.Add(DownloadHandlerAudioClip.GetContent(www));

            www.Dispose();
        }
        PlayFirstTitle();

    }

    public void PlayFirstTitle()
    {

        if (source.isPlaying) return;
        wasPlaying = true;

        if (currentPlayTrack != null)
        {
            StopCoroutine(currentPlayTrack);
        }

        currentPlayTrack = StartCoroutine(PlayTrack(0));
    }

    private IEnumerator PlayTrack(int index)
    {
        // Make sure the index is within the given clips range
        index = Mathf.Clamp(index, 0, musicClips.Count);

        // update the current track to make next and previous work
        currentTrack = index;

        // get clip by index
        var clip = musicClips[currentTrack];

        // Assign and play
        source.clip = clip;
        source.Play();

        // wait for clip end
        while (source.isPlaying)
        {
            yield return null;
        }

        NextTitle();
    }

    public void NextTitle()
    {
        if (!isInitialized) return;

        if (currentPlayTrack != null)
        {
            OnStopSpeaking?.Invoke(this, EventArgs.Empty);
            StopCoroutine(currentPlayTrack);
        }

        source.Stop();
        currentTrack = (currentTrack + 1) % musicClips.Count;

        currentPlayTrack = StartCoroutine(PlayTrack(currentTrack));
    }


    public void StopMusic()
    {
        if (!isInitialized) return;

        if (currentPlayTrack != null)
        {
            OnStopSpeaking?.Invoke(this, EventArgs.Empty);
            StopCoroutine(currentPlayTrack);
        }

        source.Stop();
    }

    public static T[] FromJsonArray<T>(string json)
    {
        string newJson = "{ \"Items\": " + json + "}";
        Debug.Log($"new json {newJson}");
        ArrayWrapper<T> wrapper = JsonUtility.FromJson<ArrayWrapper<T>>(newJson);
        return wrapper.Items;
    }
}



[Serializable]
public class SsmlRequestObject
{
    public string ApplicationId;
    public string ApplicationSecret;
    public InnerSsmlRequest SsmlRequest;
}

[Serializable]
public class InnerSsmlRequest
{
    public string Ssml;
    public string Locale;
    public string Voice;
}

[Serializable]
public class SsmlResponse
{
    public string rootElementType;
    public string url;
}


[Serializable]
public class ArrayWrapper<T>
{
    public T[] Items;
}