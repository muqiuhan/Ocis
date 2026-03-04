from __future__ import annotations
from typing import (Any, Generic, TypeVar)
from .Ocis_Server.protocol_spec import (RequestPacket, ResponsePacket)
from .fable_modules.fable_library.array_ import (get_sub_array, copy_to)
from .fable_modules.fable_library.encoding import get_utf8
from .fable_modules.fable_library.option import (default_arg, map)
from .fable_modules.fable_library.reflection import (TypeInfo, string_type, union_type)
from .fable_modules.fable_library.string_ import (to_text, printf)
from .fable_modules.fable_library.types import (uint32, uint8, Array, Union)
from .binary import (read_uint32little_endian, read_byte, read_int32little_endian, write_uint32little_endian, write_byte, write_int32little_endian)

_T = TypeVar("_T")

def TryParseRequestHeader(buffer: bytearray) -> RequestPacket | None:
    if len(buffer) < 18:
        return None

    else: 
        try: 
            magic_number: uint32 = read_uint32little_endian(buffer, 0)
            version: uint8 = read_byte(buffer, 4)
            return RequestPacket(magic_number, version, int(read_byte(buffer, 5)), read_int32little_endian(buffer, 6), read_int32little_endian(buffer, 10), read_int32little_endian(buffer, 14), bytearray([]), None) if ((version == uint8(1)) if (magic_number == uint32(1397310287)) else False) else None

        except Exception as match_value:
            return None




def TryParseRequestPacket(buffer: bytearray) -> RequestPacket | None:
    match_value: RequestPacket | None = TryParseRequestHeader(buffer)
    if match_value is None:
        return None

    else: 
        header: RequestPacket = match_value
        if len(buffer) >= header.TotalPacketLength:
            try: 
                return RequestPacket(header.MagicNumber, header.Version, header.CommandType, header.TotalPacketLength, header.KeyLength, header.ValueLength, get_sub_array(buffer, 18, header.KeyLength), get_sub_array(buffer, 18 + header.KeyLength, header.ValueLength) if (header.ValueLength > 0) else None)

            except Exception as match_value_1:
                return None


        else: 
            return None




def SerializeRequest(packet: RequestPacket) -> bytearray:
    key_len: int = len(packet.Key) or 0
    def mapping(v: bytearray, packet: Any=packet) -> int:
        return len(v)

    buffer: bytearray = [0] * ((18 + key_len) + default_arg(map(mapping, packet.Value), 0))
    offset: int = 0
    write_uint32little_endian(packet.MagicNumber, buffer, offset)
    offset = (offset + 4) or 0
    write_byte(packet.Version, buffer, offset)
    offset = (offset + 1) or 0
    write_byte(int(packet.CommandType+0x100 if packet.CommandType < 0 else packet.CommandType) & 0xFF, buffer, offset)
    offset = (offset + 1) or 0
    write_int32little_endian(packet.TotalPacketLength, buffer, offset)
    offset = (offset + 4) or 0
    write_int32little_endian(packet.KeyLength, buffer, offset)
    offset = (offset + 4) or 0
    write_int32little_endian(packet.ValueLength, buffer, offset)
    offset = (offset + 4) or 0
    if key_len > 0:
        copy_to(packet.Key, 0, buffer, offset, key_len)
        offset = (offset + key_len) or 0

    match_value: bytearray | None = packet.Value
    if match_value is None:
        pass

    else: 
        value_1: bytearray = match_value
        copy_to(value_1, 0, buffer, offset, len(value_1))

    return buffer


def CreateSuccessResponse(value: bytearray | None=None) -> ResponsePacket:
    pattern_input: tuple[int, int]
    if value is None:
        pattern_input = (0, 18)

    else: 
        v: bytearray = value
        pattern_input = (len(v), 18 + len(v))

    return ResponsePacket(uint32(1397310287), uint8(1), uint8(0), pattern_input[1], pattern_input[0], 0, value, None)


def CreateNotFoundResponse(__unit: None=None) -> ResponsePacket:
    return ResponsePacket(uint32(1397310287), uint8(1), uint8(1), 18, 0, 0, None, None)


def CreateErrorResponse(error_message: str) -> ResponsePacket:
    msg_bytes: bytearray = get_utf8().get_bytes(error_message)
    return ResponsePacket(uint32(1397310287), uint8(1), uint8(2), 18 + len(msg_bytes), 0, len(msg_bytes), None, error_message)


def IsValidPacketSize(total_length: int) -> bool:
    if total_length >= 18:
        return total_length <= ((10 * 1024) * 1024)

    else: 
        return False



def SerializeResponse(packet: ResponsePacket) -> bytearray:
    def mapping(v: bytearray, packet: Any=packet) -> int:
        return len(v)

    def mapping_1(m: str, packet: Any=packet) -> int:
        return len(get_utf8().get_bytes(m))

    buffer: bytearray = [0] * ((18 + default_arg(map(mapping, packet.Value), 0)) + default_arg(map(mapping_1, packet.ErrorMessage), 0))
    offset: int = 0
    write_uint32little_endian(packet.MagicNumber, buffer, offset)
    offset = (offset + 4) or 0
    write_byte(packet.Version, buffer, offset)
    offset = (offset + 1) or 0
    write_byte(packet.StatusCode, buffer, offset)
    offset = (offset + 1) or 0
    write_int32little_endian(packet.TotalPacketLength, buffer, offset)
    offset = (offset + 4) or 0
    write_int32little_endian(packet.ValueLength, buffer, offset)
    offset = (offset + 4) or 0
    write_int32little_endian(packet.ErrorMessageLength, buffer, offset)
    offset = (offset + 4) or 0
    match_value: bytearray | None = packet.Value
    if match_value is None:
        pass

    else: 
        value_2: bytearray = match_value
        copy_to(value_2, 0, buffer, offset, len(value_2))
        offset = (offset + len(value_2)) or 0

    match_value_1: str | None = packet.ErrorMessage
    if match_value_1 is None:
        pass

    else: 
        msg: str = match_value_1
        msg_bytes: bytearray = get_utf8().get_bytes(msg)
        copy_to(msg_bytes, 0, buffer, offset, len(msg_bytes))

    return buffer


def _expr3(gen0: TypeInfo) -> TypeInfo:
    return union_type("Ocis.Client.SDK.Protocol.ParseResult`1", [gen0], ParseResult_1, lambda: [[("Item", gen0)], [("Item", string_type)], []])


class ParseResult_1(Union, Generic[_T]):
    def __init__(self, tag: int, *fields: Any) -> None:
        super().__init__()
        self.tag: int = tag or 0
        self.fields: Array[Any] = list(fields)

    @staticmethod
    def cases() -> list[str]:
        return ["ParseSuccess", "ParseError", "InsufficientData"]


ParseResult_1_reflection = _expr3

def DeserializeResponse(buffer: bytearray) -> ParseResult_1[ResponsePacket]:
    try: 
        if len(buffer) < 18:
            return ParseResult_1(2)

        else: 
            offset: int = 0
            magic_number: uint32 = read_uint32little_endian(buffer, offset)
            offset = (offset + 4) or 0
            version: uint8 = read_byte(buffer, offset)
            offset = (offset + 1) or 0
            status_code_byte: uint8 = read_byte(buffer, offset)
            offset = (offset + 1) or 0
            total_packet_length: int = read_int32little_endian(buffer, offset) or 0
            offset = (offset + 4) or 0
            value_length: int = read_int32little_endian(buffer, offset) or 0
            offset = (offset + 4) or 0
            error_message_length: int = read_int32little_endian(buffer, offset) or 0
            offset = (offset + 4) or 0
            if len(buffer) < total_packet_length:
                return ParseResult_1(2)

            elif not ((version == uint8(1)) if (magic_number == uint32(1397310287)) else False):
                return ParseResult_1(1, "Invalid header")

            elif True if (value_length < 0) else (error_message_length < 0):
                return ParseResult_1(1, "Invalid length field")

            elif total_packet_length != ((18 + value_length) + error_message_length):
                return ParseResult_1(1, "Packet length mismatch")

            else: 
                value: bytearray | None = get_sub_array(buffer, offset, value_length) if (value_length > 0) else None
                offset = (offset + value_length) or 0
                def _arrow5(__unit: None=None) -> str | None:
                    error_bytes: bytearray = get_sub_array(buffer, offset, error_message_length)
                    return get_utf8().get_string(error_bytes)

                return ParseResult_1(0, ResponsePacket(magic_number, version, status_code_byte, total_packet_length, value_length, error_message_length, value, _arrow5() if (error_message_length > 0) else None))



    except Exception as ex:
        def _arrow4(__unit: None=None) -> str:
            arg: str = str(ex)
            return to_text(printf("Error parsing response: %s"))(arg)

        return ParseResult_1(1, _arrow4())



__all__ = ["TryParseRequestHeader", "TryParseRequestPacket", "SerializeRequest", "CreateSuccessResponse", "CreateNotFoundResponse", "CreateErrorResponse", "IsValidPacketSize", "SerializeResponse", "ParseResult_1_reflection", "DeserializeResponse"]

