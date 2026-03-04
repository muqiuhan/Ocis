from __future__ import annotations
from typing import Any
from .Ocis_Server.protocol_spec import RequestPacket
from .fable_modules.fable_library.encoding import get_utf8
from .fable_modules.fable_library.option import (default_arg, map)
from .fable_modules.fable_library.reflection import enum_type
from .fable_modules.fable_library.types import (uint32, uint8)
from .protocol import SerializeRequest

def create_packet(command_type: enum_type("Ocis.Server.ProtocolSpec.CommandType", int, [("Set", 1.0), ("Get", 2.0), ("Delete", 3.0)]), key: str, value: bytearray | None=None) -> RequestPacket:
    key_bytes: bytearray = get_utf8().get_bytes(key)
    key_len: int = len(key_bytes) or 0
    def mapping(v: bytearray, command_type: Any=command_type, key: Any=key, value: Any=value) -> int:
        return len(v)

    value_len: int = default_arg(map(mapping, value), 0) or 0
    return RequestPacket(uint32(1397310287), uint8(1), command_type, (18 + key_len) + value_len, key_len, value_len, key_bytes, value)


def create_set_request(key: str, value: bytearray) -> bytearray:
    return SerializeRequest(create_packet(1, key, value))


def create_get_request(key: str) -> bytearray:
    return SerializeRequest(create_packet(2, key, None))


def create_delete_request(key: str) -> bytearray:
    return SerializeRequest(create_packet(3, key, None))


__all__ = ["create_packet", "create_set_request", "create_get_request", "create_delete_request"]

