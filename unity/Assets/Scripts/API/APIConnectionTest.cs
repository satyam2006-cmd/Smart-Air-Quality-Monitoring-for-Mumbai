using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class APIConnectionTest : MonoBehaviour
{
    private string apiUrl = "http://127.0.0.1:8000/health";

    void Start()
    {
        StartCoroutine(CheckAPIConnection());
    }

    IEnumerator CheckAPIConnection()
    {
        Debug.Log("Connecting to Smart City API...");

        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("API CONNECTION SUCCESSFUL!");
                Debug.Log("API Response: " + request.downloadHandler.text);
            }
            else
            {
                Debug.LogError("API CONNECTION FAILED!");
                Debug.LogError("Error: " + request.error);
            }
        }
    }
}