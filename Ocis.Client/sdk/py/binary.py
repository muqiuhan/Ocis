from .fable_modules.fable_library.encoding import get_utf8
from .fable_modules.fable_library.types import (uint8, uint32)

def get_uint32(b0: uint8, b1: uint8, b2: uint8, b3: uint8) -> uint32:
    return (((((b0 | ((b1 << 8) >> 0)) >> 0) | ((b2 << 16) >> 0)) >> 0) | ((b3 << 24) >> 0)) >> 0


def get_int32(b0: uint8, b1: uint8, b2: uint8, b3: uint8) -> int:
    return ((int(b0) | (int(b1) << 8)) | (int(b2) << 16)) | (int(b3) << 24)


def read_uint32little_endian(buffer: bytearray, offset: int) -> uint32:
    return get_uint32(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3])


def read_int32little_endian(buffer: bytearray, offset: int) -> int:
    return get_int32(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3])


def read_byte(buffer: bytearray, offset: int) -> uint8:
    return buffer[offset]


def write_uint32little_endian(value: uint32, buffer: bytearray, offset: int) -> None:
    buffer[offset] = int(value+0x100 if value < 0 else value) & 0xFF
    buffer[offset + 1] = int((value >> 8)+0x100 if (value >> 8) < 0 else (value >> 8)) & 0xFF
    buffer[offset + 2] = int((value >> 16)+0x100 if (value >> 16) < 0 else (value >> 16)) & 0xFF
    buffer[offset + 3] = int((value >> 24)+0x100 if (value >> 24) < 0 else (value >> 24)) & 0xFF


def write_int32little_endian(value: int, buffer: bytearray, offset: int) -> None:
    u: uint32 = int(value+0x100000000 if value < 0 else value)
    buffer[offset] = int(u+0x100 if u < 0 else u) & 0xFF
    buffer[offset + 1] = int((u >> 8)+0x100 if (u >> 8) < 0 else (u >> 8)) & 0xFF
    buffer[offset + 2] = int((u >> 16)+0x100 if (u >> 16) < 0 else (u >> 16)) & 0xFF
    buffer[offset + 3] = int((u >> 24)+0x100 if (u >> 24) < 0 else (u >> 24)) & 0xFF


def write_byte(value: uint8, buffer: bytearray, offset: int) -> None:
    buffer[offset] = value


def string_to_bytes(str_1: str) -> bytearray:
    return get_utf8().get_bytes(str_1)


def bytes_to_string(bytes: bytearray) -> str:
    return get_utf8().get_string(bytes)


__all__ = ["get_uint32", "get_int32", "read_uint32little_endian", "read_int32little_endian", "read_byte", "write_uint32little_endian", "write_int32little_endian", "write_byte", "string_to_bytes", "bytes_to_string"]

