import base64
import binascii
import logging
import struct
from collections.abc import Awaitable, Callable

from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from google.protobuf import descriptor_pb2, descriptor_pool, message_factory
from pydantic import BaseModel, Field


app = FastAPI(title="WebSocket Backend Service")
logger = logging.getLogger("websocket-service")
logging.basicConfig(level=logging.INFO, format="%(message)s")

WsHandler = Callable[[str, int, bytes], Awaitable[None]]


class ConnectionManager:
    def __init__(self) -> None:
        self.active_connections: list[WebSocket] = []

    @property
    def online_count(self) -> int:
        return len(self.active_connections)

    async def connect(self, websocket: WebSocket) -> None:
        await websocket.accept()
        self.active_connections.append(websocket)

    def disconnect(self, websocket: WebSocket) -> None:
        if websocket in self.active_connections:
            self.active_connections.remove(websocket)

    async def broadcast_bytes(self, payload: bytes) -> int:
        sent_count = 0
        for connection in list(self.active_connections):
            try:
                await connection.send_bytes(payload)
                sent_count += 1
            except Exception:
                self.disconnect(connection)
        return sent_count


manager = ConnectionManager()


def get_client_identity(websocket: WebSocket) -> str:
    client = websocket.client
    if client is None:
        return "unknown-client"
    return f"{client.host}:{client.port}"


def build_ws_envelope_class():
    file_proto = descriptor_pb2.FileDescriptorProto()
    file_proto.name = "ws_envelope.proto"
    file_proto.package = "ws"

    message_proto = file_proto.message_type.add()
    message_proto.name = "WsEnvelope"

    msg_id_field = message_proto.field.add()
    msg_id_field.name = "msgId"
    msg_id_field.number = 1
    msg_id_field.label = descriptor_pb2.FieldDescriptorProto.LABEL_OPTIONAL
    msg_id_field.type = descriptor_pb2.FieldDescriptorProto.TYPE_INT32

    error_code_field = message_proto.field.add()
    error_code_field.name = "errorCode"
    error_code_field.number = 2
    error_code_field.label = descriptor_pb2.FieldDescriptorProto.LABEL_OPTIONAL
    error_code_field.type = descriptor_pb2.FieldDescriptorProto.TYPE_INT32

    data_field = message_proto.field.add()
    data_field.name = "data"
    data_field.number = 3
    data_field.label = descriptor_pb2.FieldDescriptorProto.LABEL_OPTIONAL
    data_field.type = descriptor_pb2.FieldDescriptorProto.TYPE_BYTES

    pool = descriptor_pool.DescriptorPool()
    pool.Add(file_proto)
    descriptor = pool.FindMessageTypeByName("ws.WsEnvelope")
    return message_factory.GetMessageClass(descriptor)


WsEnvelope = build_ws_envelope_class()


class PushEnvelopeRequest(BaseModel):
    msg_id: int = Field(..., description="消息号")
    error_code: int = Field(0, description="错误码，0=成功")
    data_base64: str | None = Field(None, description="实体PB字节，Base64编码")
    data_text: str | None = Field(None, description="调试用字符串数据，会转成UTF-8字节")


def build_unity_packet(msg_id: int, payload: bytes) -> bytes:
    total_length = 4 + len(payload)
    return struct.pack("<ii", total_length, msg_id) + payload


def parse_unity_packet(packet: bytes) -> tuple[int, bytes]:
    if len(packet) < 8:
        raise ValueError("packet length < 8")

    total_length, msg_id = struct.unpack_from("<ii", packet, 0)
    if total_length != len(packet) - 4:
        raise ValueError("packet totalLength mismatch")

    payload = packet[8:]
    return msg_id, payload


def resolve_data_bytes(request: PushEnvelopeRequest) -> bytes:
    if request.data_base64:
        try:
            return base64.b64decode(request.data_base64, validate=True)
        except binascii.Error as exc:
            raise ValueError("data_base64 非法，无法解码") from exc
    if request.data_text is not None:
        return request.data_text.encode("utf-8")
    return b""


async def handle_client_ping(client: str, msg_id: int, data: bytes) -> None:
    logger.info("%s -> msgId=%s ping, data_len=%s", client, msg_id, len(data))


async def handle_client_notice(client: str, msg_id: int, data: bytes) -> None:
    logger.info("%s -> msgId=%s notice, data_len=%s", client, msg_id, len(data))


WS_MSG_HANDLERS: dict[int, WsHandler] = {
    1001: handle_client_ping,
    2001: handle_client_notice,
}


@app.post("/api/push-pb")
async def push_pb_message(request: PushEnvelopeRequest) -> dict[str, int]:
    entity_data = resolve_data_bytes(request)
    envelope = WsEnvelope(msgId=request.msg_id, errorCode=request.error_code, data=entity_data)
    payload = envelope.SerializeToString()
    packet = build_unity_packet(request.msg_id, payload)
    sent = await manager.broadcast_bytes(packet)
    logger.info("推送成功 msgId=%s errorCode=%s sent=%s", request.msg_id, request.error_code, sent)
    return {"sent": sent, "bytes": len(packet), "online": manager.online_count}


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket) -> None:
    client_identity = get_client_identity(websocket)
    await manager.connect(websocket)
    logger.info("%s 连接成功", client_identity)
    try:
        while True:
            event = await websocket.receive()
            packet = event.get("bytes")
            if not packet:
                continue

            try:
                header_msg_id, payload = parse_unity_packet(packet)
                envelope = WsEnvelope()
                envelope.ParseFromString(payload)
            except Exception as exc:
                logger.info("%s 上行包解析失败: %s", client_identity, exc)
                continue

            route_msg_id = int(envelope.msgId) if int(envelope.msgId) != 0 else header_msg_id
            handler = WS_MSG_HANDLERS.get(route_msg_id)
            if handler is None:
                logger.info("%s 未注册处理器 msgId=%s", client_identity, route_msg_id)
                continue

            await handler(client_identity, route_msg_id, bytes(envelope.data))
    except WebSocketDisconnect:
        manager.disconnect(websocket)
        logger.info("%s 断开连接", client_identity)
