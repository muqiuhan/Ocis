import { get_UTF8 } from "./fable_modules/fable-library-ts.4.25.0/Encoding.js";
import { int32, uint8 } from "./fable_modules/fable-library-ts.4.25.0/Int32.js";
import { Option, map, defaultArg } from "./fable_modules/fable-library-ts.4.25.0/Option.js";
import { RequestPacket } from "./Ocis.Server/ProtocolSpec.js";
import { SerializeRequest } from "./Protocol.js";

function createPacket(commandType: int32, key: string, value: Option<uint8[]>): RequestPacket {
    const keyBytes: uint8[] = Array.from(get_UTF8().getBytes(key));
    const keyLen: int32 = keyBytes.length | 0;
    const valueLen: int32 = defaultArg(map<uint8[], int32>((v: uint8[]): int32 => v.length, value), 0) | 0;
    return new RequestPacket(1397310287, 1, commandType, (18 + keyLen) + valueLen, keyLen, valueLen, keyBytes, value);
}

export function createSetRequest(key: string, value: uint8[]): uint8[] {
    return SerializeRequest(createPacket(1, key, value));
}

export function createGetRequest(key: string): uint8[] {
    return SerializeRequest(createPacket(2, key, undefined));
}

export function createDeleteRequest(key: string): uint8[] {
    return SerializeRequest(createPacket(3, key, undefined));
}

