import { getBytesInt32, getBytesUInt32, toInt32, toUInt32 } from "./fable_modules/fable-library-ts.4.25.0/BitConverter.js";
import { int32, uint8, uint32 } from "./fable_modules/fable-library-ts.4.25.0/Int32.js";
import { concat, getSubArray, item } from "./fable_modules/fable-library-ts.4.25.0/Array.js";
import { RequestPacket } from "../ProtocolSpec.js";
import { value as value_1, Option } from "./fable_modules/fable-library-ts.4.25.0/Option.js";

/**
 * Parse request packet header from byte array
 */
export function TryParseRequestHeader(buffer: uint8[]): Option<RequestPacket> {
    if (buffer.length < 18) {
        return undefined;
    }
    else {
        try {
            let offset = 0;
            const magicNumber: uint32 = toUInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const version: uint8 = item(offset, buffer);
            offset = ((offset + 1) | 0);
            const commandType: int32 = ~~item(offset, buffer) | 0;
            offset = ((offset + 1) | 0);
            const totalPacketLength: int32 = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const keyLength: int32 = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            return ((magicNumber === 1397310287) && (version === 1)) ? (new RequestPacket(magicNumber, version, commandType, totalPacketLength, keyLength, toInt32(buffer, offset), [], undefined)) : undefined;
        }
        catch (matchValue: any) {
            return undefined;
        }
    }
}

/**
 * Parse complete request packet from byte array
 */
export function TryParseRequestPacket(buffer: uint8[]): Option<RequestPacket> {
    const matchValue: Option<RequestPacket> = TryParseRequestHeader(buffer);
    if (matchValue == null) {
        return undefined;
    }
    else {
        const header: RequestPacket = value_1(matchValue);
        if (buffer.length >= header.TotalPacketLength) {
            try {
                let offset = 18;
                const key: uint8[] = getSubArray<uint8>(buffer, offset, header.KeyLength);
                offset = ((offset + header.KeyLength) | 0);
                return new RequestPacket(header.MagicNumber, header.Version, header.CommandType, header.TotalPacketLength, header.KeyLength, header.ValueLength, key, (header.ValueLength > 0) ? getSubArray<uint8>(buffer, offset, header.ValueLength) : undefined);
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

/**
 * Create request packet
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
 * Serialize request packet to byte array
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

