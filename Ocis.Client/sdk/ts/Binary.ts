import { int32, uint32, uint8 } from "./fable_modules/fable-library-ts.4.25.0/Int32.js";
import { setItem, item } from "./fable_modules/fable-library-ts.4.25.0/Array.js";
import { get_UTF8 } from "./fable_modules/fable-library-ts.4.25.0/Encoding.js";

function getUint32(b0: uint8, b1: uint8, b2: uint8, b3: uint8): uint32 {
    return (((((b0 | ((b1 << 8) >>> 0)) >>> 0) | ((b2 << 16) >>> 0)) >>> 0) | ((b3 << 24) >>> 0)) >>> 0;
}

function getInt32(b0: uint8, b1: uint8, b2: uint8, b3: uint8): int32 {
    return ((~~b0 | (~~b1 << 8)) | (~~b2 << 16)) | (~~b3 << 24);
}

export function readUInt32LittleEndian(buffer: uint8[], offset: int32): uint32 {
    return getUint32(item(offset, buffer), item(offset + 1, buffer), item(offset + 2, buffer), item(offset + 3, buffer));
}

export function readInt32LittleEndian(buffer: uint8[], offset: int32): int32 {
    return getInt32(item(offset, buffer), item(offset + 1, buffer), item(offset + 2, buffer), item(offset + 3, buffer));
}

export function readByte(buffer: uint8[], offset: int32): uint8 {
    return item(offset, buffer);
}

export function writeUInt32LittleEndian(value: uint32, buffer: uint8[], offset: int32): void {
    setItem(buffer, offset, value & 0xFF);
    setItem(buffer, offset + 1, (value >>> 8) & 0xFF);
    setItem(buffer, offset + 2, (value >>> 16) & 0xFF);
    setItem(buffer, offset + 3, (value >>> 24) & 0xFF);
}

export function writeInt32LittleEndian(value: int32, buffer: uint8[], offset: int32): void {
    const u: uint32 = value >>> 0;
    setItem(buffer, offset, u & 0xFF);
    setItem(buffer, offset + 1, (u >>> 8) & 0xFF);
    setItem(buffer, offset + 2, (u >>> 16) & 0xFF);
    setItem(buffer, offset + 3, (u >>> 24) & 0xFF);
}

export function writeByte(value: uint8, buffer: uint8[], offset: int32): void {
    setItem(buffer, offset, value);
}

export function stringToBytes(str: string): uint8[] {
    return Array.from(get_UTF8().getBytes(str));
}

export function bytesToString(bytes: uint8[]): string {
    return get_UTF8().getString(bytes);
}

