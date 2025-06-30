from __future__ import annotations
from ..protocol_spec import RequestPacket
from .fable_modules.fable_library.array_ import (get_sub_array, concat)
from .fable_modules.fable_library.bit_converter import (to_uint32, to_int32, get_bytes_uint32, get_bytes_int32)
from .fable_modules.fable_library.reflection import enum_type
from .fable_modules.fable_library.types import (uint32, uint8, Array, Uint8Array)

def TryParseRequestHeader(buffer: bytearray) -> RequestPacket | None:
    if len(buffer) < 18:
        return None

    else: 
        try: 
            offset: int = 0
            magic_number: uint32 = to_uint32(buffer, offset)
            offset = (offset + 4) or 0
            version: uint8 = buffer[offset]
            offset = (offset + 1) or 0
            command_type: enum_type("Ocis.Server.ProtocolSpec.CommandType", int, [("Set", 1.0), ("Get", 2.0), ("Delete", 3.0)]) = int(buffer[offset]) or 0
            offset = (offset + 1) or 0
            total_packet_length: int = to_int32(buffer, offset)
            offset = (offset + 4) or 0
            key_length: int = to_int32(buffer, offset)
            offset = (offset + 4) or 0
            return RequestPacket(magic_number, version, command_type, total_packet_length, key_length, to_int32(buffer, offset), bytearray([]), None) if ((version == uint8(1)) if (magic_number == uint32(1397310287)) else False) else None

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
                offset: int = 18
                key: bytearray = get_sub_array(buffer, offset, header.KeyLength)
                offset = (offset + header.KeyLength) or 0
                return RequestPacket(header.MagicNumber, header.Version, header.CommandType, header.TotalPacketLength, header.KeyLength, header.ValueLength, key, get_sub_array(buffer, offset, header.ValueLength) if (header.ValueLength > 0) else None)

            except Exception as match_value_1:
                return None


        else: 
            return None




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


__all__ = ["TryParseRequestHeader", "TryParseRequestPacket", "CreateRequest", "SerializeRequest"]

