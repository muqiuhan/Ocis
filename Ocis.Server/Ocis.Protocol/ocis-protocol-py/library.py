from __future__ import annotations
from typing import (Any, Generic, TypeVar)
from ..protocol_spec import (RequestPacket, ResponsePacket)
from .fable_modules.fable_library.array_ import (concat, get_sub_array)
from .fable_modules.fable_library.bit_converter import (get_bytes_uint32, get_bytes_int32, to_uint32, to_int32)
from .fable_modules.fable_library.encoding import get_utf8
from .fable_modules.fable_library.reflection import (enum_type, TypeInfo, string_type, union_type)
from .fable_modules.fable_library.string_ import (to_text, printf)
from .fable_modules.fable_library.types import (uint32, uint8, Array, Uint8Array, Union)

_T = TypeVar("_T")

def CreateRequest(command_type: enum_type("Ocis.Server.ProtocolSpec.CommandType", int, [("Set", 1.0), ("Get", 2.0), ("Delete", 3.0)]), key: bytearray, value: bytearray | None=None) -> RequestPacket:
    value_len: int = (0 if (value is None) else len(value)) or 0
    return RequestPacket(uint32(1397310287), uint8(1), command_type, (18 + len(key)) + value_len, len(key), value_len, key, value)


def SerializeRequest(packet: RequestPacket) -> bytearray:
    parts: Array[bytearray] = []
    (parts.append(get_bytes_uint32(packet.MagicNumber)))
    (parts.append(bytearray([packet.Version])))
    (parts.append(bytearray([int(packet.CommandType+0x100 if packet.CommandType < 0 else packet.CommandType) & 0xFF])))
    (parts.append(get_bytes_int32(packet.TotalPacketLength)))
    (parts.append(get_bytes_int32(packet.KeyLength)))
    (parts.append(get_bytes_int32(packet.ValueLength)))
    (parts.append(packet.Key))
    match_value: bytearray | None = packet.Value
    if match_value is None:
        pass

    else: 
        value: bytearray = match_value
        (parts.append(value))

    return concat(parts[:], Uint8Array)


def _expr2(gen0: TypeInfo) -> TypeInfo:
    return union_type("Ocis.Protocol.ParseResult`1", [gen0], ParseResult_1, lambda: [[("Item", gen0)], [("Item", string_type)], []])


class ParseResult_1(Union, Generic[_T]):
    def __init__(self, tag: int, *fields: Any) -> None:
        super().__init__()
        self.tag: int = tag or 0
        self.fields: Array[Any] = list(fields)

    @staticmethod
    def cases() -> list[str]:
        return ["ParseSuccess", "ParseError", "InsufficientData"]


ParseResult_1_reflection = _expr2

def DeserializeResponse(buffer: bytearray) -> ParseResult_1[ResponsePacket]:
    try: 
        if len(buffer) < 18:
            return ParseResult_1(2)

        else: 
            offset: int = 0
            magic_number: uint32 = to_uint32(buffer, offset)
            offset = (offset + 4) or 0
            version: uint8 = buffer[offset]
            offset = (offset + 1) or 0
            status_code: enum_type("Ocis.Server.ProtocolSpec.StatusCode", uint8, [("Success", 0.0), ("NotFound", 1.0), ("Error", 2.0)]) = buffer[offset]
            offset = (offset + 1) or 0
            total_packet_length: int = to_int32(buffer, offset)
            offset = (offset + 4) or 0
            value_length: int = to_int32(buffer, offset)
            offset = (offset + 4) or 0
            error_message_length: int = to_int32(buffer, offset)
            offset = (offset + 4) or 0
            if len(buffer) < total_packet_length:
                return ParseResult_1(2)

            elif not ((version == uint8(1)) if (magic_number == uint32(1397310287)) else False):
                return ParseResult_1(1, "invalid header")

            elif True if (value_length < 0) else (error_message_length < 0):
                return ParseResult_1(1, "invalid length field")

            elif total_packet_length != ((18 + value_length) + error_message_length):
                return ParseResult_1(1, "packet length mismatch")

            else: 
                value: bytearray | None = get_sub_array(buffer, offset, value_length) if (value_length > 0) else None
                offset = (offset + value_length) or 0
                def _arrow4(__unit: None=None) -> str | None:
                    error_bytes: bytearray = get_sub_array(buffer, offset, error_message_length)
                    return get_utf8().get_string(error_bytes)

                return ParseResult_1(0, ResponsePacket(magic_number, version, status_code, total_packet_length, value_length, error_message_length, value, _arrow4() if (error_message_length > 0) else None))



    except Exception as ex:
        def _arrow3(__unit: None=None) -> str:
            arg: str = str(ex)
            return to_text(printf("error parsing response: %s"))(arg)

        return ParseResult_1(1, _arrow3())



def ProtocolHelper_stringToBytes(s: str) -> bytearray:
    return get_utf8().get_bytes(s)


def ProtocolHelper_bytesToString(bytes: bytearray) -> str:
    return get_utf8().get_string(bytes)


def ProtocolHelper_createSetRequest(key: str, value: str) -> RequestPacket:
    return CreateRequest(1, ProtocolHelper_stringToBytes(key), ProtocolHelper_stringToBytes(value))


def ProtocolHelper_createGetRequest(key: str) -> RequestPacket:
    return CreateRequest(2, ProtocolHelper_stringToBytes(key), None)


def ProtocolHelper_createDeleteRequest(key: str) -> RequestPacket:
    return CreateRequest(3, ProtocolHelper_stringToBytes(key), None)


__all__ = ["CreateRequest", "SerializeRequest", "ParseResult_1_reflection", "DeserializeResponse", "ProtocolHelper_stringToBytes", "ProtocolHelper_bytesToString", "ProtocolHelper_createSetRequest", "ProtocolHelper_createGetRequest", "ProtocolHelper_createDeleteRequest"]

