using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;

/// <summary>
/// WebSocket network manager.
/// Default url: ws://10.252.0.161:8000/ws
///
/// Packet format (little-endian):
/// 1) int totalLength = 4(msgId) + payloadLength
/// 2) int msgId
/// 3) byte[] payload (protobuf bytes)
/// </summary>
public class NetworkManager : Singleton<NetworkManager>
{
    public const string DefaultWsUrl = "ws://10.252.0.161:8000/ws";

    private WebSocket _socket;
    private readonly Dictionary<int, Action<byte[]>> _messageHandlers = new Dictionary<int, Action<byte[]>>();

    public bool IsConnected => _socket != null && _socket.State == WebSocketState.Open;
    public string CurrentUrl { get; private set; } = DefaultWsUrl;

    public event Action OnConnected;
    public event Action<int, string> OnDisconnected;
    public event Action<string> OnError;
    public event Action<int, byte[]> OnRawMessage;

    private void Update()
    {
        if (_socket == null)
        {
            return;
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        _socket.DispatchMessageQueue();
#endif
    }

    private async void OnDestroy()
    {
        await DisconnectAsync();
    }

    /// <summary>
    /// Connect to websocket server.
    /// </summary>
    public async Task<bool> ConnectAsync(string url = DefaultWsUrl, int timeoutSeconds = 10)
    {
        if (IsConnected)
        {
            return true;
        }

        await DisconnectAsync();

        CurrentUrl = string.IsNullOrWhiteSpace(url) ? DefaultWsUrl : url;
        _socket = new WebSocket(CurrentUrl);
        bool isOpen = false;
        string errorMsg = string.Empty;
        var openTcs = new TaskCompletionSource<bool>();
        var failTcs = new TaskCompletionSource<string>();

        _socket.OnOpen += () =>
        {
            isOpen = true;
            if (!openTcs.Task.IsCompleted)
            {
                openTcs.TrySetResult(true);
            }
            OnConnected?.Invoke();
            Debug.Log("网络连接成功: " + CurrentUrl);
        };

        _socket.OnMessage += (bytes) =>
        {
            Debug.Log("[NetworkManager] Received raw bytes, len=" + (bytes == null ? 0 : bytes.Length) + ", hex=" + ToHexPreview(bytes));
            DispatchPacket(bytes);
        };

        _socket.OnError += (error) =>
        {
            errorMsg = error;
            if (!failTcs.Task.IsCompleted)
            {
                failTcs.TrySetResult("Socket error: " + error);
            }
            HandleError("Socket error: " + error);
        };

        _socket.OnClose += (code) =>
        {
            if (!isOpen && !failTcs.Task.IsCompleted)
            {
                failTcs.TrySetResult("Socket closed before open, code=" + code);
            }
            HandleDisconnect((int)code, "socket closed");
        };

        try
        {
            Task connectTask = _socket.Connect();
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            Task finished = await Task.WhenAny(openTcs.Task, failTcs.Task, timeoutTask);

            if (finished == timeoutTask)
            {
                HandleError("Connect timeout.");
                await DisconnectAsync();
                return false;
            }

            if (finished == failTcs.Task)
            {
                string failReason = await failTcs.Task;
                HandleError("Connect failed: " + failReason);
                await DisconnectAsync();
                return false;
            }

            // Ensure connect task has completed cleanly after OnOpen.
            await connectTask;
            return IsConnected;
        }
        catch (Exception ex)
        {
            HandleError("Connect failed: " + ex.Message);
            await DisconnectAsync();
            return false;
        }
    }

    /// <summary>
    /// Disconnect actively.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_socket != null)
        {
            try
            {
                if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.Connecting)
                {
                    await _socket.Close();
                }
            }
            catch
            {
                // Ignore close exceptions during shutdown.
            }
            finally
            {
                _socket = null;
            }
        }
    }

    /// <summary>
    /// Send protobuf message.
    /// </summary>
    public async Task<bool> SendPbAsync<T>(int msgId, T message)
    {
        if (!IsConnected)
        {
            HandleError("Send failed: websocket not connected.");
            return false;
        }

        try
        {
            byte[] payload = PbCodec.Serialize(message);
            byte[] packet = BuildPacket(msgId, payload);
            await _socket.Send(packet);
            return true;
        }
        catch (Exception ex)
        {
            HandleError("Send failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Register protobuf message handler for msgId.
    /// </summary>
    public void RegisterPbHandler<T>(int msgId, Action<T> handler)
    {
        if (handler == null)
        {
            _messageHandlers.Remove(msgId);
            return;
        }

        _messageHandlers[msgId] = (rawBytes) =>
        {
            T obj = PbCodec.Deserialize<T>(rawBytes);
            handler.Invoke(obj);
        };
    }

    public void UnregisterHandler(int msgId)
    {
        _messageHandlers.Remove(msgId);
    }

    private static byte[] BuildPacket(int msgId, byte[] payload)
    {
        payload ??= Array.Empty<byte>();
        int totalLength = 4 + payload.Length;

        byte[] packet = new byte[4 + totalLength];
        Buffer.BlockCopy(BitConverter.GetBytes(totalLength), 0, packet, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(msgId), 0, packet, 4, 4);
        Buffer.BlockCopy(payload, 0, packet, 8, payload.Length);
        return packet;
    }

    private void DispatchPacket(byte[] packet)
    {
        if (packet == null || packet.Length < 8)
        {
            HandleError("Invalid packet length, len=" + (packet == null ? 0 : packet.Length) + ", textPreview=" + TryUtf8Preview(packet));
            return;
        }

        int totalLength = BitConverter.ToInt32(packet, 0);
        if (totalLength != packet.Length - 4)
        {
            HandleError("Packet totalLength mismatch, totalLength=" + totalLength + ", actual=" + (packet.Length - 4) + ", textPreview=" + TryUtf8Preview(packet));
            return;
        }

        int msgId = BitConverter.ToInt32(packet, 4);
        int payloadLen = totalLength - 4;
        byte[] payload = new byte[payloadLen];
        if (payloadLen > 0)
        {
            Buffer.BlockCopy(packet, 8, payload, 0, payloadLen);
        }

        Debug.Log("[NetworkManager] Received packet, msgId=" + msgId + ", payloadLen=" + payloadLen + ", payloadHex=" + ToHexPreview(payload) + ", payloadText=" + TryUtf8Preview(payload));
        OnRawMessage?.Invoke(msgId, payload);
        if (_messageHandlers.TryGetValue(msgId, out Action<byte[]> callback))
        {
            try
            {
                callback.Invoke(payload);
            }
            catch (Exception ex)
            {
                Debug.LogError("[NetworkManager] Handler exception, msgId=" + msgId + " ex=" + ex);
            }
        }
    }

    private void HandleDisconnect(int closeCode, string reason)
    {
        OnDisconnected?.Invoke(closeCode, reason);
        Debug.LogWarning("[NetworkManager] Disconnected, code=" + closeCode + ", reason=" + reason);
    }

    private void HandleError(string error)
    {
        OnError?.Invoke(error);
        Debug.LogError("[NetworkManager] " + error);
    }

    private static string ToHexPreview(byte[] bytes, int maxBytes = 32)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return "<empty>";
        }

        int len = Math.Min(bytes.Length, maxBytes);
        StringBuilder sb = new StringBuilder(len * 3 + 16);
        for (int i = 0; i < len; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(bytes[i].ToString("X2"));
        }

        if (bytes.Length > len)
        {
            sb.Append(" ...");
        }

        return sb.ToString();
    }

    private static string TryUtf8Preview(byte[] bytes, int maxChars = 80)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return "<empty>";
        }

        try
        {
            string text = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrEmpty(text))
            {
                return "<empty>";
            }

            if (text.Length > maxChars)
            {
                return text.Substring(0, maxChars) + "...";
            }

            return text;
        }
        catch
        {
            return "<not-utf8>";
        }
    }
}

/// <summary>
/// Protobuf codec bridge.
/// Register concrete codec in game layer (e.g. protobuf-net / Google.Protobuf).
/// </summary>
public static class PbCodec
{
    private static Func<object, byte[]> _serializer;
    private static Func<Type, byte[], object> _deserializer;

    /// <summary>
    /// Register protobuf serializer and deserializer.
    /// serializer: object -> byte[]
    /// deserializer: (Type, byte[]) -> object
    /// </summary>
    public static void Register(Func<object, byte[]> serializer, Func<Type, byte[], object> deserializer)
    {
        _serializer = serializer;
        _deserializer = deserializer;
    }

    public static byte[] Serialize<T>(T obj)
    {
        if (_serializer == null)
        {
            throw new InvalidOperationException("PbCodec serializer not registered.");
        }

        return _serializer.Invoke(obj);
    }

    public static T Deserialize<T>(byte[] data)
    {
        if (_deserializer == null)
        {
            throw new InvalidOperationException("PbCodec deserializer not registered.");
        }

        object obj = _deserializer.Invoke(typeof(T), data);
        if (obj == null)
        {
            return default;
        }

        return (T)obj;
    }
}
