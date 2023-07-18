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
        private const string Token = "eyJhbGciOiJIUzUxMiJ9.eyJ1c2VySWQiOiI4M2E3Nzg5NS02YzBjLTRhNDYtOTQzNy1kOTAzNTZhNTk0YjEiLCJleHAiOjE2OTAyODM1ODZ9.Zsku0M-8dRds5wPK5naLybroaKmsMdwL6FcH0CS4Ut9LPbRUtbJP4AvQ4Dlc4G5hXHkLiC9M-q7--aw6R1mRcg";
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
                    
                    await sockJs.Send("/matcher/round", "1",new Dictionary<string, string>());
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error: {e}");
                }
            };
            
            // sockJs.Message += async (sender, msg) =>
            // {
            //     // this event is triggered every time a message is received
            //     try
            //     {
            //         Debug.Log($"Message: {msg}");
            //     }
            //     catch (Exception ex)
            //     {
            //         Console.WriteLine($"Error: {ex}");
            //     }
            // };
            await sockJs.Connect(CancellationToken.None, Token);
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