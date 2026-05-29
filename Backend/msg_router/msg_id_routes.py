"""
msgId 路由配置中心：
- client_handler: 前端通过 WS 上行消息后，后端按 msgId 调用的函数
- push_api: 后端主动推送给前端时使用的 HTTP 接口
- data_proto: Envelope.data 对应的实体 proto 类型
"""

MSG_ID_ROUTES: dict[int, dict[str, str]] = {
    1001: {
        "name": "Ping",
        "client_handler": "main.handle_client_ping",
        "push_api": "POST /api/push-pb",
        "data_proto": "proto/messages/ping_message.proto#ws.PingMessage",
    },
    2001: {
        "name": "Notice",
        "client_handler": "main.handle_client_notice",
        "push_api": "POST /api/push-pb",
        "data_proto": "proto/messages/notice_message.proto#ws.NoticeMessage",
    },
}
