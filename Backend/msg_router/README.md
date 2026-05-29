# 消息目录约定

- `proto/common/`：公共协议（例如 `WsEnvelope`）
- `proto/messages/`：业务实体消息
- `msg_router/msg_id_routes.py`：`msgId -> 处理函数/API/proto` 映射表

## 前端上行请求体（WS）

`proto/common/ws_client_request.proto`

```proto
message WsClientRequest {
  int32 msgId = 1;
  bytes data = 3;
}
```

## 当前接口

- WebSocket 连接：`ws://<host>:8000/ws`
- 主动推送接口：`POST /api/push-pb`

## 推送请求体

```json
{
  "msg_id": 2001,
  "error_code": 0,
  "data_base64": "...."
}
```

`data_base64` 是 `Envelope.data` 的实体 PB 字节，Base64 编码。
