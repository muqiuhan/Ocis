from __future__ import annotations
from typing import (Any, Generic, TypeVar)
from .Ocis_Server.protocol_spec import ResponsePacket
from .fable_modules.fable_library.option import default_arg
from .fable_modules.fable_library.reflection import (TypeInfo, string_type, union_type, enum_type)
from .fable_modules.fable_library.types import (Array, Union, uint8)
from .protocol import (DeserializeResponse, ParseResult_1)

_T = TypeVar("_T")

def _expr2(gen0: TypeInfo) -> TypeInfo:
    return union_type("Ocis.Client.SDK.Response.ClientResult`1", [gen0], ClientResult_1, lambda: [[("Item", gen0)], [], [("Item", string_type)]])


class ClientResult_1(Union, Generic[_T]):
    def __init__(self, tag: int, *fields: Any) -> None:
        super().__init__()
        self.tag: int = tag or 0
        self.fields: Array[Any] = list(fields)

    @staticmethod
    def cases() -> list[str]:
        return ["Success", "NotFound", "Error"]


ClientResult_1_reflection = _expr2

def parse_response(bytes: bytearray) -> ParseResult_1[ResponsePacket]:
    return DeserializeResponse(bytes)


def to_client_result(parse_result: ParseResult_1[ResponsePacket]) -> ClientResult_1[None]:
    if parse_result.tag == 1:
        return ClientResult_1(2, parse_result.fields[0])

    elif parse_result.tag == 2:
        return ClientResult_1(2, "Insufficient data")

    else: 
        response: ResponsePacket = parse_result.fields[0]
        match_value: enum_type("Ocis.Server.ProtocolSpec.StatusCode", uint8, [("Success", 0.0), ("NotFound", 1.0), ("Error", 2.0)]) = response.StatusCode
        if match_value == uint8(0):
            return ClientResult_1(0, None)

        elif match_value == uint8(1):
            return ClientResult_1(1)

        elif match_value == uint8(2):
            return ClientResult_1(2, default_arg(response.ErrorMessage, "Unknown error"))

        else: 
            return ClientResult_1(2, "Invalid status code")




def to_client_result_value(parse_result: ParseResult_1[ResponsePacket]) -> ClientResult_1[bytearray]:
    if parse_result.tag == 1:
        return ClientResult_1(2, parse_result.fields[0])

    elif parse_result.tag == 2:
        return ClientResult_1(2, "Insufficient data")

    else: 
        response: ResponsePacket = parse_result.fields[0]
        match_value: enum_type("Ocis.Server.ProtocolSpec.StatusCode", uint8, [("Success", 0.0), ("NotFound", 1.0), ("Error", 2.0)]) = response.StatusCode
        if match_value == uint8(0):
            match_value_1: bytearray | None = response.Value
            if match_value_1 is None:
                return ClientResult_1(2, "Success response missing value")

            else: 
                return ClientResult_1(0, match_value_1)


        elif match_value == uint8(1):
            return ClientResult_1(1)

        elif match_value == uint8(2):
            return ClientResult_1(2, default_arg(response.ErrorMessage, "Unknown error"))

        else: 
            return ClientResult_1(2, "Invalid status code")




__all__ = ["ClientResult_1_reflection", "parse_response", "to_client_result", "to_client_result_value"]

