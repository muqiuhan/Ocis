import { writeInt32LittleEndian, writeByte, writeUInt32LittleEndian, readInt32LittleEndian, readByte, readUInt32LittleEndian } from "./Binary.js";
import { int32, uint8, uint32 } from "./fable_modules/fable-library-ts.4.25.0/Int32.js";
import { ResponsePacket, RequestPacket } from "./Ocis.Server/ProtocolSpec.js";
import { map, defaultArg, value as value_3, Option } from "./fable_modules/fable-library-ts.4.25.0/Option.js";
import { copyTo, fill, getSubArray } from "./fable_modules/fable-library-ts.4.25.0/Array.js";
import { get_UTF8 } from "./fable_modules/fable-library-ts.4.25.0/Encoding.js";
import { Union } from "./fable_modules/fable-library-ts.4.25.0/Types.js";
import { union_type, string_type, TypeInfo } from "./fable_modules/fable-library-ts.4.25.0/Reflection.js";
import { printf, toText } from "./fable_modules/fable-library-ts.4.25.0/String.js";

export function TryParseRequestHeader(buffer: uint8[]): Option<RequestPacket> {
    if (buffer.length < 18) {
        return undefined;
    }
    else {
        try {
            const magicNumber: uint32 = readUInt32LittleEndian(buffer, 0);
            const version: uint8 = readByte(buffer, 4);
            return ((magicNumber === 1397310287) && (version === 1)) ? (new RequestPacket(magicNumber, version, ~~readByte(buffer, 5), readInt32LittleEndian(buffer, 6), readInt32LittleEndian(buffer, 10), readInt32LittleEndian(buffer, 14), [], undefined)) : undefined;
        }
        catch (matchValue: any) {
            return undefined;
        }
    }
}

export function TryParseRequestPacket(buffer: uint8[]): Option<RequestPacket> {
    const matchValue: Option<RequestPacket> = TryParseRequestHeader(buffer);
    if (matchValue == null) {
        return undefined;
    }
    else {
        const header: RequestPacket = value_3(matchValue);
        if (buffer.length >= header.TotalPacketLength) {
            try {
                return new RequestPacket(header.MagicNumber, header.Version, header.CommandType, header.TotalPacketLength, header.KeyLength, header.ValueLength, getSubArray<uint8>(buffer, 18, header.KeyLength), (header.ValueLength > 0) ? getSubArray<uint8>(buffer, 18 + header.KeyLength, header.ValueLength) : undefined);
            }
            catch (matchValue_1: any) {
                return undefined;
            }
        }
        else {
            return undefined;
        }
    }
}

export function SerializeRequest(packet: RequestPacket): uint8[] {
    const keyLen: int32 = packet.Key.length | 0;
    const totalLen: int32 = ((18 + keyLen) + defaultArg(map<uint8[], int32>((v: uint8[]): int32 => v.length, packet.Value), 0)) | 0;
    const buffer: uint8[] = fill(new Array(totalLen), 0, totalLen, 0);
    let offset = 0;
    writeUInt32LittleEndian(packet.MagicNumber, buffer, offset);
    offset = ((offset + 4) | 0);
    writeByte(packet.Version, buffer, offset);
    offset = ((offset + 1) | 0);
    writeByte(packet.CommandType & 0xFF, buffer, offset);
    offset = ((offset + 1) | 0);
    writeInt32LittleEndian(packet.TotalPacketLength, buffer, offset);
    offset = ((offset + 4) | 0);
    writeInt32LittleEndian(packet.KeyLength, buffer, offset);
    offset = ((offset + 4) | 0);
    writeInt32LittleEndian(packet.ValueLength, buffer, offset);
    offset = ((offset + 4) | 0);
    if (keyLen > 0) {
        copyTo<uint8>(packet.Key, 0, buffer, offset, keyLen);
        offset = ((offset + keyLen) | 0);
    }
    const matchValue: Option<uint8[]> = packet.Value;
    if (matchValue == null) {
    }
    else {
        const value_1: uint8[] = value_3(matchValue);
        copyTo<uint8>(value_1, 0, buffer, offset, value_1.length);
    }
    return buffer;
}

export function CreateSuccessResponse(value: Option<uint8[]>): ResponsePacket {
    let patternInput: [int32, int32];
    if (value == null) {
        patternInput = ([0, 18] as [int32, int32]);
    }
    else {
        const v: uint8[] = value_3(value);
        patternInput = ([v.length, 18 + v.length] as [int32, int32]);
    }
    return new ResponsePacket(1397310287, 1, 0, patternInput[1], patternInput[0], 0, value, undefined);
}

export function CreateNotFoundResponse(): ResponsePacket {
    return new ResponsePacket(1397310287, 1, 1, 18, 0, 0, undefined, undefined);
}

export function CreateErrorResponse(errorMessage: string): ResponsePacket {
    const msgBytes: uint8[] = Array.from(get_UTF8().getBytes(errorMessage));
    return new ResponsePacket(1397310287, 1, 2, 18 + msgBytes.length, 0, msgBytes.length, undefined, errorMessage);
}

export function IsValidPacketSize(totalLength: int32): boolean {
    if (totalLength >= 18) {
        return totalLength <= ((10 * 1024) * 1024);
    }
    else {
        return false;
    }
}

export function SerializeResponse(packet: ResponsePacket): uint8[] {
    const totalLen: int32 = ((18 + defaultArg(map<uint8[], int32>((v: uint8[]): int32 => v.length, packet.Value), 0)) + defaultArg(map<string, int32>((m: string): int32 => Array.from(get_UTF8().getBytes(m)).length, packet.ErrorMessage), 0)) | 0;
    const buffer: uint8[] = fill(new Array(totalLen), 0, totalLen, 0);
    let offset = 0;
    writeUInt32LittleEndian(packet.MagicNumber, buffer, offset);
    offset = ((offset + 4) | 0);
    writeByte(packet.Version, buffer, offset);
    offset = ((offset + 1) | 0);
    writeByte(packet.StatusCode, buffer, offset);
    offset = ((offset + 1) | 0);
    writeInt32LittleEndian(packet.TotalPacketLength, buffer, offset);
    offset = ((offset + 4) | 0);
    writeInt32LittleEndian(packet.ValueLength, buffer, offset);
    offset = ((offset + 4) | 0);
    writeInt32LittleEndian(packet.ErrorMessageLength, buffer, offset);
    offset = ((offset + 4) | 0);
    const matchValue: Option<uint8[]> = packet.Value;
    if (matchValue == null) {
    }
    else {
        const value_2: uint8[] = value_3(matchValue);
        copyTo<uint8>(value_2, 0, buffer, offset, value_2.length);
        offset = ((offset + value_2.length) | 0);
    }
    const matchValue_1: Option<string> = packet.ErrorMessage;
    if (matchValue_1 == null) {
    }
    else {
        const msg: string = value_3(matchValue_1);
        const msgBytes: uint8[] = Array.from(get_UTF8().getBytes(msg));
        copyTo<uint8>(msgBytes, 0, buffer, offset, msgBytes.length);
    }
    return buffer;
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
    return union_type("Ocis.Client.SDK.Protocol.ParseResult`1", [gen0], ParseResult$1, () => [[["Item", gen0]], [["Item", string_type]], []]);
}

export function DeserializeResponse(buffer: uint8[]): ParseResult$1_$union<ResponsePacket> {
    let arg: string, errorBytes: uint8[];
    try {
        if (buffer.length < 18) {
            return ParseResult$1_InsufficientData<ResponsePacket>();
        }
        else {
            let offset = 0;
            const magicNumber: uint32 = readUInt32LittleEndian(buffer, offset);
            offset = ((offset + 4) | 0);
            const version: uint8 = readByte(buffer, offset);
            offset = ((offset + 1) | 0);
            const statusCode: uint8 = readByte(buffer, offset);
            offset = ((offset + 1) | 0);
            const totalPacketLength: int32 = readInt32LittleEndian(buffer, offset) | 0;
            offset = ((offset + 4) | 0);
            const valueLength: int32 = readInt32LittleEndian(buffer, offset) | 0;
            offset = ((offset + 4) | 0);
            const errorMessageLength: int32 = readInt32LittleEndian(buffer, offset) | 0;
            offset = ((offset + 4) | 0);
            if (buffer.length < totalPacketLength) {
                return ParseResult$1_InsufficientData<ResponsePacket>();
            }
            else if (!((magicNumber === 1397310287) && (version === 1))) {
                return ParseResult$1_ParseError<ResponsePacket>("Invalid header");
            }
            else if ((valueLength < 0) ? true : (errorMessageLength < 0)) {
                return ParseResult$1_ParseError<ResponsePacket>("Invalid length field");
            }
            else if (totalPacketLength !== ((18 + valueLength) + errorMessageLength)) {
                return ParseResult$1_ParseError<ResponsePacket>("Packet length mismatch");
            }
            else {
                const value: Option<uint8[]> = (valueLength > 0) ? getSubArray<uint8>(buffer, offset, valueLength) : undefined;
                offset = ((offset + valueLength) | 0);
                return ParseResult$1_ParseSuccess<ResponsePacket>(new ResponsePacket(magicNumber, version, statusCode, totalPacketLength, valueLength, errorMessageLength, value, (errorMessageLength > 0) ? ((errorBytes = getSubArray<uint8>(buffer, offset, errorMessageLength), get_UTF8().getString(errorBytes))) : undefined));
            }
        }
    }
    catch (ex: any) {
        return ParseResult$1_ParseError<ResponsePacket>((arg = ex.message, toText(printf("Error parsing response: %s"))(arg)));
    }
}

