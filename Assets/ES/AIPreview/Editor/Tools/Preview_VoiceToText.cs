using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ES
{
    public class Preview_VoiceToText : EditorWindow
    {
        private AudioClip recordedClip;
        private bool isRecording = false;
        private bool isContinuousMode = false;
        private string transcribedText = "";
        private string apiKey = ""; // Azure Speech API密钥
        private string region = "eastus"; // Azure区域
        private string language = "zh-CN"; // 语言设置
        private string statusMessage = "就绪";
        private string connectionId = "";

        // WebSocket相关
        private ClientWebSocket webSocket;
        private CancellationTokenSource cancellationTokenSource;
        private bool isConnected = false;

        // 音频缓冲
        private List<float> audioBuffer = new List<float>();
        private const int SAMPLE_RATE = 16000; // Azure推荐的采样率
        private const int BUFFER_SIZE = SAMPLE_RATE * 2; // 2秒缓冲

        [MenuItem("ES/AI预览/语音转文字")]
        static void Init()
        {
            Preview_VoiceToText window = (Preview_VoiceToText)EditorWindow.GetWindow(typeof(Preview_VoiceToText));
            window.Show();
        }

        [MenuItem("ES/AI预览/使用指南")]
        static void ShowUsageGuide()
        {
            string guide = @"
=== Unity语音转文字工具使用指南 ===

第一步：获取Azure Speech API密钥
─────────────────────────────────────
1. 访问：https://portal.azure.com
2. 创建Azure账户（如果没有）
3. 创建Speech服务资源：
   - 搜索 'Speech'
   - 点击 'Create'
   - 选择订阅和资源组
   - 区域选择：East US (eastus) 或 China East (chinaeast)
   - 定价层：Free F0 (每月5小时免费)
   - 创建资源
4. 进入资源 → Keys and Endpoint
5. 复制 Key 1 作为 API Key
6. 记录 Region (如：eastus)

第二步：配置工具
─────────────────
1. 在Unity中打开：ES/AI预览/语音转文字
2. 填入：
   - Azure API Key: 你的密钥
   - Azure Region: eastus (或其他)
   - 语言: zh-CN (中文) 或 en-US (英文)

第三步：开始使用
─────────────────
1. 点击 '连接WebSocket' - 建立实时连接
2. 等待状态变为 '已连接'
3. 选择模式：
   - 连续对话模式：实时转文字
   - 手动模式：录音后手动转文字
4. 点击 '开始录音' 说话
5. 点击 '停止录音'
6. 查看 '识别结果' 区域的文字

第四步：故障排除
─────────────────
问题：连接失败
解决：
- 检查API密钥是否正确
- 检查网络连接
- 确认Azure资源可用
- 检查区域设置

问题：无法识别语音
解决：
- 确保麦克风权限
- 检查音频设备
- 尝试不同的语言设置
- 确认Azure配额未用完

问题：编译错误
解决：
- 确保Unity版本支持WebSocket
- 检查.NET版本设置
- 确认所有using语句正确

支持的平台：Windows, macOS, Linux, iOS, Android
支持的语言：中文(zh-CN), 英文(en-US), 其他Azure支持的语言

如需帮助，请查看控制台日志获取详细错误信息。
";

            // 显示在Unity控制台
            Debug.Log(guide);

            // 也可以创建一个文本文件
            string filePath = Path.Combine(Application.dataPath, "VoiceToText_UsageGuide.txt");
            File.WriteAllText(filePath, guide);
            Debug.Log($"使用指南已保存到: {filePath}");

            // 刷新AssetDatabase
            UnityEditor.AssetDatabase.Refresh();
        }

        void OnEnable()
        {
            // 初始化时生成连接ID
            connectionId = Guid.NewGuid().ToString();
        }

        void OnDisable()
        {
            // 清理资源
            DisconnectWebSocket();
        }

        void OnGUI()
        {
            GUILayout.Label("语音转文字工具 (Web API + WebSocket)", EditorStyles.boldLabel);

            // API配置
            apiKey = EditorGUILayout.TextField("Azure API Key", apiKey);
            region = EditorGUILayout.TextField("Azure Region", region);
            language = EditorGUILayout.TextField("语言", language);

            EditorGUILayout.Space();

            // 模式选择
            isContinuousMode = EditorGUILayout.Toggle("连续对话模式", isContinuousMode);

            // 连接状态
            EditorGUILayout.LabelField("连接状态", isConnected ? "已连接" : "未连接", isConnected ? EditorStyles.label : EditorStyles.boldLabel);

            // 控制按钮
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !string.IsNullOrEmpty(apiKey);
            if (GUILayout.Button(isConnected ? "断开连接" : "连接WebSocket"))
            {
                if (isConnected)
                {
                    DisconnectWebSocket();
                }
                else
                {
                    ConnectWebSocket();
                }
            }

            GUI.enabled = isConnected;
            if (GUILayout.Button(isRecording ? "停止录音" : "开始录音"))
            {
                if (isRecording)
                {
                    StopRecording();
                }
                else
                {
                    StartRecording();
                }
            }

            if (!isContinuousMode && recordedClip != null)
            {
                if (GUILayout.Button("转文字"))
                {
                    TranscribeAudio();
                }
            }

            if (GUILayout.Button("清除文本"))
            {
                transcribedText = "";
                statusMessage = "文本已清除";
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;

            EditorGUILayout.Space();

            // 状态信息
            EditorGUILayout.LabelField("状态", statusMessage);

            // 转文字结果
            EditorGUILayout.LabelField("识别结果");
            transcribedText = EditorGUILayout.TextArea(transcribedText, GUILayout.Height(150));

            // 帮助信息
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(
                "快速开始：\n" +
                "1. 获取Azure Speech API密钥 (免费)\n" +
                "2. 填入API配置\n" +
                "3. 连接WebSocket\n" +
                "4. 开始录音说话\n\n" +
                "支持实时连续对话和手动转文字模式",
                MessageType.Info);

            if (GUILayout.Button("详细使用指南", GUILayout.Width(120), GUILayout.Height(60)))
            {
                ShowUsageGuide();
            }
            EditorGUILayout.EndHorizontal();
        }

        void StartRecording()
        {
            if (!isConnected)
            {
                statusMessage = "错误: 请先连接WebSocket";
                return;
            }

            audioBuffer.Clear();
            recordedClip = Microphone.Start(null, false, 10, SAMPLE_RATE);
            isRecording = true;
            statusMessage = "正在录音...";
            Debug.Log("开始录音");

            if (isContinuousMode)
            {
                // 开始实时发送音频数据
                SendAudioStreamAsync();
            }

            Repaint();
        }

        void StopRecording()
        {
            Microphone.End(null);
            isRecording = false;
            statusMessage = "录音完成";

            if (!isContinuousMode && recordedClip != null)
            {
                // 非连续模式：录音完成后立即转文字
                TranscribeAudio();
            }

            Debug.Log("录音完成，音频长度: " + (recordedClip != null ? recordedClip.length : 0) + "秒");
            Repaint();
        }

        async void SendAudioStreamAsync()
        {
            while (isRecording && isConnected)
            {
                if (recordedClip != null && Microphone.GetPosition(null) > 0)
                {
                    // 获取最新的音频数据
                    int position = Microphone.GetPosition(null);
                    float[] samples = new float[position];
                    recordedClip.GetData(samples, 0);

                    // 发送音频数据到WebSocket
                    SendAudioDataToWebSocket(samples);
                }

                await Task.Delay(100); // 每100ms发送一次
            }
        }

        async void ConnectWebSocket()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                statusMessage = "错误: 缺少API密钥";
                return;
            }

            try
            {
                statusMessage = "正在连接WebSocket...";
                Repaint();

                // 创建WebSocket连接
                string wsUrl = $"wss://{region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language={language}&format=detailed";
                webSocket = new ClientWebSocket();
                cancellationTokenSource = new CancellationTokenSource();

                // 添加认证头
                webSocket.Options.SetRequestHeader("Ocp-Apim-Subscription-Key", apiKey);
                webSocket.Options.SetRequestHeader("X-ConnectionId", connectionId);

                await webSocket.ConnectAsync(new Uri(wsUrl), cancellationTokenSource.Token);

                isConnected = true;
                statusMessage = "WebSocket已连接";

                // 开始监听消息
                StartListening();

                Debug.Log("WebSocket连接成功");
            }
            catch (Exception ex)
            {
                statusMessage = "连接失败: " + ex.Message;
                Debug.LogError("WebSocket连接失败: " + ex.Message);
                DisconnectWebSocket();
            }

            Repaint();
        }

        void DisconnectWebSocket()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }

            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
            }

            isConnected = false;
            statusMessage = "已断开连接";
            Repaint();
        }

        async void StartListening()
        {
            try
            {
                var buffer = new byte[4096];
                var segment = new ArraySegment<byte>(buffer);

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(segment, cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessWebSocketMessage(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("WebSocket连接已关闭");
                        isConnected = false;
                        statusMessage = "连接已关闭";
                        Repaint();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("WebSocket监听错误: " + ex.Message);
                isConnected = false;
                statusMessage = "连接错误";
                Repaint();
            }
        }

        void ProcessWebSocketMessage(string message)
        {
            try
            {
                // 解析Azure Speech Services的响应
                if (message.Contains("speech.phrase"))
                {
                    // 提取识别结果
                    var result = JsonUtility.FromJson<SpeechResult>(message);
                    if (result != null && result.NBest.Length > 0)
                    {
                        string text = result.NBest[0].Display;
                        if (!string.IsNullOrEmpty(text))
                        {
                            transcribedText += text + " ";
                            statusMessage = "识别成功";
                            Debug.Log("语音识别结果: " + text);
                            Repaint();
                        }
                    }
                }
                else if (message.Contains("speech.hypothesis"))
                {
                    // 临时识别结果（用于连续模式）
                    var hypothesis = JsonUtility.FromJson<SpeechHypothesis>(message);
                    if (hypothesis != null && !string.IsNullOrEmpty(hypothesis.Text))
                    {
                        statusMessage = "正在识别: " + hypothesis.Text;
                        Repaint();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("解析WebSocket消息失败: " + ex.Message);
            }
        }

        async void SendAudioDataToWebSocket(float[] samples)
        {
            if (!isConnected || webSocket.State != WebSocketState.Open)
                return;

            try
            {
                // 转换float数组为16位PCM
                byte[] pcmData = ConvertFloatToPCM16(samples);

                // 添加WAV头
                byte[] wavData = CreateWavHeader(pcmData);

                // 发送到WebSocket
                var segment = new ArraySegment<byte>(wavData);
                await webSocket.SendAsync(segment, WebSocketMessageType.Binary, true, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError("发送音频数据失败: " + ex.Message);
            }
        }

        [Serializable]
        public class SpeechResult
        {
            public RecognitionStatus RecognitionStatus;
            public int Offset;
            public int Duration;
            public NBestResult[] NBest;
        }

        [Serializable]
        public class RecognitionStatus
        {
            public string Status;
        }

        [Serializable]
        public class NBestResult
        {
            public string Display;
            public float Confidence;
        }

        [Serializable]
        public class SpeechHypothesis
        {
            public string Text;
            public int Offset;
            public int Duration;
        }

        async void TranscribeAudio()
        {
            if (recordedClip == null)
            {
                statusMessage = "错误: 没有录制的音频";
                return;
            }

            if (!isConnected)
            {
                statusMessage = "错误: WebSocket未连接";
                return;
            }

            try
            {
                statusMessage = "正在发送音频...";
                Repaint();

                // 获取音频数据
                float[] samples = new float[recordedClip.samples];
                recordedClip.GetData(samples, 0);

                // 发送音频数据
                SendAudioDataToWebSocket(samples);

                statusMessage = "音频已发送，等待识别结果...";
                Repaint();
            }
            catch (Exception ex)
            {
                statusMessage = "转文字失败: " + ex.Message;
                Debug.LogError("转文字失败: " + ex.Message);
                Repaint();
            }
        }

        byte[] ConvertFloatToPCM16(float[] samples)
        {
            byte[] pcmData = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = (short)(samples[i] * short.MaxValue);
                pcmData[i * 2] = (byte)(sample & 0xFF);
                pcmData[i * 2 + 1] = (byte)(sample >> 8);
            }
            return pcmData;
        }

        byte[] CreateWavHeader(byte[] pcmData)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                // WAV header
                stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
                stream.Write(BitConverter.GetBytes(36 + pcmData.Length), 0, 4);
                stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);
                stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);
                stream.Write(BitConverter.GetBytes(16), 0, 4);
                stream.Write(BitConverter.GetBytes((short)1), 0, 2); // PCM
                stream.Write(BitConverter.GetBytes((short)1), 0, 2); // Mono
                stream.Write(BitConverter.GetBytes(SAMPLE_RATE), 0, 4);
                stream.Write(BitConverter.GetBytes(SAMPLE_RATE * 2), 0, 4);
                stream.Write(BitConverter.GetBytes((short)2), 0, 2);
                stream.Write(BitConverter.GetBytes((short)16), 0, 2);
                stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4);
                stream.Write(BitConverter.GetBytes(pcmData.Length), 0, 4);

                // PCM data
                stream.Write(pcmData, 0, pcmData.Length);

                return stream.ToArray();
            }
        }

        }
    }

