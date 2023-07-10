using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using NikitaTrubkin.StompClient;
using syp.biz.SockJS.NET.Client;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class Main : MonoBehaviour
    {
        private const string Token = "eyJhbGciOiJIUzUxMiJ9.eyJ1c2VySWQiOiIzZmE4NWY2NC01NzE3LTQ1NjItYjNmYy0yYzk2M2Y2NmFmYTYiLCJleHAiOjE2ODg2NzQwNzN9.DzMvLdClRV2BqeFuSPGALSYv8_IExrak2bJrTNbTPgXKaGBtQSmUdlTw9mQqhWh7fXJMs2YP_g64iVowaucAwA";
        [SerializeField] private Button button;
        private SockJS sockJs;
        public void Start()
        {
            Connect();
            button.onClick.AddListener(Subscribe);
        }

        public async void Connect()
        {
            var config = Configuration.Factory.BuildDefault("http://158.160.71.110:8000/ws-stomp");
            

            sockJs = new SockJS(config);
            sockJs.Connected += async (sender, e) =>
            {
                // this event is triggered once the connection is established
                try
                {
                    Debug.Log("Connected...");
                    
                    await sockJs.Send("/match/round", "",new Dictionary<string, string>());
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error: {e}");
                }
            };
            sockJs.Message += async (sender, msg) =>
            {
                // this event is triggered every time a message is received
                try
                {
                    Debug.Log($"Message: {msg}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex}");
                }
            };
            await sockJs.Connect();
            Debug.Log("might be connected");
        }

        public void Subscribe()
        {
            sockJs.Subscribe<string>("/user/topic/inventory/component/pre-complete", (sender,msg) =>
            {
                Debug.Log(msg);
            });
        }
    }
}