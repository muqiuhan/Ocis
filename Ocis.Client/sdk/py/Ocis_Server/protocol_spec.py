from dataclasses import dataclass
from ..fable_modules.fable_library.reflection import (enum_type, TypeInfo, uint32_type, uint8_type, int32_type, array_type, option_type, record_type, string_type)
from ..fable_modules.fable_library.types import (Record, uint8, uint32)

def _expr0() -> TypeInfo:
    return record_type("Ocis.Server.ProtocolSpec.RequestPacket", [], RequestPacket, lambda: [("MagicNumber", uint32_type), ("Version", uint8_type), ("CommandType", enum_type("Ocis.Server.ProtocolSpec.CommandType", int32_type, [("Set", 1.0), ("Get", 2.0), ("Delete", 3.0)])), ("TotalPacketLength", int32_type), ("KeyLength", int32_type), ("ValueLength", int32_type), ("Key", array_type(uint8_type)), ("Value", option_type(array_type(uint8_type)))])


@dataclass(eq = False, repr = False, slots = True)
class RequestPacket(Record):
    MagicNumber: uint32
    Version: uint8
    CommandType: enum_type("Ocis.Server.ProtocolSpec.CommandType", int, [("Set", 1.0), ("Get", 2.0), ("Delete", 3.0)])
    TotalPacketLength: int
    KeyLength: int
    ValueLength: int
    Key: bytearray
    Value: bytearray | None

RequestPacket_reflection = _expr0

def _expr1() -> TypeInfo:
    return record_type("Ocis.Server.ProtocolSpec.ResponsePacket", [], ResponsePacket, lambda: [("MagicNumber", uint32_type), ("Version", uint8_type), ("StatusCode", enum_type("Ocis.Server.ProtocolSpec.StatusCode", uint8_type, [("Success", 0.0), ("NotFound", 1.0), ("Error", 2.0)])), ("TotalPacketLength", int32_type), ("ValueLength", int32_type), ("ErrorMessageLength", int32_type), ("Value", option_type(array_type(uint8_type))), ("ErrorMessage", option_type(string_type))])


@dataclass(eq = False, repr = False, slots = True)
class ResponsePacket(Record):
    MagicNumber: uint32
    Version: uint8
    StatusCode: enum_type("Ocis.Server.ProtocolSpec.StatusCode", uint8, [("Success", 0.0), ("NotFound", 1.0), ("Error", 2.0)])
    TotalPacketLength: int
    ValueLength: int
    ErrorMessageLength: int
    Value: bytearray | None
    ErrorMessage: str | None

ResponsePacket_reflection = _expr1

__all__ = ["RequestPacket_reflection", "ResponsePacket_reflection"]

