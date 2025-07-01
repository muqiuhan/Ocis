import { uint32, uint8, int32 } from "./fable_modules/fable-library-ts.4.25.0/Int32.js";
import { Option, value as value_1 } from "./fable_modules/fable-library-ts.4.25.0/Option.js";
import { ResponsePacket, RequestPacket } from "../ProtocolSpec.js";
import { toInt32, toUInt32, getBytesInt32, getBytesUInt32 } from "./fable_modules/fable-library-ts.4.25.0/BitConverter.js";
import { getSubArray, item, concat } from "./fable_modules/fable-library-ts.4.25.0/Array.js";
import { Union } from "./fable_modules/fable-library-ts.4.25.0/Types.js";
import { union_type, string_type, TypeInfo } from "./fable_modules/fable-library-ts.4.25.0/Reflection.js";
import { printf, toText } from "./fable_modules/fable-library-ts.4.25.0/String.js";
import { get_UTF8 } from "./fable_modules/fable-library-ts.4.25.0/Encoding.js";

/**
 * create request packet
 */
export function CreateRequest(commandType: int32, key: uint8[], value: Option<uint8[]>): RequestPacket {
    let valueLen: int32;
    if (value == null) {
        valueLen = 0;
    }
    else {
        const v: uint8[] = value_1(value);
        valueLen = v.length;
    }
    return new RequestPacket(1397310287, 1, commandType, (18 + key.length) + valueLen, key.length, valueLen, key, value);
}

/**
 * serialize request packet to bytes
 */
export function SerializeRequest(packet: RequestPacket): uint8[] {
    const parts: uint8[][] = [];
    void (parts.push(Array.from(getBytesUInt32(packet.MagicNumber))));
    void (parts.push([packet.Version]));
    void (parts.push([packet.CommandType & 0xFF]));
    void (parts.push(Array.from(getBytesInt32(packet.TotalPacketLength))));
    void (parts.push(Array.from(getBytesInt32(packet.KeyLength))));
    void (parts.push(Array.from(getBytesInt32(packet.ValueLength))));
    void (parts.push(packet.Key));
    const matchValue: Option<uint8[]> = packet.Value;
    if (matchValue == null) {
    }
    else {
        const value: uint8[] = value_1(matchValue);
        void (parts.push(value));
    }
    return concat<uint8>(parts.slice());
}

export type ParseResult$1_$union<T> = 
    | ParseResult$1<T, 0>
    | ParseResult$1<T, 1>
    | ParseResult$1<T, 2>

export type ParseResult$1_$cases<T> = {
    0: ["ParseSuccess", [T]],
    1: ["ParseError", [string]],
    2: ["InsufficientData", []]
}

export function ParseResult$1_ParseSuccess<T>(Item: T) {
    return new ParseResult$1<T, 0>(0, [Item]);
}

export function ParseResult$1_ParseError<T>(Item: string) {
    return new ParseResult$1<T, 1>(1, [Item]);
}

export function ParseResult$1_InsufficientData<T>() {
    return new ParseResult$1<T, 2>(2, []);
}

export class ParseResult$1<T, Tag extends keyof ParseResult$1_$cases<T>> extends Union<Tag, ParseResult$1_$cases<T>[Tag][0]> {
    constructor(readonly tag: Tag, readonly fields: ParseResult$1_$cases<T>[Tag][1]) {
        super();
    }
    cases() {
        return ["ParseSuccess", "ParseError", "InsufficientData"];
    }
}

export function ParseResult$1_$reflection(gen0: TypeInfo): TypeInfo {
    return union_type("Ocis.Protocol.ParseResult`1", [gen0], ParseResult$1, () => [[["Item", gen0]], [["Item", string_type]], []]);
}

/**
 * deserialize response from bytes
 */
export function DeserializeResponse(buffer: uint8[]): ParseResult$1_$union<ResponsePacket> {
    let arg: string, errorBytes: uint8[];
    try {
        if (buffer.length < 18) {
            return ParseResult$1_InsufficientData<ResponsePacket>();
        }
        else {
            let offset = 0;
            const magicNumber: uint32 = toUInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const version: uint8 = item(offset, buffer);
            offset = ((offset + 1) | 0);
            const statusCode: uint8 = item(offset, buffer);
            offset = ((offset + 1) | 0);
            const totalPacketLength: int32 = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const valueLength: int32 = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const errorMessageLength: int32 = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            if (buffer.length < totalPacketLength) {
                return ParseResult$1_InsufficientData<ResponsePacket>();
            }
            else if (!((magicNumber === 1397310287) && (version === 1))) {
                return ParseResult$1_ParseError<ResponsePacket>("invalid header");
            }
            else if ((valueLength < 0) ? true : (errorMessageLength < 0)) {
                return ParseResult$1_ParseError<ResponsePacket>("invalid length field");
            }
            else if (totalPacketLength !== ((18 + valueLength) + errorMessageLength)) {
                return ParseResult$1_ParseError<ResponsePacket>("packet length mismatch");
            }
            else {
                const value: Option<uint8[]> = (valueLength > 0) ? getSubArray<uint8>(buffer, offset, valueLength) : undefined;
                offset = ((offset + valueLength) | 0);
                return ParseResult$1_ParseSuccess<ResponsePacket>(new ResponsePacket(magicNumber, version, statusCode, totalPacketLength, valueLength, errorMessageLength, value, (errorMessageLength > 0) ? ((errorBytes = getSubArray<uint8>(buffer, offset, errorMessageLength), get_UTF8().getString(errorBytes))) : undefined));
            }
        }
    }
    catch (ex: any) {
        return ParseResult$1_ParseError<ResponsePacket>((arg = ex.message, toText(printf("error parsing response: %s"))(arg)));
    }
}

/**
 * convert string to bytes
 */
export function ProtocolHelper_stringToBytes(s: string): uint8[] {
    return Array.from(get_UTF8().getBytes(s));
}

/**
 * convert bytes to string
 */
export function ProtocolHelper_bytesToString(bytes: uint8[]): string {
    return get_UTF8().getString(bytes);
}

/**
 * create SET request
 */
export function ProtocolHelper_createSetRequest(key: string, value: string): RequestPacket {
    return CreateRequest(1, ProtocolHelper_stringToBytes(key), ProtocolHelper_stringToBytes(value));
}

/**
 * create GET request
 */
export function ProtocolHelper_createGetRequest(key: string): RequestPacket {
    return CreateRequest(2, ProtocolHelper_stringToBytes(key), undefined);
}

/**
 * create DELETE request
 */
export function ProtocolHelper_createDeleteRequest(key: string): RequestPacket {
    return CreateRequest(3, ProtocolHelper_stringToBytes(key), undefined);
}

