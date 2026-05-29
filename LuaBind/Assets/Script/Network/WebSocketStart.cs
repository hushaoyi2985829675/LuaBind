using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WebSocketStart : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        NetworkManager.Instance.ConnectAsync();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
